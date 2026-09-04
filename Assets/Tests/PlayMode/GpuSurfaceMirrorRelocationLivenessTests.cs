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
    /// Regression for large camera/player relocations that replace the near-ring mirror working set
    /// in one step. Slow traversal is covered separately; this test isolates the admission-only
    /// saturation observed by Kentridge macro survey evidence after a distant survey relocation.
    /// </summary>
    public sealed class GpuSurfaceMirrorRelocationLivenessTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const float RelocationMetres = 384f;
        private const double MaxWarmupSeconds = 60.0;
        private const double MaxObservationSeconds = 60.0;
        private const double MaxSaturatedAdmissionSeconds = 20.0;

        [UnityTest, Timeout(180000)]
        public IEnumerator DistantRelocationCannotLeaveEveryGpuWorkerAdmissionPending()
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
                name = "GpuSurfaceMirrorRelocationLivenessTests.Relocation",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                VoxelSurfaceMetrics metrics = default;
                var warmup = Stopwatch.StartNew();
                while (warmup.Elapsed.TotalSeconds < MaxWarmupSeconds)
                {
                    yield return null;
                    camera.Render();
                    metrics = VoxelRenderBridge.SurfaceMetrics;
                    if (metrics.VisibleSolidChunks > 0
                        && GpuSurfaceMirrorCoordinator.ReadyBlockCount > 0
                        && metrics.GpuCompletedSolidBuilds > 0)
                        break;
                }

                Assert.Greater(metrics.VisibleSolidChunks, 0,
                    "Relocation liveness harness never reached initial visible coverage.");
                Assert.Greater(GpuSurfaceMirrorCoordinator.ReadyBlockCount, 0,
                    "Relocation liveness harness never initialized the shared GPU mirror.");
                Assert.Greater(metrics.GpuCompletedSolidBuilds, 0ul,
                    "Relocation liveness harness never completed an initial GPU build.");

                ulong baselineCompleted = metrics.GpuCompletedSolidBuilds;
                Vector3 relocated = showcase.transform.position;
                relocated.x += RelocationMetres;
                showcase.transform.position = relocated;

                bool sawRecoveryBacklog = false;
                bool sawSaturatedAdmission = false;
                double saturatedAdmissionStarted = -1.0;
                var observation = Stopwatch.StartNew();
                while (observation.Elapsed.TotalSeconds < MaxObservationSeconds)
                {
                    yield return null;
                    camera.Render();
                    metrics = VoxelRenderBridge.SurfaceMetrics;

                    int pending = GpuSurfaceMirrorCoordinator.PendingBlockCount;
                    int demand = GpuSurfaceMirrorCoordinator.DemandFootprintCount;
                    int active = GpuSurfaceMirrorCoordinator.ActiveExtractions;
                    bool recoveryBacklog = pending > 0;
                    bool saturatedAdmission = recoveryBacklog
                        && demand >= GpuSurfaceMirrorCoordinator.MaxConcurrentExtractionChains
                        && active == 0;

                    sawRecoveryBacklog |= recoveryBacklog;
                    sawSaturatedAdmission |= saturatedAdmission;
                    if (saturatedAdmission)
                    {
                        if (saturatedAdmissionStarted < 0.0)
                            saturatedAdmissionStarted = observation.Elapsed.TotalSeconds;
                        double stalledSeconds =
                            observation.Elapsed.TotalSeconds - saturatedAdmissionStarted;
                        Assert.Less(stalledSeconds, MaxSaturatedAdmissionSeconds,
                            $"All GPU workers stayed mirror-admission pending for "
                          + $"{stalledSeconds:F1}s after a {RelocationMetres:F0}m relocation: "
                          + $"ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, "
                          + $"pending={pending}, demand={demand}, active={active}, "
                          + $"mixedResident={GpuSurfaceMirrorCoordinator.ResidentMixedBrickCount}/"
                          + $"{GpuSurfaceMirrorCoordinator.MirrorSlotCapacity}, "
                          + $"gpuCompleted={metrics.GpuCompletedSolidBuilds - baselineCompleted}, "
                          + $"visible={metrics.VisibleSolidChunks}, "
                          + $"missing={metrics.MissingVisibleSolidChunks}. Recovery must reclaim or "
                          + "publish enough of the replaced working set for at least one request to "
                          + "cross mirror admission.");
                    }
                    else
                    {
                        saturatedAdmissionStarted = -1.0;
                    }

                    if (sawRecoveryBacklog
                        && metrics.GpuCompletedSolidBuilds >= baselineCompleted + 4
                        && metrics.VisibleSolidChunks > 0
                        && demand < GpuSurfaceMirrorCoordinator.MaxConcurrentExtractionChains)
                        break;
                }

                Assert.True(sawRecoveryBacklog,
                    "Distant relocation never exercised shared-mirror recovery.");
                Assert.GreaterOrEqual(metrics.GpuCompletedSolidBuilds - baselineCompleted, 4ul,
                    $"GPU extraction did not recover useful throughput after a distant relocation: "
                  + $"completed={metrics.GpuCompletedSolidBuilds - baselineCompleted}, "
                  + $"ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, "
                  + $"pending={GpuSurfaceMirrorCoordinator.PendingBlockCount}, "
                  + $"demand={GpuSurfaceMirrorCoordinator.DemandFootprintCount}, "
                  + $"active={GpuSurfaceMirrorCoordinator.ActiveExtractions}, "
                  + $"mixedResident={GpuSurfaceMirrorCoordinator.ResidentMixedBrickCount}/"
                  + $"{GpuSurfaceMirrorCoordinator.MirrorSlotCapacity}, "
                  + $"sawSaturatedAdmission={sawSaturatedAdmission}.");
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
