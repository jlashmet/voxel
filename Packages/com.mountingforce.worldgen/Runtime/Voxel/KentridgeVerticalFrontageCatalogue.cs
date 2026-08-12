using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Compiles the macro vertical-frontage plan into continuous occupied undercrofts. These are not
    /// extra gameplay buildings: they are the architectural face of the terrace itself, behind the
    /// varied upper frontage and below galleries/access that need to cut through it.
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

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
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
                    // Anonymous buildings are 86 and must remain visually in front of this shared
                    // terrace facade. Galleries/dwellings/access are 90+ and can further specialise it.
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
                    MaxPrimitives = 96,
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
                        (zone.StartDm.Y + KentridgeVerticalFrontagePlanner.FrontInsetDm) * s),
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

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
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

            // Start as one structural terrace volume, then hollow the lower floor. This is what makes
            // the shelf read as occupied architecture rather than a wall applied to a dirt cliff.
            b.Box(0, 0, 0, width, height, depth, stone);
            if (width > 2 * wall && depth > 2 * wall && height > baseH + topH)
                b.Carve(
                    wall,
                    baseH,
                    wall,
                    width - 2 * wall,
                    height - baseH - topH,
                    depth - 2 * wall);

            // The exact same gap reserved by the block planner remains a full-height passage into the
            // upper court. Urban access runs later at precedence 94 and can build its stair/gateway here.
            int gapCentre = (zone.GapCentreDm - zone.MinXDm) * s;
            int gapWidth = zone.GapWidthDm * s;
            int gapStart = math.clamp(gapCentre - gapWidth / 2, 0, math.max(0, width - gapWidth));
            b.Carve(gapStart, 0, 0, gapWidth, height, depth);

            int pitch = zone.BayPitchDm * s;
            int openingH = OpeningHeightDm(zone.Style) * s;
            int openingBottom = OpeningBottomDm * s;
            int openingSide = OpeningSideDm * s;
            int pier = Math.Max(3 * s, pitch / 7);

            for (int bayStart = 0; bayStart < width; bayStart += pitch)
            {
                int bayEnd = Math.Min(width, bayStart + pitch);
                int bayWidth = bayEnd - bayStart;
                if (bayWidth <= openingSide * 2 + 4 * s) continue;
                if (Overlaps(bayStart, bayEnd, gapStart, gapStart + gapWidth)) continue;

                int openingX = bayStart + openingSide;
                int openingW = bayWidth - openingSide * 2;
                int maxOpeningH = Math.Max(1, height - topH - openingBottom - 2 * s);
                int h = Math.Min(openingH, maxOpeningH);
                b.Carve(openingX, openingBottom, 0, openingW, h, wall + s);

                // A recessed lit plane makes each opening read as a room/loggia rather than a hole.
                int backZ = Math.Max(wall + s, depth - wall - 2 * s);
                b.Box(openingX, openingBottom + 2 * s, backZ,
                    openingW, Math.Max(1, h - 4 * s), 2 * s, warm);

                // Vertical structure gives the long facade a human-scale rhythm without turning it
                // into a decorative stripe around the terrain.
                b.Box(bayStart, baseH, 0,
                    Math.Min(pier, bayWidth), height - baseH, wall, frame);
            }

            // A continuous floor/deck at shelf level ties the lower rooms to the buildings above.
            b.Box(0, height - topH, 0, width, topH, depth, stone);
            return b.Finish();
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
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)mode);
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
