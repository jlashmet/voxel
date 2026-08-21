using System;
using System.Globalization;
using UnityEngine;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Opt-in real-player benchmark for the settled, motionless VoxelShowcase view.
    ///
    /// This is deliberately separate from <see cref="ShowcasePlayerHarness"/>'s rotating visual
    /// survey. A changing camera forces visibility work and periodic PNG captures perturb frame
    /// time, so neither belongs inside the main-thread render-submission baseline.
    ///
    ///   -voxel-stationary-sample-seconds N   measured interval after convergence
    ///   -voxel-stationary-timeout-seconds N  fail if convergence/sample does not finish by N
    /// </summary>
    public static class StationaryRenderBenchmarkHarness
    {
        private const string SampleArgument = "-voxel-stationary-sample-seconds";
        private const string TimeoutArgument = "-voxel-stationary-timeout-seconds";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            double sampleSeconds = Value(SampleArgument, 0.0);
            if (sampleSeconds <= 0.0) return;

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var root = new GameObject("Stationary Render Benchmark")
            {
                hideFlags = HideFlags.DontSave
            };
            Reporter reporter = root.AddComponent<Reporter>();
            reporter.SampleSeconds = Math.Max(1.0, sampleSeconds);
            reporter.TimeoutSeconds = Math.Max(
                reporter.SampleSeconds + 5.0,
                Value(TimeoutArgument, Math.Max(120.0, reporter.SampleSeconds + 60.0)));
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "STATIONARY armed sample={0:0.0}s timeout={1:0.0}s",
                reporter.SampleSeconds, reporter.TimeoutSeconds));
        }

        private static double Value(string name, double fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.Ordinal)) continue;
                if (double.TryParse(args[i + 1], NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
            }
            return fallback;
        }

        private sealed class Reporter : MonoBehaviour
        {
            private const int ConvergedFramesRequired = 30;
            private const int MinimumRendererSamples = 128;
            private const int FrameCapacity = 32768;
            private const float PositionToleranceSquared = 1e-10f;
            private const float RotationToleranceDegrees = 0.0001f;
            private const float ProjectionTolerance = 1e-6f;

            internal double SampleSeconds;
            internal double TimeoutSeconds;

            // Allocated once when the harness object is created. The measured interval only writes
            // into this fixed storage; sorting and string formatting happen after sampling ends.
            private readonly float[] _frameMilliseconds = new float[FrameCapacity];
            private int _frameCount;
            private double _sampleElapsed;
            private double _totalElapsed;
            private int _convergedFrames;
            private bool _sampling;
            private bool _finished;

            private Camera _camera;
            private Vector3 _settlePosition;
            private Quaternion _settleRotation;
            private Matrix4x4 _settleProjection;
            private Vector3 _samplePosition;
            private Quaternion _sampleRotation;
            private Matrix4x4 _sampleProjection;

            private bool _cameraStable = true;
            private bool _projectionStable = true;
            private int _visibleMin = int.MaxValue;
            private int _visibleMax = int.MinValue;
            private int _missingMax;
            private int _runningJobsMax;
            private int _pendingUploadsMax;

            private void Update()
            {
                if (_finished) return;

                float dt = Time.unscaledDeltaTime;
                _totalElapsed += dt;

                if (_camera == null)
                {
                    _camera = Camera.main;
                    if (_camera == null) _camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
                    if (_camera != null) CaptureSettlePose();
                }

                if (_camera == null)
                {
                    if (_totalElapsed >= TimeoutSeconds) Fail("no active camera");
                    return;
                }

                SurfaceBenchmarkState state =
                    RenderingDiagnosticsComposition.GetSurfaceBenchmarkState();

                if (!_sampling)
                {
                    WaitForConvergence(in state);
                    if (!_sampling && _totalElapsed >= TimeoutSeconds)
                    {
                        Fail(string.Format(CultureInfo.InvariantCulture,
                            "did not converge before timeout visible={0} missing={1} jobs={2} uploads={3}",
                            state.VisibleSolidChunks, state.MissingVisibleSolidChunks,
                            state.RunningSolidJobs, state.SolidMeshesAwaitingUpload));
                    }
                    return;
                }

                SampleFrame(dt, in state);
                if (_sampleElapsed < SampleSeconds) return;

                // VoxelSurfaceScheduler.VisibilityTiming is a 128-frame rolling window rather than
                // a resettable benchmark counter. Requiring at least 128 stationary frames means
                // its final window contains no startup/convergence frames.
                if (_frameCount < MinimumRendererSamples) return;
                Finish();
            }

            private void WaitForConvergence(in SurfaceBenchmarkState state)
            {
                if (!PoseMatches(_settlePosition, _settleRotation, _settleProjection))
                {
                    _convergedFrames = 0;
                    CaptureSettlePose();
                    return;
                }

                if (!state.IsConverged)
                {
                    _convergedFrames = 0;
                    return;
                }

                _convergedFrames++;
                if (_convergedFrames < ConvergedFramesRequired) return;

                _sampling = true;
                _sampleElapsed = 0.0;
                _frameCount = 0;
                _samplePosition = _camera.transform.position;
                _sampleRotation = _camera.transform.rotation;
                _sampleProjection = _camera.projectionMatrix;
                _cameraStable = true;
                _projectionStable = true;
                _visibleMin = _visibleMax = state.VisibleSolidChunks;
                _missingMax = state.MissingVisibleSolidChunks;
                _runningJobsMax = state.RunningSolidJobs;
                _pendingUploadsMax = state.SolidMeshesAwaitingUpload;
                RenderingDiagnosticsComposition.ResetSolidRenderBenchmark();

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "STATIONARY begin t={0:0.0}s visible={1}",
                    _totalElapsed, state.VisibleSolidChunks));
            }

            private void SampleFrame(float dt, in SurfaceBenchmarkState state)
            {
                _sampleElapsed += dt;
                if (_frameCount < FrameCapacity)
                    _frameMilliseconds[_frameCount++] = dt * 1000f;

                if ((_camera.transform.position - _samplePosition).sqrMagnitude
                    > PositionToleranceSquared
                    || Quaternion.Angle(_camera.transform.rotation, _sampleRotation)
                    > RotationToleranceDegrees)
                    _cameraStable = false;
                if (!MatrixApproximately(_camera.projectionMatrix, _sampleProjection,
                                         ProjectionTolerance))
                    _projectionStable = false;

                if (state.VisibleSolidChunks < _visibleMin) _visibleMin = state.VisibleSolidChunks;
                if (state.VisibleSolidChunks > _visibleMax) _visibleMax = state.VisibleSolidChunks;
                if (state.MissingVisibleSolidChunks > _missingMax)
                    _missingMax = state.MissingVisibleSolidChunks;
                if (state.RunningSolidJobs > _runningJobsMax)
                    _runningJobsMax = state.RunningSolidJobs;
                if (state.SolidMeshesAwaitingUpload > _pendingUploadsMax)
                    _pendingUploadsMax = state.SolidMeshesAwaitingUpload;
            }

            private void Finish()
            {
                _finished = true;
                SolidRenderBenchmarkSnapshot render =
                    RenderingDiagnosticsComposition.GetSolidRenderBenchmark();

                int sampledFrames = _frameCount;
                Array.Sort(_frameMilliseconds, 0, sampledFrames);
                double fps = sampledFrames / Math.Max(0.0001, _sampleElapsed);
                double frameP50 = Percentile(_frameMilliseconds, sampledFrames, 0.50);
                double frameP95 = Percentile(_frameMilliseconds, sampledFrames, 0.95);
                double frameP99 = Percentile(_frameMilliseconds, sampledFrames, 0.99);
                double frameMax = sampledFrames > 0 ? _frameMilliseconds[sampledFrames - 1] : 0.0;

                bool visibleStable = _visibleMin == _visibleMax && _visibleMin > 0;
                bool coverageStable = _missingMax == 0;
                bool workStayedConverged = _runningJobsMax == 0 && _pendingUploadsMax == 0;
                bool enoughRenderSamples = render.SampleCount >= MinimumRendererSamples;
                bool pass = _cameraStable && _projectionStable && visibleStable
                    && coverageStable && workStayedConverged && enoughRenderSamples;

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "STATIONARY result={0} seconds={1:0.00} frames={2} fps={3:0.0} "
                    + "frame.ms[p50={4:0.000} p95={5:0.000} p99={6:0.000} max={7:0.000}] "
                    + "cameraStable={8} projectionStable={9} visible[min={10} max={11}] "
                    + "missingMax={12} jobsMax={13} uploadsMax={14} renderSamples={15} "
                    + "visibility.ms[p50={16:0.0000} p95={17:0.0000} p99={18:0.0000} max={19:0.0000}] "
                    + "staging.ms[p50={20:0.0000} p95={21:0.0000} p99={22:0.0000} max={23:0.0000}] "
                    + "submission.ms[p50={24:0.0000} p95={25:0.0000} p99={26:0.0000} max={27:0.0000}] "
                    + "solids[mean={28:0.0} last={29}] draws[mean={30:0.0} last={31}]",
                    pass ? "PASS" : "FAIL", _sampleElapsed, sampledFrames, fps,
                    frameP50, frameP95, frameP99, frameMax,
                    _cameraStable ? 1 : 0, _projectionStable ? 1 : 0,
                    _visibleMin, _visibleMax, _missingMax, _runningJobsMax, _pendingUploadsMax,
                    render.SampleCount,
                    render.VisibilityP50Ms, render.VisibilityP95Ms,
                    render.VisibilityP99Ms, render.VisibilityMaxMs,
                    render.StagingP50Ms, render.StagingP95Ms,
                    render.StagingP99Ms, render.StagingMaxMs,
                    render.SubmissionP50Ms, render.SubmissionP95Ms,
                    render.SubmissionP99Ms, render.SubmissionMaxMs,
                    render.MeanVisibleSolidCount, render.LastVisibleSolidCount,
                    render.MeanUnitySubmissionCalls, render.LastUnitySubmissionCalls));

                Application.Quit(pass ? 0 : 1);
            }

            private void CaptureSettlePose()
            {
                _settlePosition = _camera.transform.position;
                _settleRotation = _camera.transform.rotation;
                _settleProjection = _camera.projectionMatrix;
            }

            private bool PoseMatches(Vector3 position, Quaternion rotation, Matrix4x4 projection)
            {
                return (_camera.transform.position - position).sqrMagnitude
                           <= PositionToleranceSquared
                    && Quaternion.Angle(_camera.transform.rotation, rotation)
                           <= RotationToleranceDegrees
                    && MatrixApproximately(_camera.projectionMatrix, projection,
                                           ProjectionTolerance);
            }

            private static bool MatrixApproximately(Matrix4x4 a, Matrix4x4 b, float tolerance)
            {
                for (int i = 0; i < 16; i++)
                    if (Mathf.Abs(a[i] - b[i]) > tolerance) return false;
                return true;
            }

            private static double Percentile(float[] sorted, int count, double fraction)
            {
                if (count <= 0) return 0.0;
                int index = Math.Min(count - 1,
                    Math.Max(0, (int)Math.Ceiling(fraction * count) - 1));
                return sorted[index];
            }

            private void Fail(string reason)
            {
                _finished = true;
                Debug.LogError($"STATIONARY result=FAIL reason={reason}");
                Application.Quit(1);
            }
        }
    }
}
