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
                "STATIONARY armed sample={0:0.0}s timeout={1:0.0}s device={2}",
                reporter.SampleSeconds, reporter.TimeoutSeconds, SystemInfo.graphicsDeviceType));
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
            private readonly FrameTiming[] _latestFrameTiming = new FrameTiming[1];
            private readonly double[] _cpuFrameMilliseconds = new double[FrameCapacity];
            private readonly double[] _cpuMainMilliseconds = new double[FrameCapacity];
            private readonly double[] _cpuRenderMilliseconds = new double[FrameCapacity];
            private readonly double[] _cpuPresentWaitMilliseconds = new double[FrameCapacity];
            private readonly double[] _gpuFrameMilliseconds = new double[FrameCapacity];
            private int _frameCount;
            private int _cpuFrameCount;
            private int _cpuMainCount;
            private int _cpuRenderCount;
            private int _cpuPresentWaitCount;
            private int _gpuFrameCount;
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

                // Scheduler prepare/visibility are 128-frame rolling windows rather than resettable
                // benchmark counters. Requiring at least 128 stationary frames means their final
                // windows contain no startup/convergence frames.
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
                _cpuFrameCount = 0;
                _cpuMainCount = 0;
                _cpuRenderCount = 0;
                _cpuPresentWaitCount = 0;
                _gpuFrameCount = 0;
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

                // CaptureFrameTimings is intentionally armed only after convergence. The first
                // GetLatestTimings calls may return no sample; they are skipped rather than pulling
                // a startup frame into the stationary distribution.
                FrameTimingManager.CaptureFrameTimings();
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "STATIONARY begin t={0:0.0}s visible={1}",
                    _totalElapsed, state.VisibleSolidChunks));
            }

            private void SampleFrame(float dt, in SurfaceBenchmarkState state)
            {
                _sampleElapsed += dt;
                if (_frameCount < FrameCapacity)
                    _frameMilliseconds[_frameCount++] = dt * 1000f;

                CaptureUnityFrameTiming();

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

            private void CaptureUnityFrameTiming()
            {
                uint timingCount = FrameTimingManager.GetLatestTimings(1, _latestFrameTiming);
                if (timingCount > 0)
                {
                    FrameTiming timing = _latestFrameTiming[0];
                    AddPositive(timing.cpuFrameTime, _cpuFrameMilliseconds, ref _cpuFrameCount);
                    AddPositive(timing.cpuMainThreadFrameTime,
                                _cpuMainMilliseconds, ref _cpuMainCount);
                    AddPositive(timing.cpuRenderThreadFrameTime,
                                _cpuRenderMilliseconds, ref _cpuRenderCount);
                    AddPositive(timing.cpuMainThreadPresentWaitTime,
                                _cpuPresentWaitMilliseconds, ref _cpuPresentWaitCount);
                    AddPositive(timing.gpuFrameTime, _gpuFrameMilliseconds, ref _gpuFrameCount);
                }
                FrameTimingManager.CaptureFrameTimings();
            }

            private static void AddPositive(double milliseconds, double[] destination, ref int count)
            {
                if (milliseconds <= 0.0 || double.IsNaN(milliseconds)
                    || double.IsInfinity(milliseconds) || count >= destination.Length) return;
                destination[count++] = milliseconds;
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

                TimingStats cpuFrame = Summarize(_cpuFrameMilliseconds, _cpuFrameCount);
                TimingStats cpuMain = Summarize(_cpuMainMilliseconds, _cpuMainCount);
                TimingStats cpuRender = Summarize(_cpuRenderMilliseconds, _cpuRenderCount);
                TimingStats cpuPresent = Summarize(
                    _cpuPresentWaitMilliseconds, _cpuPresentWaitCount);
                TimingStats gpuFrame = Summarize(_gpuFrameMilliseconds, _gpuFrameCount);

                bool visibleStable = _visibleMin == _visibleMax && _visibleMin > 0;
                bool coverageStable = _missingMax == 0;
                bool workStayedConverged = _runningJobsMax == 0 && _pendingUploadsMax == 0;
                bool enoughRenderSamples = render.SampleCount >= MinimumRendererSamples;
                bool pass = _cameraStable && _projectionStable && visibleStable
                    && coverageStable && workStayedConverged && enoughRenderSamples;

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "STATIONARY result={0} device={1} seconds={2:0.00} frames={3} fps={4:0.0} "
                    + "frame.ms[p50={5:0.000} p95={6:0.000} p99={7:0.000} max={8:0.000}] "
                    + "cpu.ms[n={9} p50={10:0.000} p95={11:0.000} p99={12:0.000} max={13:0.000}] "
                    + "main.ms[n={14} p50={15:0.000} p95={16:0.000} p99={17:0.000} max={18:0.000}] "
                    + "render.ms[n={19} p50={20:0.000} p95={21:0.000} p99={22:0.000} max={23:0.000}] "
                    + "presentWait.ms[n={24} p50={25:0.000} p95={26:0.000} p99={27:0.000} max={28:0.000}] "
                    + "gpu.ms[n={29} p50={30:0.000} p95={31:0.000} p99={32:0.000} max={33:0.000}] "
                    + "cameraStable={34} projectionStable={35} visible[min={36} max={37}] "
                    + "missingMax={38} jobsMax={39} uploadsMax={40} renderSamples={41} "
                    + "prepare.ms[p50={42:0.0000} p95={43:0.0000} p99={44:0.0000} max={45:0.0000}] "
                    + "visibility.ms[p50={46:0.0000} p95={47:0.0000} p99={48:0.0000} max={49:0.0000}] "
                    + "staging.ms[p50={50:0.0000} p95={51:0.0000} p99={52:0.0000} max={53:0.0000}] "
                    + "submission.ms[p50={54:0.0000} p95={55:0.0000} p99={56:0.0000} max={57:0.0000}] "
                    + "solids[mean={58:0.0} last={59}] draws[mean={60:0.0} last={61}]",
                    pass ? "PASS" : "FAIL", SystemInfo.graphicsDeviceType,
                    _sampleElapsed, sampledFrames, fps,
                    frameP50, frameP95, frameP99, frameMax,
                    cpuFrame.Count, cpuFrame.P50, cpuFrame.P95, cpuFrame.P99, cpuFrame.Max,
                    cpuMain.Count, cpuMain.P50, cpuMain.P95, cpuMain.P99, cpuMain.Max,
                    cpuRender.Count, cpuRender.P50, cpuRender.P95, cpuRender.P99, cpuRender.Max,
                    cpuPresent.Count, cpuPresent.P50, cpuPresent.P95, cpuPresent.P99, cpuPresent.Max,
                    gpuFrame.Count, gpuFrame.P50, gpuFrame.P95, gpuFrame.P99, gpuFrame.Max,
                    _cameraStable ? 1 : 0, _projectionStable ? 1 : 0,
                    _visibleMin, _visibleMax, _missingMax, _runningJobsMax, _pendingUploadsMax,
                    render.SampleCount,
                    render.SchedulerPrepareP50Ms, render.SchedulerPrepareP95Ms,
                    render.SchedulerPrepareP99Ms, render.SchedulerPrepareMaxMs,
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

            private readonly struct TimingStats
            {
                public readonly int Count;
                public readonly double P50;
                public readonly double P95;
                public readonly double P99;
                public readonly double Max;

                public TimingStats(int count, double p50, double p95, double p99, double max)
                {
                    Count = count;
                    P50 = p50;
                    P95 = p95;
                    P99 = p99;
                    Max = max;
                }
            }

            private static TimingStats Summarize(double[] samples, int count)
            {
                if (count <= 0) return default;
                Array.Sort(samples, 0, count);
                return new TimingStats(
                    count,
                    Percentile(samples, count, 0.50),
                    Percentile(samples, count, 0.95),
                    Percentile(samples, count, 0.99),
                    samples[count - 1]);
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

            private static double Percentile(double[] sorted, int count, double fraction)
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
