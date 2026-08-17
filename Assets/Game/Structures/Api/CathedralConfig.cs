using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Cathedral-specific composition layered over ChurchConfig. Nave, choir/sanctuary, apse,
    /// primary aisles, clerestory, portal, and palette remain church semantics; only cathedral-scale
    /// additions live here.
    /// </summary>
    public struct CathedralConfig
    {
        public ChurchConfig Church;
        public StructureFootprintConfig Footprint;

        public int TranseptWidth;
        public int TranseptDepth;
        public int TranseptHeight;
        public int TranseptCentreFromNaveFront;
        public RoofConfig TranseptRoof;
        public int CrossingClearanceHeight;

        public int ExtraAisleCountPerSide;
        public int ExtraAisleWidth;
        public int ExtraAisleHeight;
        public RoofConfig ExtraAisleRoof;
        public OpeningConfig ExtraAisleArch;
        public OpeningConfig ExtraAisleWindow;

        public bool SideChapelsEnabled;
        public int SideChapelCountPerSide;
        public int SideChapelWidth;
        public int SideChapelDepth;
        public int SideChapelHeight;
        public int SideChapelSpacing;
        public RoofConfig SideChapelRoof;
        public OpeningConfig SideChapelArch;

        public bool WestFrontTowersEnabled;
        public TowerConfig WestFrontTower;
        public int WestTowerCentreOffset;
        public bool WestTowerSpiresEnabled;
        public int WestTowerSpireHeight;

        public bool CrossingTowerEnabled;
        public TowerConfig CrossingTower;
        public bool CrossingSpireEnabled;
        public int CrossingSpireHeight;

        public bool RoseWindowEnabled;
        public OpeningConfig RoseWindow;

        public bool CryptEnabled;
        public int CryptWidth;
        public int CryptDepth;
        public int CryptHeight;
        public int CryptTopOffset;
        public AttachmentAnchorConfig CryptAnchor;
        public AttachmentAnchorConfig CaveAnchor;

        public int BaseAssemblyWidth => Church.NaveWidth +
            (Church.AislesEnabled ? Church.AisleWidth * 2 : 0);

        public int NaveAssemblyWidth => BaseAssemblyWidth +
            ExtraAisleCountPerSide * ExtraAisleWidth * 2;

        public int SideChapelAssemblyWidth => SideChapelsEnabled
            ? Church.SanctuaryWidth + SideChapelDepth * 2
            : Church.SanctuaryWidth;

        public int OverallWidth => math.max(
            math.max(NaveAssemblyWidth, TranseptWidth),
            SideChapelAssemblyWidth);

        public int OverallLength => Church.OverallLength;

        public bool IsWellFormed
        {
            get
            {
                if (!Church.IsWellFormed || !Footprint.IsWellFormed ||
                    Footprint.Primary.Size.x != OverallWidth ||
                    Footprint.Primary.Size.y != OverallLength)
                    return false;

                if (TranseptWidth <= NaveAssemblyWidth ||
                    TranseptDepth <= Church.WallThickness * 2 ||
                    TranseptHeight <= Church.WallThickness * 2 ||
                    TranseptCentreFromNaveFront < TranseptDepth / 2 ||
                    TranseptCentreFromNaveFront + TranseptDepth / 2 > Church.NaveLength ||
                    !TranseptRoof.IsWellFormed ||
                    CrossingClearanceHeight <= 2 ||
                    CrossingClearanceHeight >= math.min(TranseptHeight, Church.NaveWalls.Height))
                    return false;

                if (ExtraAisleCountPerSide < 0 || ExtraAisleCountPerSide > 2)
                    return false;
                if (ExtraAisleCountPerSide > 0 &&
                    (!Church.AislesEnabled || ExtraAisleWidth <= Church.WallThickness * 2 ||
                     ExtraAisleHeight <= Church.WallThickness * 2 ||
                     ExtraAisleHeight >= Church.AisleHeight ||
                     !ExtraAisleRoof.IsWellFormed ||
                     !ExtraAisleArch.IsWellFormed ||
                     ExtraAisleArch.Kind != StructureOpeningKind.Arch ||
                     ExtraAisleArch.MaxCountForSpan(Church.NaveLength) < 1 ||
                     !ExtraAisleWindow.IsWellFormed ||
                     ExtraAisleWindow.Kind != StructureOpeningKind.Window ||
                     ExtraAisleWindow.MaxCountForSpan(Church.NaveLength) < 1))
                    return false;

                if (SideChapelsEnabled)
                {
                    long groupLength = SideChapelWidth +
                        (long)(SideChapelCountPerSide - 1) * SideChapelSpacing;
                    if (SideChapelCountPerSide < 1 || SideChapelCountPerSide > 8 ||
                        SideChapelWidth <= Church.WallThickness * 2 ||
                        SideChapelDepth <= Church.WallThickness * 2 ||
                        SideChapelHeight <= Church.WallThickness * 2 ||
                        SideChapelSpacing < SideChapelWidth ||
                        groupLength > Church.SanctuaryLength - Church.WallThickness * 2 ||
                        !SideChapelRoof.IsWellFormed ||
                        !SideChapelArch.IsWellFormed ||
                        SideChapelArch.Kind != StructureOpeningKind.Arch ||
                        SideChapelArch.Width >= SideChapelWidth ||
                        SideChapelArch.Height + SideChapelArch.BottomOffset >= SideChapelHeight)
                        return false;
                }

                if (WestFrontTowersEnabled)
                {
                    int inner = math.min(WestFrontTower.Width, WestFrontTower.Depth) -
                        Church.WallThickness * 2;
                    if (!ValidTower(in WestFrontTower) ||
                        WestTowerCentreOffset <= WestFrontTower.Width / 2 ||
                        WestTowerCentreOffset + WestFrontTower.Width / 2 > OverallWidth / 2 ||
                        Church.MainPortal.Width >= inner ||
                        (WestTowerSpiresEnabled && WestTowerSpireHeight <= 0))
                        return false;
                }

                if (CrossingTowerEnabled)
                {
                    if (!ValidTower(in CrossingTower) ||
                        CrossingTower.Width > NaveAssemblyWidth ||
                        CrossingTower.Depth > TranseptDepth ||
                        (CrossingSpireEnabled && CrossingSpireHeight <= 0))
                        return false;
                }

                if (RoseWindowEnabled &&
                    (!RoseWindow.IsWellFormed || RoseWindow.Kind != StructureOpeningKind.Window ||
                     RoseWindow.Width >= Church.NaveWidth ||
                     RoseWindow.Height + RoseWindow.BottomOffset >= Church.NaveWalls.Height))
                    return false;

                if (CryptEnabled)
                {
                    if (CryptWidth <= Church.WallThickness * 2 ||
                        CryptDepth <= Church.WallThickness * 2 ||
                        CryptHeight <= 4 || CryptTopOffset < 2 ||
                        CryptWidth > Church.SanctuaryWidth ||
                        CryptDepth > Church.SanctuaryLength + TranseptDepth ||
                        CryptAnchor.Kind != StructureAttachmentKind.Crypt ||
                        CaveAnchor.Kind != StructureAttachmentKind.Cave ||
                        !CryptAnchor.IsWellFormed || !CaveAnchor.IsWellFormed)
                        return false;
                }

                return true;
            }
        }

        public int3 ResolveCryptAnchor(int3 origin) => origin + CryptAnchor.LocalPosition;
        public int3 ResolveCaveAnchor(int3 origin) => origin + CaveAnchor.LocalPosition;

        private static bool ValidTower(in TowerConfig tower) =>
            tower.IsWellFormed &&
            tower.Shape == StructureTowerShape.Square &&
            tower.Placement == StructureTowerPlacement.Explicit &&
            tower.TopStyle == StructureTowerTopStyle.Roof &&
            tower.Count == 1;
    }

    public static class CathedralPresets
    {
        public static CathedralConfig Simple(in StructureMaterialPalette palette)
        {
            ChurchConfig church = CathedralChurch(
                72, 150, 64,
                20, 40,
                58, 48, 56,
                30, 50, 26,
                in palette);

            CathedralConfig config = Base(in church, in palette);
            config.TranseptWidth = 116;
            config.TranseptDepth = 34;
            config.TranseptHeight = 60;
            config.TranseptCentreFromNaveFront = 112;
            config.TranseptRoof = Roof(RoofStyle.Gable, RoofAxis.X, 16, 24);
            config.CrossingClearanceHeight = 42;
            config.ExtraAisleCountPerSide = 0;
            config.SideChapelsEnabled = false;
            config.WestFrontTowersEnabled = true;
            config.WestFrontTower = Tower(30, 30, 82, in church.Window);
            config.WestTowerCentreOffset = 22;
            config.WestTowerSpiresEnabled = false;
            config.CrossingTowerEnabled = false;
            config.RoseWindowEnabled = true;
            config.RoseWindow = Rose(22, 22, 32);
            config.CryptEnabled = false;
            RebuildFootprint(ref config);
            return config;
        }

        public static CathedralConfig Gothic(in StructureMaterialPalette palette)
        {
            ChurchConfig church = CathedralChurch(
                88, 220, 86,
                24, 52,
                70, 70, 72,
                38, 68, 36,
                in palette);
            church.MainPortal = Door(20, 40, 4);
            church.Window = Window(12, 26, 14, 30, 16);
            church.ClerestoryEnabled = true;
            church.ClerestoryWindow = Window(10, 18, 58, 28, 16);

            CathedralConfig config = Base(in church, in palette);
            config.TranseptWidth = 170;
            config.TranseptDepth = 42;
            config.TranseptHeight = 80;
            config.TranseptCentreFromNaveFront = 164;
            config.TranseptRoof = Roof(RoofStyle.Gable, RoofAxis.X, 22, 28);
            config.CrossingClearanceHeight = 58;
            config.ExtraAisleCountPerSide = 1;
            config.ExtraAisleWidth = 18;
            config.ExtraAisleHeight = 38;
            config.ExtraAisleRoof = Roof(RoofStyle.Shed, RoofAxis.Z, 8, 24);
            config.ExtraAisleArch = Arch(12, 26, 28, 16);
            config.ExtraAisleWindow = Window(9, 16, 10, 28, 16);
            config.SideChapelsEnabled = true;
            config.SideChapelCountPerSide = 3;
            config.SideChapelWidth = 18;
            config.SideChapelDepth = 20;
            config.SideChapelHeight = 36;
            config.SideChapelSpacing = 20;
            config.SideChapelRoof = Roof(RoofStyle.Gable, RoofAxis.X, 10, 18);
            config.SideChapelArch = Arch(10, 24, 0, 0);
            config.WestFrontTowersEnabled = true;
            config.WestFrontTower = Tower(36, 36, 112, in church.Window);
            config.WestTowerCentreOffset = 31;
            config.WestTowerSpiresEnabled = true;
            config.WestTowerSpireHeight = 58;
            config.CrossingTowerEnabled = true;
            config.CrossingTower = Tower(44, 40, 66, in church.ClerestoryWindow);
            config.CrossingSpireEnabled = true;
            config.CrossingSpireHeight = 62;
            config.RoseWindowEnabled = true;
            config.RoseWindow = Rose(28, 28, 46);
            config.CryptEnabled = true;
            config.CryptWidth = 56;
            config.CryptDepth = 54;
            config.CryptHeight = 18;
            config.CryptTopOffset = 6;
            int frontZ = church.Footprint.Primary.Min.y;
            int sanctuaryCentreZ = frontZ + church.NaveLength + church.SanctuaryLength / 2;
            config.CryptAnchor = new AttachmentAnchorConfig
            {
                Kind = StructureAttachmentKind.Crypt,
                LocalPosition = new int3(0, -config.CryptTopOffset - 2, sanctuaryCentreZ),
                Facing = Facing.Down,
                SnapToGround = false,
            };
            config.CaveAnchor = new AttachmentAnchorConfig
            {
                Kind = StructureAttachmentKind.Cave,
                LocalPosition = new int3(
                    0,
                    -config.CryptTopOffset - config.CryptHeight / 2,
                    sanctuaryCentreZ + config.CryptDepth / 2 - church.WallThickness - 1),
                Facing = Facing.North,
                SnapToGround = false,
            };
            RebuildFootprint(ref config);
            return config;
        }

        private static CathedralConfig Base(
            in ChurchConfig church,
            in StructureMaterialPalette palette)
        {
            return new CathedralConfig
            {
                Church = church,
                ExtraAisleCountPerSide = 0,
                ExtraAisleWidth = 16,
                ExtraAisleHeight = 32,
                ExtraAisleRoof = Roof(RoofStyle.Shed, RoofAxis.Z, 8, 24),
                ExtraAisleArch = Arch(10, 22, 28, 14),
                ExtraAisleWindow = Window(8, 14, 10, 28, 14),
                SideChapelCountPerSide = 1,
                SideChapelWidth = 18,
                SideChapelDepth = 18,
                SideChapelHeight = 32,
                SideChapelSpacing = 20,
                SideChapelRoof = Roof(RoofStyle.Gable, RoofAxis.X, 10, 18),
                SideChapelArch = Arch(10, 22, 0, 0),
                WestTowerSpireHeight = 48,
                CrossingSpireHeight = 52,
                RoseWindow = Rose(20, 20, 30),
                CryptAnchor = new AttachmentAnchorConfig
                {
                    Kind = StructureAttachmentKind.Crypt,
                    LocalPosition = new int3(0, -8, 0),
                    Facing = Facing.Down,
                    SnapToGround = false,
                },
                CaveAnchor = new AttachmentAnchorConfig
                {
                    Kind = StructureAttachmentKind.Cave,
                    LocalPosition = new int3(0, -14, 0),
                    Facing = Facing.North,
                    SnapToGround = false,
                },
            };
        }

        private static ChurchConfig CathedralChurch(
            int naveWidth,
            int naveLength,
            int naveHeight,
            int aisleWidth,
            int aisleHeight,
            int sanctuaryWidth,
            int sanctuaryLength,
            int sanctuaryHeight,
            int apseRadius,
            int apseHeight,
            int apseRoofHeight,
            in StructureMaterialPalette palette)
        {
            ChurchConfig church = ChurchPresets.ParishChurch(in palette);
            church.NaveWalls.Length = naveWidth;
            church.NaveWalls.Height = naveHeight;
            church.NaveLength = naveLength;
            church.AislesEnabled = true;
            church.AisleWidth = aisleWidth;
            church.AisleHeight = aisleHeight;
            church.SanctuaryWidth = sanctuaryWidth;
            church.SanctuaryLength = sanctuaryLength;
            church.SanctuaryHeight = sanctuaryHeight;
            church.ApseEnabled = true;
            church.ApseRadius = apseRadius;
            church.ApseHeight = apseHeight;
            church.ApseRoofHeight = apseRoofHeight;
            church.BellTowerPlacement = ChurchBellTowerPlacement.None;
            church.SpireEnabled = false;
            church.MainPortal = Door(18, 34, 3);
            church.Window = Window(10, 22, 12, 28, 14);
            church.ClerestoryEnabled = true;
            church.ClerestoryWindow = Window(9, 14, aisleHeight + 8, 28, 14);
            church.AisleArch.Height = math.min(aisleHeight - 8, 30);
            church.SanctuaryArch.Width = math.min(32, sanctuaryWidth - church.WallThickness * 2 - 2);
            church.SanctuaryArch.Height = math.min(40, math.min(naveHeight, sanctuaryHeight) - 10);
            church.Footprint.Primary = new StructureFootprintRect(
                new int2(-church.OverallWidth / 2, -church.OverallLength / 2),
                new int2(church.OverallWidth, church.OverallLength));
            return church;
        }

        private static void RebuildFootprint(ref CathedralConfig config)
        {
            config.Footprint = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(
                    new int2(-config.OverallWidth / 2, config.Church.Footprint.Primary.Min.y),
                    new int2(config.OverallWidth, config.OverallLength)),
                BasePlane = BasePlaneRule.FixedAltitude,
                FoundationStyle = StructureFoundationStyle.Slab,
                FoundationDepth = config.Church.Footprint.FoundationDepth,
                FoundationMaterial = StructureMaterialRole.Foundation,
            };
        }

        private static TowerConfig Tower(
            int width,
            int depth,
            int height,
            in OpeningConfig opening) => new TowerConfig
        {
            Shape = StructureTowerShape.Square,
            Placement = StructureTowerPlacement.Explicit,
            TopStyle = StructureTowerTopStyle.Roof,
            Width = width,
            Depth = depth,
            Height = height,
            TaperPercent = 0,
            Count = 1,
            Spacing = 0,
            Roof = Roof(RoofStyle.Gable, RoofAxis.Z, 18, 24),
            OpeningsEnabled = true,
            Opening = opening,
            WallMaterialRole = StructureMaterialRole.PrimaryWall,
            TrimMaterialRole = StructureMaterialRole.Trim,
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
            WidthVariation = 0,
            HeightVariation = 0,
            FrameMaterialRole = StructureMaterialRole.Trim,
            FillMaterialRole = StructureMaterialRole.Opening,
        };

        private static OpeningConfig Window(int width, int height, int bottom, int spacing, int margin) =>
            new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = width,
                Height = height,
                BottomOffset = bottom,
                Spacing = spacing,
                StartMargin = margin,
                EndMargin = margin,
                FrameThickness = 2,
                LintelThickness = 2,
                WidthVariation = 0,
                HeightVariation = 0,
                FrameMaterialRole = StructureMaterialRole.Trim,
                FillMaterialRole = StructureMaterialRole.Glass,
            };

        private static OpeningConfig Rose(int width, int height, int bottom) =>
            Window(width, height, bottom, 0, 0);

        private static OpeningConfig Arch(int width, int height, int spacing, int margin) =>
            new OpeningConfig
            {
                Kind = StructureOpeningKind.Arch,
                Width = width,
                Height = height,
                BottomOffset = 0,
                Spacing = spacing,
                StartMargin = margin,
                EndMargin = margin,
                FrameThickness = 1,
                LintelThickness = 1,
                WidthVariation = 0,
                HeightVariation = 0,
                FrameMaterialRole = StructureMaterialRole.Trim,
                FillMaterialRole = StructureMaterialRole.Opening,
            };

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
    }
}
