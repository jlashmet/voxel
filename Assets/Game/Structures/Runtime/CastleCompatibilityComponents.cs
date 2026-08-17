using Game.Materials.Api;
using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Resolves the game material palette for the canonical shared castle component config.
    /// Geometry/dimension policy lives in <see cref="CastleComponentPresets"/>; this runtime bridge
    /// only supplies the legacy game material ids so the compatibility path stays byte-for-byte
    /// aligned with the existing castle authorers while they migrate to semantic roles.
    /// </summary>
    public static class CastleCompatibilityComponents
    {
        public static CastleComponentConfig Resolve(in CastlePlan plan)
        {
            StructureMaterialPalette palette = LegacyPalette();
            return CastleComponentPresets.Compatibility(in plan, in palette);
        }

        private static StructureMaterialPalette LegacyPalette()
        {
            return new StructureMaterialPalette
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
}
