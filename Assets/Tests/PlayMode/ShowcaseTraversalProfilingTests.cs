using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Diagnostic-only cost attribution for SceneIssue 20260825-192751-413-VoxelShowcase.
    /// This deliberately does not relax or replace the traversal acceptance test. It reproduces
    /// the same production movement path and records where the elapsed frame time is spent before
    /// selecting another optimization.
    /// </summary>
    public sealed class ShowcaseTraversalProfilingTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int TraversalFrames = 420;

        private sealed class FrameSample
        {
            public int Frame;
            public Vector3 Position;
            public double PlayerLoopMs;
            public double RenderMs;
            public double TotalMs;
            public bool Streaming;
            public int Visible;
            public int Missing;
            public int Dirty;
            public int Jobs;
            public int UploadMeshes;
            public long PendingUploadBytes;
            public long UploadedBytes;
            public double SchedulerMs;
            public double AdmissionMs;
            public double WorkerMs;
            public double UploadMs;
            public double GcCollectMs;
            public double MainThreadMs;
            public double RenderThreadMs;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ContinuousTraversalReportsPlayerLoopRenderAndStreamingCosts()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
            VoxelFarTerrain far = UnityEngine.Object.FindFirstObjectByType<VoxelFarTerrain>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(far);
            Assert.NotNull(camera);

            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseTraversalProfilingTests.Traversal",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            ProfilerRecorder scheduler = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "Voxel.Surface.SchedulerPrepare", 1);
            ProfilerRecorder admission = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "Voxel.Surface.WorkerAdmission", 1);
            ProfilerRecorder worker = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "Voxel.Surface.WorkerPrepare", 1);
            ProfilerRecorder upload = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "Voxel.Surface.Upload", 1);
            ProfilerRecorder gcCollect = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC.Collect", 1);
            // These built-in recorder names are platform-dependent. Invalid recorders are reported
            // as unavailable rather than treated as evidence; the Stopwatch split remains valid.
            ProfilerRecorder mainThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal, "Main Thread", 1);
            ProfilerRecorder renderThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal, "Render Thread", 1);

            try
            {
                yield return WaitForFallbackSafeVisibleCoverage(camera, far, 1200);

                Vector3 origin = showcase.transform.position;
                Quaternion originRotation = showcase.transform.rotation;
                var samples = new List<FrameSample>(TraversalFrames);
                var playerLoopClock = new Stopwatch();
                var renderClock = new Stopwatch();

                for (int frame = 0; frame < TraversalFrames; frame++)
                {
                    float progress = frame / (TraversalFrames - 1f);
                    Vector3 position = origin + new Vector3(
                        frame * 0.5f,
                        0f,
                        Mathf.Sin(progress * Mathf.PI * 6f) * 18f);
                    showcase.transform.position = position;
                    showcase.transform.rotation = originRotation;

                    playerLoopClock.Restart();
                    yield return null;
                    playerLoopClock.Stop();

                    double schedulerMs = RecorderMs(in scheduler);
                    double admissionMs = RecorderMs(in admission);
                    double workerMs = RecorderMs(in worker);
                    double uploadMs = RecorderMs(in upload);
                    double gcCollectMs = RecorderMs(in gcCollect);
                    double mainThreadMs = RecorderMs(in mainThread);
                    double renderThreadMs = RecorderMs(in renderThread);

                    renderClock.Restart();
                    camera.Render();
                    renderClock.Stop();

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        $"Profiling traversal frame {frame} synchronously completed geometry work.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"Profiling traversal frame {frame} lost every visible voxel draw.");
                    if (NearCoverageIsIncomplete(in metrics))
                    {
                        Assert.LessOrEqual(far.HoleRadiusMetres, 0.05f,
                            $"Profiling traversal frame {frame} opened a {far.HoleRadiusMetres:F2} m "
                          + "far-field hole while near coverage was incomplete.");
                    }

                    bool streaming = metrics.SolidDirtyChunks > 0
                                  || metrics.RunningSolidJobs > 0
                                  || metrics.SolidMeshesAwaitingUpload > 0
                                  || metrics.SolidPendingUploadBytes > 0
                                  || metrics.LastFrameSolidUploadedBytes > 0;

                    samples.Add(new FrameSample
                    {
                        Frame = frame,
                        Position = position,
                        PlayerLoopMs = playerLoopClock.Elapsed.TotalMilliseconds,
                        RenderMs = renderClock.Elapsed.TotalMilliseconds,
                        TotalMs = playerLoopClock.Elapsed.TotalMilliseconds
                                + renderClock.Elapsed.TotalMilliseconds,
                        Streaming = streaming,
                        Visible = metrics.VisibleSolidChunks,
                        Missing = metrics.MissingVisibleSolidChunks,
                        Dirty = metrics.SolidDirtyChunks,
                        Jobs = metrics.RunningSolidJobs,
                        UploadMeshes = metrics.SolidMeshesAwaitingUpload,
                        PendingUploadBytes = metrics.SolidPendingUploadBytes,
                        UploadedBytes = metrics.LastFrameSolidUploadedBytes,
                        SchedulerMs = schedulerMs,
                        AdmissionMs = admissionMs,
                        WorkerMs = workerMs,
                        UploadMs = uploadMs,
                        GcCollectMs = gcCollectMs,
                        MainThreadMs = mainThreadMs,
                        RenderThreadMs = renderThreadMs,
                    });
                }

                LogSummary("all", samples);
                var streamingSamples = samples.FindAll(sample => sample.Streaming);
                var idleSamples = samples.FindAll(sample => !sample.Streaming);
                LogSummary("streaming-active", streamingSamples);
                LogSummary("streaming-inactive", idleSamples);

                samples.Sort((left, right) => right.TotalMs.CompareTo(left.TotalMs));
                int slowCount = Math.Min(16, samples.Count);
                for (int i = 0; i < slowCount; i++)
                {
                    FrameSample sample = samples[i];
                    UnityEngine.Debug.Log(
                        $"### SHOWCASE_TRAVERSAL_SLOW rank={i + 1} frame={sample.Frame} "
                      + $"total={sample.TotalMs:F3}ms loop={sample.PlayerLoopMs:F3}ms "
                      + $"render={sample.RenderMs:F3}ms streaming={sample.Streaming} "
                      + $"pos=({sample.Position.x:F1},{sample.Position.y:F1},{sample.Position.z:F1}) "
                      + $"visible={sample.Visible} missing={sample.Missing} dirty={sample.Dirty} "
                      + $"jobs={sample.Jobs} uploadMeshes={sample.UploadMeshes} "
                      + $"pendingBytes={sample.PendingUploadBytes} uploadedBytes={sample.UploadedBytes} "
                      + $"prof[scheduler={sample.SchedulerMs:F3} admission={sample.AdmissionMs:F3} "
                      + $"worker={sample.WorkerMs:F3} upload={sample.UploadMs:F3} "
                      + $"gc={sample.GcCollectMs:F3} main={sample.MainThreadMs:F3} "
                      + $"renderThread={sample.RenderThreadMs:F3}]");
                }

                UnityEngine.Debug.Log(
                    $"### SHOWCASE_TRAVERSAL_PROFILER_AVAIL scheduler={scheduler.Valid} "
                  + $"admission={admission.Valid} worker={worker.Valid} upload={upload.Valid} "
                  + $"gc={gcCollect.Valid} main={mainThread.Valid} renderThread={renderThread.Valid}");

                VoxelSurfaceMetrics stageMetrics = VoxelRenderBridge.SurfaceMetrics;
                LogStageTiming("scheduler", in stageMetrics.SchedulerPrepareTiming);
                LogStageTiming("journal", in stageMetrics.ChangeJournalTiming);
                LogStageTiming("invalidation", in stageMetrics.InvalidationTiming);
                LogStageTiming("discovery", in stageMetrics.SurfaceDiscoveryTiming);
                LogStageTiming("worker", in stageMetrics.WorkerPrepareTiming);
                LogStageTiming("visibility", in stageMetrics.VisibilityTiming);
                LogStageTiming("rule-sync", in stageMetrics.RuleSyncTiming);
                LogStageTiming("residency-prune", in stageMetrics.ResidencyPruneTiming);
                LogStageTiming("capacity", in stageMetrics.CapacityTiming);
                LogStageTiming("build-selection", in stageMetrics.BuildSelectionTiming);
                LogStageTiming("snapshot", in stageMetrics.SnapshotTiming);
                LogStageTiming("density-only", in stageMetrics.DensityOnlyTiming);
                LogStageTiming("density-turnaround", in stageMetrics.DensityJobTurnaroundTiming);
                LogStageTiming("topology-turnaround", in stageMetrics.TopologyJobTurnaroundTiming);
                LogStageTiming("topology-compact", in stageMetrics.TopologyCompactTiming);
                LogStageTiming("faceted-turnaround", in stageMetrics.FacetedJobTurnaroundTiming);
                LogStageTiming("faceted-merge", in stageMetrics.FacetedMergeTiming);
                LogStageTiming("profile-emit", in stageMetrics.ProfileEmitTiming);
                LogStageTiming("upload", in stageMetrics.UploadTiming);
                LogStageTiming("queue-latency", in stageMetrics.QueueLatencyTiming);
                LogStageTiming("build-latency", in stageMetrics.BuildLatencyTiming);
                LogStageTiming("gpu-build-latency", in stageMetrics.GpuBuildLatencyTiming);

                Assert.AreEqual(TraversalFrames, samples.Count,
                    "Diagnostic traversal did not capture the expected frame count.");
            }
            finally
            {
                scheduler.Dispose();
                admission.Dispose();
                worker.Dispose();
                upload.Dispose();
                gcCollect.Dispose();
                mainThread.Dispose();
                renderThread.Dispose();
                camera.targetTexture = previousTarget;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void LogSummary(string phase, List<FrameSample> samples)
        {
            if (samples.Count == 0)
            {
                UnityEngine.Debug.Log($"### SHOWCASE_TRAVERSAL_PROFILE phase={phase} frames=0");
                return;
            }

            var total = new List<double>(samples.Count);
            var loop = new List<double>(samples.Count);
            var render = new List<double>(samples.Count);
            var scheduler = new List<double>(samples.Count);
            var admission = new List<double>(samples.Count);
            var worker = new List<double>(samples.Count);
            var upload = new List<double>(samples.Count);
            var gc = new List<double>(samples.Count);
            foreach (FrameSample sample in samples)
            {
                total.Add(sample.TotalMs);
                loop.Add(sample.PlayerLoopMs);
                render.Add(sample.RenderMs);
                scheduler.Add(sample.SchedulerMs);
                admission.Add(sample.AdmissionMs);
                worker.Add(sample.WorkerMs);
                upload.Add(sample.UploadMs);
                gc.Add(sample.GcCollectMs);
            }

            total.Sort();
            loop.Sort();
            render.Sort();
            scheduler.Sort();
            admission.Sort();
            worker.Sort();
            upload.Sort();
            gc.Sort();

            UnityEngine.Debug.Log(
                $"### SHOWCASE_TRAVERSAL_PROFILE phase={phase} frames={samples.Count} "
              + $"total[p50={Percentile(total, 0.50):F3} p95={Percentile(total, 0.95):F3} "
              + $"p99={Percentile(total, 0.99):F3} max={total[^1]:F3}] "
              + $"loop[p50={Percentile(loop, 0.50):F3} p95={Percentile(loop, 0.95):F3} "
              + $"p99={Percentile(loop, 0.99):F3} max={loop[^1]:F3}] "
              + $"render[p50={Percentile(render, 0.50):F3} p95={Percentile(render, 0.95):F3} "
              + $"p99={Percentile(render, 0.99):F3} max={render[^1]:F3}] "
              + $"profP95[scheduler={Percentile(scheduler, 0.95):F3} "
              + $"admission={Percentile(admission, 0.95):F3} worker={Percentile(worker, 0.95):F3} "
              + $"upload={Percentile(upload, 0.95):F3} gc={Percentile(gc, 0.95):F3}]");
        }

        private static void LogStageTiming(string stage, in VoxelTimingSummary timing)
        {
            UnityEngine.Debug.Log(
                $"### SHOWCASE_TRAVERSAL_STAGE stage={stage} samples={timing.SampleCount} "
              + $"last={timing.LastMs:F3}ms p50={timing.P50Ms:F3}ms "
              + $"p95={timing.P95Ms:F3}ms p99={timing.P99Ms:F3}ms max={timing.MaxMs:F3}ms");
        }

        private static double RecorderMs(in ProfilerRecorder recorder) =>
            recorder.Valid ? recorder.LastValue * 0.000001 : 0.0;

        private static double Percentile(List<double> sorted, double percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(sorted.Count * percentile)) - 1,
                0,
                sorted.Count - 1);
            return sorted[index];
        }

        private static IEnumerator WaitForFallbackSafeVisibleCoverage(
            Camera camera,
            VoxelFarTerrain far,
            int maxFrames)
        {
            int stableFrames = 0;
            VoxelSurfaceMetrics last = default;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                yield return null;
                camera.Render();
                last = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                    "Geometry work blocked the player frame while preparing the profiling traversal.");

                bool nearIncomplete = NearCoverageIsIncomplete(in last);
                bool fallbackSafe = !nearIncomplete || far.HoleRadiusMetres <= 0.05f;
                bool ready = last.VisibleSolidChunks > 0 && fallbackSafe;
                stableFrames = ready ? stableFrames + 1 : 0;
                if (stableFrames >= 4)
                    yield break;
            }

            Assert.Fail(
                $"Showcase never reached four fallback-safe visible frames for profiling; "
              + $"known={last.SolidKnownChunks} resident={last.SolidResidentChunks} "
              + $"dirty={last.SolidDirtyChunks} visible={last.VisibleSolidChunks} "
              + $"missing={last.MissingVisibleSolidChunks} jobs={last.RunningSolidJobs} "
              + $"farHole={far.HoleRadiusMetres:F2}m.");
        }

        private static bool NearCoverageIsIncomplete(in VoxelSurfaceMetrics metrics) =>
            metrics.MissingVisibleSolidChunks > 0
            || metrics.SolidDirtyChunks > 0
            || metrics.RunningSolidJobs > 0
            || metrics.SolidMeshesAwaitingUpload > 0
            || metrics.SolidPendingUploadBytes > 0;

        private static void SetShowcaseField<T>(VoxelShowcase showcase, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"VoxelShowcase.{fieldName} was not found.");
            field.SetValue(showcase, value);
        }
    }
}
