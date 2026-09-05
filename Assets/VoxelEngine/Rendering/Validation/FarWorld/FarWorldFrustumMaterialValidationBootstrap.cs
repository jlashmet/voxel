using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Adds one bounded tapered/materialized far feature to the existing FarWorld validation scene.
    /// The fixture uses the production presentation installer and production far-feature renderer;
    /// it supplies only deterministic validation input and no parallel rendering implementation.
    /// </summary>
    internal static class FarWorldFrustumMaterialValidationBootstrap
    {
        private const string ValidationSceneName = "FarWorldVisibilityDemo";
        private const byte StoneMaterialIndex = 2;
        private const string FarFeatureShaderResource = "ProceduralFarFeature";
        private const string FarFeatureShaderName = "Voxel/ProceduralFarFeature";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != ValidationSceneName)
                return;

            Shader shader = Resources.Load<Shader>(FarFeatureShaderResource);
            if (shader == null || !shader.isSupported || shader.name != FarFeatureShaderName)
                throw new InvalidOperationException(
                    $"Far-world validation requires supported shader '{FarFeatureShaderName}', resolved '{shader?.name ?? "<missing>"}'.");
            Debug.Log($"FARWORLD_FRUSTUM_SHADER ready: name={shader.name}, supported={shader.isSupported}.");

            VoxelMaterialPresentationInstaller.Apply(new[]
            {
                new MaterialPresentationDefinition(0, new float4(0.28f, 0.43f, 0.19f, 1f)),
                new MaterialPresentationDefinition(1, new float4(0.39f, 0.29f, 0.18f, 1f)),
                new MaterialPresentationDefinition(StoneMaterialIndex, new float4(0.43f, 0.42f, 0.40f, 1f)),
                new MaterialPresentationDefinition(3, new float4(0.82f, 0.84f, 0.82f, 1f)),
            });

            var root = new GameObject("Production Far Frustum Material Validation")
            {
                hideFlags = HideFlags.DontSave,
            };
            var renderer = root.AddComponent<ProceduralFarFeatureRenderer>();
            var geometry = new FarFeatureGeometry(new[]
            {
                new FarFeatureGeometryPrimitive(
                    FarFeatureGeometryShape.Frustum,
                    new float3(-0.5f, 0f, -0.5f),
                    new float3(0.5f, 1f, 0.5f),
                    axis: 1,
                    startRadiusScale: 1f,
                    endRadiusScale: 0.24f),
            });

            var position = new float3(-180f, 73f, 820f);
            var scale = new float3(90f, 145f, 90f);
            renderer.SetInstances(new[]
            {
                new FarFeatureInstance(
                    stableId: 0xF4A0ul,
                    position: position,
                    rotation: quaternion.identity,
                    scale: scale,
                    boundsCenter: position + new float3(0f, scale.y * 0.5f, 0f),
                    boundsExtents: scale * 0.5f,
                    geometryKey: "validation-frustum-taper",
                    styleKey: "validation-stone-frustum",
                    tier: FarFeatureTier.Mid,
                    flags: FarFeatureVisualFlags.Landmark,
                    geometry: geometry,
                    materialIndex: StoneMaterialIndex),
            });

            Debug.Log(
                "FARWORLD_FRUSTUM_VALIDATION ready: shape=Frustum, " +
                $"startRadiusScale=1.00, endRadiusScale=0.24, material={StoneMaterialIndex}.");
        }
    }
}
