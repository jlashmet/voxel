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
    /// Semantic-free water motion/look family. Game material names never cross this API; content
    /// selects one of these renderer behaviors for any opaque material index it owns.
    /// </summary>
    public enum WaterPresentationProfile : byte
    {
        None = 0,
        Still = 1,
        Flowing = 2,
        Waterfall = 3,
    }

    /// <summary>
    /// Packed reusable water presentation. Values are deliberately independent of world/game
    /// material identity so the same renderer contract can be installed by any application.
    /// </summary>
    public readonly struct WaterPresentationDefinition
    {
        public readonly float4 Shallow; // rgb, base opacity
        public readonly float4 Deep;    // rgb, depth fade distance
        public readonly float4 Motion;  // profile, flow x, flow z, flow speed
        public readonly float4 Detail;  // wave scale, normal strength, refraction, smoothness
        public readonly float4 Foam;    // surface, contact, scale, speed
        public readonly float4 Cascade; // turbulence, edge foam, impact foam, mist

        public WaterPresentationProfile Profile => (WaterPresentationProfile)(byte)Motion.x;
        public bool IsWater => Profile != WaterPresentationProfile.None;

        public WaterPresentationDefinition(
            WaterPresentationProfile profile,
            float4 shallow,
            float4 deep,
            float2 flowDirection,
            float flowSpeed,
            float waveScale,
            float normalStrength,
            float refractionStrength,
            float smoothness,
            float surfaceFoam,
            float contactFoam,
            float foamScale,
            float foamSpeed,
            float turbulence = 0f,
            float edgeFoam = 0f,
            float impactFoam = 0f,
            float mist = 0f)
        {
            float2 direction = math.lengthsq(flowDirection) > 0.0001f
                ? math.normalize(flowDirection) : new float2(1f, 0f);
            Shallow = shallow;
            Deep = deep;
            Motion = new float4((float)profile, direction.x, direction.y, flowSpeed);
            Detail = new float4(waveScale, normalStrength, refractionStrength, smoothness);
            Foam = new float4(surfaceFoam, contactFoam, foamScale, foamSpeed);
            Cascade = new float4(turbulence, edgeFoam, impactFoam, mist);
        }
    }

    /// <summary>
    /// Semantic-free GPU presentation for one material index. The game/content layer chooses which
    /// material receives these values; Rendering only interprets the packed shader properties.
    /// Water presentation is optional and defaults to <see cref="WaterPresentationProfile.None"/>.
    /// </summary>
    public readonly struct MaterialPresentationDefinition
    {
        public readonly byte MaterialIndex;
        public readonly float4 Albedo;
        public readonly float4 Sampling;  // albedo layer, normal layer, projection, texture blend
        public readonly float4 Surface;   // UV scale, normal strength, roughness, luminance-only
        public readonly float4 Variation; // luminance pivot, detail, chroma, macro variation
        public readonly WaterPresentationDefinition Water;

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
            float macroVariation = 0f,
            WaterPresentationDefinition water = default)
        {
            MaterialIndex = materialIndex;
            Albedo = new float4(albedo.xyz, 1f);
            Sampling = new float4(albedoLayer, normalLayer, (float)projection, textureBlend);
            Surface = new float4(uvScale, normalStrength, roughness, luminanceOnly ? 1f : 0f);
            Variation = new float4(luminancePivot, detailStrength, chromaStrength, macroVariation);
            Water = water;
        }
    }
}
