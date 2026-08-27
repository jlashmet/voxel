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
    /// Diagnostic-only A/B for SceneIssue 20260825-192751-413-VoxelShowcase. Production defaults
    /// remain untouched: this test temporarily matches the in-flight surface-build ceiling to the
    /// eight Unity job workers used by the traversal CI lane, then restores the configured value.
    /// </summary>
    public sealed class ShowcaseTraversalConcurrencyProfilingTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int TraversalFrames = 420;

        [UnityTest, Timeout(900000)]
        public IEnumerator EightBuildCeilingReportsTraversalAndSnapshotCost()
        {
            int previousCeiling = VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging;
            VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = 8;

            ProfilerRecorder snapshot = default;
            ProfilerRecorder worker = default;
            ProfilerRecorder scheduler = default;
            RenderTexture target = null;
            Camera camera = null;
            RenderTexture previousTarget = null;

            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;

                VoxelShowcase showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                VoxelFarTerrain far = UnityEngine.Object.FindFirstObjectByType<VoxelFarTerrain>();
                camera = Camera.main;
                Assert.NotNull(showcase);
                Assert.NotNull(far);
                Assert.NotNull(camera);

                SetShowcaseField(showcase, "m_FlyMode", true);
                SetShowcaseField(showcase, "_mouseLook", false);

                target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
                {
                    name = "ShowcaseTraversalConcurrencyProfilingTests.Traversal",
                    antiAliasing = 1,
                };
                previousTarget = camera.targetTexture;
                target.Create();
                camera.targetTexture = target;

                snapshot = ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts, "Voxel.Surface.Snapshot", 1);
                worker = ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts, "Voxel.Surface.WorkerPrepare", 1);
                scheduler = ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts, "Voxel.Surface.SchedulerPrepare", 1);

                yield return WaitForFallbackSafeVisibleCoverage(camera, far, 1200);

                Vector3 origin = showcase.transform.position;
                Quaternion originRotation = showcase.transform.rotation;
                var frameMs = new List<double>(TraversalFrames);
                var snapshotMs = new List<double>(TraversalFrames);
                var workerMs = new List<double>(TraversalFrames);
                var schedulerMs = new List<double>(TraversalFrames);
                var frameClock = new Stopwatch();
                int maxRunningJobs = 0;
                int maxMissing = 0;

                for (int frame = 0; frame < TraversalFrames; frame++)
                {
                    float progress = frame / (TraversalFrames - 1f);
                    showcase.transform.position = origin + new Vector3(
                        frame * 0.5f, 0f, Mathf.Sin(progress * Mathf.PI * 6f) * 18f);
                    showcase.transform.rotation = originRotation;

                    frameClock.Restart();
                    yield return null;
                    camera.Render();
                    frameClock.Stop();

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        $"8-build traversal frame {frame} synchronously completed geometry work.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"8-build traversal frame {frame} lost every visible voxel draw.");
                    if (NearCoverageIsIncomplete(in metrics))
                    {
                        Assert.LessOrEqual(far.HoleRadiusMetres, 0.05f,
                            $"8-build traversal frame {frame} opened a {far.HoleRadiusMetres:F2} m "
                          + "far-field hole while near geometry was incomplete.");
                    }

                    frameMs.Add(frameClock.Elapsed.TotalMilliseconds);
                    snapshotMs.Add(RecorderMs(in snapshot));
                    workerMs.Add(RecorderMs(in worker));
                    schedulerMs.Add(RecorderMs(in scheduler));
                    maxRunningJobs = Math.Max(maxRunningJobs, metrics.RunningSolidJobs);
                    maxMissing = Math.Max(maxMissing, metrics.MissingVisibleSolidChunks);
                }

                frameMs.Sort();
                snapshotMs.Sort();
                workerMs.Sort();
                schedulerMs.Sort();
                UnityEngine.Debug.Log(
                    $"### SHOWCASE_CONCURRENCY_PROFILE buildCeiling=8 frames={TraversalFrames} "
                  + $"frame[p50={Percentile(frameMs, 0.50):F3} p95={Percentile(frameMs, 0.95):F3} "
                  + $"p99={Percentile(frameMs, 0.99):F3} max={frameMs[^1]:F3}] "
                  + $"snapshot[p95={Percentile(snapshotMs, 0.95):F3} max={snapshotMs[^1]:F3}] "
                  + $"worker[p95={Percentile(workerMs, 0.95):F3} max={workerMs[^1]:F3}] "
                  + $"scheduler[p95={Percentile(schedulerMs, 0.95):F3} max={schedulerMs[^1]:F3}] "
                  + $"maxRunningJobs={maxRunningJobs} maxMissing={maxMissing} "
                  + $"recorders[snapshot={snapshot.Valid} worker={worker.Valid} scheduler={scheduler.Valid}]");
            }
            finally
            {
                if (snapshot.Valid) snapshot.Dispose();
                if (worker.Valid) worker.Dispose();
                if (scheduler.Valid) scheduler.Dispose();
                if (camera != null) camera.targetTexture = previousTarget;
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = previousCeiling;
            }
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
                    "Geometry work blocked the player frame while preparing the 8-build discriminator.");
                bool incomplete = NearCoverageIsIncomplete(in last);
                bool ready = last.VisibleSolidChunks > 0
                          && (!incomplete || far.HoleRadiusMetres <= 0.05f);
                stableFrames = ready ? stableFrames + 1 : 0;
                if (stableFrames >= 4) yield break;
            }

            Assert.Fail(
                $"8-build discriminator never reached fallback-safe coverage; "
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

        private static void SetShowcaseField<T>(VoxelShowcase showcase, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"VoxelShowcase.{fieldName} was not found.");
            field.SetValue(showcase, value);
        }
    }
}
