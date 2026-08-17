using Game.Materials.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Semantic material mapping used while migrating castle authoring to shared structure
    /// components. Game material identity stays in the game layer; reusable components consume
    /// only semantic roles.
    /// </summary>
    internal static class CastleStructurePalette
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
    }
}
