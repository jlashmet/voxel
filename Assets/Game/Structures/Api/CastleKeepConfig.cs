using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Keep-specific composition over shared structure contracts. All dimensions are integer voxels;
    /// the active castle build may project these values into CastlePlan while legacy keep subpasses
    /// are migrated incrementally.
    /// </summary>
    public struct CastleKeepConfig
    {
        public StructureWallRunConfig WallX;
        public StructureWallRunConfig WallZ;
        public FloorLevelConfig Levels;
        public RoofConfig Roof;
        public BattlementConfig Battlements;
        public OpeningConfig MainEntrance;
        public OpeningConfig Windows;
        public StructureMaterialPalette Palette;

        public int Width => WallX.Length;
        public int Depth => WallZ.Length;
        public int Height => WallX.Height;
        public int WallThickness => WallX.Thickness;
        public int FloorCount => Levels.FloorCount;
        public int FloorHeight => Levels.LevelHeight;

        public bool IsWellFormed =>
            WallX.IsWellFormed &&
            WallZ.IsWellFormed &&
            WallX.Height == WallZ.Height &&
            WallX.Thickness == WallZ.Thickness &&
            (WallX.Length & 1) == 0 &&
            (WallZ.Length & 1) == 0 &&
            Levels.IsWellFormed &&
            Roof.IsWellFormed &&
            Battlements.IsWellFormed &&
            MainEntrance.Kind == StructureOpeningKind.Arch &&
            MainEntrance.IsWellFormed &&
            Windows.Kind == StructureOpeningKind.Window &&
            Windows.IsWellFormed;
    }

    /// <summary>Compatibility defaults for the existing authored keep.</summary>
    public static class CastleKeepPresets
    {
        public static CastleKeepConfig Compatibility(in CastlePlan plan)
        {
            var wallX = new StructureWallRunConfig
            {
                Length = plan.KeepHalfX * 2,
                Height = plan.KeepHeight,
                Thickness = 8,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
            };
            var wallZ = wallX;
            wallZ.Length = plan.KeepHalfZ * 2;

            return new CastleKeepConfig
            {
                WallX = wallX,
                WallZ = wallZ,
                Levels = new FloorLevelConfig
                {
                    FloorCount = plan.Floors,
                    LevelHeight = plan.FloorHeight,
                    SlabThickness = 3,
                    MinimumLevelHeightDelta = 0,
                    MaximumLevelHeightDelta = 0,
                    SlabMaterialRole = StructureMaterialRole.Floor,
                },
                Roof = new RoofConfig
                {
                    Style = RoofStyle.Gable,
                    RidgeAxis = RoofAxis.X,
                    PitchRise = 1,
                    PitchRun = 2,
                    EaveOverhang = 0,
                    Thickness = 1,
                    ParapetHeight = 6,
                    MaterialRole = StructureMaterialRole.Roof,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                Battlements = new BattlementConfig
                {
                    ParapetThickness = 7,
                    ParapetHeight = 6,
                    MerlonWidth = 24,
                    MerlonHeight = 20,
                    GapWidth = 20,
                    CornerMerlonWidth = 24,
                    MaterialRole = StructureMaterialRole.PrimaryWall,
                },
                MainEntrance = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Arch,
                    Width = 30,
                    Height = 34,
                    BottomOffset = 1,
                    Spacing = 0,
                    FrameThickness = 0,
                    LintelThickness = 0,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                Windows = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 16,
                    Height = plan.FloorHeight - 18,
                    BottomOffset = 12,
                    Spacing = plan.KeepHalfX / 2,
                    StartMargin = 0,
                    EndMargin = 0,
                    FrameThickness = 0,
                    LintelThickness = 0,
                    FillMaterialRole = StructureMaterialRole.Glass,
                },
                Palette = new StructureMaterialPalette
                {
                    Foundation = 2,
                    PrimaryWall = 1,
                    SecondaryWall = 2,
                    Trim = 2,
                    Roof = 9,
                    Floor = 6,
                    Opening = 0,
                    Glass = 15,
                    Detail = 2,
                },
            };
        }
    }
}
