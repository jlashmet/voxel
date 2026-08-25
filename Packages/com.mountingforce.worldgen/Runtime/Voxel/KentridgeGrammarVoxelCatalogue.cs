using System;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Selects the architectural presentation compiled by the Kentridge grammar. The legacy
    /// baseline is retained only for visual regression and side-by-side comparison; gameplay
    /// should use <see cref="Current"/>.
    /// </summary>
    public enum KentridgeArchitectureVariant : byte
    {
        LegacyBaseline = 0,
        Current = 1,
    }

    /// <summary>
    /// Gameplay-building backend for Kentridge's semantic building grammar.
    ///
    /// Every stable role gets its own deterministic definition. Generated houses and shops compile
    /// from KentridgeBuildingGrammar; deliberately bespoke landmarks compile from
    /// KentridgeBespokeVoxelPrograms. Both paths consume the same registered architecture style and
    /// author foundation/shell/opening/detail/roof roles directly, so the active catalogue never
    /// reconstructs geometry policy from material ids or primitive dimensions.
    /// </summary>
    public static class KentridgeGrammarVoxelCatalogue
    {
        private const int DefinitionCount = 17;
        private const int FoundationSinkDm = 5;
        private const int EntranceFacadeGapDm = 3;
        private const int EntranceFrameThicknessDm = 2;
        private const int InnCanopyHalfWidthDm = 25;
        private const int PubSignExtensionDm = 25;
        private const int MayorPorticoHalfWidthDm = 24;
        private const int HomeCanopyHalfWidthDm = 16;
        private const int FrontageSideMarginDm = 6;

        private sealed class CompiledProgram
        {
            public int[] Code;
            public int3 Door;
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator) =>
            Build(seed, settings, KentridgeArchitectureVariant.Current, allocator);

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            KentridgeArchitectureVariant variant,
            Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            ArchitectureTheme theme = plan.Theme;
            int scale = settings.VoxelsPerDecimetre;

            BuildingPlot[] plots = PlotsByRole(plan);
            var programs = new CompiledProgram[DefinitionCount];
            int programLength = 0;
            for (int roleId = 0; roleId < DefinitionCount; roleId++)
            {
                BuildingPlot plot = plots[roleId];
                KentridgeBuildingForm form = KentridgeBuildingGrammar.Resolve(plot, seed);
                programs[roleId] = form.IsGenerated
                    ? GeneratedHouseProgram(plan, theme, settings, form, variant)
                    : BespokeProgram(theme, settings, form, variant);
                programLength += programs[roleId].Code.Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: DefinitionCount,
                rules: DefinitionCount,
                parameters: 0,
                anchors: DefinitionCount,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: DefinitionCount,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int roleId = 0; roleId < DefinitionCount; roleId++)
            {
                BuildingPlot plot = plots[roleId];
                CompiledProgram program = programs[roleId];
                for (int p = 0; p < program.Code.Length; p++)
                    catalogue.Program[programOffset + p] = program.Code[p];

                Int3 footprintDm = SettlementFootprints.For(plan, plot.Archetype);
                int3 footprint = new int3(
                    footprintDm.X * scale,
                    footprintDm.Y * scale,
                    footprintDm.Z * scale);
                KentridgeRole role = (KentridgeRole)roleId;

                catalogue.Anchors[roleId] = new AnchorSpec
                {
                    Name = plot.Archetype == StructureArchetype.Well
                        ? "interaction"
                        : "door",
                    LocalPosition = program.Door,
                    Facing = Facing.South,
                    SnapToGround = false,
                };

                catalogue.Definitions[roleId] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes(
                        "kentridge-role-" + role.ToString().ToLowerInvariant()),
                    Kind = FeatureKind.Structure,
                    BasePlane = BasePlaneRule.LowestGround,
                    Footprint = footprint,
                    MaxSlope = plot.Archetype == StructureArchetype.Well ? 2 : 3,
                    Precedence = plot.Archetype == StructureArchetype.Mansion ? 130 : 100,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = roleId,
                    AnchorCount = 1,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Code.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = 256,
                };

                int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plan,
                    plot, seed, scale);
                catalogue.ExplicitPlacements[roleId] = new ExplicitPlacement
                {
                    Position = new int3(
                        plot.PositionDm.X * scale,
                        targetSurface - FoundationSinkDm * scale,
                        plot.PositionDm.Y * scale),
                    Orientation = (byte)plot.Frontage,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };

                catalogue.Rules[roleId] = new PlacementRule
                {
                    DefinitionId = roleId,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 3,
                    MinSpacing = 0,
                    ClusterMin = 0,
                    ClusterMax = 0,
                    ExclusionMask = 0,
                    ExplicitOffset = roleId,
                    ExplicitCount = 1,
                };

                programOffset += program.Code.Length;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge grammar catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static BuildingPlot[] PlotsByRole(SettlementPlan plan)
        {
            var plots = new BuildingPlot[DefinitionCount];
            var seen = new bool[DefinitionCount];

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.RoleId < 0 || plot.RoleId >= DefinitionCount)
                    throw new InvalidOperationException(
                        "Kentridge plot has out-of-range role id: " + plot.RoleId);
                if (seen[plot.RoleId])
                    throw new InvalidOperationException(
                        "Kentridge plot role appears twice: " + plot.RoleId);
                plots[plot.RoleId] = plot;
                seen[plot.RoleId] = true;
            }

            for (int roleId = 0; roleId < DefinitionCount; roleId++)
                if (!seen[roleId])
                    throw new InvalidOperationException(
                        "Kentridge is missing stable role id: " + roleId);

            return plots;
        }

        private static CompiledProgram BespokeProgram(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            KentridgeBuildingForm form,
            KentridgeArchitectureVariant variant)
        {
            KentridgeBespokeVoxelPrograms.Program program =
                KentridgeBespokeVoxelPrograms.Build(
                    form.Archetype,
                    theme,
                    settings,
                    ResolveGeometry(form),
                    variant);
            return new CompiledProgram
            {
                Code = program.Code,
                Door = program.Door,
            };
        }

        private static CompiledProgram GeneratedHouseProgram(
            SettlementPlan plan,
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            KentridgeBuildingForm form,
            KentridgeArchitectureVariant variant)
        {
            KentridgeBuildingGrammar.ValidateGenerated(form);

            int s = settings.VoxelsPerDecimetre;
            Int3 envelopeDm = SettlementFootprints.For(plan, form.Archetype);
            int envelopeW = envelopeDm.X * s;
            int envelopeD = envelopeDm.Z * s;
            int w = form.WidthDm * s;
            int d = form.DepthDm * s;
            int x0 = (envelopeW - w) / 2;
            int z0 = 10 * s;
            int f = theme.FoundationHeightDm * s;
            int t = theme.WallThicknessDm * s;
            int floor = theme.FloorHeightDm * s;
            int beam = theme.BeamWidthDm * s;
            int overhang = form.UpperOverhangDm * s;
            int roofOverhang = theme.RoofOverhangDm * s;
            int roofH = form.RoofHeightDm * s;

            byte foundation = settings.Materials.Resolve(theme.Foundation);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = ResolveWindowMaterial(settings, theme, form.WindowStyle);
            byte roof = form.RoleId == (int)KentridgeRole.MagicShop
                     || form.RoleId == (int)KentridgeRole.MayorHouse
                ? settings.Materials.Resolve(MaterialRole.Slate)
                : settings.Materials.Resolve(theme.Roof);
            byte cloth = settings.Materials.Resolve(MaterialRole.Cloth);
            bool ashlarTown = !string.Equals(
                plan.Theme.Id, KentridgeDefinition.Id, StringComparison.Ordinal);

            var b = new ProgramBuilder(ResolveGeometry(form), s);

            b.FoundationBox(x0, 0, z0, w, f, d, foundation);
            EmitShell(b, x0, f, z0, w, floor, d, t, wall);

            int upperX = x0 - overhang;
            int upperZ = z0 - overhang;
            int upperW = w + 2 * overhang;
            int upperD = d + overhang;
            int upperH = Math.Max(0, form.Storeys - 1) * floor;
            if (upperH > 0)
                EmitShell(b, upperX, f + floor, upperZ,
                    upperW, upperH, upperD, t, wall);

            bool hasWing = form.Footprint == KentridgeFootprintForm.RearWing
                        || form.Footprint == KentridgeFootprintForm.SideWing;
            int wingX = 0, wingZ = 0, wingW = 0, wingD = 0;
            if (hasWing)
            {
                ResolveWing(form, envelopeW, envelopeD, x0, z0, w, d, s,
                    out wingX, out wingZ, out wingW, out wingD);
                b.FoundationBox(wingX, 0, wingZ, wingW, f, wingD, foundation);
                EmitShell(b, wingX, f, wingZ, wingW, floor, wingD, t, wall);
                AddStructuralFrame(
                    b, wingX, wingZ, wingW, wingD, f, floor,
                    beam, timber, ashlarTown, s);
            }

            int doorW = (form.IsShop ? 17 : 13) * s;
            int doorH = theme.DoorHeightDm * s;
            int doorX = x0 + w / 2 - doorW / 2 + form.DoorOffsetDm * s;
            doorX = math.clamp(doorX, x0 + 7 * s, x0 + w - doorW - 7 * s);
            b.Carve(doorX, f, z0, doorW, doorH, t + s);

            if (form.IsShop)
                AddShopfront(b, form, x0, z0, w, f, t, floor, doorX, doorW,
                    glass, timber, cloth, s);
            else
                AddFrontageWindows(b, form, x0, z0, w, f, t, floor, 0,
                    glass, s, doorX, doorW, variant);

            for (int storey = 1; storey < form.Storeys; storey++)
            {
                int y = f + storey * floor + theme.WindowBaseDm * s;
                AddFrontageWindowsAtY(
                    b, form.FrontageRhythm,
                    upperX, upperZ, upperW,
                    y, t, theme.WindowHeightDm * s,
                    glass, form.WindowStyle, s,
                    int.MinValue, 0, int.MinValue, int.MinValue);
            }

            AddRearAndSideWindows(
                b, form,
                x0, z0, w, d,
                upperX, upperZ, upperW, upperD,
                f, t, floor,
                theme.WindowBaseDm * s,
                theme.WindowHeightDm * s,
                glass, s);

            AddStructuralFrame(
                b, x0, z0, w, d, f, floor,
                beam, timber, ashlarTown, s);
            if (upperH > 0)
                AddStructuralFrame(
                    b, upperX, upperZ, upperW, upperD,
                    f + floor, upperH, beam, timber, ashlarTown, s);

            // A public entrance owns the whole gameplay approach corridor, not just the wall
            // aperture. Keep this tied to the access resolver contract: if gameplay is asked to reach
            // ExteriorApproach, generation must guarantee body-height air all the way to that point.
            // Use a sharp spatial carve here so rounded opening policy cannot shrink guaranteed body
            // clearance; the visible doorway above already uses the semantic opening treatment.
            int doorExteriorClearance =
                KentridgeGameplaySiteAccessResolver.ApproachDistanceDecimetres * s;
            int doorFacadeDepth = math.max(t + s, 2 * beam);
            b.InteriorCarve(
                doorX,
                f,
                z0 - doorExteriorClearance,
                doorW,
                doorH,
                doorExteriorClearance + doorFacadeDepth);

            // The engine has always supported an integer half-ellipse prism, but the town grammar
            // stopped at rectangular box carves. Put the curved head above the full gameplay
            // clearance so every role gets an architectural entrance without narrowing the
            // CharacterMotor corridor that the access contract guarantees.
            if (variant == KentridgeArchitectureVariant.Current)
            {
                ArchitectureVoxelPatterns.FramedArchedOpening(
                    b.Inner,
                    doorX,
                    f,
                    z0 - 2 * s,
                    doorW,
                    doorH,
                    7 * s,
                    doorFacadeDepth + 2 * s,
                    2 * s,
                    foundation);

                AddRoleSignature(
                    b, (KentridgeRole)form.RoleId,
                    x0, z0, w, f, floor,
                    doorX, doorW,
                    foundation, timber, cloth, roof, s);
            }

            if (hasWing)
            {
                PrismProfile wingProfile = form.Roof == KentridgeRoofForm.GableWithLeanTo
                    ? PrismProfile.Shed
                    : PrismProfile.Gable;
                int wingRoofH = Math.Max(12, form.RoofHeightDm - 7) * s;
                b.Prism(
                    wingX - 2 * s, f + floor, wingZ - 2 * s,
                    wingW + 4 * s, wingRoofH, wingD + 4 * s,
                    wingProfile, roof);
            }

            int roofX = upperH > 0 ? upperX : x0;
            int roofZ = upperH > 0 ? upperZ : z0;
            int roofW = upperH > 0 ? upperW : w;
            int roofD = upperH > 0 ? upperD : d;
            int roofY = f + form.Storeys * floor;
            EmitMainRoof(b, form.Roof,
                roofX - roofOverhang,
                roofY,
                roofZ - roofOverhang,
                roofW + 2 * roofOverhang,
                roofH,
                roofD + 2 * roofOverhang,
                roof, s);

            int chimney = 8 * s;
            int chimneyX = form.ChimneyOnRight
                ? roofX + roofW - 18 * s
                : roofX + 10 * s;
            int chimneyZ = roofZ + roofD - 20 * s;
            b.Box(chimneyX, roofY - 4 * s, chimneyZ,
                chimney, roofH + 15 * s, chimney, foundation);

            int3 door = new int3(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new CompiledProgram { Code = b.Finish(), Door = door };
        }

        private static void AddRoleSignature(
            ProgramBuilder b,
            KentridgeRole role,
            int x0, int z0, int width,
            int foundationY, int floorHeight,
            int doorX, int doorWidth,
            byte stone, byte timber, byte cloth, byte roof,
            int s)
        {
            int frontZ = z0 - 5 * s;
            int centre = doorX + doorWidth / 2;
            switch (role)
            {
                case KentridgeRole.Inn:
                    // A deep, supported arrival canopy reads from both street level and the survey.
                    b.Box(centre - InnCanopyHalfWidthDm * s, foundationY + 27 * s, frontZ - 8 * s,
                        2 * InnCanopyHalfWidthDm * s, 3 * s, 14 * s, timber);
                    b.Box(centre - 23 * s, foundationY, frontZ - 7 * s,
                        4 * s, 29 * s, 4 * s, timber);
                    b.Box(centre + 19 * s, foundationY, frontZ - 7 * s,
                        4 * s, 29 * s, 4 * s, timber);
                    break;
                case KentridgeRole.Pub:
                    // Projecting bracket and sign make the hospitality use legible down the lane.
                    b.Box(doorX + doorWidth + 5 * s, foundationY + 25 * s, frontZ,
                        18 * s, 3 * s, 3 * s, timber);
                    b.Box(doorX + doorWidth + 18 * s, foundationY + 15 * s, frontZ,
                        3 * s, 12 * s, 3 * s, timber);
                    b.Box(doorX + doorWidth + 11 * s, foundationY + 13 * s, frontZ - 2 * s,
                        14 * s, 11 * s, 2 * s, cloth);
                    break;
                case KentridgeRole.MayorHouse:
                    // Formal stone portico and balcony separate the civic residence from houses.
                    b.Box(centre - MayorPorticoHalfWidthDm * s, foundationY + 29 * s, frontZ - 4 * s,
                        2 * MayorPorticoHalfWidthDm * s, 4 * s, 12 * s, stone);
                    b.Box(centre - 21 * s, foundationY, frontZ,
                        5 * s, 31 * s, 5 * s, stone);
                    b.Box(centre + 16 * s, foundationY, frontZ,
                        5 * s, 31 * s, 5 * s, stone);
                    break;
                case KentridgeRole.WeaponShop:
                    AddShopPiers(b, centre, foundationY, frontZ, stone, 5 * s, 22 * s, s);
                    b.Box(centre - 19 * s, foundationY + 22 * s, frontZ,
                        38 * s, 3 * s, 5 * s, timber);
                    break;
                case KentridgeRole.ArmorShop:
                    AddShopPiers(b, centre, foundationY, frontZ, stone, 7 * s, 28 * s, s);
                    b.Box(centre - 25 * s, foundationY + 27 * s, frontZ,
                        50 * s, 5 * s, 6 * s, stone);
                    break;
                case KentridgeRole.MagicShop:
                    // Curved hood repeats the portal arch at a larger silhouette scale.
                    b.Prism(centre - 22 * s, foundationY + 27 * s, frontZ - 6 * s,
                        44 * s, 12 * s, 12 * s, PrismProfile.Arch, roof);
                    b.Box(centre - 2 * s, foundationY + floorHeight - 2 * s, frontZ,
                        4 * s, 18 * s, 4 * s, timber);
                    break;
                case KentridgeRole.AbandonedHouse:
                    // Deliberately incomplete brace lengths retain a readable damaged silhouette.
                    b.Box(x0 + 8 * s, foundationY + 7 * s, frontZ,
                        24 * s, 3 * s, 4 * s, timber);
                    b.Box(x0 + width - 27 * s, foundationY + 18 * s, frontZ,
                        19 * s, 3 * s, 4 * s, timber);
                    break;
                default:
                    // Named homes get deterministic planter/porch rhythms instead of sharing an
                    // unmodified grammar façade. Role identity chooses the offset and width.
                    int roleVariant = ((int)role * 7) % 13;
                    int planterWidth = (16 + roleVariant) * s;
                    int planterX = x0 + (8 + roleVariant) * s;
                    planterX = math.min(planterX, x0 + width - planterWidth - 8 * s);
                    b.Box(planterX, foundationY + 16 * s, frontZ,
                        planterWidth, 3 * s, 5 * s, timber);
                    b.Box(centre - HomeCanopyHalfWidthDm * s, foundationY + 25 * s, frontZ,
                        2 * HomeCanopyHalfWidthDm * s, 3 * s, 8 * s, roof);
                    break;
            }
        }

        private static void AddShopPiers(
            ProgramBuilder b, int centre, int y, int z,
            byte material, int width, int height, int s)
        {
            b.Box(centre - 24 * s, y, z, width, height, 5 * s, material);
            b.Box(centre + (24 * s - width), y, z, width, height, 5 * s, material);
        }

        private static StructureGeometryProfile ResolveGeometry(KentridgeBuildingForm form)
        {
            IArchitectureStyleCompiler style =
                BuiltInArchitectureStyles.Registry.Require(form.Intent.StyleId);
            return style.ResolveGeometry(form.Intent, form.Inner);
        }

        private static void EmitShell(
            ProgramBuilder b,
            int x, int y, int z,
            int w, int h, int d,
            int thickness,
            byte material)
        {
            b.ShellBox(x, y, z, w, h, d, material);
            b.InteriorCarve(x + thickness, y, z + thickness,
                w - 2 * thickness, h, d - 2 * thickness);
        }

        private static void ResolveWing(
            KentridgeBuildingForm form,
            int envelopeW,
            int envelopeD,
            int x0,
            int z0,
            int w,
            int d,
            int s,
            out int wingX,
            out int wingZ,
            out int wingW,
            out int wingD)
        {
            wingW = form.WingWidthDm * s;
            wingD = form.WingDepthDm * s;
            const int edgeMarginDm = 4;
            int edge = edgeMarginDm * s;
            int overlap = 6 * s;

            if (form.Footprint == KentridgeFootprintForm.RearWing)
            {
                wingX = form.WingOnRight
                    ? x0 + w - wingW - 8 * s
                    : x0 + 8 * s;
                wingX = math.clamp(wingX, edge, envelopeW - wingW - edge);
                wingZ = math.min(envelopeD - wingD - edge, z0 + d - overlap);
                wingZ = math.max(edge, wingZ);
                return;
            }

            wingZ = z0 + d / 2 - wingD / 2;
            wingZ = math.clamp(wingZ, edge, envelopeD - wingD - edge);
            wingX = form.WingOnRight
                ? x0 + w - overlap
                : x0 - wingW + overlap;
            wingX = math.clamp(wingX, edge, envelopeW - wingW - edge);
        }

        private static void AddShopfront(
            ProgramBuilder b,
            KentridgeBuildingForm form,
            int x0, int z0, int w,
            int foundationY, int thickness, int floor,
            int doorX, int doorW,
            byte glass, byte timber, byte cloth,
            int s)
        {
            int windowY = foundationY + 8 * s;
            int windowH = 17 * s;
            int leftX = x0 + 8 * s;
            int leftW = math.max(10 * s, doorX - leftX - 5 * s);
            int rightX = doorX + doorW + 5 * s;
            int rightW = math.max(10 * s, x0 + w - 8 * s - rightX);

            AddWindowZ(b, leftX, windowY, z0, leftW, windowH,
                thickness + s, glass, form.WindowStyle);
            AddWindowZ(b, rightX, windowY, z0, rightW, windowH,
                thickness + s, glass, form.WindowStyle);

            int awningY = foundationY + 28 * s;
            int awningInset = form.RoleId == (int)KentridgeRole.MagicShop ? 12 : 6;
            b.Box(x0 + awningInset * s, awningY, z0 - 12 * s,
                w - 2 * awningInset * s, 3 * s, 14 * s, cloth);
            b.Box(x0 + 6 * s, foundationY + 1 * s, z0 - 2 * s,
                w - 12 * s, 3 * s, 7 * s, timber);
        }

        private static void AddFrontageWindows(
            ProgramBuilder b,
            KentridgeBuildingForm form,
            int x0, int z0, int w,
            int foundationY, int thickness, int floor,
            int storey,
            byte glass,
            int s,
            int doorX,
            int doorW,
            KentridgeArchitectureVariant variant)
        {
            int y = foundationY + storey * floor + 20 * s;
            ResolveEntranceWindowReservation(
                (KentridgeRole)form.RoleId,
                variant,
                doorX,
                doorW,
                s,
                out int reservedMinX,
                out int reservedMaxX);
            AddFrontageWindowsAtY(
                b, form.FrontageRhythm,
                x0, z0, w,
                y, thickness, 12 * s,
                glass, form.WindowStyle, s,
                doorX, doorW, reservedMinX, reservedMaxX);
        }

        private static void ResolveEntranceWindowReservation(
            KentridgeRole role,
            KentridgeArchitectureVariant variant,
            int doorX,
            int doorW,
            int s,
            out int reservedMinX,
            out int reservedMaxX)
        {
            if (variant != KentridgeArchitectureVariant.Current)
            {
                reservedMinX = doorX - EntranceFacadeGapDm * s;
                reservedMaxX = doorX + doorW + EntranceFacadeGapDm * s;
                return;
            }

            int treatmentMinX = doorX - EntranceFrameThicknessDm * s;
            int treatmentMaxX = doorX + doorW + EntranceFrameThicknessDm * s;
            int centre = doorX + doorW / 2;

            switch (role)
            {
                case KentridgeRole.Inn:
                    treatmentMinX = math.min(
                        treatmentMinX, centre - InnCanopyHalfWidthDm * s);
                    treatmentMaxX = math.max(
                        treatmentMaxX, centre + InnCanopyHalfWidthDm * s);
                    break;
                case KentridgeRole.Pub:
                    treatmentMaxX = math.max(
                        treatmentMaxX, doorX + doorW + PubSignExtensionDm * s);
                    break;
                case KentridgeRole.MayorHouse:
                    treatmentMinX = math.min(
                        treatmentMinX, centre - MayorPorticoHalfWidthDm * s);
                    treatmentMaxX = math.max(
                        treatmentMaxX, centre + MayorPorticoHalfWidthDm * s);
                    break;
                case KentridgeRole.AbandonedHouse:
                    // Its damaged braces are façade texture rather than part of the entrance.
                    break;
                default:
                    treatmentMinX = math.min(
                        treatmentMinX, centre - HomeCanopyHalfWidthDm * s);
                    treatmentMaxX = math.max(
                        treatmentMaxX, centre + HomeCanopyHalfWidthDm * s);
                    break;
            }

            reservedMinX = treatmentMinX - EntranceFacadeGapDm * s;
            reservedMaxX = treatmentMaxX + EntranceFacadeGapDm * s;
        }

        private static void AddFrontageWindowsAtY(
            ProgramBuilder b,
            KentridgeFrontageRhythm rhythm,
            int x0, int z0, int w,
            int y, int thickness, int windowH,
            byte glass,
            KentridgeWindowStyle style,
            int s,
            int doorX,
            int doorW,
            int reservedMinX,
            int reservedMaxX)
        {
            int windowW = 11 * s;
            int[] centres;
            switch (rhythm)
            {
                case KentridgeFrontageRhythm.ThreeBay:
                    centres = new[]
                    {
                        x0 + w / 5,
                        x0 + w / 2,
                        x0 + 4 * w / 5,
                    };
                    break;
                case KentridgeFrontageRhythm.Asymmetric:
                    centres = new[]
                    {
                        x0 + w / 4,
                        x0 + 2 * w / 3,
                    };
                    break;
                default:
                    centres = new[]
                    {
                        x0 + w / 4,
                        x0 + 3 * w / 4,
                    };
                    break;
            }

            for (int i = 0; i < centres.Length; i++)
            {
                int wx = centres[i] - windowW / 2;
                if (doorX != int.MinValue
                    && wx < reservedMaxX
                    && wx + windowW > reservedMinX)
                {
                    if (centres[i] <= doorX)
                        wx = reservedMinX - windowW;
                    else if (centres[i] >= doorX + doorW)
                        wx = reservedMaxX;
                    else
                        continue;
                }

                if (doorX != int.MinValue)
                {
                    int minimumWindowX = x0 + FrontageSideMarginDm * s;
                    int maximumWindowX = x0 + w - FrontageSideMarginDm * s - windowW;
                    if (wx < minimumWindowX || wx > maximumWindowX)
                        continue;
                }

                AddWindowZ(b, wx, y, z0, windowW, windowH,
                    thickness + s, glass, style);
            }
        }

        private static void AddRearAndSideWindows(
            ProgramBuilder b,
            KentridgeBuildingForm form,
            int x0, int z0, int w, int d,
            int upperX, int upperZ, int upperW, int upperD,
            int foundationY, int thickness, int floor,
            int windowBase, int windowH,
            byte glass, int s)
        {
            for (int storey = 0; storey < form.Storeys; storey++)
            {
                bool upper = storey > 0;
                int bx = upper ? upperX : x0;
                int bz = upper ? upperZ : z0;
                int bw = upper ? upperW : w;
                int bd = upper ? upperD : d;
                int y = foundationY + storey * floor + windowBase;
                int rearZ = bz + bd - (thickness + s);
                int sideRightX = bx + bw - (thickness + s);
                int windowW = 11 * s;

                AddWindowZ(b, bx + bw / 4 - windowW / 2, y, rearZ,
                    windowW, windowH, thickness + s, glass, form.WindowStyle);
                AddWindowZ(b, bx + 3 * bw / 4 - windowW / 2, y, rearZ,
                    windowW, windowH, thickness + s, glass, form.WindowStyle);

                int sideZ = bz + bd / 2 - windowW / 2;
                AddWindowX(b, bx, y, sideZ,
                    thickness + s, windowH, windowW, glass, form.WindowStyle);
                AddWindowX(b, sideRightX, y, sideZ,
                    thickness + s, windowH, windowW, glass, form.WindowStyle);
            }
        }

        private static void EmitMainRoof(
            ProgramBuilder b,
            KentridgeRoofForm form,
            int x, int y, int z,
            int w, int h, int d,
            byte material,
            int s)
        {
            if (form != KentridgeRoofForm.TwinGable)
            {
                b.Prism(x, y, z, w, h, d, PrismProfile.Gable, material);
                return;
            }

            int overlap = 3 * s;
            int half = w / 2 + overlap;
            b.Prism(x, y, z, half, h, d, PrismProfile.Gable, material);
            b.Prism(x + w / 2 - overlap, y, z,
                half, h, d, PrismProfile.Gable, material);
        }

        private static byte ResolveWindowMaterial(
            VoxelWorldGenSettings settings,
            ArchitectureTheme theme,
            KentridgeWindowStyle style)
        {
            return style == KentridgeWindowStyle.Warm
                ? settings.Materials.Resolve(MaterialRole.WarmWindow)
                : settings.Materials.Resolve(theme.Window);
        }

        private static void AddWindowZ(
            ProgramBuilder b,
            int x, int y, int z,
            int width, int height, int depth,
            byte material,
            KentridgeWindowStyle style)
        {
            if (width <= 0 || height <= 0 || depth <= 0) return;
            // Glazing must stay planar. Going through Box() would fill the pane with the
            // structure's detail profile, so a rounded architectural style would round the glass
            // itself; GlazedOpening is the shared pattern that carves the aperture semantically
            // and keeps the pane flat, and is what the bespoke landmark programs already use.
            ArchitectureVoxelPatterns.GlazedOpening(
                b.Inner, x, y, z, width, height, depth, material,
                fillPane: style != KentridgeWindowStyle.Open);
        }

        private static void AddWindowX(
            ProgramBuilder b,
            int x, int y, int z,
            int depth, int height, int width,
            byte material,
            KentridgeWindowStyle style)
        {
            if (width <= 0 || height <= 0 || depth <= 0) return;
            ArchitectureVoxelPatterns.GlazedOpening(
                b.Inner, x, y, z, depth, height, width, material,
                fillPane: style != KentridgeWindowStyle.Open);
        }

        private static void AddTimberFrame(
            ProgramBuilder b,
            int x0, int z0, int width, int depth,
            int baseY, int wallHeight, int beam,
            byte timber)
        {
            b.Box(x0, baseY, z0, beam, wallHeight, 2 * beam, timber);
            b.Box(x0 + width - beam, baseY, z0,
                beam, wallHeight, 2 * beam, timber);
            b.Box(x0, baseY, z0 + depth - 2 * beam,
                beam, wallHeight, 2 * beam, timber);
            b.Box(x0 + width - beam, baseY, z0 + depth - 2 * beam,
                beam, wallHeight, 2 * beam, timber);

            int[] levels =
            {
                baseY,
                baseY + wallHeight / 2,
                baseY + wallHeight - beam,
            };
            for (int i = 0; i < levels.Length; i++)
            {
                int y = levels[i];
                b.Box(x0, y, z0, width, beam, 2 * beam, timber);
                b.Box(x0, y, z0 + depth - 2 * beam,
                    width, beam, 2 * beam, timber);
                b.Box(x0, y, z0, 2 * beam, beam, depth, timber);
                b.Box(x0 + width - 2 * beam, y, z0,
                    2 * beam, beam, depth, timber);
            }
        }

        private static void AddStructuralFrame(
            ProgramBuilder b,
            int x0, int z0, int width, int depth,
            int baseY, int wallHeight, int beam,
            byte material,
            bool ashlarTown,
            int s)
        {
            if (!ashlarTown)
            {
                AddTimberFrame(
                    b, x0, z0, width, depth,
                    baseY, wallHeight, beam, material);
                return;
            }

            // Hightown is a planned stone town, not Kentridge's timber frame recoloured charcoal.
            // Broad corner piers and projecting string courses give it a vertical ashlar rhythm,
            // while leaving the wall planes and windows readable between them.
            int pier = beam + 2 * s;
            int projection = 2 * s;
            b.Box(x0 - projection, baseY, z0 - projection,
                pier, wallHeight, pier, material);
            b.Box(x0 + width - pier + projection, baseY, z0 - projection,
                pier, wallHeight, pier, material);
            b.Box(x0 - projection, baseY, z0 + depth - pier + projection,
                pier, wallHeight, pier, material);
            b.Box(x0 + width - pier + projection, baseY, z0 + depth - pier + projection,
                pier, wallHeight, pier, material);

            int course = 3 * s;
            int[] levels = { baseY, baseY + wallHeight - course };
            for (int i = 0; i < levels.Length; i++)
            {
                int y = levels[i];
                b.Box(x0 - projection, y, z0 - projection,
                    width + 2 * projection, course, beam + projection, material);
                b.Box(x0 - projection, y, z0 + depth - beam,
                    width + 2 * projection, course, beam + projection, material);
                b.Box(x0 - projection, y, z0,
                    beam + projection, course, depth, material);
                b.Box(x0 + width - beam, y, z0,
                    beam + projection, course, depth, material);
            }
        }

        /// <summary>
        /// Kentridge-specific vocabulary over the generic architecture bytecode builder. This keeps
        /// the house grammar readable while ensuring semantic roles are authored at the source instead
        /// of reconstructed later from material ids and dimensions.
        /// </summary>
        private sealed class ProgramBuilder
        {
            private readonly ArchitectureShapeProgramBuilder _inner;

            public ProgramBuilder(StructureGeometryProfile profile, int voxelsPerDecimetre)
            {
                _inner = new ArchitectureShapeProgramBuilder(profile, voxelsPerDecimetre);
            }

            /// <summary>
            /// Raw builder, for shared construction patterns in
            /// <see cref="ArchitectureVoxelPatterns"/> that need to override the semantic profile
            /// defaults this wrapper applies — glazing being the case that must stay planar.
            /// </summary>
            public ArchitectureShapeProgramBuilder Inner => _inner;

            public void FoundationBox(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material) =>
                _inner.FoundationBox(x, y, z, sx, sy, sz, material);

            public void ShellBox(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material) =>
                _inner.ShellBox(x, y, z, sx, sy, sz, material);

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material,
                PrimitiveMode mode = PrimitiveMode.Fill) =>
                _inner.DetailBox(x, y, z, sx, sy, sz, material, mode);

            public void Carve(
                int x, int y, int z,
                int sx, int sy, int sz) =>
                _inner.OpeningCarve(x, y, z, sx, sy, sz);

            public void InteriorCarve(
                int x, int y, int z,
                int sx, int sy, int sz) =>
                _inner.InteriorCarve(x, y, z, sx, sy, sz);

            public void Prism(
                int x, int y, int z,
                int sx, int sy, int sz,
                PrismProfile profile,
                byte material) =>
                _inner.Prism(x, y, z, sx, sy, sz, profile, material);

            public void Anchor(int index, int3 p, Facing facing) =>
                _inner.Anchor(index, p, facing);

            public int[] Finish() => _inner.Finish();
        }
    }
}