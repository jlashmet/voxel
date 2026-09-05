using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Composition boundary for application-owned material presentation.
    /// Rendering.Runtime remains hidden from game/content assemblies.
    /// </summary>
    public static class MaterialPresentationComposition
    {
        public static void Apply(MaterialPresentationDefinition[] definitions) =>
            VoxelMaterialPresentationInstaller.Apply(definitions);

        /// <summary>
        /// Resolves canonical voxel material/coating identity into semantic-free coarse values for
        /// distant feature massing. Raw palette indices remain inside composition; the rendering API
        /// receives only the already-installed presentation values.
        /// </summary>
        public static FarFeaturePresentation ResolveFarFeaturePresentation(
            byte materialIndex,
            ushort surfaceStyle,
            byte coatingIndex)
        {
            int material = math.min((int)materialIndex, VoxelPresentationCatalogue.MaxMaterials - 1);
            Vector4 installedAlbedo = VoxelPresentationCatalogue.MaterialAlbedo[material];
            float4 albedo = new(
                installedAlbedo.x,
                installedAlbedo.y,
                installedAlbedo.z,
                installedAlbedo.w);
            float roughness = math.saturate(VoxelPresentationCatalogue.MaterialSurface[material].z);

            int coating = math.min((int)coatingIndex, VoxelPresentationCatalogue.MaxCoatings - 1);
            Vector4 coatingSampling = VoxelPresentationCatalogue.CoatingSampling[coating];
            if (coatingSampling.w > 0f)
            {
                Vector4 coatingResponse = VoxelPresentationCatalogue.CoatingResponse[coating];
                // Far-feature massing has one material per conservative proxy rather than a
                // per-fragment orientation response. Use the midpoint of the production vertical
                // response as the deterministic coarse projection; this preserves authored coating
                // identity without introducing a scene/material special case.
                float orientation = math.lerp(coatingResponse.x, coatingResponse.y, 0.5f);
                float amount = math.saturate(coatingSampling.w * orientation);
                Vector4 tint = VoxelPresentationCatalogue.CoatingTint[coating];
                albedo = math.lerp(
                    albedo,
                    new float4(tint.x, tint.y, tint.z, tint.w),
                    amount);
                if (coatingResponse.w >= 0f)
                    roughness = math.lerp(roughness, math.saturate(coatingResponse.w), amount);
            }

            // Reconstruction style remains part of the opaque StyleKey used for cache identity.
            // Its near-surface pattern depends on face position and SurfaceFlags, neither of which
            // a one-material far proxy can reproduce faithfully; do not invent a style-specific tint.
            _ = surfaceStyle;
            return new FarFeaturePresentation(albedo, roughness);
        }
    }
}
