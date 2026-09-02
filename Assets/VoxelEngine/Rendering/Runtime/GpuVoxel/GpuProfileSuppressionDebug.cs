using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Temporary enable_gpu-branch diagnostic: disable profile-owned regular-triangle suppression
    /// before any scene loads so SmallVoxelShowcase can A/B the missing-geometry failure without
    /// changing extraction, publication, streaming, or draw submission.
    /// </summary>
    internal static class GpuProfileSuppressionDebug
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DisableProfileSuppression()
        {
            Environment.SetEnvironmentVariable("VOXEL_GPU_PROFILE_SUPPRESSION", "0");
        }
    }
}
