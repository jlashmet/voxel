using Unity.Mathematics;

namespace VoxelEngine.Rendering.Api
{
    /// <summary>Generic texture projection understood by the voxel material shader.</summary>
    public enum MaterialTextureProjection : byte
    {
        Face = 0,
        Triplanar = 1,
    }

    /// <summary>
    /// Semantic-free GPU presentation for one opaque material index.
    /// The game/content layer chooses which material receives these values; Rendering only
    /// interprets the packed shader properties.
    /// </summary>
    public readonly struct MaterialPresentationDefinition
    {
        public readonly byte MaterialIndex;
        public readonly float4 Albedo;
        public readonly float4 Sampling;  // albedo layer, normal layer, projection, texture blend
        public readonly float4 Surface;   // UV scale, normal strength, roughness, luminance-only
        public readonly float4 Variation; // luminance pivot, detail, chroma, macro variation

        public MaterialPresentationDefinition(
            byte materialIndex,
            float4 albedo,
            int albedoLayer = 0,
            int normalLayer = 0,
            MaterialTextureProjection projection = MaterialTextureProjection.Face,
            float textureBlend = 0f,
            float uvScale = 1f / 36f,
            float normalStrength = 0f,
            float roughness = 0.76f,
            bool luminanceOnly = false,
            float luminancePivot = 0.68f,
            float detailStrength = 0f,
            float chromaStrength = 0f,
            float macroVariation = 0f)
        {
            MaterialIndex = materialIndex;
            Albedo = new float4(albedo.xyz, 1f);
            Sampling = new float4(albedoLayer, normalLayer, (float)projection, textureBlend);
            Surface = new float4(uvScale, normalStrength, roughness, luminanceOnly ? 1f : 0f);
            Variation = new float4(luminancePivot, detailStrength, chromaStrength, macroVariation);
        }
    }
}
