using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Compiles the macro vertical-frontage plan into continuous occupied undercrofts. The undercroft
    /// depth is embedded into the terrace: its rear wall is uphill and its open pier/lintel face ends
    /// exactly on the authored downhill block edge. The programme excavates the occupied volume before
    /// rebuilding hard architecture, so these read as rooms cut into the hill rather than projections
    /// pasted onto the outside of it.
    /// </summary>
    public static class KentridgeVerticalFrontageCatalogue
    {
        private const int Precedence = 85;
        private const int WallThicknessDm = 4;
        private const int BaseDm = 5;
        private const int TopSlabDm = 5;
        private const int OpeningBottomDm = 8;
        private const int OpeningSideDm = 5;

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            KentridgeVerticalFrontagePlan plan = KentridgeVerticalFrontagePlanner.Build(seed);
            var programs = new int[plan.Zones.Count][];
            int programLength = 0;

            for (int i = 0; i < plan.Zones.Count; i++)
            {
                programs[i] = FrontageProgram(plan.Zones[i], settings);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: plan.Zones.Count,
                rules: plan.Zones.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: plan.Zones.Count,
                overrides: 0,
                allocator);

            int s = settings.VoxelsPerDecimetre;
            int programOffset = 0;
            for (int i = 0; i < plan.Zones.Count; i++)
            {
                KentridgeVerticalFrontageZone zone = plan.Zones[i];
                int[] program = programs[i];
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-vertical-" + i),
                    Kind = FeatureKind.Infrastructure,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(
                        zone.LengthDm * s,
                        zone.HeightDm * s,
                        zone.DepthDm * s),
                    MaxSlope = 32,
                    // Court paving shares 85 but sits at shelf level. Anonymous buildings are 86 and
                    // win above/through this excavation; access/galleries at 90+ can specialise it.
                    Precedence = Precedence,
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
                    MaxPrimitives = 98,
                };

                int shelfSurface = KentridgeVerticalProfile.SurfaceYAtDm(
                    zone.ElevationSampleDm.X,
                    zone.ElevationSampleDm.Y,
                    seed,
                    s);
                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = new int3(
                        zone.MinXDm * s,
                        shelfSurface
                            - (zone.HeightDm + KentridgeVerticalFrontagePlanner.TopBelowShelfDm) * s,
                        (zone.StartDm.Y - zone.DepthDm
                            + KentridgeVerticalFrontagePlanner.FrontInsetDm) * s),
                    Orientation = 0,
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
                    "Kentridge vertical frontage catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static int[] FrontageProgram(
            KentridgeVerticalFrontageZone zone,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int width = zone.LengthDm * s;
            int height = zone.HeightDm * s;
            int depth = zone.DepthDm * s;
            int wall = WallThicknessDm * s;
            int baseH = BaseDm * s;
            int topH = TopSlabDm * s;

            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            byte warm = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte frame = zone.Style == KentridgeVerticalFrontageStyle.MarketArcade
                || zone.Style == KentridgeVerticalFrontageStyle.UrbanUndercroft
                ? timber
                : dark;

            var b = new ProgramBuilder();
            int gapCentre = (zone.GapCentreDm - zone.MinXDm) * s;
            int gapWidth = zone.GapWidthDm * s;
            int gapStart = math.clamp(gapCentre - gapWidth / 2, 0, math.max(0, width - gapWidth));
            int gapEnd = math.min(width, gapStart + gapWidth);

            // Excavate the room into the shelf before rebuilding the hard architectural shell. This
            // is what makes the undercroft visible after moving its depth back inside the terrace.
            int clearH = math.max(1, height - baseH - topH);
            b.Carve(0, baseH, 0, width, clearH, depth);

            // Floor and roof deck stop at the aligned gateway, leaving that bay open through the full
            // undercroft depth for later vertical circulation.
            AddSpan(b, 0, gapStart, 0, 0, depth, baseH, stone);
            AddSpan(b, gapEnd, width, 0, 0, depth, baseH, stone);
            AddSpan(b, 0, gapStart, height - topH, 0, depth, topH, stone);
            AddSpan(b, gapEnd, width, height - topH, 0, depth, topH, stone);

            // Local z=0 is uphill/rear. The visible open frontage is at the far/downhill edge.
            int backZ = 0;
            int frontZ = math.max(0, depth - wall);
            int backH = math.max(1, height - baseH - topH);
            AddSpan(b, 0, gapStart, baseH, backZ, wall, backH, stone);
            AddSpan(b, gapEnd, width, baseH, backZ, wall, backH, stone);

            int pitch = zone.BayPitchDm * s;
            int openingH = OpeningHeightDm(zone.Style) * s;
            int openingBottom = OpeningBottomDm * s;
            int openingSide = OpeningSideDm * s;
            int pier = Math.Max(3 * s, pitch / 7);
            int lintel = 4 * s;

            for (int bayStart = 0; bayStart < width; bayStart += pitch)
            {
                int bayEnd = Math.Min(width, bayStart + pitch);
                int bayWidth = bayEnd - bayStart;
                if (bayWidth <= openingSide * 2 + 4 * s) continue;
                if (Overlaps(bayStart, bayEnd, gapStart, gapEnd)) continue;

                b.Box(bayStart, baseH, frontZ,
                    Math.Min(pier, bayWidth), height - baseH - topH, wall, frame);
                b.Box(bayStart, height - topH - lintel, frontZ,
                    bayWidth, lintel, wall, frame);

                // Warm recessed panels sit on the uphill rear wall and are visible through the open
                // downhill bays, giving the terrace face real depth instead of a flat lit wall.
                int openingX = bayStart + openingSide;
                int openingW = bayWidth - openingSide * 2;
                int maxOpeningH = Math.Max(1, height - topH - openingBottom - 2 * s);
                int h = Math.Min(openingH, maxOpeningH);
                int panelZ = Math.Min(math.max(wall, wall + s), math.max(wall, depth - 2 * s));
                b.Box(openingX, openingBottom, panelZ,
                    openingW, h, Math.Min(2 * s, Math.Max(1, depth - panelZ)), warm);
            }

            if (!Overlaps(Math.Max(0, width - pier), width, gapStart, gapEnd))
                b.Box(Math.Max(0, width - pier), baseH, frontZ,
                    Math.Min(pier, width), height - baseH - topH, wall, frame);

            return b.Finish();
        }

        private static void AddSpan(
            ProgramBuilder b,
            int start,
            int end,
            int y,
            int z,
            int depth,
            int height,
            byte material)
        {
            int width = end - start;
            if (width <= 0 || depth <= 0 || height <= 0) return;
            b.Box(start, y, z, width, height, depth, material);
        }

        private static int OpeningHeightDm(KentridgeVerticalFrontageStyle style)
        {
            switch (style)
            {
                case KentridgeVerticalFrontageStyle.CivicLoggia:
                    return 28;
                case KentridgeVerticalFrontageStyle.NobleTerrace:
                    return 24;
                default:
                    return 22;
            }
        }

        private static bool Overlaps(int a0, int a1, int b0, int b1)
        {
            return a0 < b1 && b0 < a1;
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
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, 0, 0, (int)mode);
            }

            public void Carve(int x, int y, int z, int sx, int sy, int sz)
            {
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);
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
