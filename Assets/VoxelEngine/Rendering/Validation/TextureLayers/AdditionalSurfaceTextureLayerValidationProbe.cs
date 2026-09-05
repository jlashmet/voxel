using System.Collections;
using UnityEngine;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Module-owned built-player proof for renderer-configured opaque texture layers. The paired
    /// scene runs the production voxel showcase/render feature; this probe verifies that the active
    /// renderer asset installed its semantic-free extra slots and that the production surface pass
    /// actually converged while those slots were configured.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Validation/Additional Surface Texture Layer Probe")]
    [DisallowMultipleComponent]
    public sealed class AdditionalSurfaceTextureLayerValidationProbe : MonoBehaviour
    {
        private const int ExpectedAdditionalLayers = 6;
        private const int RequiredStableFrames = 20;
        private const int MaximumWaitFrames = 900;

        private IEnumerator Start()
        {
            int stableFrames = 0;
            int waitedFrames = 0;
            while (waitedFrames++ < MaximumWaitFrames && stableFrames < RequiredStableFrames)
            {
                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                bool configured =
                    VoxelPresentationCatalogue.AdditionalTextureLayerCount == ExpectedAdditionalLayers;
                bool productionPassLive = VoxelRenderBridge.SurfacePassRecordCount > 0
                    && VoxelRenderBridge.RenderFeatureCreateCount > 0
                    && (metrics.SolidResidentChunks > 0 || metrics.WaterResidentChunks > 0);

                if (configured && productionPassLive)
                    stableFrames++;
                else
                    stableFrames = 0;

                yield return null;
            }

            VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
            if (stableFrames < RequiredStableFrames)
            {
                Debug.LogError(
                    "TEXTURE_LAYER_VALIDATION failure: " +
                    $"extras={VoxelPresentationCatalogue.AdditionalTextureLayerCount} " +
                    $"featureCreates={VoxelRenderBridge.RenderFeatureCreateCount} " +
                    $"surfacePasses={VoxelRenderBridge.SurfacePassRecordCount} " +
                    $"solidResident={finalMetrics.SolidResidentChunks} " +
                    $"waterResident={finalMetrics.WaterResidentChunks}.");
                yield break;
            }

            Debug.Log(
                "TEXTURE_LAYER_VALIDATION ready: " +
                $"extras={VoxelPresentationCatalogue.AdditionalTextureLayerCount} " +
                $"featureCreates={VoxelRenderBridge.RenderFeatureCreateCount} " +
                $"surfacePasses={VoxelRenderBridge.SurfacePassRecordCount} " +
                $"solidResident={finalMetrics.SolidResidentChunks} " +
                $"waterResident={finalMetrics.WaterResidentChunks}.");
        }
    }
}
