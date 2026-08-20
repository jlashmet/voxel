using System;
using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Storage.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// One game-owned authored material row. Engine subsystems receive only their projection;
    /// this type prevents simulation and rendering identities from drifting apart in content.
    /// </summary>
    public readonly struct GameMaterialRuntimeDefinition
    {
        public readonly byte MaterialId;
        public readonly bool HasSimulation;
        public readonly MaterialDefinition Simulation;
        public readonly MaterialPresentationDefinition Rendering;

        public GameMaterialRuntimeDefinition(
            byte materialId,
            bool hasSimulation,
            in MaterialDefinition simulation,
            in MaterialPresentationDefinition rendering)
        {
            MaterialId = materialId;
            HasSimulation = hasSimulation;
            Simulation = simulation;
            Rendering = rendering;
        }
    }

    /// <summary>
    /// Canonical runtime authoring source for this game's materials. Semantic identity, physical
    /// choices and presentation choices meet here once; each engine subsystem gets a compact,
    /// semantic-free projection compiled from these rows.
    /// </summary>
    public static class GameMaterialRuntimeCatalogue
    {
        private const int StoneTexture = 0;
        private const int WoodTexture = 1;
        private const int SandTexture = 2;
        private const int RockTexture = 3;
        private const int SlateTexture = 4;
        private const int GrassTexture = 5;
        private const int DirtTexture = 6;
        private const int DarkStoneTexture = 7;

        private const uint WeatherCoatings =
            (1u << Coatings.Moss) |
            (1u << Coatings.Snow) |
            (1u << Coatings.Soot) |
            (1u << Coatings.Wet);

        private static readonly GameMaterialRuntimeDefinition[] s_Definitions =
        {
            Row(GameMaterialIds.Empty, false,
                Sim(GameMaterialIds.Empty, 0, DestructionClass.None, SurfaceStyles.Smooth, 0u,
                    placementSurfaceStyle: SurfaceStyles.MaterialDefault),
                Solid(GameMaterialIds.Empty, 1.00f, 0.00f, 1.00f)),

            Row(GameMaterialIds.Stone, true,
                Sim(GameMaterialIds.Stone, 200, DestructionClass.Crumble, SurfaceStyles.Smooth, WeatherCoatings),
                Textured(GameMaterialIds.Stone, 0.43f, 0.45f, 0.48f, StoneTexture, true, 0.18f)),

            Row(GameMaterialIds.Wood, true,
                Sim(GameMaterialIds.Wood, 90, DestructionClass.Splinter, SurfaceStyles.Planar, WeatherCoatings, true),
                Textured(GameMaterialIds.Wood, 0.46f, 0.29f, 0.14f, WoodTexture, false, 0.16f)),

            Row(GameMaterialIds.Sand, true,
                Sim(GameMaterialIds.Sand, 20, DestructionClass.Powder, SurfaceStyles.Smooth,
                    1u << Coatings.Wet, placementSurfaceStyle: SurfaceStyles.MaterialDefault),
                StylizedTerrain(GameMaterialIds.Sand, 0.66f, 0.57f, 0.39f, SandTexture)),

            Row(GameMaterialIds.Glass, true,
                Sim(GameMaterialIds.Glass, 10, DestructionClass.Powder, SurfaceStyles.Sharp, 1u << Coatings.Wet),
                // Pale and glossy, so a glazed opening reads as glass. It was an opaque
                // orange-brown at the same value as the surrounding masonry, which made every
                // window in every authored house disappear into its wall.
                Solid(GameMaterialIds.Glass, 0.55f, 0.69f, 0.76f, roughness: 0.06f)),

            Row(GameMaterialIds.Bedrock, true,
                Sim(GameMaterialIds.Bedrock, 255, DestructionClass.None, SurfaceStyles.Planar, 0u),
                Solid(GameMaterialIds.Bedrock, 0.15f, 0.15f, 0.17f,
                    projection: MaterialTextureProjection.Triplanar)),

            Row(GameMaterialIds.DarkStone, true,
                Sim(GameMaterialIds.DarkStone, 210, DestructionClass.Crumble, SurfaceStyles.Smooth, WeatherCoatings),
                Textured(GameMaterialIds.DarkStone, 0.23f, 0.25f, 0.28f, DarkStoneTexture, true, 0.18f,
                    detailStrength: 0.72f, luminancePivot: 0.58f,
                    chromaStrength: 0.025f, macroVariation: 0.075f)),

            Row(GameMaterialIds.Slate, true,
                Sim(GameMaterialIds.Slate, 120, DestructionClass.Crumble, SurfaceStyles.Planar, WeatherCoatings),
                Textured(GameMaterialIds.Slate, 0.24f, 0.26f, 0.32f, SlateTexture, false, 0.16f,
                    roughness: 0.42f)),

            Row(GameMaterialIds.Tile, true,
                Sim(GameMaterialIds.Tile, 110, DestructionClass.Crumble, SurfaceStyles.Planar, WeatherCoatings),
                Textured(GameMaterialIds.Tile, 0.46f, 0.24f, 0.18f, SlateTexture, false, 0.16f,
                    roughness: 0.42f)),

            Row(GameMaterialIds.Cloth, true,
                Sim(GameMaterialIds.Cloth, 15, DestructionClass.Splinter, SurfaceStyles.Planar, WeatherCoatings, true),
                Solid(GameMaterialIds.Cloth, 0.62f, 0.12f, 0.14f)),

            Row(GameMaterialIds.Grass, true,
                Sim(GameMaterialIds.Grass, 25, DestructionClass.Powder, SurfaceStyles.Smooth,
                    WeatherCoatings, placementSurfaceStyle: SurfaceStyles.MaterialDefault),
                StylizedTerrain(GameMaterialIds.Grass, 0.28f, 0.46f, 0.20f, GrassTexture)),

            Row(GameMaterialIds.Water, true,
                Sim(GameMaterialIds.Water, 5, DestructionClass.Spreading, SurfaceStyles.Smooth, 0u,
                    placementSurfaceStyle: SurfaceStyles.MaterialDefault),
                Solid(GameMaterialIds.Water, 0.10f, 0.43f, 0.56f, roughness: 0.18f)),

            Row(GameMaterialIds.Gold, true,
                Sim(GameMaterialIds.Gold, 180, DestructionClass.Crumble, SurfaceStyles.Sharp, 1u << Coatings.Soot),
                Solid(GameMaterialIds.Gold, 0.80f, 0.66f, 0.26f)),

            Row(GameMaterialIds.Dirt, true,
                Sim(GameMaterialIds.Dirt, 30, DestructionClass.Powder, SurfaceStyles.Smooth,
                    WeatherCoatings, placementSurfaceStyle: SurfaceStyles.MaterialDefault),
                StylizedTerrain(GameMaterialIds.Dirt, 0.36f, 0.27f, 0.18f, DirtTexture)),

            Row(GameMaterialIds.Moss, true,
                Sim(GameMaterialIds.Moss, 40, DestructionClass.Powder, SurfaceStyles.Smooth,
                    WeatherCoatings, placementSurfaceStyle: SurfaceStyles.MaterialDefault,
                    placementCoating: Coatings.Moss),
                Textured(GameMaterialIds.Moss, 0.32f, 0.40f, 0.24f, GrassTexture, true, 0.16f,
                    roughness: 0.48f)),

            Row(GameMaterialIds.LitWindow, true,
                Sim(GameMaterialIds.LitWindow, 18, DestructionClass.Powder, SurfaceStyles.Sharp, 1u << Coatings.Wet),
                // Kept below HDR emission, but intentionally much warmer and brighter than glass:
                // the authored "warm window" role was previously a charcoal swatch and could not
                // read as inhabited at any time of day.
                Solid(GameMaterialIds.LitWindow, 0.96f, 0.52f, 0.16f, roughness: 0.18f)),

            // These rows were historically unregistered in the simulation palette. They are now
            // explicit but intentionally inert so ownership migration does not change gameplay.
            Row(GameMaterialIds.Cascade, true,
                Sim(GameMaterialIds.Cascade, 0, DestructionClass.None, SurfaceStyles.Smooth, 0u),
                Solid(GameMaterialIds.Cascade, 0.22f, 0.62f, 0.78f, roughness: 0.18f)),

            Row(GameMaterialIds.Crystal, true,
                Sim(GameMaterialIds.Crystal, 0, DestructionClass.None, SurfaceStyles.Smooth, 0u),
                Solid(GameMaterialIds.Crystal, 0.08f, 0.56f, 0.82f, roughness: 0.10f)),

            Row(GameMaterialIds.MasonrySmall, true,
                Sim(GameMaterialIds.MasonrySmall, 200, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
                Masonry(GameMaterialIds.MasonrySmall, 0.65f, 0.56f, 0.41f, 2f)),

            Row(GameMaterialIds.MasonryMedium, true,
                Sim(GameMaterialIds.MasonryMedium, 210, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
                Masonry(GameMaterialIds.MasonryMedium, 0.68f, 0.58f, 0.42f, 1f)),

            Row(GameMaterialIds.MasonryLarge, true,
                Sim(GameMaterialIds.MasonryLarge, 220, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
                Masonry(GameMaterialIds.MasonryLarge, 0.63f, 0.54f, 0.40f, 0.5f)),

            Row(GameMaterialIds.FlowerWhite, true,
                Sim(GameMaterialIds.FlowerWhite, 0, DestructionClass.None, SurfaceStyles.Smooth, 0u),
                Solid(GameMaterialIds.FlowerWhite, 1.00f, 1.00f, 1.00f)),
        };

        public const int Count = GameMaterialCatalogue.Count;
        public const int SimulationCount = Count - 1; // Empty is presentation-only.

        public static ReadOnlySpan<GameMaterialRuntimeDefinition> Definitions => s_Definitions;

        public static ref readonly GameMaterialRuntimeDefinition Get(byte materialId)
        {
            if (materialId >= s_Definitions.Length || s_Definitions[materialId].MaterialId != materialId)
                throw new ArgumentOutOfRangeException(nameof(materialId), materialId, "Unknown game material id.");
            return ref s_Definitions[materialId];
        }

        private static GameMaterialRuntimeDefinition Row(
            byte materialId,
            bool hasSimulation,
            MaterialDefinition simulation,
            MaterialPresentationDefinition rendering)
        {
            if (simulation.MaterialId != materialId || rendering.MaterialIndex != materialId)
                throw new ArgumentException("Material projections must use the row's canonical id.");
            return new GameMaterialRuntimeDefinition(materialId, hasSimulation, in simulation, in rendering);
        }

        private static MaterialDefinition Sim(
            byte materialId,
            byte hardness,
            DestructionClass destructionClass,
            ushort surfaceStyle,
            uint allowedCoatings,
            bool flammable = false,
            ushort placementSurfaceStyle = SurfaceStyles.Planar,
            byte placementCoating = Coatings.None) =>
            new(materialId, hardness, destructionClass, surfaceStyle, allowedCoatings,
                flammable, placementSurfaceStyle, placementCoating);

        private static MaterialPresentationDefinition Solid(
            byte materialIndex,
            float r,
            float g,
            float b,
            float roughness = 0.76f,
            MaterialTextureProjection projection = MaterialTextureProjection.Face) =>
            new(materialIndex, new float4(r, g, b, 1f),
                projection: projection, roughness: roughness);

        private static MaterialPresentationDefinition Textured(
            byte materialIndex,
            float r,
            float g,
            float b,
            int layer,
            bool triplanar,
            float normalStrength,
            float detailStrength = 0f,
            float luminancePivot = 0.68f,
            float chromaStrength = 0f,
            float macroVariation = 0f,
            float roughness = 0.76f) =>
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

        /// <summary>
        /// Low-frequency, colour-led terrain treatment. The old rows let a high-contrast source
        /// texture and normal map dominate every metre of ground, producing the repeated embossed
        /// pattern visible from town streets. This keeps enough luminance variation to avoid a flat
        /// colour field while making the authored palette and terrain silhouette lead the image.
        /// </summary>
        private static MaterialPresentationDefinition StylizedTerrain(
            byte materialIndex,
            float r,
            float g,
            float b,
            int layer) =>
            new(materialIndex, new float4(r, g, b, 1f), layer, layer,
                MaterialTextureProjection.Triplanar,
                textureBlend: 0.16f,
                uvScale: 1f / 52f,
                normalStrength: 0.035f,
                roughness: 0.90f,
                luminanceOnly: true,
                luminancePivot: 0.66f,
                detailStrength: 0.20f,
                chromaStrength: 0.015f,
                macroVariation: 0.07f);

        private static MaterialPresentationDefinition Masonry(
            byte materialIndex,
            float r,
            float g,
            float b,
            float textureScale) =>
            new(materialIndex, new float4(r, g, b, 1f), RockTexture, RockTexture,
                MaterialTextureProjection.Triplanar,
                textureBlend: 1f,
                uvScale: textureScale / 36f,
                normalStrength: 0.18f,
                roughness: 0.76f,
                luminanceOnly: true,
                luminancePivot: 0.68f,
                detailStrength: 0.58f,
                chromaStrength: 0.06f,
                macroVariation: 0.075f);
    }
}
