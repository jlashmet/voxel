using System;
using UnityEngine;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Installs application-authored material presentation into the renderer's fixed GPU rows.
    /// Material indices remain opaque; this class has no game material vocabulary.
    /// </summary>
    public static class VoxelMaterialPresentationInstaller
    {
        public static void Apply(MaterialPresentationDefinition[] definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            uint seen = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                int materialIndex = definitions[i].MaterialIndex;
                if ((uint)materialIndex >= VoxelPresentationCatalogue.MaxMaterials)
                    throw new ArgumentOutOfRangeException(
                        nameof(definitions), materialIndex,
                        $"Material presentation index must be below {VoxelPresentationCatalogue.MaxMaterials}.");

                uint bit = 1u << materialIndex;
                if ((seen & bit) != 0)
                    throw new ArgumentException(
                        $"Material presentation index {materialIndex} is defined more than once.",
                        nameof(definitions));
                seen |= bit;
            }

            // Make application data authoritative. Any engine-backed legacy rows are erased before
            // applying this catalogue, so an omitted game material cannot silently inherit semantic
            // presentation from VoxelPresentationCatalogue.
            Vector4 neutralAlbedo = new(1f, 1f, 1f, 1f);
            Vector4 neutralSampling = Vector4.zero;
            Vector4 neutralSurface = new(1f / 36f, 0f, 0.76f, 0f);
            Vector4 neutralVariation = new(0.68f, 0f, 0f, 0f);
            for (int materialIndex = 0; materialIndex < VoxelPresentationCatalogue.MaxMaterials; materialIndex++)
            {
                VoxelPresentationCatalogue.MaterialAlbedo[materialIndex] = neutralAlbedo;
                VoxelPresentationCatalogue.MaterialSampling[materialIndex] = neutralSampling;
                VoxelPresentationCatalogue.MaterialSurface[materialIndex] = neutralSurface;
                VoxelPresentationCatalogue.MaterialVariation[materialIndex] = neutralVariation;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                MaterialPresentationDefinition definition = definitions[i];
                int materialIndex = definition.MaterialIndex;
                VoxelPresentationCatalogue.MaterialAlbedo[materialIndex] = ToVector4(definition.Albedo);
                VoxelPresentationCatalogue.MaterialSampling[materialIndex] = ToVector4(definition.Sampling);
                VoxelPresentationCatalogue.MaterialSurface[materialIndex] = ToVector4(definition.Surface);
                VoxelPresentationCatalogue.MaterialVariation[materialIndex] = ToVector4(definition.Variation);
            }
        }

        private static Vector4 ToVector4(Unity.Mathematics.float4 value) =>
            new(value.x, value.y, value.z, value.w);
    }
}
