using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Minimal liveness discriminator for SceneIssue 20260825-192751-413.
    ///
    /// The full showcase traversal proved that a few GPU chunks can complete before all workers
    /// become permanently pending. This smaller harness watches the shared-mirror state directly:
    /// demanded recovery must advance while older covered GPU work is still in flight, and a
    /// nonresident region touched only by the exact snapshot's optional sampling halo must remain
    /// canonical empty rather than becoming an unrecoverable GPU admission dependency.
    /// </summary>
    public sealed class GpuSurfaceMirrorRecoveryLivenessTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const float TravelMetres = 96f;
        private const float StepMetres = 0.5f;
        private const double MaxWarmupSeconds = 60.0;
        private const int ObservationFrames = 900;
        private const int MaxBacklogActiveStallFrames = 180;

        [UnityTest, Timeout(180000)]
        public IEnumerator DemandRecoveryCannotBeStarvedByCoveredGpuWork()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(camera);

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "GpuSurfaceMirrorRecoveryLivenessTests.Recovery",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                VoxelSurfaceMetrics metrics = default;
                var warmupClock = Stopwatch.StartNew();
                while (warmupClock.Elapsed.TotalSeconds < MaxWarmupSeconds)
                {
                    yield return null;
                    camera.Render();
                    metrics = VoxelRenderBridge.SurfaceMetrics;
                    if (metrics.VisibleSolidChunks > 0
                        && GpuSurfaceMirrorCoordinator.ReadyBlockCount > 0)
                        break;
                }

                Assert.Greater(metrics.VisibleSolidChunks, 0,
                    $"Focused mirror-liveness harness never reached initial visible coverage within "
                  + $"{MaxWarmupSeconds:F0}s; missing={metrics.MissingVisibleSolidChunks}, "
                  + $"jobs={metrics.RunningSolidJobs}, gpuCompleted={metrics.GpuCompletedSolidBuilds}, "
                  + $"gpuWaitSlices={metrics.GpuReadbackWaitSlices}.");
                Assert.Greater(GpuSurfaceMirrorCoordinator.ReadyBlockCount, 0,
                    $"Focused mirror-liveness harness never initialized the shared GPU mirror "
                  + $"within {MaxWarmupSeconds:F0}s.");

                Vector3 origin = showcase.transform.position;
                Vector3 position = origin;
                ulong baselineGpuCompleted = metrics.GpuCompletedSolidBuilds;
                ulong baselineConcurrentRecovery =
                    GpuSurfaceMirrorCoordinator.ConcurrentDemandRecoverySlices;
                ulong lastGpuCompleted = baselineGpuCompleted;
                int lastReadyBlocks = GpuSurfaceMirrorCoordinator.ReadyBlockCount;
                int stalledBacklogActiveFrames = 0;
                int maxStalledBacklogActiveFrames = 0;
                bool sawRecoveryBacklog = false;
                bool sawBacklogOverlapActiveExtraction = false;
                bool sawConcurrentDemandRecovery = false;

                for (int frame = 0; frame < ObservationFrames; frame++)
                {
                    if (position.x - origin.x < TravelMetres)
                    {
                        float step = Mathf.Min(StepMetres, TravelMetres - (position.x - origin.x));
                        position.x += step;
                        showcase.transform.position = position;
                    }

                    yield return null;
                    camera.Render();
                    metrics = VoxelRenderBridge.SurfaceMetrics;

                    bool recoveryBacklog = !GpuSurfaceMirrorCoordinator.RecoveryComplete;
                    int activeExtractions = GpuSurfaceMirrorCoordinator.ActiveExtractions;
                    int readyBlocks = GpuSurfaceMirrorCoordinator.ReadyBlockCount;
                    ulong gpuCompleted = metrics.GpuCompletedSolidBuilds;
                    bool progress = readyBlocks != lastReadyBlocks || gpuCompleted != lastGpuCompleted;

                    sawRecoveryBacklog |= recoveryBacklog;
                    sawBacklogOverlapActiveExtraction |= recoveryBacklog && activeExtractions > 0;
                    sawConcurrentDemandRecovery |=
                        GpuSurfaceMirrorCoordinator.ConcurrentDemandRecoverySlices
                        > baselineConcurrentRecovery;

                    if (recoveryBacklog && activeExtractions > 0 && !progress)
                        stalledBacklogActiveFrames++;
                    else
                        stalledBacklogActiveFrames = 0;

                    maxStalledBacklogActiveFrames = Mathf.Max(
                        maxStalledBacklogActiveFrames, stalledBacklogActiveFrames);

                    Assert.Less(stalledBacklogActiveFrames, MaxBacklogActiveStallFrames,
                        $"Shared GPU mirror recovery made no progress for "
                      + $"{stalledBacklogActiveFrames} rendered frames while "
                      + $"activeExtractions={activeExtractions}, recoveryPending={recoveryBacklog}, "
                      + $"readyBlocks={readyBlocks}, gpuCompleted={gpuCompleted}, "
                      + $"gpuWaitSlices={metrics.GpuReadbackWaitSlices}, "
                      + $"visible={metrics.VisibleSolidChunks}, "
                      + $"missing={metrics.MissingVisibleSolidChunks}. Covered GPU work is starving "
                      + "new demanded mirror blocks instead of yielding a recovery drain point.");

                    lastReadyBlocks = readyBlocks;
                    lastGpuCompleted = gpuCompleted;

                    if (position.x - origin.x >= TravelMetres
                        && sawRecoveryBacklog
                        && sawConcurrentDemandRecovery
                        && gpuCompleted >= baselineGpuCompleted + 4
                        && metrics.VisibleSolidChunks > 0
                        && GpuSurfaceMirrorCoordinator.OptionalNonResidentHaloBlocksAccepted > 0)
                        break;
                }

                Assert.True(sawRecoveryBacklog,
                    "Focused traversal never exercised shared-mirror demand recovery.");
                Assert.True(sawBacklogOverlapActiveExtraction,
                    "Focused traversal never overlapped new mirror demand with an older GPU "
                  + "extraction, so it did not cover the readback head-of-line condition.");
                Assert.Greater(
                    GpuSurfaceMirrorCoordinator.ConcurrentDemandRecoverySlices,
                    baselineConcurrentRecovery,
                    "New demanded mirror blocks never advanced while older GPU extractions were "
                  + "still active; demand remains globally serialized behind async readbacks.");
                Assert.Greater(GpuSurfaceMirrorCoordinator.OptionalNonResidentHaloBlocksAccepted, 0ul,
                    "Focused traversal never exercised a nonresident optional snapshot halo; the "
                  + "regression did not cover the admission state that previously stayed pending.");
                Assert.GreaterOrEqual(metrics.GpuCompletedSolidBuilds - baselineGpuCompleted, 4ul,
                    $"Focused traversal did not sustain GPU completion after new demand: "
                  + $"completed={metrics.GpuCompletedSolidBuilds - baselineGpuCompleted}, "
                  + $"activeExtractions={GpuSurfaceMirrorCoordinator.ActiveExtractions}, "
                  + $"recoveryPending={!GpuSurfaceMirrorCoordinator.RecoveryComplete}, "
                  + $"readyBlocks={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, "
                  + $"concurrentRecoverySlices="
                  + $"{GpuSurfaceMirrorCoordinator.ConcurrentDemandRecoverySlices - baselineConcurrentRecovery}, "
                  + $"optionalHaloAccepted="
                  + $"{GpuSurfaceMirrorCoordinator.OptionalNonResidentHaloBlocksAccepted}, "
                  + $"overlappedActiveExtraction={sawBacklogOverlapActiveExtraction}, "
                  + $"maxBacklogActiveStallFrames={maxStalledBacklogActiveFrames}.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}