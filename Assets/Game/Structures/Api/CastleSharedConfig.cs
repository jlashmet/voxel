using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Shared architectural component view of the legacy castle plan. The plan remains the stable
    /// deterministic compatibility input; this projection prevents castle authorers from carrying
    /// private copies of generic foundation, wall, tower, opening, floor, and battlement policy.
    /// </summary>
    public struct CastleSharedConfig
    {
        public StructureFootprintConfig KeepFoundation;
        public StructureWallRunConfig KeepWalls;
        public FloorLevelConfig KeepFloors;
        public StructureWallRunConfig CurtainWallX;
        public StructureWallRunConfig CurtainWallZ;
        public TowerConfig CornerTowers;
        public OpeningConfig MainGate;
        public BattlementConfig CurtainBattlements;

        public bool IsWellFormed =>
            KeepFoundation.IsWellFormed &&
            KeepWalls.IsWellFormed &&
            KeepFloors.IsWellFormed &&
            CurtainWallX.IsWellFormed &&
            CurtainWallZ.IsWellFormed &&
            CornerTowers.IsWellFormed &&
            MainGate.IsWellFormed &&
            CurtainBattlements.IsWellFormed;
    }

    /// <summary>
    /// Compatibility projection for the existing seeded castle family. Values deliberately mirror
    /// the current authoring constants so adopting shared configs does not change default geometry.
    /// </summary>
    public static class CastleCompatibilityPreset
    {
        public static CastleSharedConfig FromPlan(in CastlePlan plan)
        {
            var wallMaterial = StructureMaterialRole.PrimaryWall;
            var trimMaterial = StructureMaterialRole.SecondaryWall;

            return new CastleSharedConfig
            {
                KeepFoundation = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(
                        new int2(-6, -6),
                        new int2(plan.KeepHalfX * 2 + 12, plan.KeepHalfZ * 2 + 12)),
                    BasePlane = BasePlaneRule.LowestGround,
                    FoundationStyle = StructureFoundationStyle.Slab,
                    FoundationDepth = 30,
                    FoundationMaterial = StructureMaterialRole.Foundation,
                },
                KeepWalls = new StructureWallRunConfig
                {
                    Length = plan.KeepHalfX * 2,
                    Height = plan.KeepHeight,
                    Thickness = 8,
                    PrimaryMaterial = wallMaterial,
                    CornerBehavior = StructureWallCornerBehavior.Overlap,
                },
                KeepFloors = new FloorLevelConfig
                {
                    FloorCount = plan.Floors,
                    LevelHeight = plan.FloorHeight,
                    SlabThickness = 3,
                    SlabMaterialRole = StructureMaterialRole.Floor,
                },
                CurtainWallX = CurtainWall(plan.BaileyHalfX * 2, plan),
                CurtainWallZ = CurtainWall(plan.BaileyHalfZ * 2, plan),
                CornerTowers = new TowerConfig
                {
                    Shape = StructureTowerShape.Round,
                    Placement = StructureTowerPlacement.Corners,
                    TopStyle = StructureTowerTopStyle.Parapet,
                    Radius = plan.TowerRadius,
                    Height = plan.TowerHeight,
                    Count = 4,
                    Spacing = 0,
                    OpeningsEnabled = true,
                    Opening = new OpeningConfig
                    {
                        Kind = StructureOpeningKind.Window,
                        Width = 14,
                        Height = 24,
                        BottomOffset = 9,
                        Spacing = plan.FloorHeight,
                        FrameThickness = 3,
                        LintelThickness = 2,
                        FrameMaterialRole = trimMaterial,
                        FillMaterialRole = StructureMaterialRole.Opening,
                    },
                    WallMaterialRole = wallMaterial,
                    TrimMaterialRole = trimMaterial,
                },
                MainGate = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Arch,
                    Width = CastleLayout.FrontGateWidth,
                    Height = CastleLayout.FrontGateHeight,
                    BottomOffset = 1,
                    FrameThickness = 0,
                    LintelThickness = 0,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                CurtainBattlements = new BattlementConfig
                {
                    ParapetThickness = 8,
                    ParapetHeight = 0,
                    MerlonWidth = 26,
                    MerlonHeight = 20,
                    GapWidth = 18,
                    CornerMerlonWidth = 26,
                    MaterialRole = wallMaterial,
                },
            };
        }

        private static StructureWallRunConfig CurtainWall(int length, in CastlePlan plan)
        {
            var wall = new StructureWallRunConfig
            {
                Length = length,
                Height = plan.WallHeight,
                Thickness = plan.WallThickness,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
                RepetitionSpacing = 90,
                RepetitionOffset = 40,
            };
            wall.MaterialBands.Add(new StructureWallMaterialBand(
                0, math.min(22, plan.WallHeight), StructureMaterialRole.SecondaryWall));
            return wall;
        }
    }
}
