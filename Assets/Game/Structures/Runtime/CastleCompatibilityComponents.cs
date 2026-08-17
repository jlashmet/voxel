using Game.Materials.Api;
using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Resolves the canonical shared-component compatibility config consumed by the active castle
    /// authoring path. The API preset owns geometry policy; this runtime bridge supplies only the
    /// game's stable material ids for semantic palette roles.
    /// </summary>
    public static class CastleCompatibilityComponents
    {
        public static CastleComponentConfig Resolve(in CastlePlan plan)
        {
            StructureMaterialPalette palette = LegacyPalette();
            return CastleComponentPresets.Compatibility(in plan, in palette);
        }

        private static StructureMaterialPalette LegacyPalette() => new StructureMaterialPalette
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
            Detail = GameMaterialIds.DarkStone,
        };
    }
}
