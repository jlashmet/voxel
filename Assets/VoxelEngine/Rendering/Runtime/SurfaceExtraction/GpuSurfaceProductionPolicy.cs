using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Production policy for the exact near-ring GPU surface backend.
    ///
    /// The original cutover was rolled back after a traversal lost visible geometry. The failure
    /// predated the raw-mirror semantic classifier and packed Storage semantic decoder that are now
    /// part of the production compute path. With unsupported surfaces routed back through the CPU
    /// chain and the previous ready lease retained until GPU count/write agreement is proven, GPU
    /// extraction is the default for supported step-1/step-2 chunks.
    ///
    /// VOXEL_DISABLE_GPU_CUTOVER=1 remains an explicit diagnostic/emergency fallback so the same
    /// build can still be A/B measured without changing source.
    /// </summary>
    internal static class GpuSurfaceProductionPolicy
    {
        internal const string LegacyDisableVariable = "VOXEL_DISABLE_GPU_CUTOVER";

        // Keep the second parameter for source compatibility with the former experimental policy.
        // Opt-in is no longer required: production is enabled unless explicitly disabled.
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
