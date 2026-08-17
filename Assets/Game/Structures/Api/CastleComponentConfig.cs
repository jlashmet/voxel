using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Canonical game-owned castle composition expressed through the reusable structure-authoring
    /// contracts. Castle semantics stay in the game layer while foundation, floors, walls, towers,
    /// openings, battlements, and semantic materials use the same bounded configs as other archetypes.
    /// Richer curtain polygon/layout policy is projected through <see cref="CastleCurtainPresets"/>
    /// so this compatibility bundle has only one source for wall dimensions and battlements.
    /// </summary>
    public struct CastleComponentConfig
    {
        public StructureFootprintConfig BaileyFootprint;
        public StructureFootprintConfig KeepFoundation;
        public int KeepFoundationTopOffset;

        /// <summary>Keep width/height/thickness; <see cref="KeepDepth"/> supplies the second axis.</summary>
        public StructureWallRunConfig KeepWalls;
        public int KeepDepth;
        public FloorLevelConfig KeepFloors;
        public RoofConfig KeepRoof;
        public BattlementConfig KeepParapet;
        public OpeningConfig KeepEntrance;
        public OpeningConfig KeepWindow;

        public StructureWallRunConfig CurtainWallX;
        public StructureWallRunConfig CurtainWallZ;
        public TowerConfig CornerTowers;
        public CastleGatehouseConfig Gatehouse;
        public BattlementConfig CurtainBattlements;
        public StructureMaterialPalette Palette;

        public int KeepWidth => KeepWalls.Length;
        public int KeepHeight => KeepWalls.Height;
        public int KeepWallThickness => KeepWalls.Thickness;
        public int KeepLevelCount => KeepFloors.FloorCount;

        // Transitional read-only aliases keep older callers source-compatible while gatehouse
        // semantics move under one castle-specific configuration graph.
        public TowerConfig GateTowers => Gatehouse.FlankingTowers;
        public OpeningConfig MainGate => Gatehouse.GateOpening;
        public BattlementConfig GatehouseBattlements => Gatehouse.Battlements;

        public bool IsWellFormed =>
            BaileyFootprint.IsWellFormed &&
            KeepFoundation.IsWellFormed &&
            KeepFoundationTopOffset >= 0 &&
            KeepWalls.IsWellFormed &&
            KeepDepth > KeepWalls.Thickness * 2 &&
            KeepFloors.IsWellFormed &&
            KeepRoof.IsWellFormed &&
            KeepParapet.IsWellFormed &&
            KeepEntrance.Kind == StructureOpeningKind.Arch && KeepEntrance.IsWellFormed &&
            KeepWindow.Kind == StructureOpeningKind.Window && KeepWindow.IsWellFormed &&
            CurtainWallX.IsWellFormed &&
            CurtainWallZ.IsWellFormed &&
            CornerTowers.IsWellFormed &&
            Gatehouse.IsWellFormed &&
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
            var wallX = CurtainWall(plan.BaileyHalfX * 2, plan.WallHeight, plan.WallThickness);
            var wallZ = CurtainWall(plan.BaileyHalfZ * 2, plan.WallHeight, plan.WallThickness);
            OpeningConfig towerWindow = TowerWindow(plan.FloorHeight);
            TowerConfig gateTowers = Tower(
                StructureTowerPlacement.Explicit,
                plan.GateTowerRadius,
                plan.GateTowerHeight,
                2,
                in towerWindow);
            OpeningConfig mainGate = GateOpening();
            BattlementConfig gatehouseBattlements = GatehouseBattlements();

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
                KeepFoundationTopOffset = 4,
                KeepWalls = KeepWall(plan.KeepHalfX * 2, plan.KeepHeight, 8),
                KeepDepth = plan.KeepHalfZ * 2,
                KeepFloors = new FloorLevelConfig
                {
                    FloorCount = plan.Floors,
                    LevelHeight = plan.FloorHeight,
                    SlabThickness = 3,
                    SlabMaterialRole = StructureMaterialRole.Floor,
                },
                KeepRoof = new RoofConfig
                {
                    Style = RoofStyle.Gable,
                    RidgeAxis = RoofAxis.X,
                    PitchRise = 70,
                    PitchRun = math.max(1, plan.KeepHalfZ * 2),
                    EaveOverhang = 0,
                    Thickness = 1,
                    ParapetHeight = 0,
                    MaterialRole = StructureMaterialRole.Roof,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                KeepParapet = new BattlementConfig
                {
                    ParapetThickness = 7,
                    ParapetHeight = 6,
                    MerlonWidth = 24,
                    MerlonHeight = 20,
                    GapWidth = 20,
                    CornerMerlonWidth = 24,
                    MaterialRole = StructureMaterialRole.PrimaryWall,
                },
                KeepEntrance = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Arch,
                    Width = 30,
                    Height = 34,
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
                KeepWindow = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 16,
                    Height = math.max(1, plan.FloorHeight - 18),
                    BottomOffset = 12,
                    Spacing = 0,
                    StartMargin = 0,
                    EndMargin = 0,
                    FrameThickness = 3,
                    LintelThickness = 0,
                    WidthVariation = 0,
                    HeightVariation = 4,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Glass,
                },
                CurtainWallX = wallX,
                CurtainWallZ = wallZ,
                CornerTowers = Tower(
                    StructureTowerPlacement.Corners,
                    plan.TowerRadius,
                    plan.TowerHeight,
                    4,
                    in towerWindow),
                Gatehouse = new CastleGatehouseConfig
                {
                    Width = 108,
                    Depth = plan.WallThickness * 2,
                    Height = plan.WallHeight + 22,
                    TowerCentreOffset = 54,
                    GateLeafDepth = CastleLayout.FrontGateDepth,
                    FlankingTowers = gateTowers,
                    GateOpening = mainGate,
                    PortcullisOpening = new OpeningConfig
                    {
                        Kind = StructureOpeningKind.Arch,
                        Width = CastleLayout.FrontGateWidth + 4,
                        Height = CastleLayout.FrontGateHeight + 14,
                        BottomOffset = 0,
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
                    Battlements = gatehouseBattlements,
                    RoadAnchor = new AttachmentAnchorConfig
                    {
                        Kind = StructureAttachmentKind.Road,
                        LocalPosition = new int3(
                            0,
                            plan.PlateauHeight,
                            -plan.BaileyHalfZ - plan.WallThickness - 149),
                        Facing = Facing.South,
                        SnapToGround = false,
                    },
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

        private static StructureWallRunConfig CurtainWall(int length, int height, int thickness)
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
            wall.MaterialBands.Add(new StructureWallMaterialBand(
                height * 66 / 100,
                2,
                StructureMaterialRole.SecondaryWall));
            return wall;
        }

        private static StructureWallRunConfig KeepWall(int length, int height, int thickness) =>
            new StructureWallRunConfig
            {
                Length = length,
                Height = height,
                Thickness = thickness,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
            };

        private static OpeningConfig TowerWindow(int floorHeight) => new OpeningConfig
        {
            Kind = StructureOpeningKind.Window,
            Width = 14,
            Height = 24,
            BottomOffset = 9,
            Spacing = floorHeight,
            StartMargin = 0,
            EndMargin = 0,
            FrameThickness = 3,
            LintelThickness = 2,
            WidthVariation = 0,
            HeightVariation = 0,
            FrameMaterialRole = StructureMaterialRole.Trim,
            FillMaterialRole = StructureMaterialRole.Glass,
        };

        private static OpeningConfig GateOpening() => new OpeningConfig
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
        };

        private static BattlementConfig GatehouseBattlements() => new BattlementConfig
        {
            ParapetThickness = 8,
            ParapetHeight = 0,
            MerlonWidth = 18,
            MerlonHeight = 18,
            GapWidth = 12,
            CornerMerlonWidth = 18,
            MaterialRole = StructureMaterialRole.PrimaryWall,
        };

        private static TowerConfig Tower(
            StructureTowerPlacement placement,
            int radius,
            int height,
            int count,
            in OpeningConfig opening) => new TowerConfig
        {
            Shape = StructureTowerShape.Round,
            Placement = placement,
            TopStyle = StructureTowerTopStyle.Parapet,
            Radius = radius,
            Height = height,
            Count = count,
            Spacing = 0,
            OpeningsEnabled = true,
            Opening = opening,
            WallMaterialRole = StructureMaterialRole.PrimaryWall,
            TrimMaterialRole = StructureMaterialRole.Trim,
        };
    }
}
