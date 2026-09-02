using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Enables the near-ring GPU surface backend by default on the GPU investigation branch.
    ///
    /// Unsupported surfaces continue to route through the CPU fallback chain. Set
    /// VOXEL_DISABLE_GPU_CUTOVER=1 to force CPU rendering for A/B diagnostics.
    /// </summary>
    internal static class GpuSurfaceProductionPolicy
    {
        internal const string LegacyDisableVariable = "VOXEL_DISABLE_GPU_CUTOVER";

        // Keep the second parameter for source compatibility with the production policy.
        internal static bool ShouldDisableLegacyGpuCutover(
            string explicitDisable, string experimentalOptIn) => explicitDisable == "1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Apply()
        {
            string explicitDisable = Environment.GetEnvironmentVariable(LegacyDisableVariable);
            bool disable = ShouldDisableLegacyGpuCutover(explicitDisable, null);
            Environment.SetEnvironmentVariable(LegacyDisableVariable, disable ? "1" : null);
        }
    }
}
