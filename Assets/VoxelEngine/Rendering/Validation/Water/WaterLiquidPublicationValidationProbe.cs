using System.Collections;
using UnityEngine;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Module-owned acceptance probe for the Water validation tableau. The generic built-player
    /// harness only waits for declared log evidence; this component defines the Water module's
    /// stronger requirement that the production liquid owner itself has published visible
    /// geometry, instead of accepting solid-terrain convergence as a proxy.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Validation/Water Liquid Publication Probe")]
    [DisallowMultipleComponent]
    public sealed class WaterLiquidPublicationValidationProbe : MonoBehaviour
    {
        private const int RequiredStableFrames = 20;
        private const int MaximumWaitFrames = 900;

        private IEnumerator Start()
        {
            int stableFrames = 0;
            int waitedFrames = 0;
            while (waitedFrames++ < MaximumWaitFrames && stableFrames < RequiredStableFrames)
            {
                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                if (metrics.WaterResidentChunks > 0
                    && metrics.VisibleWaterChunks > 0
                    && metrics.CompletedWaterBuilds > 0)
                {
                    stableFrames++;
                }
                else
                {
                    stableFrames = 0;
                }

                yield return null;
            }

            VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
            if (stableFrames < RequiredStableFrames)
            {
                Debug.LogError(
                    $"WATER_VALIDATION liquid renderer did not converge: resident={finalMetrics.WaterResidentChunks}, " +
                    $"dirty={finalMetrics.WaterDirtyChunks}, visible={finalMetrics.VisibleWaterChunks}, " +
                    $"completed={finalMetrics.CompletedWaterBuilds}.");
                yield break;
            }

            Debug.Log(
                $"WATER_VALIDATION liquid-ready: resident={finalMetrics.WaterResidentChunks}, " +
                $"dirty={finalMetrics.WaterDirtyChunks}, visible={finalMetrics.VisibleWaterChunks}, " +
                $"completed={finalMetrics.CompletedWaterBuilds}.");
        }
    }
}
