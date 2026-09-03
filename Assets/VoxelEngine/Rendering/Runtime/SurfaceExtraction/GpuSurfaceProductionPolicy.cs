using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Keeps the near-ring GPU surface cutover opt-in while the GPU path is still being stabilized.
    ///
    /// Production starts on the CPU renderer unless a fresh process explicitly opts in with
    /// VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1. VOXEL_DISABLE_GPU_CUTOVER=1 always wins so the
    /// same build can still force the CPU path for diagnostics and regression comparison.
    /// </summary>
    internal static class GpuSurfaceProductionPolicy
    {
        internal const string ExperimentalOptInVariable =
            "VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER";
        internal const string LegacyDisableVariable = "VOXEL_DISABLE_GPU_CUTOVER";

        internal static bool ShouldDisableLegacyGpuCutover(
            string explicitDisable, string experimentalOptIn) =>
            explicitDisable == "1" || experimentalOptIn != "1";

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
