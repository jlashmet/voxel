using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// The generic module player-validation harness deliberately forces the CPU baseline for
    /// ordinary PlayMode/player coverage. This validation scene exists specifically to prove the
    /// production GPU mirror path, so it opts back into that path before any scene component can
    /// initialize. The override is isolated to the dedicated validation player process.
    /// </summary>
    internal static class GpuSurfaceMirrorRelocationValidationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnableGpuCutoverForDedicatedValidationPlayer()
        {
            Environment.SetEnvironmentVariable("VOXEL_DISABLE_GPU_CUTOVER", null);
        }
    }
}
