using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    public enum ChurchBellTowerPlacement : byte
    {
        None = 0,
        FrontCentre = 1,
        FrontLeft = 2,
        FrontRight = 3,
    }

    /// <summary>
    /// Church-specific plan semantics composed from shared footprint, wall, opening, roof, tower,
    /// and palette contracts. The local plan always faces South; EntryFacing rotates it cardinally.
    /// </summary>
    public struct ChurchConfig
    {
        public Facing EntryFacing;
        public StructureFootprintConfig Footprint;

        public StructureWallRunConfig NaveWalls;
        public int NaveLength;
        public RoofConfig NaveRoof;

        public bool AislesEnabled;
        public int AisleWidth;
        public int AisleHeight;
        public RoofConfig AisleRoof;
        public OpeningConfig AisleArch;

        public int SanctuaryWidth;
        public int SanctuaryLength;
        public int SanctuaryHeight;
        public RoofConfig SanctuaryRoof;
        public OpeningConfig SanctuaryArch;

        public bool ApseEnabled;
        public int ApseRadius;
        public int ApseHeight;
        public int ApseRoofHeight;

        public OpeningConfig MainPortal;
        public bool SideDoorsEnabled;
        public OpeningConfig SideDoor;
        public OpeningConfig Window;
        public bool ClerestoryEnabled;
        public OpeningConfig ClerestoryWindow;

        public ChurchBellTowerPlacement BellTowerPlacement;
        public TowerConfig BellTower;
        public bool SpireEnabled;
        public int SpireHeight;

        public StructureMaterialPalette Palette;

        public int NaveWidth => NaveWalls.Length;
        public int WallThickness => NaveWalls.Thickness;
        public int OverallWidth => math.max(
            AislesEnabled ? NaveWidth + AisleWidth * 2 : NaveWidth,
            math.max(SanctuaryWidth, ApseEnabled ? ApseRadius * 2 : 0));
        public int OverallLength => NaveLength + SanctuaryLength + (ApseEnabled ? ApseRadius : 0);

        public bool IsWellFormed
        {
            get
            {
                if (!StructureCardinalTransform.IsCardinal(EntryFacing) ||
                    !Footprint.IsWellFormed || !NaveWalls.IsWellFormed ||
                    NaveLength <= WallThickness * 2 ||
                    Footprint.Primary.Size.x != OverallWidth ||
                    Footprint.Primary.Size.y != OverallLength ||
                    !NaveRoof.IsWellFormed)
                    return false;

                if (AislesEnabled &&
                    (AisleWidth <= WallThickness * 2 || AisleHeight <= WallThickness * 2 ||
                     AisleHeight >= NaveWalls.Height || !AisleRoof.IsWellFormed ||
                     !AisleArch.IsWellFormed || AisleArch.Kind != StructureOpeningKind.Arch ||
                     AisleArch.Height + AisleArch.BottomOffset >= AisleHeight ||
                     AisleArch.MaxCountForSpan(NaveLength) < 1))
                    return false;

                if (SanctuaryWidth <= WallThickness * 2 || SanctuaryLength <= WallThickness * 2 ||
                    SanctuaryHeight <= WallThickness * 2 || !SanctuaryRoof.IsWellFormed ||
                    !SanctuaryArch.IsWellFormed || SanctuaryArch.Kind != StructureOpeningKind.Arch ||
                    SanctuaryArch.Width >= math.min(NaveWidth, SanctuaryWidth) ||
                    SanctuaryArch.Height + SanctuaryArch.BottomOffset >=
                        math.min(NaveWalls.Height, SanctuaryHeight))
                    return false;

                if (ApseEnabled &&
                    (ApseRadius <= WallThickness || ApseHeight <= WallThickness * 2 ||
                     ApseRoofHeight <= 0))
                    return false;

                if (!MainPortal.IsWellFormed || MainPortal.Kind != StructureOpeningKind.Door ||
                    MainPortal.Width >= NaveWidth ||
                    MainPortal.Height + MainPortal.BottomOffset >= NaveWalls.Height)
                    return false;

                if (SideDoorsEnabled &&
                    (!SideDoor.IsWellFormed || SideDoor.Kind != StructureOpeningKind.Door ||
                     SideDoor.Width >= NaveLength ||
                     SideDoor.Height + SideDoor.BottomOffset >=
                         (AislesEnabled ? AisleHeight : NaveWalls.Height)))
                    return false;

                if (!Window.IsWellFormed || Window.Kind != StructureOpeningKind.Window ||
                    Window.MaxCountForSpan(NaveLength) < 1 ||
                    Window.Height + Window.BottomOffset >=
                        (AislesEnabled ? AisleHeight : NaveWalls.Height))
                    return false;

                if (ClerestoryEnabled &&
                    (!ClerestoryWindow.IsWellFormed ||
                     ClerestoryWindow.Kind != StructureOpeningKind.Window ||
                     ClerestoryWindow.MaxCountForSpan(NaveLength) < 1 ||
                     ClerestoryWindow.Height + ClerestoryWindow.BottomOffset >= NaveWalls.Height ||
                     (AislesEnabled && ClerestoryWindow.BottomOffset <= AisleHeight)))
                    return false;

                if (BellTowerPlacement != ChurchBellTowerPlacement.None)
                {
                    if (!BellTower.IsWellFormed ||
                        BellTower.Shape != StructureTowerShape.Square ||
                        BellTower.Placement != StructureTowerPlacement.Explicit ||
                        BellTower.Count != 1 || BellTower.Width > NaveWidth ||
                        BellTower.Depth > NaveLength / 2 ||
                        (SpireEnabled && SpireHeight <= 0))
                        return false;
                }

                return true;
            }
        }
    }

    public static class ChurchPresets
    {
        public static ChurchConfig Chapel(in StructureMaterialPalette palette)
        {
            return Create(
                48, 80, 42,
                false, 0, 0,
                38, 24, 38,
                true, 20, 34, 16,
                ChurchBellTowerPlacement.None,
                in palette);
        }

        public static ChurchConfig ParishChurch(in StructureMaterialPalette palette)
        {
            return Create(
                60, 120, 56,
                true, 18, 34,
                48, 32, 48,
                true, 24, 42, 22,
                ChurchBellTowerPlacement.FrontLeft,
                in palette);
        }

        private static ChurchConfig Create(
            int naveWidth, int naveLength, int naveHeight,
            bool aisles, int aisleWidth, int aisleHeight,
            int sanctuaryWidth, int sanctuaryLength, int sanctuaryHeight,
            bool apse, int apseRadius, int apseHeight, int apseRoofHeight,
            ChurchBellTowerPlacement towerPlacement,
            in StructureMaterialPalette palette)
        {
            const int wall = 5;
            int overallWidth = math.max(
                aisles ? naveWidth + aisleWidth * 2 : naveWidth,
                math.max(sanctuaryWidth, apse ? apseRadius * 2 : 0));
            int overallLength = naveLength + sanctuaryLength + (apse ? apseRadius : 0);

            OpeningConfig window = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 10,
                Height = 18,
                BottomOffset = 10,
                Spacing = 26,
                StartMargin = 14,
                EndMargin = 14,
                FrameThickness = 2,
                LintelThickness = 2,
                WidthVariation = 0,
                HeightVariation = 0,
                FrameMaterialRole = StructureMaterialRole.Trim,
                FillMaterialRole = StructureMaterialRole.Glass,
            };
            OpeningConfig bellWindow = window;
            bellWindow.Width = 8;
            bellWindow.Height = 14;
            bellWindow.BottomOffset = 38;
            bellWindow.Spacing = 0;
            bellWindow.StartMargin = 0;
            bellWindow.EndMargin = 0;

            return new ChurchConfig
            {
                EntryFacing = Facing.South,
                Footprint = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(
                        new int2(-overallWidth / 2, -overallLength / 2),
                        new int2(overallWidth, overallLength)),
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FoundationStyle = StructureFoundationStyle.Slab,
                    FoundationDepth = 4,
                    FoundationMaterial = StructureMaterialRole.Foundation,
                },
                NaveWalls = new StructureWallRunConfig
                {
                    Length = naveWidth,
                    Height = naveHeight,
                    Thickness = wall,
                    PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                    CornerBehavior = StructureWallCornerBehavior.Overlap,
                },
                NaveLength = naveLength,
                NaveRoof = Roof(RoofStyle.Gable, RoofAxis.Z, 18, 24),
                AislesEnabled = aisles,
                AisleWidth = aisleWidth,
                AisleHeight = aisleHeight,
                AisleRoof = Roof(RoofStyle.Flat, RoofAxis.Z, 0, 0),
                AisleArch = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Arch,
                    Width = 12,
                    Height = math.max(12, aisleHeight - 10),
                    BottomOffset = 0,
                    Spacing = 26,
                    StartMargin = 12,
                    EndMargin = 12,
                    FrameThickness = 1,
                    LintelThickness = 1,
                    WidthVariation = 0,
                    HeightVariation = 0,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                SanctuaryWidth = sanctuaryWidth,
                SanctuaryLength = sanctuaryLength,
                SanctuaryHeight = sanctuaryHeight,
                SanctuaryRoof = Roof(RoofStyle.Gable, RoofAxis.Z, 16, 24),
                SanctuaryArch = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Arch,
                    Width = math.min(26, sanctuaryWidth - wall * 2 - 2),
                    Height = math.min(30, math.min(naveHeight, sanctuaryHeight) - 8),
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
                ApseEnabled = apse,
                ApseRadius = apseRadius,
                ApseHeight = apseHeight,
                ApseRoofHeight = apseRoofHeight,
                MainPortal = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 14,
                    Height = 28,
                    BottomOffset = 0,
                    Spacing = 0,
                    StartMargin = 0,
                    EndMargin = 0,
                    FrameThickness = 3,
                    LintelThickness = 3,
                    WidthVariation = 0,
                    HeightVariation = 0,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                SideDoorsEnabled = aisles,
                SideDoor = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 10,
                    Height = 24,
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
                Window = window,
                ClerestoryEnabled = aisles,
                ClerestoryWindow = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 8,
                    Height = 10,
                    BottomOffset = aisleHeight + 6,
                    Spacing = 24,
                    StartMargin = 12,
                    EndMargin = 12,
                    FrameThickness = 1,
                    LintelThickness = 1,
                    WidthVariation = 0,
                    HeightVariation = 0,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Glass,
                },
                BellTowerPlacement = towerPlacement,
                BellTower = BellTower(in bellWindow),
                SpireEnabled = towerPlacement != ChurchBellTowerPlacement.None,
                SpireHeight = 34,
                Palette = palette,
            };
        }

        private static RoofConfig Roof(RoofStyle style, RoofAxis axis, int rise, int run) =>
            new RoofConfig
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

        private static TowerConfig BellTower(in OpeningConfig opening) => new TowerConfig
        {
            Shape = StructureTowerShape.Square,
            Placement = StructureTowerPlacement.Explicit,
            TopStyle = StructureTowerTopStyle.Roof,
            Width = 22,
            Depth = 22,
            Height = 76,
            TaperPercent = 0,
            Count = 1,
            Spacing = 0,
            Roof = Roof(RoofStyle.Gable, RoofAxis.Z, 16, 22),
            OpeningsEnabled = true,
            Opening = opening,
            WallMaterialRole = StructureMaterialRole.PrimaryWall,
            TrimMaterialRole = StructureMaterialRole.Trim,
        };
    }
}
