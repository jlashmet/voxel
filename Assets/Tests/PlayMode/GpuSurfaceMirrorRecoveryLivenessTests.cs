using System.Collections;
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
    /// a demanded recovery backlog may overlap an already-dispatched extraction briefly, but that
    /// overlap must drain. If covered work can continuously reacquire the mirror while recovery is
    /// queued, ReadyBlockCount and completed GPU builds stop changing and new camera coverage can
    /// never become drawable.
    /// </summary>
    public sealed class GpuSurfaceMirrorRecoveryLivenessTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const float TravelMetres = 96f;
        private const float StepMetres = 0.5f;
        private const int WarmupFrames = 360;
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

            VoxelSurfaceMetrics metrics = default;
            for (int i = 0; i < WarmupFrames; i++)
            {
                yield return null;
                camera.Render();
                metrics = VoxelRenderBridge.SurfaceMetrics;
                if (metrics.VisibleSolidChunks > 0
                    && GpuSurfaceMirrorCoordinator.ReadyBlockCount > 0)
                    break;
            }

            Assert.Greater(metrics.VisibleSolidChunks, 0,
                "Focused mirror-liveness harness never reached initial visible coverage.");
            Assert.Greater(GpuSurfaceMirrorCoordinator.ReadyBlockCount, 0,
                "Focused mirror-liveness harness never initialized the shared GPU mirror.");

            Vector3 origin = showcase.transform.position;
            Vector3 position = origin;
            ulong baselineGpuCompleted = metrics.GpuCompletedSolidBuilds;
            ulong lastGpuCompleted = baselineGpuCompleted;
            int lastReadyBlocks = GpuSurfaceMirrorCoordinator.ReadyBlockCount;
            int stalledBacklogActiveFrames = 0;
            int maxStalledBacklogActiveFrames = 0;
            bool sawRecoveryBacklog = false;
            bool sawBacklogOverlapActiveExtraction = false;

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
                    && gpuCompleted >= baselineGpuCompleted + 4
                    && metrics.VisibleSolidChunks > 0)
                    break;
            }

            Assert.True(sawRecoveryBacklog,
                "Focused traversal never exercised shared-mirror demand recovery.");
            Assert.GreaterOrEqual(metrics.GpuCompletedSolidBuilds - baselineGpuCompleted, 4ul,
                $"Focused traversal did not sustain GPU completion after new demand: "
              + $"completed={metrics.GpuCompletedSolidBuilds - baselineGpuCompleted}, "
              + $"activeExtractions={GpuSurfaceMirrorCoordinator.ActiveExtractions}, "
              + $"recoveryPending={!GpuSurfaceMirrorCoordinator.RecoveryComplete}, "
              + $"readyBlocks={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, "
              + $"overlappedActiveExtraction={sawBacklogOverlapActiveExtraction}, "
              + $"maxBacklogActiveStallFrames={maxStalledBacklogActiveFrames}.");
        }
    }
}
