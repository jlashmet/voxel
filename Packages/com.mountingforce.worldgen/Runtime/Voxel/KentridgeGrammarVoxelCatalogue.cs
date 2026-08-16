using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Gameplay-building backend for Kentridge's semantic building grammar.
    ///
    /// Every stable role gets its own deterministic definition. Ordinary houses and shops compile
    /// from KentridgeBuildingGrammar; the already-distinct church/inn/warehouse/mansion/well programs
    /// are copied from the legacy catalogue as a temporary source library. This keeps exactly seventeen
    /// gameplay Structure instances while removing the old "one geometry per archetype" restriction.
    /// </summary>
    public static class KentridgeGrammarVoxelCatalogue
    {
        private const int DefinitionCount = 17;
        private const int FoundationSinkDm = 5;

        private sealed class CompiledProgram
        {
            public int[] Code;
            public int3 Door;
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            ArchitectureTheme theme = plan.Theme;
            int scale = settings.VoxelsPerDecimetre;

            BuildingPlot[] plots = PlotsByRole(plan);
            var programs = new CompiledProgram[DefinitionCount];
            FeatureCatalogue legacy = KentridgeVoxelCatalogue.Build(
                seed, settings, Allocator.Temp);

            try
            {
                int programLength = 0;
                for (int roleId = 0; roleId < DefinitionCount; roleId++)
                {
                    BuildingPlot plot = plots[roleId];
                    KentridgeBuildingForm form = KentridgeBuildingGrammar.Resolve(plot, seed);
                    programs[roleId] = form.IsGenerated
                        ? GeneratedHouseProgram(theme, settings, form)
                        : CopyLegacyProgram(in legacy, plot.Archetype);
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

                    Int3 footprintDm = KentridgeDefinition.FootprintDm(plot.Archetype);
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

                    int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(
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
            finally
            {
                legacy.Dispose();
            }
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

        private static CompiledProgram CopyLegacyProgram(
            in FeatureCatalogue legacy,
            StructureArchetype archetype)
        {
            FeatureDefinition source = legacy.Definitions[(int)archetype];
            var code = new int[source.ProgramLength];
            for (int i = 0; i < code.Length; i++)
                code[i] = legacy.Program[source.ProgramOffset + i];

            AnchorSpec anchor = legacy.Anchors[source.AnchorOffset];
            return new CompiledProgram
            {
                Code = code,
                Door = anchor.LocalPosition,
            };
        }

        private static CompiledProgram GeneratedHouseProgram(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            KentridgeBuildingForm form)
        {
            KentridgeBuildingGrammar.ValidateGenerated(form);

            int s = settings.VoxelsPerDecimetre;
            Int3 envelopeDm = KentridgeDefinition.FootprintDm(form.Archetype);
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

            var b = new ProgramBuilder();

            b.Box(x0, 0, z0, w, f, d, foundation);
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
                b.Box(wingX, 0, wingZ, wingW, f, wingD, foundation);
                EmitShell(b, wingX, f, wingZ, wingW, floor, wingD, t, wall);
                AddTimberFrame(b, wingX, wingZ, wingW, wingD, f, floor, beam, timber);
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
                    glass, s, doorX, doorW);

            for (int storey = 1; storey < form.Storeys; storey++)
            {
                int y = f + storey * floor + theme.WindowBaseDm * s;
                AddFrontageWindowsAtY(
                    b, form.FrontageRhythm,
                    upperX, upperZ, upperW,
                    y, t, theme.WindowHeightDm * s,
                    glass, form.WindowStyle, s,
                    int.MinValue, 0);
            }

            AddRearAndSideWindows(
                b, form,
                x0, z0, w, d,
                upperX, upperZ, upperW, upperD,
                f, t, floor,
                theme.WindowBaseDm * s,
                theme.WindowHeightDm * s,
                glass, s);

            AddTimberFrame(b, x0, z0, w, d, f, floor, beam, timber);
            if (upperH > 0)
                AddTimberFrame(b, upperX, upperZ, upperW, upperD,
                    f + floor, upperH, beam, timber);

            // A public entrance owns the whole gameplay approach corridor, not just the wall
            // aperture. Keep this tied to the access resolver contract: if gameplay is asked to reach
            // ExteriorApproach, generation must guarantee body-height air all the way to that point.
            // The carve begins at threshold height so walkable ground below remains intact.
            int doorExteriorClearance =
                KentridgeGameplaySiteAccessResolver.ApproachDistanceDecimetres * s;
            int doorFacadeDepth = math.max(t + s, 2 * beam);
            b.Carve(
                doorX,
                f,
                z0 - doorExteriorClearance,
                doorW,
                doorH,
                doorExteriorClearance + doorFacadeDepth);

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

        private static void EmitShell(
            ProgramBuilder b,
            int x, int y, int z,
            int w, int h, int d,
            int thickness,
            byte material)
        {
            b.Box(x, y, z, w, h, d, material);
            b.Carve(x + thickness, y, z + thickness,
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
            int doorW)
        {
            int y = foundationY + storey * floor + 20 * s;
            AddFrontageWindowsAtY(
                b, form.FrontageRhythm,
                x0, z0, w,
                y, thickness, 12 * s,
                glass, form.WindowStyle, s,
                doorX, doorW);
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
            int doorW)
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
                    && wx < doorX + doorW + 3 * s
                    && wx + windowW > doorX - 3 * s)
                    continue;
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
            b.Carve(x, y, z, width, height, depth);
            if (style != KentridgeWindowStyle.Open)
                b.Box(x, y, z, width, height, depth, material);
        }

        private static void AddWindowX(
            ProgramBuilder b,
            int x, int y, int z,
            int depth, int height, int width,
            byte material,
            KentridgeWindowStyle style)
        {
            if (width <= 0 || height <= 0 || depth <= 0) return;
            b.Carve(x, y, z, depth, height, width);
            if (style != KentridgeWindowStyle.Open)
                b.Box(x, y, z, depth, height, width, material);
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

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material,
                PrimitiveMode mode = PrimitiveMode.Fill)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                    material, 0, 0, (int)mode);
            }

            public void Carve(
                int x, int y, int z,
                int sx, int sy, int sz)
            {
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);
            }

            public void Prism(
                int x, int y, int z,
                int sx, int sy, int sz,
                PrismProfile profile,
                byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitPrism, x, y, z, sx, sy, sz,
                    (int)profile, material, 0, 0, (int)PrimitiveMode.Fill);
            }

            public void Anchor(int index, int3 p, Facing facing)
            {
                Op(ShapeOp.SetAnchor, index, p.x, p.y, p.z, (int)facing);
            }

            public int[] Finish()
            {
                Op(ShapeOp.End);
                return _code.ToArray();
            }

            private void Op(ShapeOp op, params int[] operands)
            {
                _code.Add((int)op);
                _code.Add(0);
                _code.AddRange(operands);
            }
        }
    }
}
