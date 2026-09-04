using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// CI-only second discriminator for the current cold-view convergence bottleneck.
    ///
    /// The 12 -> 8 run materially improved the same VoxelShowcase replay, but count batches are
    /// serialized behind one graphics fence. Keep only one active four-record count batch worth
    /// of CPU build demand during this experiment. If this beats the eight-chain run, the durable
    /// scheduler limit should follow useful GPU queue depth rather than total context count. If it
    /// does not, stop tuning concurrency and isolate recovery-queue churn instead. Remove this
    /// experiment before production closure.
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
            int usefulQueueDepth = Math.Max(1,
                GpuSurfaceMirrorCoordinator.MaxConcurrentExtractionChains / 2);
            if (configured <= usefulQueueDepth) return;

            VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = usefulQueueDepth;
            Debug.Log($"GPU_CONVERGENCE_EXPERIMENT builds={configured}->{usefulQueueDepth} "
                      + "reason=serialized-count-batch-depth");
        }
    }
}
