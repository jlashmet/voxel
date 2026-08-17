using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Canonical game-owned castle composition expressed through the reusable structure-authoring
    /// contracts. Castle semantics stay in the game layer while foundation, floors, walls, towers,
    /// openings, battlements, and semantic materials use the same bounded configs as other archetypes.
    /// </summary>
    public struct CastleComponentConfig
    {
        public StructureFootprintConfig BaileyFootprint;
        public StructureFootprintConfig KeepFoundation;
        public StructureWallRunConfig KeepWalls;
        public FloorLevelConfig KeepFloors;
        public StructureWallRunConfig CurtainWallX;
        public StructureWallRunConfig CurtainWallZ;
        public TowerConfig CornerTowers;
        public OpeningConfig MainGate;
        public BattlementConfig CurtainBattlements;
        public StructureMaterialPalette Palette;

        public bool IsWellFormed =>
            BaileyFootprint.IsWellFormed &&
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
    /// Compatibility mapping from the existing seeded castle plan into the canonical shared
    /// components. The palette is supplied by game runtime so this API stays independent of game
    /// material ids. Values intentionally preserve the historical castle dimensions and cadence.
    /// </summary>
    public static class CastleComponentPresets
    {
        public static CastleComponentConfig Compatibility(
            in CastlePlan plan,
            in StructureMaterialPalette palette)
        {
            var wallX = Wall(plan.BaileyHalfX * 2, plan.WallHeight, plan.WallThickness);
            var wallZ = Wall(plan.BaileyHalfZ * 2, plan.WallHeight, plan.WallThickness);

            return new CastleComponentConfig
            {
                BaileyFootprint = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(
                        new int2(-plan.BaileyHalfX, -plan.BaileyHalfZ),
                        new int2(plan.BaileyHalfX * 2, plan.BaileyHalfZ * 2)),
                    BasePlane = BasePlaneRule.FixedAltitude,
                    // The legacy site stage still owns its plateau/cliff sculpt. Declaring the
                    // bailey footprint here gives shared bounds without authoring a second foundation.
                    FoundationStyle = StructureFoundationStyle.None,
                    FoundationDepth = 0,
                    FoundationMaterial = StructureMaterialRole.Foundation,
                },
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
                KeepWalls = Wall(plan.KeepHalfX * 2, plan.KeepHeight, 8),
                KeepFloors = new FloorLevelConfig
                {
                    FloorCount = plan.Floors,
                    LevelHeight = plan.FloorHeight,
                    SlabThickness = 3,
                    SlabMaterialRole = StructureMaterialRole.Floor,
                },
                CurtainWallX = wallX,
                CurtainWallZ = wallZ,
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
                        StartMargin = 0,
                        EndMargin = 0,
                        FrameThickness = 3,
                        LintelThickness = 2,
                        WidthVariation = 0,
                        HeightVariation = 0,
                        FrameMaterialRole = StructureMaterialRole.Trim,
                        FillMaterialRole = StructureMaterialRole.Glass,
                    },
                    WallMaterialRole = StructureMaterialRole.PrimaryWall,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                MainGate = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Arch,
                    Width = CastleLayout.FrontGateWidth,
                    Height = CastleLayout.FrontGateHeight,
                    BottomOffset = 1,
                    Spacing = 0,
                    StartMargin = 0,
                    EndMargin = 0,
                    FrameThickness = 0,
                    LintelThickness = 0,
                    WidthVariation = 0,
                    HeightVariation = 0,
                    FrameMaterialRole = StructureMaterialRole.Trim,
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
                    MaterialRole = StructureMaterialRole.PrimaryWall,
                },
                Palette = palette,
            };
        }

        private static StructureWallRunConfig Wall(int length, int height, int thickness)
        {
            var wall = new StructureWallRunConfig
            {
                Length = length,
                Height = height,
                Thickness = thickness,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
                RepetitionSpacing = 90,
                RepetitionOffset = 40,
            };
            wall.MaterialBands.Add(new StructureWallMaterialBand(
                0,
                math.min(22, height),
                StructureMaterialRole.SecondaryWall));
            return wall;
        }
    }
}
