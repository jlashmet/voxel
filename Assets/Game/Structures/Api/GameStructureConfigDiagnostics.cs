using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// First-failure diagnostics for game-owned structure compositions. This intentionally mirrors
    /// top-level IsWellFormed contracts without adding mutable validation state to configs.
    /// </summary>
    public static class GameStructureConfigDiagnostics
    {
        public static StructureDiagnostic Shed(in ShedConfig config)
        {
            if (!config.Footprint.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidFootprint, "Footprint",
                    "Shed footprint/foundation is invalid.");
            if (!config.Walls.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidWalls, "Walls",
                    "Shed wall run is invalid.");
            if (config.Width <= config.WallThickness * 2 || config.Depth <= config.WallThickness * 2)
                return Invalid(StructureDiagnosticCode.InvalidDimensions, "Footprint.Primary.Size",
                    "Shed dimensions do not leave positive interior space.");
            if (config.Footprint.Primary.Size.y != config.Depth || config.Walls.Length != config.Width)
                return Invalid(StructureDiagnosticCode.InvalidDimensions, "Depth/Walls.Length",
                    "Shed depth and wall-run width must match its primary footprint.");
            if (!config.Door.IsWellFormed || config.Door.Kind != StructureOpeningKind.Door)
                return Invalid(StructureDiagnosticCode.InvalidOpening, "Door",
                    "Shed door must be a well-formed door opening.");
            if (config.DoorCount < 1 || config.DoorCount > 4)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "DoorCount",
                    "Shed supports 1..4 grouped doors.");
            if (!Cardinal(config.DoorFacade))
                return Invalid(StructureDiagnosticCode.InvalidFacing, "DoorFacade",
                    "Shed door facade must be cardinal.");
            if (config.Door.BottomOffset < 0 || config.Door.Height + config.Door.BottomOffset >= config.Height)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "Door.BottomOffset/Height",
                    "Door vertical span must remain inside the shed wall height.");
            if (!OpeningsFit(config.DoorFacade, config.Door.Width, config.DoorCount,
                    config.DoorSpacing, config.DoorGroupOffset, config.Width, config.Depth,
                    config.WallThickness))
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "DoorGroupOffset/DoorSpacing",
                    "Grouped shed doors do not fit between wall corners.");

            if (config.WindowsEnabled)
            {
                if (!config.Window.IsWellFormed || config.Window.Kind != StructureOpeningKind.Window)
                    return Invalid(StructureDiagnosticCode.InvalidOpening, "Window",
                        "Enabled shed window must be a well-formed window opening.");
                if (config.WindowCount < 1 || config.WindowCount > 12)
                    return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "WindowCount",
                        "Enabled shed windows require count 1..12.");
                if (!Cardinal(config.WindowFacade))
                    return Invalid(StructureDiagnosticCode.InvalidFacing, "WindowFacade",
                        "Shed window facade must be cardinal.");
                if (config.Window.BottomOffset < 0 ||
                    config.Window.Height + config.Window.BottomOffset >= config.Height)
                    return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "Window.BottomOffset/Height",
                        "Window vertical span must remain inside the shed wall height.");
                if (!OpeningsFit(config.WindowFacade, config.Window.Width, config.WindowCount,
                        config.WindowSpacing, config.WindowGroupOffset, config.Width, config.Depth,
                        config.WallThickness))
                    return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "WindowGroupOffset/WindowSpacing",
                        "Grouped shed windows do not fit between wall corners.");
            }

            if (!config.Roof.IsWellFormed ||
                (config.Roof.Style != RoofStyle.Flat && config.Roof.Style != RoofStyle.Gable &&
                 config.Roof.Style != RoofStyle.Shed))
                return Invalid(StructureDiagnosticCode.InvalidRoof, "Roof",
                    "Shed roof must be a well-formed flat, gable, or shed roof.");
            return StructureDiagnostic.Valid;
        }

        public static StructureDiagnostic Church(in ChurchConfig config)
        {
            if (!StructureCardinalTransform.IsCardinal(config.EntryFacing))
                return Invalid(StructureDiagnosticCode.InvalidFacing, "EntryFacing",
                    "Church entry facing must be cardinal.");
            if (!config.Footprint.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidFootprint, "Footprint",
                    "Church footprint/foundation is invalid.");
            if (!config.NaveWalls.IsWellFormed || config.NaveLength <= config.WallThickness * 2)
                return Invalid(StructureDiagnosticCode.InvalidWalls, "NaveWalls/NaveLength",
                    "Nave wall run or nave length is invalid.");
            if (config.Footprint.Primary.Size.x != config.OverallWidth ||
                config.Footprint.Primary.Size.y != config.OverallLength)
                return Invalid(StructureDiagnosticCode.InvalidDimensions, "Footprint.Primary.Size",
                    "Church footprint must equal the resolved overall assembly width/length.");
            if (!config.NaveRoof.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidRoof, "NaveRoof",
                    "Nave roof is invalid.");

            if (config.AislesEnabled &&
                (config.AisleWidth <= config.WallThickness * 2 ||
                 config.AisleHeight <= config.WallThickness * 2 ||
                 config.AisleHeight >= config.NaveWalls.Height ||
                 !config.AisleRoof.IsWellFormed || !config.AisleArch.IsWellFormed ||
                 config.AisleArch.Kind != StructureOpeningKind.Arch ||
                 config.AisleArch.Height + config.AisleArch.BottomOffset >= config.AisleHeight ||
                 config.AisleArch.MaxCountForSpan(config.NaveLength) < 1))
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Aisles",
                    "Enabled aisles require bounded width/height, a valid roof, and at least one fitting aisle arch.");

            if (config.SanctuaryWidth <= config.WallThickness * 2 ||
                config.SanctuaryLength <= config.WallThickness * 2 ||
                config.SanctuaryHeight <= config.WallThickness * 2 ||
                !config.SanctuaryRoof.IsWellFormed || !config.SanctuaryArch.IsWellFormed ||
                config.SanctuaryArch.Kind != StructureOpeningKind.Arch ||
                config.SanctuaryArch.Width >= Minimum(config.NaveWidth, config.SanctuaryWidth) ||
                config.SanctuaryArch.Height + config.SanctuaryArch.BottomOffset >=
                    Minimum(config.NaveWalls.Height, config.SanctuaryHeight))
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Sanctuary",
                    "Sanctuary dimensions/roof/connecting arch are invalid for the nave opening.");

            if (config.ApseEnabled &&
                (config.ApseRadius <= config.WallThickness ||
                 config.ApseHeight <= config.WallThickness * 2 || config.ApseRoofHeight <= 0))
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Apse",
                    "Enabled apse requires radius beyond wall thickness and positive wall/roof height.");

            if (!config.MainPortal.IsWellFormed || config.MainPortal.Kind != StructureOpeningKind.Door ||
                config.MainPortal.Width >= config.NaveWidth ||
                config.MainPortal.Height + config.MainPortal.BottomOffset >= config.NaveWalls.Height)
                return Invalid(StructureDiagnosticCode.InvalidOpening, "MainPortal",
                    "Main portal must be a door that fits inside the nave facade.");

            if (!config.Window.IsWellFormed || config.Window.Kind != StructureOpeningKind.Window ||
                config.Window.MaxCountForSpan(config.NaveLength) < 1)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "Window",
                    "Church side-window cadence cannot place a valid window along the nave.");

            if (config.ClerestoryEnabled &&
                (!config.ClerestoryWindow.IsWellFormed ||
                 config.ClerestoryWindow.Kind != StructureOpeningKind.Window ||
                 config.ClerestoryWindow.MaxCountForSpan(config.NaveLength) < 1 ||
                 config.ClerestoryWindow.Height + config.ClerestoryWindow.BottomOffset >= config.NaveWalls.Height ||
                 (config.AislesEnabled && config.ClerestoryWindow.BottomOffset <= config.AisleHeight)))
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "ClerestoryWindow",
                    "Clerestory windows must fit above enabled aisles and below the nave roof line.");

            if (config.BellTowerPlacement != ChurchBellTowerPlacement.None && !config.BellTower.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "BellTower",
                    "Enabled bell tower config is invalid.");
            if (config.BellTowerPlacement != ChurchBellTowerPlacement.None &&
                config.SpireEnabled && config.SpireHeight <= 0)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "SpireHeight",
                    "Enabled church spire requires positive height.");

            return StructureDiagnostic.Valid;
        }

        public static StructureDiagnostic Cathedral(in CathedralWorldbuildingConfig config)
        {
            StructureDiagnostic church = Church(in config.Cathedral.Church);
            if (!church.IsValid) return Prefix("Cathedral.Church.", church);

            CathedralConfig c = config.Cathedral;
            if (!c.Footprint.IsWellFormed ||
                c.Footprint.Primary.Size.x != c.OverallWidth ||
                c.Footprint.Primary.Size.y != c.OverallLength)
                return Invalid(StructureDiagnosticCode.InvalidFootprint, "Cathedral.Footprint",
                    "Cathedral footprint must match the resolved church/transept/chapel assembly.");
            if (c.TranseptWidth <= c.NaveAssemblyWidth ||
                c.TranseptDepth <= c.Church.WallThickness * 2 ||
                c.TranseptHeight <= c.Church.WallThickness * 2 ||
                c.TranseptCentreFromNaveFront < c.TranseptDepth / 2 ||
                c.TranseptCentreFromNaveFront + c.TranseptDepth / 2 > c.Church.NaveLength ||
                !c.TranseptRoof.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Cathedral.Transept",
                    "Transept must cross the nave within its length and use valid dimensions/roof.");
            if (c.CrossingClearanceHeight <= 2 ||
                c.CrossingClearanceHeight >= Minimum(c.TranseptHeight, c.Church.NaveWalls.Height))
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Cathedral.CrossingClearanceHeight",
                    "Crossing clearance must remain inside both transept and nave wall heights.");
            if (c.ExtraAisleCountPerSide < 0 || c.ExtraAisleCountPerSide > 2)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Cathedral.ExtraAisleCountPerSide",
                    "Cathedral supports 0..2 extra aisles per side.");
            if (c.ExtraAisleCountPerSide > 0 &&
                (c.ExtraAisleWidth <= c.Church.WallThickness * 2 ||
                 c.ExtraAisleHeight <= c.Church.WallThickness * 2 ||
                 c.ExtraAisleHeight >= c.Church.AisleHeight || !c.ExtraAisleRoof.IsWellFormed ||
                 !c.ExtraAisleArch.IsWellFormed || !c.ExtraAisleWindow.IsWellFormed))
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Cathedral.ExtraAisles",
                    "Enabled extra aisles require dimensions below the inner aisle height plus valid roof/arch/windows.");
            if (c.SideChapelsEnabled &&
                (c.SideChapelCountPerSide < 1 || c.SideChapelCountPerSide > 8 ||
                 c.SideChapelWidth <= c.Church.WallThickness * 2 ||
                 c.SideChapelDepth <= c.Church.WallThickness * 2 ||
                 c.SideChapelHeight <= c.Church.WallThickness * 2 ||
                 c.SideChapelSpacing < c.SideChapelWidth || !c.SideChapelRoof.IsWellFormed ||
                 !c.SideChapelArch.IsWellFormed))
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Cathedral.SideChapels",
                    "Enabled side chapels require count 1..8, fitting dimensions/spacing, roof, and connecting arch.");
            if (c.WestFrontTowersEnabled && !c.WestFrontTower.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Cathedral.WestFrontTower",
                    "Enabled west-front tower config is invalid.");
            if (c.CrossingTowerEnabled && !c.CrossingTower.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Cathedral.CrossingTower",
                    "Enabled crossing tower config is invalid.");
            if (c.RoseWindowEnabled &&
                (!c.RoseWindow.IsWellFormed || c.RoseWindow.Kind != StructureOpeningKind.Window ||
                 c.RoseWindow.Width >= c.Church.NaveWidth ||
                 c.RoseWindow.Height + c.RoseWindow.BottomOffset >= c.Church.NaveWalls.Height))
                return Invalid(StructureDiagnosticCode.InvalidOpening, "Cathedral.RoseWindow",
                    "Enabled rose window must fit inside the west nave facade.");
            if (c.CryptEnabled &&
                (c.CryptWidth <= c.Church.WallThickness * 2 ||
                 c.CryptDepth <= c.Church.WallThickness * 2 || c.CryptHeight <= 4 ||
                 c.CryptTopOffset < 2 || !c.CryptAnchor.IsWellFormed || !c.CaveAnchor.IsWellFormed))
                return Invalid(StructureDiagnosticCode.InvalidAttachment, "Cathedral.Crypt/CaveAnchor",
                    "Enabled crypt requires bounded dimensions/top offset and valid crypt/cave attachments.");
            if (config.ButtressesEnabled && !config.NaveButtresses.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "NaveButtresses",
                    "Enabled cathedral buttress configuration is invalid.");
            if (config.ButtressesEnabled &&
                config.NaveButtresses.MaxCountForSpan(c.Church.NaveLength) <= 0)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "NaveButtresses.Spacing/Margins",
                    "Buttress spacing/margins cannot place a support along the nave.");
            return StructureDiagnostic.Valid;
        }

        public static StructureDiagnostic Temple(in TempleConfig config)
        {
            if (!StructureCardinalTransform.IsCardinal(config.EntryFacing))
                return Invalid(StructureDiagnosticCode.InvalidFacing, "EntryFacing",
                    "Temple entry facing must be cardinal.");
            if (!config.Footprint.IsWellFormed ||
                config.Footprint.Primary.Size.x != config.PlatformWidth ||
                config.Footprint.Primary.Size.y != config.PlatformDepth)
                return Invalid(StructureDiagnosticCode.InvalidFootprint, "Footprint/Platform",
                    "Temple footprint must match positive platform dimensions.");
            if (config.PlatformWidth <= 0 || config.PlatformDepth <= 0 || config.PlatformHeight <= 0)
                return Invalid(StructureDiagnosticCode.InvalidDimensions, "Platform",
                    "Temple platform dimensions must be positive.");
            if (!config.ApproachStairs.IsWellFormed || config.ApproachStairs.Width >= config.PlatformWidth)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "ApproachStairs",
                    "Approach stairs must be well formed and narrower than the platform.");
            if (config.WallThickness <= 0 ||
                config.SanctuaryWidth <= config.WallThickness * 2 ||
                config.SanctuaryDepth <= config.WallThickness * 2 ||
                config.SanctuaryHeight <= config.WallThickness * 2 ||
                config.SanctuaryWidth >= config.PlatformWidth ||
                config.SanctuaryDepth >= config.PlatformDepth)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Sanctuary",
                    "Sanctuary must leave interior space and fit inside the platform.");
            if (!config.SanctuaryDoor.IsWellFormed ||
                config.SanctuaryDoor.Kind != StructureOpeningKind.Door ||
                config.SanctuaryDoor.Width >= config.SanctuaryWidth - config.WallThickness * 2 ||
                config.SanctuaryDoor.Height + config.SanctuaryDoor.BottomOffset >= config.SanctuaryHeight)
                return Invalid(StructureDiagnosticCode.InvalidOpening, "SanctuaryDoor",
                    "Sanctuary door must fit inside the front wall.");
            if (!config.SanctuaryRoof.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidRoof, "SanctuaryRoof",
                    "Sanctuary roof is invalid.");
            if (config.CourtyardEnabled &&
                (config.CourtyardWidth <= config.SanctuaryWidth ||
                 config.CourtyardDepth <= config.SanctuaryDepth ||
                 config.CourtyardWidth >= config.PlatformWidth ||
                 config.CourtyardDepth >= config.PlatformDepth ||
                 config.CourtyardWallHeight <= config.WallThickness * 2 ||
                 !config.CourtyardGate.IsWellFormed))
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Courtyard",
                    "Enabled courtyard must surround the sanctuary, fit on the platform, and have a valid gate.");
            if (config.ColonnadeEnabled &&
                (!config.Columns.IsWellFormed || config.ColumnInset < config.Columns.Width ||
                 config.Columns.MaxCountForSpan(config.PlatformWidth - config.ColumnInset * 2, 0) < 2 ||
                 config.Columns.MaxCountForSpan(config.PlatformDepth - config.ColumnInset * 2, 0) < 2))
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Colonnade",
                    "Enabled colonnade needs valid columns, sufficient inset, and at least two columns per platform axis.");
            return StructureDiagnostic.Valid;
        }

        public static StructureDiagnostic Castle(in CastlePresetConfig config)
        {
            CastleComponentConfig c = config.Components;
            if (!c.BaileyFootprint.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidFootprint, "Components.BaileyFootprint",
                    "Castle bailey footprint is invalid.");
            if (!c.KeepFoundation.IsWellFormed || c.KeepFoundationTopOffset < 0)
                return Invalid(StructureDiagnosticCode.InvalidFoundation, "Components.KeepFoundation",
                    "Keep foundation/top offset is invalid.");
            if (!c.KeepWalls.IsWellFormed || c.KeepDepth <= c.KeepWalls.Thickness * 2)
                return Invalid(StructureDiagnosticCode.InvalidWalls, "Components.KeepWalls/KeepDepth",
                    "Keep walls/depth do not leave positive interior space.");
            if (!c.KeepFloors.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidFloors, "Components.KeepFloors",
                    "Keep floor-level configuration is invalid.");
            if (!c.KeepRoof.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidRoof, "Components.KeepRoof",
                    "Keep roof configuration is invalid.");
            if (!c.KeepParapet.IsWellFormed || !c.CurtainBattlements.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Components.Battlements",
                    "Keep or curtain battlement cadence is invalid.");
            if (!c.KeepEntrance.IsWellFormed || c.KeepEntrance.Kind != StructureOpeningKind.Arch)
                return Invalid(StructureDiagnosticCode.InvalidOpening, "Components.KeepEntrance",
                    "Keep entrance must be a well-formed arch.");
            if (!c.KeepWindow.IsWellFormed || c.KeepWindow.Kind != StructureOpeningKind.Window)
                return Invalid(StructureDiagnosticCode.InvalidOpening, "Components.KeepWindow",
                    "Keep window must be a well-formed window.");
            if (!c.CurtainWallX.IsWellFormed || !c.CurtainWallZ.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidWalls, "Components.CurtainWalls",
                    "Castle curtain wall runs are invalid.");
            if (!c.CornerTowers.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Components.CornerTowers",
                    "Castle corner tower configuration is invalid.");
            if (!c.Gatehouse.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Components.Gatehouse",
                    "Castle gatehouse configuration is invalid.");
            if (!c.Courtyard.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Components.Courtyard",
                    "Castle courtyard composition is invalid.");
            if (!c.Moat.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Components.Moat",
                    "Castle moat configuration is invalid.");
            if (!c.UndergroundAttachments.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidAttachment, "Components.UndergroundAttachments",
                    "Castle dungeon/cave attachment configuration is invalid.");
            if (!config.Curtain.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidComposition, "Curtain",
                    "Castle curtain layout policy is invalid.");
            return StructureDiagnostic.Valid;
        }

        private static StructureDiagnostic Prefix(string prefix, StructureDiagnostic diagnostic) =>
            diagnostic.IsValid
                ? diagnostic
                : new StructureDiagnostic(diagnostic.Code, prefix + diagnostic.Field, diagnostic.Message);

        private static StructureDiagnostic Invalid(
            StructureDiagnosticCode code,
            string field,
            string message) => new StructureDiagnostic(code, field, message);

        private static bool Cardinal(Facing facing) =>
            facing == Facing.North || facing == Facing.East ||
            facing == Facing.South || facing == Facing.West;

        private static int Minimum(int a, int b) => a < b ? a : b;

        private static bool OpeningsFit(
            Facing facade,
            int openingWidth,
            int count,
            int spacing,
            int groupOffset,
            int width,
            int depth,
            int wallThickness)
        {
            if (spacing < 0) return false;
            int span = facade == Facing.North || facade == Facing.South ? width : depth;
            int centreSpacing = count <= 1 ? 0 : spacing;
            if (count > 1 && centreSpacing < openingWidth) return false;
            long groupWidth = openingWidth + (long)(count - 1) * centreSpacing;
            long left = (long)span / 2 + groupOffset - groupWidth / 2;
            long right = left + groupWidth;
            return left >= wallThickness && right <= span - wallThickness;
        }
    }
}
