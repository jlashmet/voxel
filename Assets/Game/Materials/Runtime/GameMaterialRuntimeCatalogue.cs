using System;
using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Storage.Api;

namespace Game.Materials.Runtime
{
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

        // Extra renderer texture layers are configured generically by VoxelRenderFeature. The
        // semantic role-to-layer mapping remains game-owned here rather than leaking house names
        // into VoxelEngine.Rendering.
        private const int HousePlasterTexture = 8;
        private const int HouseTimberTexture = 9;
        private const int HouseRoofTexture = 10;
        private const int HouseStoneTexture = 11;
        private const int HouseDoorTexture = 12;
        private const int HouseFoliageTexture = 13;

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
                WaterStyle(GameMaterialIds.Water, WaterPresentationProfile.Still,
                    new float4(0.26f, 0.70f, 0.78f, 0.66f),
                    new float4(0.015f, 0.18f, 0.38f, 1.60f),
                    new float2(1f, 0f), 0.14f, 0.55f, 0.16f, 0.012f, 0.62f,
                    0.18f, 0.78f, 1.20f, 0.18f)),

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
                    uvScale: 1f / 7f, roughness: 0.48f)),

            Row(GameMaterialIds.LitWindow, true,
                Sim(GameMaterialIds.LitWindow, 18, DestructionClass.Powder, SurfaceStyles.Sharp, 1u << Coatings.Wet),
                Solid(GameMaterialIds.LitWindow, 0.96f, 0.52f, 0.16f, roughness: 0.18f)),

            Row(GameMaterialIds.Cascade, true,
                Sim(GameMaterialIds.Cascade, 0, DestructionClass.None, SurfaceStyles.Smooth, 0u),
                WaterStyle(GameMaterialIds.Cascade, WaterPresentationProfile.Waterfall,
                    new float4(0.56f, 0.84f, 0.92f, 0.82f),
                    new float4(0.055f, 0.32f, 0.56f, 0.72f),
                    new float2(0f, 1f), 1.75f, 0.30f, 0.34f, 0.006f, 0.34f,
                    0.72f, 0.94f, 2.20f, 1.55f,
                    turbulence: 0.86f, edgeFoam: 0.88f, impactFoam: 0.96f, mist: 0.58f)),

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

            Row(GameMaterialIds.RiverWater, true,
                Sim(GameMaterialIds.RiverWater, 5, DestructionClass.Spreading, SurfaceStyles.Smooth, 0u,
                    placementSurfaceStyle: SurfaceStyles.MaterialDefault),
                WaterStyle(GameMaterialIds.RiverWater, WaterPresentationProfile.Flowing,
                    new float4(0.20f, 0.62f, 0.72f, 0.72f),
                    new float4(0.010f, 0.15f, 0.32f, 1.15f),
                    new float2(1f, 0.22f), 0.92f, 0.42f, 0.23f, 0.015f, 0.54f,
                    0.34f, 0.84f, 1.55f, 0.82f)),

            Row(GameMaterialIds.HousePlaster, true,
                Sim(GameMaterialIds.HousePlaster, 45, DestructionClass.Crumble,
                    SurfaceStyles.Smooth, WeatherCoatings),
                ReferenceTextured(GameMaterialIds.HousePlaster, 0.83f, 0.78f, 0.66f,
                    HousePlasterTexture, true, 1f / 15f, 0.86f)),

            Row(GameMaterialIds.HouseTimber, true,
                Sim(GameMaterialIds.HouseTimber, 95, DestructionClass.Splinter,
                    SurfaceStyles.Planar, WeatherCoatings, true),
                ReferenceTextured(GameMaterialIds.HouseTimber, 0.30f, 0.19f, 0.11f,
                    HouseTimberTexture, false, 1f / 12f, 0.66f)),

            Row(GameMaterialIds.HouseRoof, true,
                Sim(GameMaterialIds.HouseRoof, 125, DestructionClass.Crumble,
                    SurfaceStyles.Planar, WeatherCoatings),
                ReferenceTextured(GameMaterialIds.HouseRoof, 0.25f, 0.34f, 0.43f,
                    HouseRoofTexture, false, 1f / 10f, 0.54f)),

            Row(GameMaterialIds.HouseStone, true,
                Sim(GameMaterialIds.HouseStone, 215, DestructionClass.Crumble,
                    SurfaceStyles.MasonryJoint, WeatherCoatings),
                ReferenceTextured(GameMaterialIds.HouseStone, 0.47f, 0.45f, 0.39f,
                    HouseStoneTexture, true, 1f / 13f, 0.90f)),

            Row(GameMaterialIds.HouseDoor, true,
                Sim(GameMaterialIds.HouseDoor, 95, DestructionClass.Splinter,
                    SurfaceStyles.Planar, WeatherCoatings, true),
                // This supplied ornamental plate is dark/gold in source. Preserve its value/detail
                // while using the renderer's luminance-only path over the reference's blue paint.
                Textured(GameMaterialIds.HouseDoor, 0.12f, 0.42f, 0.82f,
                    HouseDoorTexture, false, 0f,
                    detailStrength: 1f, luminancePivot: 0.46f, chromaStrength: 0f,
                    macroVariation: 0.035f, uvScale: 1f / 11f, roughness: 0.66f)),

            Row(GameMaterialIds.HouseFoliage, true,
                Sim(GameMaterialIds.HouseFoliage, 12, DestructionClass.Powder,
                    SurfaceStyles.Smooth, 1u << Coatings.Wet),
                ReferenceTextured(GameMaterialIds.HouseFoliage, 0.27f, 0.40f, 0.15f,
                    HouseFoliageTexture, true, 1f / 8f, 0.92f)),
        };

        public const int Count = GameMaterialCatalogue.Count;
        public const int SimulationCount = Count - 1;

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

        private static MaterialPresentationDefinition WaterStyle(
            byte materialIndex,
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
            float mist = 0f) =>
            new(materialIndex, shallow,
                roughness: math.saturate(1f - smoothness),
                water: new WaterPresentationDefinition(profile, shallow, deep, flowDirection,
                    flowSpeed, waveScale, normalStrength, refractionStrength, smoothness,
                    surfaceFoam, contactFoam, foamScale, foamSpeed,
                    turbulence, edgeFoam, impactFoam, mist));

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
            float uvScale = 1f / 36f,
            float roughness = 0.76f) =>
            new(materialIndex, new float4(r, g, b, 1f), layer, layer,
                triplanar ? MaterialTextureProjection.Triplanar : MaterialTextureProjection.Face,
                textureBlend: detailStrength > 0f ? 1f : 0.28f,
                uvScale: uvScale,
                normalStrength: normalStrength,
                roughness: roughness,
                luminanceOnly: detailStrength > 0f,
                luminancePivot: luminancePivot,
                detailStrength: detailStrength,
                chromaStrength: chromaStrength,
                macroVariation: macroVariation);

        private static MaterialPresentationDefinition ReferenceTextured(
            byte materialIndex,
            float r,
            float g,
            float b,
            int layer,
            bool triplanar,
            float uvScale,
            float roughness) =>
            new(materialIndex, new float4(r, g, b, 1f), layer, layer,
                triplanar ? MaterialTextureProjection.Triplanar : MaterialTextureProjection.Face,
                textureBlend: 1f,
                uvScale: uvScale,
                normalStrength: 0f,
                roughness: roughness,
                luminanceOnly: false,
                luminancePivot: 0.68f,
                detailStrength: 0f,
                chromaStrength: 0f,
                macroVariation: 0.035f);

        private static MaterialPresentationDefinition StylizedTerrain(
            byte materialIndex,
            float r,
            float g,
            float b,
            int layer) =>
            new(materialIndex, new float4(r, g, b, 1f), layer, layer,
                MaterialTextureProjection.Triplanar,
                textureBlend: 0.16f,
                uvScale: 1f / 7f,
                normalStrength: 0.08f,
                roughness: 0.88f,
                luminanceOnly: true,
                luminancePivot: 0.66f,
                detailStrength: 0.58f,
                chromaStrength: 0.10f,
                macroVariation: 0.22f);

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
