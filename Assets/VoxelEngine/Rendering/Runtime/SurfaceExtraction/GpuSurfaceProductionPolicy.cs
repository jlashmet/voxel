using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Keeps the legacy per-worker GPU-v1 surface cutover out of production after deterministic
    /// traversal proved that it can starve near-ring convergence until every visible draw drops.
    ///
    /// The backend stays available for explicit experiments. Production starts on the optimized
    /// asynchronous CPU renderer unless a fresh process opts in with
    /// VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1. VOXEL_DISABLE_GPU_CUTOVER=1 always wins.
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
