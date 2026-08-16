using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Game-owned rendering projection for the canonical material vocabulary.
    /// Texture-array layer assignments and visual response are content choices, not engine facts.
    /// </summary>
    public static class GameMaterialRenderingDefinitions
    {
        // Texture-array layer assignments are part of this game's presentation catalogue.
        private const int StoneTexture = 0;
        private const int WoodTexture = 1;
        private const int SandTexture = 2;
        private const int RockTexture = 3;
        private const int SlateTexture = 4;
        private const int GrassTexture = 5;
        private const int DirtTexture = 6;
        private const int DarkStoneTexture = 7;

        public const int Count = GameMaterialCatalogue.Count;

        public static MaterialPresentationDefinition[] Create() => new[]
        {
            Solid(GameMaterialIds.Empty,        1.00f, 0.00f, 1.00f),
            Textured(GameMaterialIds.Stone,     0.43f, 0.45f, 0.48f, StoneTexture, true, 0.18f),
            Textured(GameMaterialIds.Wood,      0.46f, 0.29f, 0.14f, WoodTexture, false, 0.16f),
            Textured(GameMaterialIds.Sand,      0.82f, 0.72f, 0.46f, SandTexture, true, 0.16f),
            Solid(GameMaterialIds.Glass,        0.78f, 0.48f, 0.18f, roughness: 0.24f),
            Solid(GameMaterialIds.Bedrock,      0.15f, 0.15f, 0.17f,
                projection: MaterialTextureProjection.Triplanar),
            Textured(GameMaterialIds.DarkStone, 0.23f, 0.25f, 0.28f, DarkStoneTexture, true, 0.18f,
                detailStrength: 0.72f, luminancePivot: 0.58f, chromaStrength: 0.025f, macroVariation: 0.075f),
            Textured(GameMaterialIds.Slate,     0.24f, 0.26f, 0.32f, SlateTexture, false, 0.16f,
                roughness: 0.42f),
            Textured(GameMaterialIds.Tile,      0.46f, 0.24f, 0.18f, SlateTexture, false, 0.16f,
                roughness: 0.42f),
            Solid(GameMaterialIds.Cloth,        0.62f, 0.12f, 0.14f),
            Textured(GameMaterialIds.Grass,     0.31f, 0.44f, 0.20f, GrassTexture, true, 0.16f),
            Solid(GameMaterialIds.Water,        0.10f, 0.43f, 0.56f, roughness: 0.18f),
            Solid(GameMaterialIds.Gold,         0.80f, 0.66f, 0.26f),
            Textured(GameMaterialIds.Dirt,      0.38f, 0.31f, 0.24f, DirtTexture, true, 0.16f),
            Textured(GameMaterialIds.Moss,      0.32f, 0.40f, 0.24f, GrassTexture, true, 0.16f,
                roughness: 0.48f),
            Solid(GameMaterialIds.LitWindow,    0.16f, 0.19f, 0.18f, roughness: 0.24f),
            Solid(GameMaterialIds.Cascade,      0.22f, 0.62f, 0.78f, roughness: 0.18f),
            Solid(GameMaterialIds.Crystal,      0.08f, 0.56f, 0.82f, roughness: 0.10f),
            Masonry(GameMaterialIds.MasonrySmall,  0.65f, 0.56f, 0.41f, 2f),
            Masonry(GameMaterialIds.MasonryMedium, 0.68f, 0.58f, 0.42f, 1f),
            Masonry(GameMaterialIds.MasonryLarge,  0.63f, 0.54f, 0.40f, 0.5f),
            Solid(GameMaterialIds.FlowerWhite,  1.00f, 1.00f, 1.00f),
        };

        private static MaterialPresentationDefinition Solid(
            byte materialIndex, float r, float g, float b,
            float roughness = 0.76f,
            MaterialTextureProjection projection = MaterialTextureProjection.Face) =>
            new(materialIndex, new float4(r, g, b, 1f),
                projection: projection, roughness: roughness);

        private static MaterialPresentationDefinition Textured(
            byte materialIndex, float r, float g, float b, int layer, bool triplanar,
            float normalStrength, float detailStrength = 0f, float luminancePivot = 0.68f,
            float chromaStrength = 0f, float macroVariation = 0f, float roughness = 0.76f) =>
            new(materialIndex, new float4(r, g, b, 1f), layer, layer,
                triplanar ? MaterialTextureProjection.Triplanar : MaterialTextureProjection.Face,
                textureBlend: detailStrength > 0f ? 1f : 0.28f,
                normalStrength: normalStrength,
                roughness: roughness,
                luminanceOnly: detailStrength > 0f,
                luminancePivot: luminancePivot,
                detailStrength: detailStrength,
                chromaStrength: chromaStrength,
                macroVariation: macroVariation);

        private static MaterialPresentationDefinition Masonry(
            byte materialIndex, float r, float g, float b, float textureScale) =>
            new(materialIndex, new float4(r, g, b, 1f), RockTexture, RockTexture,
                MaterialTextureProjection.Triplanar, textureBlend: 1f,
                uvScale: textureScale / 36f, normalStrength: 0.18f, roughness: 0.76f,
                luminanceOnly: true, luminancePivot: 0.68f,
                detailStrength: 0.58f, chromaStrength: 0.06f, macroVariation: 0.075f);
    }
}
