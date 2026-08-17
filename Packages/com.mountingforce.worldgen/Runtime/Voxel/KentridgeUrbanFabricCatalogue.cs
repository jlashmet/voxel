using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Realises KentridgeUrbanMassingPlan as varied anonymous buildings instead of silhouette boxes.
    /// Every site gets a deterministic architectural form and low-level geometry profile, while
    /// remaining Infrastructure so named gameplay roles and the seventeen-Structure invariant are
    /// untouched. Geometry roles are authored directly through ArchitectureShapeProgramBuilder;
    /// renderer-specific surface ids never enter settlement or architecture planning.
    /// </summary>
    public static class KentridgeUrbanFabricCatalogue
    {
        // Density policy remains a Kentridge choice. Segment splitting, site counts and stable centre
        // placement are city-independent and live in SettlementPlotLayout.PackFrontage.
        private const int ModulePitchDm = 80;

        private readonly struct FabricSite
        {
            public readonly KentridgeFrontageRun Run;
            public readonly KentridgeUrbanFabricForm Form;
            public readonly StructureGeometryProfile Geometry;
            public readonly Int2 PositionDm;

            public FabricSite(
                KentridgeFrontageRun run,
                KentridgeUrbanFabricForm form,
                StructureGeometryProfile geometry,
                Int2 positionDm)
            {
                Run = run;
                Form = form;
                Geometry = geometry;
                PositionDm = positionDm;
            }
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(seed);
            var sites = new List<FabricSite>(48);

            for (int runIndex = 0; runIndex < plan.FrontageRuns.Count; runIndex++)
                ExpandRun(plan.FrontageRuns[runIndex], seed, runIndex, sites);

            var programs = new int[sites.Count][];
            int programLength = 0;
            for (int i = 0; i < sites.Count; i++)
            {
                programs[i] = FabricProgram(sites[i], settings);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: sites.Count,
                rules: sites.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: sites.Count,
                overrides: 0,
                allocator);

            int scale = settings.VoxelsPerDecimetre;
            int programOffset = 0;
            for (int i = 0; i < sites.Count; i++)
            {
                FabricSite site = sites[i];
                int[] program = programs[i];
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-fabric-" + i),
                    Kind = FeatureKind.Infrastructure,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(
                        KentridgeUrbanFabricGrammar.EnvelopeDm * scale,
                        HeightDm(site.Form) * scale,
                        KentridgeUrbanFabricGrammar.EnvelopeDm * scale),
                    MaxSlope = 32,
                    Precedence = 86,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = 0,
                    AnchorCount = 0,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = 160,
                };

                int shelfSurface = KentridgeVerticalProfile.SurfaceYAtDm(
                    site.Run.ElevationSampleDm.X,
                    site.Run.ElevationSampleDm.Y,
                    seed,
                    scale);
                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = new int3(
                        site.PositionDm.X * scale,
                        shelfSurface - site.Run.EmbedBelowShelfDm * scale,
                        site.PositionDm.Y * scale),
                    Orientation = (byte)site.Run.Frontage,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };

                catalogue.Rules[i] = new PlacementRule
                {
                    DefinitionId = i,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 32,
                    MinSpacing = 0,
                    ClusterMin = 0,
                    ClusterMax = 0,
                    ExclusionMask = 0,
                    ExplicitOffset = i,
                    ExplicitCount = 1,
                };

                programOffset += program.Length;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge urban fabric catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static void ExpandRun(
            KentridgeFrontageRun run,
            uint seed,
            int runIndex,
            List<FabricSite> sites)
        {
            int start = run.IsHorizontal
                ? Math.Min(run.StartDm.X, run.EndDm.X)
                : Math.Min(run.StartDm.Y, run.EndDm.Y);
            int end = run.IsHorizontal
                ? Math.Max(run.StartDm.X, run.EndDm.X)
                : Math.Max(run.StartDm.Y, run.EndDm.Y);
            int effectiveCoverage = Math.Min(94, run.CoveragePercent + 14);
            SettlementFrontageSite[] packed = SettlementPlotLayout.PackFrontage(
                start,
                end,
                effectiveCoverage,
                ModulePitchDm,
                run.HasGap,
                run.GapCentreDm,
                run.GapWidthDm);
            UrbanFabricIntent intent = KentridgeDefinition.UrbanFabricIntent(run);

            for (int i = 0; i < packed.Length; i++)
            {
                SettlementFrontageSite slot = packed[i];
                KentridgeUrbanFabricForm form = KentridgeUrbanFabricGrammar.Resolve(
                    run, seed, runIndex, slot.SiteIndex);
                StructureGeometryProfile geometry = UrbanFabricGeometryProfiles.Resolve(
                    intent,
                    form.Inner,
                    BuiltInArchitectureStyles.Registry);
                sites.Add(new FabricSite(
                    run,
                    form,
                    geometry,
                    SiteOrigin(run, slot.CentreAlongDm)));
            }
        }

        private static Int2 SiteOrigin(KentridgeFrontageRun run, int centreAlongDm)
        {
            int envelope = KentridgeUrbanFabricGrammar.EnvelopeDm;
            int half = envelope / 2;

            if (run.IsHorizontal)
            {
                int z = run.StartDm.Y;
                if (run.Frontage == FrontageDirection.North)
                    z -= envelope;
                return new Int2(centreAlongDm - half, z);
            }

            int x = run.StartDm.X;
            if (run.Frontage == FrontageDirection.East)
                x -= envelope;
            return new Int2(x, centreAlongDm - half);
        }

        private static int HeightDm(KentridgeUrbanFabricForm form)
        {
            ArchitectureTheme theme = KentridgeDefinition.Theme;
            return theme.FoundationHeightDm
                 + form.Storeys * theme.FloorHeightDm
                 + form.RoofHeightDm
                 + 16;
        }

        private static int[] FabricProgram(
            FabricSite site,
            VoxelWorldGenSettings settings)
        {
            KentridgeUrbanFabricForm form = site.Form;
            KentridgeUrbanFabricGrammar.Validate(site.Run, form);

            int s = settings.VoxelsPerDecimetre;
            ArchitectureTheme theme = KentridgeDefinition.Theme;
            int envelope = KentridgeUrbanFabricGrammar.EnvelopeDm * s;
            int w = form.WidthDm * s;
            int d = form.DepthDm * s;
            int x0 = (envelope - w) / 2;
            int z0 = (envelope - d) / 2;
            int f = theme.FoundationHeightDm * s;
            int t = theme.WallThicknessDm * s;
            int floor = theme.FloorHeightDm * s;
            int beam = theme.BeamWidthDm * s;
            int overhang = form.UpperOverhangDm * s;
            int roofOverhang = KentridgeUrbanFabricGrammar.RoofOverhangDm * s;
            int roofH = form.RoofHeightDm * s;

            byte foundation = settings.Materials.Resolve(theme.Foundation);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = form.WindowStyle == KentridgeWindowStyle.Warm
                ? settings.Materials.Resolve(MaterialRole.WarmWindow)
                : settings.Materials.Resolve(theme.Window);
            byte roof = site.Run.District == DistrictKind.Civic
                     || site.Run.District == DistrictKind.Noble
                ? settings.Materials.Resolve(MaterialRole.Slate)
                : settings.Materials.Resolve(theme.Roof);
            byte cloth = settings.Materials.Resolve(MaterialRole.Cloth);

            var b = new ProgramBuilder(site.Geometry, s);
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

            int doorW = 11 * s;
            int doorOffset = form.FrontageRhythm == KentridgeFrontageRhythm.Asymmetric
                ? (form.AnnexOnRight ? -8 : 8) * s
                : 0;
            int doorX = x0 + w / 2 - doorW / 2 + doorOffset;
            doorX = math.clamp(doorX, x0 + 6 * s, x0 + w - doorW - 6 * s);
            b.Carve(doorX, f, z0, doorW, theme.DoorHeightDm * s, t + s);

            AddFrontWindows(b, form, x0, z0, w,
                f + theme.WindowBaseDm * s, t, glass, s, doorX, doorW);
            for (int storey = 1; storey < form.Storeys; storey++)
            {
                int y = f + storey * floor + theme.WindowBaseDm * s;
                AddFrontWindows(b, form, upperX, upperZ, upperW,
                    y, t, glass, s, int.MinValue, 0);
            }

            AddRearAndSideWindows(b, form,
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

            if (form.HasAwning)
            {
                int awningInset = 7 * s;
                b.Box(x0 + awningInset, f + 28 * s, z0 - 10 * s,
                    w - 2 * awningInset, 3 * s, 12 * s, cloth);
            }

            if (form.Roof == KentridgeRoofForm.GableWithLeanTo)
                AddLeanToAnnex(b, form, envelope, x0, z0, w, d,
                    f, floor, t, wall, timber, roof, s);

            int roofX = upperH > 0 ? upperX : x0;
            int roofZ = upperH > 0 ? upperZ : z0;
            int roofW = upperH > 0 ? upperW : w;
            int roofD = upperH > 0 ? upperD : d;
            int roofY = f + form.Storeys * floor;
            EmitRoof(b, form.Roof,
                roofX - roofOverhang,
                roofY,
                roofZ - roofOverhang,
                roofW + 2 * roofOverhang,
                roofH,
                roofD + 2 * roofOverhang,
                roof, s);

            int chimney = 7 * s;
            int chimneyX = form.ChimneyOnRight
                ? roofX + roofW - 15 * s
                : roofX + 8 * s;
            b.Box(chimneyX, roofY - 3 * s, roofZ + roofD - 17 * s,
                chimney, roofH + 12 * s, chimney, foundation);

            return b.Finish();
        }

        private static void EmitShell(
            ProgramBuilder b,
            int x, int y, int z,
            int w, int h, int d,
            int t,
            byte wall)
        {
            b.ShellBox(x, y, z, w, h, d, wall);
            b.InteriorCarve(x + t, y, z + t, w - 2 * t, h, d - 2 * t);
        }

        private static void AddFrontWindows(
            ProgramBuilder b,
            KentridgeUrbanFabricForm form,
            int x0, int z0, int w,
            int y, int t,
            byte glass,
            int s,
            int doorX,
            int doorW)
        {
            int windowW = 10 * s;
            int windowH = 12 * s;
            int[] centres;
            switch (form.FrontageRhythm)
            {
                case KentridgeFrontageRhythm.ThreeBay:
                    centres = new[] { x0 + w / 5, x0 + w / 2, x0 + 4 * w / 5 };
                    break;
                case KentridgeFrontageRhythm.Asymmetric:
                    centres = new[] { x0 + w / 4, x0 + 2 * w / 3 };
                    break;
                default:
                    centres = new[] { x0 + w / 4, x0 + 3 * w / 4 };
                    break;
            }

            for (int i = 0; i < centres.Length; i++)
            {
                int wx = centres[i] - windowW / 2;
                if (doorX != int.MinValue
                    && wx < doorX + doorW + 2 * s
                    && wx + windowW > doorX - 2 * s)
                    continue;
                AddWindowZ(b, wx, y, z0, windowW, windowH, t + s, glass);
            }
        }

        private static void AddRearAndSideWindows(
            ProgramBuilder b,
            KentridgeUrbanFabricForm form,
            int x0, int z0, int w, int d,
            int upperX, int upperZ, int upperW, int upperD,
            int f, int t, int floor,
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
                int y = f + storey * floor + windowBase;
                int rearZ = bz + bd - (t + s);
                int rightX = bx + bw - (t + s);
                int windowW = 10 * s;

                AddWindowZ(b, bx + bw / 3 - windowW / 2, y, rearZ,
                    windowW, windowH, t + s, glass);
                AddWindowZ(b, bx + 2 * bw / 3 - windowW / 2, y, rearZ,
                    windowW, windowH, t + s, glass);

                int sideZ = bz + bd / 2 - windowW / 2;
                AddWindowX(b, bx, y, sideZ, t + s, windowH, windowW, glass);
                AddWindowX(b, rightX, y, sideZ, t + s, windowH, windowW, glass);
            }
        }

        private static void AddLeanToAnnex(
            ProgramBuilder b,
            KentridgeUrbanFabricForm form,
            int envelope,
            int x0, int z0, int w, int d,
            int f, int floor, int t,
            byte wall, byte timber, byte roof,
            int s)
        {
            int annexW = 14 * s;
            int annexD = math.min(30 * s, d - 10 * s);
            int overlap = 4 * s;
            int ax = form.AnnexOnRight
                ? x0 + w - overlap
                : x0 - annexW + overlap;
            ax = math.clamp(ax, 2 * s, envelope - annexW - 2 * s);
            int az = z0 + d - annexD;

            EmitShell(b, ax, f, az, annexW, floor, annexD, t, wall);
            AddTimberFrame(b, ax, az, annexW, annexD, f, floor,
                math.max(2 * s, t - s), timber);
            b.Prism(ax - 2 * s, f + floor, az - 2 * s,
                annexW + 4 * s, 14 * s, annexD + 4 * s,
                PrismProfile.Shed, roof);
        }

        private static void EmitRoof(
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

        private static void AddWindowZ(
            ProgramBuilder b,
            int x, int y, int z,
            int width, int height, int depth,
            byte material)
        {
            b.Carve(x, y, z, width, height, depth);
            b.GlazingBox(x, y, z, width, height, depth, material);
        }

        private static void AddWindowX(
            ProgramBuilder b,
            int x, int y, int z,
            int depth, int height, int width,
            byte material)
        {
            b.Carve(x, y, z, depth, height, width);
            b.GlazingBox(x, y, z, depth, height, width, material);
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
            private readonly ArchitectureShapeProgramBuilder _inner;

            public ProgramBuilder(StructureGeometryProfile profile, int voxelsPerDecimetre)
            {
                _inner = new ArchitectureShapeProgramBuilder(profile, voxelsPerDecimetre);
            }

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

            public void GlazingBox(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material) =>
                _inner.DetailBox(
                    x, y, z, sx, sy, sz, material,
                    cornerRadiusDm: 0,
                    surface: StructureSurfaceTreatment.Planar);

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

            public int[] Finish() => _inner.Finish();
        }
    }
}
