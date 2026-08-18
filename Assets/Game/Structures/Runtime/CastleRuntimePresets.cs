using Game.Materials.Api;
using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Game-material binding for the shared castle compatibility configuration.</summary>
    internal static class CastleRuntimePresets
    {
        public static CastleComponentConfig Compatibility(in CastlePlan plan)
        {
            var palette = new StructureMaterialPalette
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
                Opening = GameMaterialIds.Wood,
                Glass = GameMaterialIds.LitWindow,
                Detail = GameMaterialIds.Cloth,
            };
            return CastleComponentPresets.Compatibility(in plan, in palette);
        }
    }
}
