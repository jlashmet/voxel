using System;
using Game.Materials.Api;
using VoxelEngine.Storage.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Canonical game-owned authoring catalogue for the stable material vocabulary.
    /// Semantic names live here; Storage receives only semantic-free MaterialDefinition values.
    /// Rendering-specific texture and shader data remains owned by Rendering.
    /// </summary>
    public static class GameMaterialCatalogue
    {
        public const int CanonicalMaterialCount = 22;

        private const uint WeatherCoatings =
            (1u << Coatings.Moss) |
            (1u << Coatings.Snow) |
            (1u << Coatings.Soot) |
            (1u << Coatings.Wet);

        private static readonly byte[] s_BuildableMaterials =
        {
            GameMaterialIds.Stone,
            GameMaterialIds.Wood,
            GameMaterialIds.Sand,
            GameMaterialIds.Glass,
        };

        private static readonly string[] s_Names =
        {
            "empty", "stone", "wood", "sand", "glass", "bedrock",
            "dark stone", "slate", "tile", "cloth", "grass", "water",
            "gold", "dirt", "moss", "lit window", "cascade", "crystal",
            "small masonry", "medium masonry", "large masonry", "white flower",
        };

        private static readonly MaterialDefinition[] s_Definitions =
        {
            Define(GameMaterialIds.Empty, 0, DestructionClass.None, SurfaceStyles.Smooth, 0u),
            Define(GameMaterialIds.Stone, 200, DestructionClass.Crumble, SurfaceStyles.Smooth, WeatherCoatings),
            Define(GameMaterialIds.Wood, 90, DestructionClass.Splinter, SurfaceStyles.Planar, WeatherCoatings, flammable: true),
            Define(GameMaterialIds.Sand, 20, DestructionClass.Powder, SurfaceStyles.Smooth, 1u << Coatings.Wet),
            Define(GameMaterialIds.Glass, 10, DestructionClass.Powder, SurfaceStyles.Sharp, 1u << Coatings.Wet),
            Define(GameMaterialIds.Bedrock, 255, DestructionClass.None, SurfaceStyles.Planar, 0u),
            Define(GameMaterialIds.DarkStone, 210, DestructionClass.Crumble, SurfaceStyles.Smooth, WeatherCoatings),
            Define(GameMaterialIds.Slate, 120, DestructionClass.Crumble, SurfaceStyles.Planar, WeatherCoatings),
            Define(GameMaterialIds.Tile, 110, DestructionClass.Crumble, SurfaceStyles.Planar, WeatherCoatings),
            Define(GameMaterialIds.Cloth, 15, DestructionClass.Splinter, SurfaceStyles.Planar, WeatherCoatings, flammable: true),
            Define(GameMaterialIds.Grass, 25, DestructionClass.Powder, SurfaceStyles.Smooth, WeatherCoatings, flammable: true),
            Define(GameMaterialIds.Water, 5, DestructionClass.Spreading, SurfaceStyles.Smooth, 0u),
            Define(GameMaterialIds.Gold, 180, DestructionClass.Crumble, SurfaceStyles.Sharp, 1u << Coatings.Soot),
            Define(GameMaterialIds.Dirt, 30, DestructionClass.Powder, SurfaceStyles.Smooth, WeatherCoatings),
            Define(GameMaterialIds.Moss, 40, DestructionClass.Powder, SurfaceStyles.Smooth, WeatherCoatings, flammable: true),
            Define(GameMaterialIds.LitWindow, 18, DestructionClass.Powder, SurfaceStyles.Sharp, 1u << Coatings.Wet),
            Define(GameMaterialIds.Cascade, 5, DestructionClass.Spreading, SurfaceStyles.Smooth, 0u),
            Define(GameMaterialIds.Crystal, 160, DestructionClass.Crumble, SurfaceStyles.Sharp, 1u << Coatings.Wet),
            Define(GameMaterialIds.MasonrySmall, 200, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
            Define(GameMaterialIds.MasonryMedium, 210, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
            Define(GameMaterialIds.MasonryLarge, 220, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, WeatherCoatings),
            Define(GameMaterialIds.FlowerWhite, 4, DestructionClass.Powder, SurfaceStyles.Smooth, 1u << Coatings.Wet, flammable: true),
        };

        public static int Count => s_Definitions.Length;
        public static int BuildableCount => s_BuildableMaterials.Length;
        public static ReadOnlySpan<MaterialDefinition> Definitions => s_Definitions;

        public static bool IsCanonicalId(byte materialId) =>
            materialId < s_Definitions.Length && s_Definitions[materialId].MaterialId == materialId;

        public static ref readonly MaterialDefinition Get(byte materialId)
        {
            if (!IsCanonicalId(materialId))
                throw new ArgumentOutOfRangeException(nameof(materialId), materialId, "Unknown game material id.");

            return ref s_Definitions[materialId];
        }

        public static string NameOf(byte materialId) =>
            IsCanonicalId(materialId) ? s_Names[materialId] : "unknown";

        public static byte BuildableAt(int index)
        {
            if ((uint)index >= (uint)s_BuildableMaterials.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return s_BuildableMaterials[index];
        }

        private static MaterialDefinition Define(
            byte materialId,
            byte hardness,
            DestructionClass destructionClass,
            ushort defaultSurfaceStyle,
            uint allowedCoatings,
            bool flammable = false) =>
            new(materialId, hardness, destructionClass, defaultSurfaceStyle, allowedCoatings, flammable);
    }
}
