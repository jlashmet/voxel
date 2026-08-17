using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Small-building composition over the shared footprint, wall, opening, roof, and material
    /// contracts. Shed-specific policy is limited to facade placement and supported roof families.
    /// </summary>
    public struct ShedConfig
    {
        public StructureFootprintConfig Footprint;
        public StructureWallRunConfig Walls;
        public int Depth;

        public OpeningConfig Door;
        public int DoorCount;
        public Facing DoorFacade;
        public int DoorGroupOffset;
        public int DoorSpacing;

        public bool WindowsEnabled;
        public OpeningConfig Window;
        public int WindowCount;
        public Facing WindowFacade;
        public int WindowGroupOffset;
        public int WindowSpacing;

        public RoofConfig Roof;
        public StructureMaterialPalette Palette;

        public int Width => Footprint.Primary.Size.x;
        public int Height => Walls.Height;
        public int WallThickness => Walls.Thickness;

        public bool IsWellFormed
        {
            get
            {
                if (!Footprint.IsWellFormed || !Walls.IsWellFormed ||
                    Width <= Walls.Thickness * 2 || Depth <= Walls.Thickness * 2 ||
                    Footprint.Primary.Size.y != Depth || Walls.Length != Width ||
                    !Door.IsWellFormed || Door.Kind != StructureOpeningKind.Door ||
                    DoorCount < 1 || DoorCount > 4 || !Cardinal(DoorFacade) ||
                    Door.BottomOffset < 0 || Door.Height + Door.BottomOffset >= Height ||
                    DoorSpacing < 0 || !OpeningsFit(DoorFacade, Door.Width, DoorCount,
                        DoorSpacing, DoorGroupOffset))
                    return false;

                if (WindowsEnabled)
                {
                    if (!Window.IsWellFormed || Window.Kind != StructureOpeningKind.Window ||
                        WindowCount < 1 || WindowCount > 12 || !Cardinal(WindowFacade) ||
                        Window.BottomOffset < 0 || Window.Height + Window.BottomOffset >= Height ||
                        WindowSpacing < 0 || !OpeningsFit(WindowFacade, Window.Width, WindowCount,
                            WindowSpacing, WindowGroupOffset))
                        return false;
                }

                if (!Roof.IsWellFormed ||
                    (Roof.Style != RoofStyle.Flat && Roof.Style != RoofStyle.Gable &&
                     Roof.Style != RoofStyle.Shed))
                    return false;

                return true;
            }
        }

        private bool OpeningsFit(Facing facade, int openingWidth, int count, int spacing,
            int groupOffset)
        {
            int span = facade == Facing.North || facade == Facing.South ? Width : Depth;
            int centreSpacing = count <= 1 ? 0 : spacing;
            if (count > 1 && centreSpacing < openingWidth)
                return false;

            long groupWidth = openingWidth + (long)(count - 1) * centreSpacing;
            long left = (long)span / 2 + groupOffset - groupWidth / 2;
            long right = left + groupWidth;
            return left >= Walls.Thickness && right <= span - Walls.Thickness;
        }

        private static bool Cardinal(Facing facing) =>
            facing == Facing.North || facing == Facing.East ||
            facing == Facing.South || facing == Facing.West;
    }

    public static class ShedPresets
    {
        public static ShedConfig Storage(in StructureMaterialPalette palette) =>
            Create(48, 40, 34, 4, 2, RoofStyle.Gable, RoofAxis.X,
                16, 28, 1, Facing.South, 0, 0,
                false, 0, 0, 0, Facing.East, in palette);

        public static ShedConfig Workshop(in StructureMaterialPalette palette) =>
            Create(64, 48, 40, 5, 3, RoofStyle.Flat, RoofAxis.X,
                18, 30, 1, Facing.South, -12, 0,
                true, 14, 16, 2, Facing.East, in palette);

        public static ShedConfig LeanTo(in StructureMaterialPalette palette) =>
            Create(52, 36, 32, 4, 2, RoofStyle.Shed, RoofAxis.X,
                16, 26, 1, Facing.South, 0, 0,
                true, 12, 14, 1, Facing.West, in palette);

        private static ShedConfig Create(
            int width, int depth, int height, int wallThickness, int foundationDepth,
            RoofStyle roofStyle, RoofAxis roofAxis,
            int doorWidth, int doorHeight, int doorCount, Facing doorFacade,
            int doorOffset, int doorSpacing,
            bool windowsEnabled, int windowWidth, int windowHeight, int windowCount,
            Facing windowFacade, in StructureMaterialPalette palette)
        {
            var config = new ShedConfig
            {
                Footprint = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(
                        new int2(-width / 2, -depth / 2),
                        new int2(width, depth)),
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FoundationStyle = StructureFoundationStyle.Slab,
                    FoundationDepth = foundationDepth,
                    FoundationMaterial = StructureMaterialRole.Foundation,
                },
                Walls = new StructureWallRunConfig
                {
                    Length = width,
                    Height = height,
                    Thickness = wallThickness,
                    PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                    CornerBehavior = StructureWallCornerBehavior.Overlap,
                },
                Depth = depth,
                Door = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = doorWidth,
                    Height = doorHeight,
                    BottomOffset = 0,
                    Spacing = 0,
                    StartMargin = 0,
                    EndMargin = 0,
                    FrameThickness = 2,
                    LintelThickness = 2,
                    WidthVariation = 0,
                    HeightVariation = 0,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                DoorCount = doorCount,
                DoorFacade = doorFacade,
                DoorGroupOffset = doorOffset,
                DoorSpacing = doorSpacing,
                WindowsEnabled = windowsEnabled,
                Window = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = math.max(1, windowWidth),
                    Height = math.max(1, windowHeight),
                    BottomOffset = 12,
                    Spacing = 0,
                    StartMargin = 0,
                    EndMargin = 0,
                    FrameThickness = 2,
                    LintelThickness = 2,
                    WidthVariation = 0,
                    HeightVariation = 0,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Glass,
                },
                WindowCount = windowCount,
                WindowFacade = windowFacade,
                WindowGroupOffset = 0,
                WindowSpacing = windowCount > 1 ? windowWidth + 8 : 0,
                Roof = new RoofConfig
                {
                    Style = roofStyle,
                    RidgeAxis = roofAxis,
                    PitchRise = roofStyle == RoofStyle.Flat ? 0 : 16,
                    PitchRun = roofStyle == RoofStyle.Flat ? 0 : 24,
                    EaveOverhang = roofStyle == RoofStyle.Flat ? 2 : 4,
                    Thickness = 2,
                    ParapetHeight = 0,
                    MaterialRole = StructureMaterialRole.Roof,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                Palette = palette,
            };

            return config;
        }
    }
}
