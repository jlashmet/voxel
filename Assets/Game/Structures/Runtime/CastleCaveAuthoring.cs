using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using SharedCaveAuthoring = VoxelEngine.Structures.Runtime.CaveAuthoring;
using CaveAuthoringResult = VoxelEngine.Structures.Runtime.CaveAuthoringResult;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Castle compatibility adapter into the one shared cave generator. Castle content owns the
    /// preset, seed, anchor, and game material mapping; tunnel/network mechanics are engine-shared.
    /// </summary>
    public static class CastleCaveAuthoring
    {
        private const ulong CastleCaveSeedSalt = 0x434153544C454341ul; // "CASTLECA"

        public static CaveConfig CompatibilityConfig
        {
            get
            {
                CaveConfig config = CaveConfig.Default;
                config.TunnelWidth = 28;
                config.TunnelHeight = 32;
                config.SegmentLength = 16;
                config.MainSegmentCount = 14;
                config.TurnChancePercent = 30;
                config.VerticalChancePercent = 28;
                config.MaxVerticalStepPerSegment = 4;
                config.SurfaceDescentSegments = 0;
                config.SurfaceDescentPerSegment = 0;
                config.MinimumSurfaceCover = 0;
                config.BranchChancePercent = 38;
                config.MaxBranches = 7;
                config.MaxBranchDepth = 2;
                config.BranchSegmentCount = 5;
                config.MinBranchSeparation = 24;
                config.ChamberChancePercent = 55;
                config.MinChamberRadius = 18;
                config.MaxChamberRadius = 60;
                config.MinChamberHeight = 24;
                config.MaxChamberHeight = 58;
                config.FloorRoughness = 2;
                config.CeilingRoughness = 4;
                config.WallRoughness = 3;
                config.BoundsHalfExtents = new int3(260, 100, 260);
                config.MinVerticalOffset = -64;
                config.MaxVerticalOffset = 64;
                config.EnableLoops = false;
                return config;
            }
        }

        public static CaveMaterialPalette CompatibilityPalette => new CaveMaterialPalette
        {
            Opening = GameMaterialIds.Empty,
            Rock = GameMaterialIds.DarkStone,
            Accent = GameMaterialIds.Crystal,
            Decoration = GameMaterialIds.Moss,
            Water = GameMaterialIds.Water,
        };

        public static CaveGenerationRequest Request(in CastlePlan plan, int3 at)
        {
            ulong seed = FeatureHash.Mix((ulong)plan.Seed ^ CastleCaveSeedSalt);
            // FeatureHash.Mix(0) is non-zero for this salted input; guard anyway because Cave requests
            // reserve zero as invalid rather than silently accepting an accidental missing seed.
            if (seed == 0)
                seed = 1;

            return CaveGenerationRequest.Underground(
                seed,
                at,
                Facing.South,
                28,
                32,
                8);
        }

        public static CaveAuthoringResult Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 at)
        {
            CaveConfig config = CompatibilityConfig;
            CaveMaterialPalette palette = CompatibilityPalette;
            CaveGenerationRequest request = Request(in plan, at);
            return SharedCaveAuthoring.Author(
                authoring,
                in request,
                in config,
                in palette);
        }
    }
}
