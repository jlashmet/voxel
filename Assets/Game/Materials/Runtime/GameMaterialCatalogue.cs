using System;
using Game.Materials.Api;
using VoxelEngine.Storage.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Canonical game-owned physical definitions for the stable material vocabulary.
    /// Keep semantic identity here; rendering remains responsible for GPU presentation.
    /// </summary>
    public static class GameMaterialCatalogue
    {
        public const int CanonicalMaterialCount = 22;

        private const uint WeatherCoatings =
            (1u << Coatings.Moss) |
            (1u << Coatings.Snow) |
            (1u << Coatings.Soot) |
            (1u << Coatings.Wet);

        private static readonly GameMaterialDefinition[] s_Definitions =
        {
            new(GameMaterialIds.Empty, "empty", 0, DestructionClass.None, SurfaceStyles.Smooth, 0u),
            new(GameMaterialIds.Stone, "stone", 200, DestructionClass.Crumble, SurfaceStyles.Smooth, WeatherCoatings),
            new(GameMaterialIds.Wood, "wood", 90, DestructionClass.Splinter, SurfaceStyles.Planar, WeatherCoatings, flammable: true),
            new(GameMaterialIds.Sand, "sand", 20, DestructionClass.Powder, SurfaceStyles.Smooth, 1u << Coatings.Wet),
            new(GameMaterialIds.Glass, "glass", 10, DestructionClass.Powder, SurfaceStyles.Sharp, 1u << Coatings.Wet),
            new(GameMaterialIds.Bedrock, "bedrock", 255, DestructionClass.None, SurfaceStyles.Planar, 0u),
            new(GameMaterialIds.DarkStone, "dark stone", 210, DestructionClass.Crumble, SurfaceStyles.Smooth, WeatherCoatings),
            new(GameMaterialIds.Slate, "slate", 120, DestructionClass.Crumble, SurfaceStyles.Planar, WeatherCoatings),
            new(GameMaterialIds.Tile, "tile", 110, DestructionClass.Crumble, SurfaceStyles.Planar, WeatherCoatings),
            new(GameMaterialIds.Cloth, "cloth", 15, DestructionClass.Splinter, SurfaceStyles.Planar, WeatherCoatings, flammable: true),
            new(GameMaterialIds.Grass, "grass", 25, DestructionClass.Powder, SurfaceStyles.Smooth, WeatherCoatings, flammable: true),
            new(GameMaterialIds.Water, "water", 5, DestructionClass.Spreading, SurfaceStyles.Smooth, 0u),
            new(GameMaterialIds.Gold, "gold", 180, DestructionClass.Crumble, SurfaceStyles.Sharp, 1u << Coatings.Soot),
            new(GameMaterialIds.Dirt, "dirt", 30, DestructionClass.Powder, SurfaceStyles.Smooth, WeatherCoatings),
            new(GameMaterialIds.Moss, "moss", 40, DestructionClass.Powder, SurfaceStyles.Smooth, WeatherCoatings, flammable: true),
            new(GameMaterialIds.LitWindow, "lit window", 18, DestructionClass.Powder, SurfaceStyles.Sharp, 1u << Coatings.Wet),
            new(GameMaterialIds.Cascade, "cascade", 5, DestructionClass.Spreading, SurfaceStyles.Smooth, 0u),
            new(GameMaterialIds.Crystal, "crystal", 160, DestructionClass.Crumble, SurfaceStyles.Sharp, 1u << Coatings.Wet),
            new(GameMaterialIds.MasonrySmall, "small masonry", 200, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
            new(GameMaterialIds.MasonryMedium, "medium masonry", 210, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
            new(GameMaterialIds.MasonryLarge, "large masonry", 220, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
            new(GameMaterialIds.FlowerWhite, "white flower", 4, DestructionClass.Powder, SurfaceStyles.Smooth, 1u << Coatings.Wet, flammable: true),
        };

        public static int Count => s_Definitions.Length;

        public static ReadOnlySpan<GameMaterialDefinition> Definitions => s_Definitions;

        public static ref readonly GameMaterialDefinition Get(byte materialId)
        {
            if (materialId >= s_Definitions.Length || s_Definitions[materialId].Id != materialId)
                throw new ArgumentOutOfRangeException(nameof(materialId), materialId, "Unknown game material id.");

            return ref s_Definitions[materialId];
        }

        public static string GetName(byte materialId) => Get(materialId).Name;
    }
}
