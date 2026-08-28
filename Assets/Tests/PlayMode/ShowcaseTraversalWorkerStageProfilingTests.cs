using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Diagnostic-only per-frame worker-stage attribution for SceneIssue
    /// 20260825-192751-413-VoxelShowcase. This intentionally leaves production budgets and the
    /// traversal acceptance unchanged.
    /// </summary>
    public sealed class ShowcaseTraversalWorkerStageProfilingTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int TraversalFrames = 420;

        private sealed class Sample
        {
            public int Frame;
            public double SchedulerMs;
            public double AdmissionMs;
            public double WorkerMs;
            public double SnapshotMs;
            public double CompactMs;
            public double FacetedMergeMs;
            public double ProfileMs;
            public int Visible;
            public int Missing;
            public int Dirty;
            public int Jobs;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ContinuousTraversalReportsPerFrameWorkerStages()
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
                name = "ShowcaseTraversalWorkerStageProfilingTests.Traversal",
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
            ProfilerRecorder snapshot = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "Voxel.Surface.Snapshot", 1);
            ProfilerRecorder compact = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "Voxel.Surface.TopologyCompact", 1);
            ProfilerRecorder facetedMerge = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "Voxel.Surface.FacetedMerge", 1);
            ProfilerRecorder profile = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "Voxel.Surface.ProfileEmit", 1);

            try
            {
                yield return WaitForFallbackSafeVisibleCoverage(camera, far, 1200);

                Vector3 origin = showcase.transform.position;
                Quaternion originRotation = showcase.transform.rotation;
                var samples = new List<Sample>(TraversalFrames);

                for (int frame = 0; frame < TraversalFrames; frame++)
                {
                    float progress = frame / (TraversalFrames - 1f);
                    showcase.transform.position = origin + new Vector3(
                        frame * 0.5f, 0f, Mathf.Sin(progress * Mathf.PI * 6f) * 18f);
                    showcase.transform.rotation = originRotation;

                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        $"Worker-stage traversal frame {frame} synchronously completed geometry work.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"Worker-stage traversal frame {frame} lost every visible voxel draw.");
                    if (NearCoverageIsIncomplete(in metrics))
                    {
                        Assert.LessOrEqual(far.HoleRadiusMetres, 0.05f,
                            $"Worker-stage traversal frame {frame} opened a {far.HoleRadiusMetres:F2} m "
                          + "far-field hole while near coverage was incomplete.");
                    }

                    samples.Add(new Sample
                    {
                        Frame = frame,
                        SchedulerMs = RecorderMs(in scheduler),
                        AdmissionMs = RecorderMs(in admission),
                        WorkerMs = RecorderMs(in worker),
                        SnapshotMs = RecorderMs(in snapshot),
                        CompactMs = RecorderMs(in compact),
                        FacetedMergeMs = RecorderMs(in facetedMerge),
                        ProfileMs = RecorderMs(in profile),
                        Visible = metrics.VisibleSolidChunks,
                        Missing = metrics.MissingVisibleSolidChunks,
                        Dirty = metrics.SolidDirtyChunks,
                        Jobs = metrics.RunningSolidJobs,
                    });

                    camera.Render();
                }

                LogSummary(samples);
                samples.Sort((left, right) => right.WorkerMs.CompareTo(left.WorkerMs));
                int slowCount = Math.Min(20, samples.Count);
                for (int i = 0; i < slowCount; i++)
                {
                    Sample sample = samples[i];
                    UnityEngine.Debug.Log(
                        $"### SHOWCASE_WORKER_STAGE_SLOW rank={i + 1} frame={sample.Frame} "
                      + $"scheduler={sample.SchedulerMs:F3}ms admission={sample.AdmissionMs:F3}ms "
                      + $"worker={sample.WorkerMs:F3}ms snapshot={sample.SnapshotMs:F3}ms "
                      + $"compact={sample.CompactMs:F3}ms facetedMerge={sample.FacetedMergeMs:F3}ms "
                      + $"profile={sample.ProfileMs:F3}ms visible={sample.Visible} "
                      + $"missing={sample.Missing} dirty={sample.Dirty} jobs={sample.Jobs}");
                }

                UnityEngine.Debug.Log(
                    $"### SHOWCASE_WORKER_STAGE_AVAIL scheduler={scheduler.Valid} "
                  + $"admission={admission.Valid} worker={worker.Valid} snapshot={snapshot.Valid} "
                  + $"compact={compact.Valid} facetedMerge={facetedMerge.Valid} profile={profile.Valid}");
                Assert.AreEqual(TraversalFrames, samples.Count);
            }
            finally
            {
                scheduler.Dispose();
                admission.Dispose();
                worker.Dispose();
                snapshot.Dispose();
                compact.Dispose();
                facetedMerge.Dispose();
                profile.Dispose();
                camera.targetTexture = previousTarget;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void LogSummary(List<Sample> samples)
        {
            var scheduler = new List<double>(samples.Count);
            var admission = new List<double>(samples.Count);
            var worker = new List<double>(samples.Count);
            var snapshot = new List<double>(samples.Count);
            var compact = new List<double>(samples.Count);
            var facetedMerge = new List<double>(samples.Count);
            var profile = new List<double>(samples.Count);
            foreach (Sample sample in samples)
            {
                scheduler.Add(sample.SchedulerMs);
                admission.Add(sample.AdmissionMs);
                worker.Add(sample.WorkerMs);
                snapshot.Add(sample.SnapshotMs);
                compact.Add(sample.CompactMs);
                facetedMerge.Add(sample.FacetedMergeMs);
                profile.Add(sample.ProfileMs);
            }

            scheduler.Sort();
            admission.Sort();
            worker.Sort();
            snapshot.Sort();
            compact.Sort();
            facetedMerge.Sort();
            profile.Sort();

            UnityEngine.Debug.Log(
                $"### SHOWCASE_WORKER_STAGE frames={samples.Count} "
              + $"scheduler[p95={Percentile(scheduler, 0.95):F3} max={scheduler[^1]:F3}] "
              + $"admission[p95={Percentile(admission, 0.95):F3} max={admission[^1]:F3}] "
              + $"worker[p50={Percentile(worker, 0.50):F3} p95={Percentile(worker, 0.95):F3} "
              + $"p99={Percentile(worker, 0.99):F3} max={worker[^1]:F3}] "
              + $"snapshot[p95={Percentile(snapshot, 0.95):F3} max={snapshot[^1]:F3}] "
              + $"compact[p95={Percentile(compact, 0.95):F3} max={compact[^1]:F3}] "
              + $"facetedMerge[p95={Percentile(facetedMerge, 0.95):F3} max={facetedMerge[^1]:F3}] "
              + $"profile[p95={Percentile(profile, 0.95):F3} max={profile[^1]:F3}]");
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
            Camera camera, VoxelFarTerrain far, int maxFrames)
        {
            int stableFrames = 0;
            VoxelSurfaceMetrics last = default;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                yield return null;
                camera.Render();
                last = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                    "Geometry work blocked the player frame while preparing worker-stage profiling.");
                bool nearIncomplete = NearCoverageIsIncomplete(in last);
                bool ready = last.VisibleSolidChunks > 0
                          && (!nearIncomplete || far.HoleRadiusMetres <= 0.05f);
                stableFrames = ready ? stableFrames + 1 : 0;
                if (stableFrames >= 4) yield break;
            }

            Assert.Fail(
                $"Showcase never reached fallback-safe visible coverage for worker-stage profiling; "
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
