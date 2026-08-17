using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Game-owned castle composition expressed through the reusable structure-authoring contracts.
    /// Castle semantics stay in the game layer while walls, towers, openings, battlements, footprint,
    /// and semantic materials remain shared configuration understood by other archetypes.
    /// </summary>
    public struct CastleComponentConfig
    {
        public StructureFootprintConfig BaileyFootprint;
        public StructureWallRunConfig CurtainWallX;
        public StructureWallRunConfig CurtainWallZ;
        public TowerConfig CornerTowers;
        public OpeningConfig MainGate;
        public BattlementConfig CurtainBattlements;
        public StructureMaterialPalette Palette;

        public bool IsWellFormed =>
            BaileyFootprint.IsWellFormed &&
            CurtainWallX.IsWellFormed &&
            CurtainWallZ.IsWellFormed &&
            CornerTowers.IsWellFormed &&
            MainGate.IsWellFormed &&
            CurtainBattlements.IsWellFormed;
    }

    /// <summary>
    /// Compatibility mapping from the existing castle plan into shared structure components.
    /// The palette is supplied by game runtime so this API stays independent of game material ids.
    /// </summary>
    public static class CastleComponentPresets
    {
        public static CastleComponentConfig Compatibility(
            in CastlePlan plan,
            in StructureMaterialPalette palette)
        {
            var wallX = Wall(plan.BaileyHalfX * 2, in plan);
            var wallZ = Wall(plan.BaileyHalfZ * 2, in plan);

            var towerDoor = new OpeningConfig
            {
                Kind = StructureOpeningKind.Arch,
                Width = 14,
                Height = 30,
                BottomOffset = 2,
                Spacing = 0,
                StartMargin = 0,
                EndMargin = 0,
                FrameThickness = 0,
                LintelThickness = 0,
                WidthVariation = 0,
                HeightVariation = 0,
                FrameMaterialRole = StructureMaterialRole.Trim,
                FillMaterialRole = StructureMaterialRole.Opening,
            };

            return new CastleComponentConfig
            {
                BaileyFootprint = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(
                        new int2(-plan.BaileyHalfX, -plan.BaileyHalfZ),
                        new int2(plan.BaileyHalfX * 2, plan.BaileyHalfZ * 2)),
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FoundationStyle = StructureFoundationStyle.TerrainFill,
                    FoundationDepth = plan.CliffDrop,
                    MaxTerraceStep = 0,
                    FoundationMaterial = StructureMaterialRole.Foundation,
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
                    Opening = towerDoor,
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

        private static StructureWallRunConfig Wall(int length, in CastlePlan plan)
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
                0,
                math.min(22, plan.WallHeight),
                StructureMaterialRole.SecondaryWall));
            return wall;
        }
    }
}
