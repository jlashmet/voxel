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

            for (int i = 0; i < definitions.Length; i++)
            {
                MaterialPresentationDefinition definition = definitions[i];
                int materialIndex = definition.MaterialIndex;
                if ((uint)materialIndex >= VoxelPresentationCatalogue.MaxMaterials)
                    throw new ArgumentOutOfRangeException(
                        nameof(definitions), materialIndex,
                        $"Material presentation index must be below {VoxelPresentationCatalogue.MaxMaterials}.");

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
