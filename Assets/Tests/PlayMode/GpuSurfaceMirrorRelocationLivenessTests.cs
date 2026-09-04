using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Regression for large camera/player relocations that replace the near-ring mirror working set
    /// in one step. Slow traversal is covered separately; these tests isolate the admission-only
    /// saturation observed by Kentridge macro survey evidence after a distant survey relocation.
    /// </summary>
    public sealed class GpuSurfaceMirrorRelocationLivenessTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const float PrimeTravelMetres = 32f;
        private const float PrimeStepMetres = 0.5f;
        private const float RelocationMetres = 384f;
        private const double MaxCoverageWarmupSeconds = 30.0;
        private const double MaxGpuPrimingSeconds = 45.0;
        private const double MaxObservationSeconds = 60.0;
        private const double MaxSaturatedAdmissionSeconds = 20.0;

        [UnityTest, Timeout(180000)]
        public IEnumerator DistantRelocationCannotLeaveEveryGpuWorkerAdmissionPending()
        {
            IEnumerator execution = RunRelocation(distantChangeChurn: false);
            while (execution.MoveNext())
                yield return execution.Current;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DistantUnrelatedReadyBlockChangesCannotStarveRelocatedCoverage()
        {
            IEnumerator execution = RunRelocation(distantChangeChurn: true);
            while (execution.MoveNext())
                yield return execution.Current;
        }

        private IEnumerator RunRelocation(bool distantChangeChurn)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(camera);
            Assert.False(CpuTransvoxelChunkCache.GpuCutoverDisabled,
                "Relocation liveness requires the production near-ring GPU cutover.");

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "GpuSurfaceMirrorRelocationLivenessTests.Relocation",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            bool heldControlDemand = false;
            int3 controlBlock = default;
            int3 controlCoreMin = default;
            int3 controlCoreMax = default;

            try
            {
                VoxelSurfaceMetrics metrics = default;
                var coverageWarmup = Stopwatch.StartNew();
                while (coverageWarmup.Elapsed.TotalSeconds < MaxCoverageWarmupSeconds)
                {
                    yield return null;
                    camera.Render();
                    metrics = VoxelRenderBridge.SurfaceMetrics;
                    if (metrics.VisibleSolidChunks > 0)
                        break;
                }

                Assert.Greater(metrics.VisibleSolidChunks, 0,
                    $"Relocation liveness harness never reached initial visible coverage; "
                  + $"missing={metrics.MissingVisibleSolidChunks}, "
                  + $"jobs={metrics.RunningSolidJobs}, "
                  + $"gpuAvailable={metrics.GpuCutoverAvailable}, "
                  + $"gpuCompleted={metrics.GpuCompletedSolidBuilds}.");
                Assert.True(metrics.GpuCutoverAvailable,
                    "Production workers do not advertise the near-ring GPU cutover.");

                // GPU extraction is demand-driven. A stationary showcase can reach fallback-safe
                // visible coverage before the exact near rings need replacement work, so first use
                // the same bounded movement that the production migration regression uses. Only
                // after the mirror has demonstrably admitted and completed GPU work do we perform
                // the one-step relocation this regression is meant to discriminate.
                Vector3 primeOrigin = showcase.transform.position;
                Vector3 primedPosition = primeOrigin;
                var gpuPriming = Stopwatch.StartNew();
                while (gpuPriming.Elapsed.TotalSeconds < MaxGpuPrimingSeconds)
                {
                    if (primedPosition.x - primeOrigin.x < PrimeTravelMetres)
                    {
                        primedPosition.x += Mathf.Min(
                            PrimeStepMetres,
                            PrimeTravelMetres - (primedPosition.x - primeOrigin.x));
                        showcase.transform.position = primedPosition;
                    }

                    yield return null;
                    camera.Render();
                    metrics = VoxelRenderBridge.SurfaceMetrics;
                    if (GpuSurfaceMirrorCoordinator.ReadyBlockCount > 0
                        && metrics.GpuCompletedSolidBuilds > 0)
                        break;
                }

                Assert.Greater(GpuSurfaceMirrorCoordinator.ReadyBlockCount, 0,
                    $"Relocation liveness harness never initialized the shared GPU mirror after "
                  + $"a bounded {primedPosition.x - primeOrigin.x:F1}m production-style priming "
                  + $"traversal; gpuAvailable={metrics.GpuCutoverAvailable}, "
                  + $"gpuCompleted={metrics.GpuCompletedSolidBuilds}, "
                  + $"gpuFallback={metrics.GpuFallbackSolidBuilds}, "
                  + $"jobs={metrics.RunningSolidJobs}, visible={metrics.VisibleSolidChunks}, "
                  + $"missing={metrics.MissingVisibleSolidChunks}.");
                Assert.Greater(metrics.GpuCompletedSolidBuilds, 0ul,
                    $"Relocation liveness harness never completed an initial GPU build after "
                  + $"{primedPosition.x - primeOrigin.x:F1}m of bounded priming movement; "
                  + $"ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, "
                  + $"gpuFallback={metrics.GpuFallbackSolidBuilds}.");

                HashSet<int3> readyBlocks = null;
                MethodInfo applyChange = null;
                ulong syntheticChangeVersion = GpuSurfaceMirrorCoordinator.MirroredVersion;
                int injectedDistantChanges = 0;
                if (distantChangeChurn)
                {
                    FieldInfo readyField = typeof(GpuSurfaceMirrorCoordinator).GetField(
                        "s_ReadyBlocks", BindingFlags.Static | BindingFlags.NonPublic);
                    applyChange = typeof(GpuSurfaceMirrorCoordinator).GetMethod(
                        "ApplyChange", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.NotNull(readyField,
                        "The discriminator requires the coordinator's authoritative ready set.");
                    Assert.NotNull(applyChange,
                        "The discriminator requires the production change-invalidation path.");
                    readyBlocks = readyField.GetValue(null) as HashSet<int3>;
                    Assert.NotNull(readyBlocks);
                    Assert.Greater(readyBlocks.Count, 0,
                        "No old-location ready block was available for unrelated change churn.");
                    foreach (int3 ready in readyBlocks)
                    {
                        controlBlock = ready;
                        break;
                    }

                    controlCoreMin = controlBlock << VoxelReadGrid.BlockEdgeLog2;
                    controlCoreMax = controlCoreMin + new int3(VoxelReadGrid.BlockEdge);
                    GpuSurfaceMirrorCoordinator.RequestCoverage(
                        controlBlock, 1, controlCoreMin, controlCoreMax);
                    heldControlDemand = true;
                }

                ulong baselineCompleted = metrics.GpuCompletedSolidBuilds;
                Vector3 relocated = showcase.transform.position;
                relocated.x += RelocationMetres;
                showcase.transform.position = relocated;

                bool sawRecoveryBacklog = false;
                bool sawSaturatedAdmission = false;
                double saturatedAdmissionStarted = -1.0;
                int saturatedDemandThreshold = GpuSurfaceMirrorCoordinator.MaxConcurrentExtractionChains
                    + (distantChangeChurn ? 1 : 0);
                var observation = Stopwatch.StartNew();
                while (observation.Elapsed.TotalSeconds < MaxObservationSeconds)
                {
                    yield return null;
                    camera.Render();
                    metrics = VoxelRenderBridge.SurfaceMetrics;

                    if (distantChangeChurn && readyBlocks.Contains(controlBlock))
                    {
                        var change = new VoxelChangeRecord(
                            ++syntheticChangeVersion,
                            controlBlock >> VoxelReadGrid.BlocksPerRegionEdgeLog2,
                            controlCoreMin,
                            controlCoreMax,
                            VoxelChangeKind.Occupancy);
                        applyChange.Invoke(null, new object[] { change });
                        injectedDistantChanges++;
                    }

                    int pending = GpuSurfaceMirrorCoordinator.PendingBlockCount;
                    int demand = GpuSurfaceMirrorCoordinator.DemandFootprintCount;
                    int active = GpuSurfaceMirrorCoordinator.ActiveExtractions;
                    bool recoveryBacklog = pending > 0;
                    bool saturatedAdmission = recoveryBacklog
                        && demand >= saturatedDemandThreshold
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
                          + $"missing={metrics.MissingVisibleSolidChunks}, "
                          + $"distantChanges={injectedDistantChanges}. Recovery must reclaim or "
                          + "publish enough of the replaced working set for at least one request to "
                          + "cross mirror admission even while unrelated ready data changes.");
                    }
                    else
                    {
                        saturatedAdmissionStarted = -1.0;
                    }

                    if (sawRecoveryBacklog
                        && metrics.GpuCompletedSolidBuilds >= baselineCompleted + 4
                        && metrics.VisibleSolidChunks > 0
                        && demand < saturatedDemandThreshold)
                        break;
                }

                Assert.True(sawRecoveryBacklog,
                    "Distant relocation never exercised shared-mirror recovery.");
                if (distantChangeChurn)
                {
                    Assert.GreaterOrEqual(injectedDistantChanges, 8,
                        $"The discriminator did not sustain enough unrelated ready-block changes; "
                      + $"injected={injectedDistantChanges}.");
                }
                Assert.GreaterOrEqual(metrics.GpuCompletedSolidBuilds - baselineCompleted, 4ul,
                    $"GPU extraction did not recover useful throughput after a distant relocation: "
                  + $"completed={metrics.GpuCompletedSolidBuilds - baselineCompleted}, "
                  + $"ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, "
                  + $"pending={GpuSurfaceMirrorCoordinator.PendingBlockCount}, "
                  + $"demand={GpuSurfaceMirrorCoordinator.DemandFootprintCount}, "
                  + $"active={GpuSurfaceMirrorCoordinator.ActiveExtractions}, "
                  + $"mixedResident={GpuSurfaceMirrorCoordinator.ResidentMixedBrickCount}/"
                  + $"{GpuSurfaceMirrorCoordinator.MirrorSlotCapacity}, "
                  + $"sawSaturatedAdmission={sawSaturatedAdmission}, "
                  + $"distantChanges={injectedDistantChanges}.");
            }
            finally
            {
                if (heldControlDemand)
                {
                    GpuSurfaceMirrorCoordinator.ReleaseCoverage(
                        controlBlock, 1, controlCoreMin, controlCoreMax);
                }
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
