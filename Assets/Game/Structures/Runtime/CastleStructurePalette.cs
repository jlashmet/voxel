using Game.Materials.Api;
using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Semantic material mapping used while migrating castle authoring to shared structure
    /// components. Game material identity stays in the game layer; reusable components consume
    /// only semantic roles.
    /// </summary>
    public static class CastleStructurePalette
    {
        public static StructureMaterialPalette Compatibility => new StructureMaterialPalette
        {
            Foundation = GameMaterialIds.DarkStone,
            PrimaryWall = GameMaterialIds.Stone,
            SecondaryWall = GameMaterialIds.DarkStone,
            Trim = GameMaterialIds.DarkStone,
            Roof = GameMaterialIds.Slate,
            Floor = GameMaterialIds.Wood,
            Column = GameMaterialIds.Stone,
            Accent = GameMaterialIds.Gold,
            Underground = GameMaterialIds.DarkStone,
            Opening = GameMaterialIds.Empty,
            Glass = GameMaterialIds.LitWindow,
            Detail = GameMaterialIds.Cloth,
        };

        /// <summary>
        /// Single game-runtime compatibility bridge from the historical CastlePlan into the
        /// canonical shared-component bundle. Keeping the palette binding here prevents each
        /// castle stage from inventing its own projection or material mapping.
        /// </summary>
        public static CastleComponentConfig ResolveCompatibility(in CastlePlan plan)
        {
            StructureMaterialPalette palette = Compatibility;
            return CastleComponentPresets.Compatibility(in plan, in palette);
        }
    }
}
