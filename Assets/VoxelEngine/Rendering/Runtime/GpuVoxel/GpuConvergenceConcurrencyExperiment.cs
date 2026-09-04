using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// CI-only discriminator for the current cold-view convergence bottleneck.
    ///
    /// The production scheduler currently admits twelve GPU builds while the GPU extraction
    /// backend can own only eight count/write chains. A step-2 request demands an 18^3 brick
    /// footprint, so the four requests that cannot enter extraction still retain mirror demand
    /// and can churn the shared ready set without increasing GPU throughput. This experiment
    /// clamps only the standalone CI player to the real extraction-chain capacity. If the
    /// VoxelShowcase replay closes visible holes faster and/or raises convergence FPS, the durable
    /// fix belongs at the scheduler admission boundary; this file must then be removed.
    /// </summary>
    internal static class GpuConvergenceConcurrencyExperiment
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ClampCiGpuConvergenceConcurrency()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("CI"), "true",
                               StringComparison.OrdinalIgnoreCase))
                return;

            int configured = VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging;
            int capacity = GpuSurfaceMirrorCoordinator.MaxConcurrentExtractionChains;
            if (configured <= capacity) return;

            VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = capacity;
            Debug.Log($"GPU_CONVERGENCE_EXPERIMENT builds={configured}->{capacity} "
                      + "reason=extraction-chain-capacity");
        }
    }
}
