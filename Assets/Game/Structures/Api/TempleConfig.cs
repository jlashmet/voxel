using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    public struct TempleConfig
    {
        public Facing EntryFacing;
        public StructureFootprintConfig Footprint;
        public int PlatformWidth, PlatformDepth, PlatformHeight;
        public StairConfig ApproachStairs;

        public int SanctuaryWidth, SanctuaryDepth, SanctuaryHeight, WallThickness;
        public OpeningConfig SanctuaryDoor;
        public RoofConfig SanctuaryRoof;

        public bool CourtyardEnabled;
        public int CourtyardWidth, CourtyardDepth;
        public int CourtyardWallHeight;
        public OpeningConfig CourtyardGate;

        public bool ColonnadeEnabled;
        public ColumnConfig Columns;
        public int ColumnInset;

        public StructureMaterialPalette Palette;

        public bool IsWellFormed
        {
            get
            {
                if (!StructureCardinalTransform.IsCardinal(EntryFacing) || !Footprint.IsWellFormed ||
                    PlatformWidth <= 0 || PlatformDepth <= 0 || PlatformHeight <= 0 ||
                    Footprint.Primary.Size.x != PlatformWidth || Footprint.Primary.Size.y != PlatformDepth ||
                    !ApproachStairs.IsWellFormed || ApproachStairs.Width >= PlatformWidth ||
                    SanctuaryWidth <= WallThickness * 2 || SanctuaryDepth <= WallThickness * 2 ||
                    SanctuaryHeight <= WallThickness * 2 || WallThickness <= 0 ||
                    SanctuaryWidth >= PlatformWidth || SanctuaryDepth >= PlatformDepth ||
                    !SanctuaryDoor.IsWellFormed || SanctuaryDoor.Kind != StructureOpeningKind.Door ||
                    SanctuaryDoor.Width >= SanctuaryWidth - WallThickness * 2 ||
                    SanctuaryDoor.Height + SanctuaryDoor.BottomOffset >= SanctuaryHeight ||
                    !SanctuaryRoof.IsWellFormed)
                    return false;

                if (CourtyardEnabled &&
                    (CourtyardWidth <= SanctuaryWidth || CourtyardDepth <= SanctuaryDepth ||
                     CourtyardWidth >= PlatformWidth || CourtyardDepth >= PlatformDepth ||
                     CourtyardWallHeight <= WallThickness * 2 ||
                     !CourtyardGate.IsWellFormed || CourtyardGate.Kind != StructureOpeningKind.Door ||
                     CourtyardGate.Width >= CourtyardWidth - WallThickness * 2 ||
                     CourtyardGate.Height + CourtyardGate.BottomOffset >= CourtyardWallHeight))
                    return false;

                if (ColonnadeEnabled &&
                    (!Columns.IsWellFormed || ColumnInset < Columns.Width ||
                     Columns.MaxCountForSpan(PlatformWidth - ColumnInset * 2, 0) < 2 ||
                     Columns.MaxCountForSpan(PlatformDepth - ColumnInset * 2, 0) < 2))
                    return false;

                return true;
            }
        }
    }

    public static class TemplePresets
    {
        public static TempleConfig ClassicalColumned(in StructureMaterialPalette palette)
        {
            TempleConfig c = Base(96, 132, 8, 56, 70, 42, in palette);
            c.ColonnadeEnabled = true;
            c.Columns = Columns(5, 38, 14);
            c.ColumnInset = 10;
            c.SanctuaryRoof = Roof(RoofStyle.Gable, RoofAxis.Z, 16, 24);
            return c;
        }

        public static TempleConfig CourtyardTemple(in StructureMaterialPalette palette)
        {
            TempleConfig c = Base(124, 156, 7, 54, 62, 38, in palette);
            c.CourtyardEnabled = true;
            c.CourtyardWidth = 92;
            c.CourtyardDepth = 112;
            // The wall has to outrun its own gate: validity requires the opening to end below the
            // wall top, so a 2.6 m gate needs more than the 2.4 m wall this used to have.
            c.CourtyardWallHeight = 32;
            // 2.6 m of headroom. At the previous 2.0 m the gate cleared a 1.8 m character by two
            // voxels, which reads as a crawl-under rather than a way in.
            c.CourtyardGate = Door(22, 26, 2);
            c.ColonnadeEnabled = true;
            c.Columns = Columns(4, 28, 16);
            c.ColumnInset = 12;
            c.SanctuaryRoof = Roof(RoofStyle.Hip, RoofAxis.Z, 12, 24);
            return c;
        }

        private static TempleConfig Base(
            int platformWidth, int platformDepth, int platformHeight,
            int sanctuaryWidth, int sanctuaryDepth, int sanctuaryHeight,
            in StructureMaterialPalette palette)
        {
            const int wall = 5;
            return new TempleConfig
            {
                EntryFacing = Facing.South,
                Footprint = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(
                        new int2(-platformWidth / 2, -platformDepth / 2),
                        new int2(platformWidth, platformDepth)),
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FoundationStyle = StructureFoundationStyle.Slab,
                    FoundationDepth = platformHeight,
                    FoundationMaterial = StructureMaterialRole.Foundation,
                },
                PlatformWidth = platformWidth,
                PlatformDepth = platformDepth,
                PlatformHeight = platformHeight,
                ApproachStairs = new StairConfig
                {
                    Direction = StructureRunDirection.PositiveZ,
                    Layout = StructureStairLayout.Straight,
                    Width = 34,
                    StepCount = platformHeight,
                    StepRise = 1,
                    StepRun = 2,
                    StepsPerFlight = platformHeight,
                    Landing = new LandingConfig
                    {
                        Width = 34,
                        Length = 4,
                        Thickness = 1,
                        MaterialRole = StructureMaterialRole.Foundation,
                    },
                    MaterialRole = StructureMaterialRole.Foundation,
                },
                SanctuaryWidth = sanctuaryWidth,
                SanctuaryDepth = sanctuaryDepth,
                SanctuaryHeight = sanctuaryHeight,
                WallThickness = wall,
                SanctuaryDoor = Door(16, 28, 3),
                SanctuaryRoof = Roof(RoofStyle.Flat, RoofAxis.Z, 0, 0),
                CourtyardGate = Door(16, 20, 2),
                Columns = Columns(5, 34, 14),
                ColumnInset = 10,
                Palette = palette,
            };
        }

        private static ColumnConfig Columns(int width, int height, int spacing) => new ColumnConfig
        {
            Shape = StructureColumnShape.Round,
            Width = width,
            Height = height,
            BaseHeight = 2,
            CapitalHeight = 3,
            Spacing = spacing,
            ShaftMaterialRole = StructureMaterialRole.Column,
            BaseMaterialRole = StructureMaterialRole.Trim,
            CapitalMaterialRole = StructureMaterialRole.Trim,
        };

        private static OpeningConfig Door(int width, int height, int frame) => new OpeningConfig
        {
            Kind = StructureOpeningKind.Door,
            Width = width,
            Height = height,
            BottomOffset = 0,
            Spacing = 0,
            StartMargin = 0,
            EndMargin = 0,
            FrameThickness = frame,
            LintelThickness = frame,
            FrameMaterialRole = StructureMaterialRole.Trim,
            FillMaterialRole = StructureMaterialRole.Opening,
        };

        private static RoofConfig Roof(RoofStyle style, RoofAxis axis, int rise, int run) => new RoofConfig
        {
            Style = style,
            RidgeAxis = axis,
            PitchRise = rise,
            PitchRun = run,
            EaveOverhang = style == RoofStyle.Flat ? 2 : 4,
            Thickness = 2,
            ParapetHeight = 0,
            MaterialRole = StructureMaterialRole.Roof,
            TrimMaterialRole = StructureMaterialRole.Trim,
        };
    }
}
