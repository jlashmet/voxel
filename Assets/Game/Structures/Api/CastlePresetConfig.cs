using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    public struct CastleBuildStageConfig
    {
        public bool Site, CurtainWalls, CornerTowers, Gatehouse, Courtyard, Keep, Dungeon, Landscape;

        public static CastleBuildStageConfig Full => new CastleBuildStageConfig
        {
            Site = true, CurtainWalls = true, CornerTowers = true, Gatehouse = true,
            Courtyard = true, Keep = true, Dungeon = true, Landscape = true,
        };
    }

    public struct CastlePresetConfig
    {
        public CastleComponentConfig Components;
        public CastleCurtainConfig Curtain;
        public CastleBuildStageConfig Stages;
        public bool IsWellFormed => Components.IsWellFormed && Curtain.IsWellFormed;
    }

    public static class CastlePresets
    {
        public static CastlePresetConfig Compatibility(in CastlePlan plan,
            in StructureMaterialPalette palette)
        {
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            return new CastlePresetConfig
            {
                Components = components,
                Curtain = CastleCurtainPresets.Compatibility(in components),
                Stages = CastleBuildStageConfig.Full,
            };
        }

        public static CastlePresetConfig KeepOnly(in CastlePlan plan,
            in StructureMaterialPalette palette)
        {
            CastlePresetConfig preset = Compatibility(in plan, in palette);
            preset.Stages = new CastleBuildStageConfig { Site = true, Keep = true };
            return preset;
        }

        public static CastlePresetConfig WalledCastle(in CastlePlan plan,
            in StructureMaterialPalette palette)
        {
            CastlePresetConfig preset = Compatibility(in plan, in palette);
            preset.Stages = new CastleBuildStageConfig
            {
                Site = true, CurtainWalls = true, CornerTowers = true,
                Gatehouse = true, Courtyard = true, Keep = true,
            };
            return preset;
        }
    }
}
