using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Production policy for the supported near-ring GPU surface cutover.
    ///
    /// Supported GPU extraction is enabled by default. VOXEL_DISABLE_GPU_CUTOVER=1 is the
    /// explicit emergency/A-B fallback to the CPU renderer. The retired experimental opt-in is
    /// accepted only as an ignored compatibility input so old launch environments cannot
    /// accidentally disable the production GPU path.
    /// </summary>
    internal static class GpuSurfaceProductionPolicy
    {
        internal const string ExperimentalOptInVariable =
            "VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER";
        internal const string LegacyDisableVariable = "VOXEL_DISABLE_GPU_CUTOVER";

        internal static bool ShouldDisableLegacyGpuCutover(
            string explicitDisable, string experimentalOptIn) =>
            explicitDisable == "1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Apply()
        {
            string explicitDisable = Environment.GetEnvironmentVariable(LegacyDisableVariable);
            string experimentalOptIn =
                Environment.GetEnvironmentVariable(ExperimentalOptInVariable);
            bool disable = ShouldDisableLegacyGpuCutover(explicitDisable, experimentalOptIn);
            Environment.SetEnvironmentVariable(LegacyDisableVariable, disable ? "1" : null);
        }
    }
}
