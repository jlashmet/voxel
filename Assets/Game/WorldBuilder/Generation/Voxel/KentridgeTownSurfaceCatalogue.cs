using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Voxel backend for Kentridge's public-space plan.
    ///
    /// Streets are subdivided into roughly 12.8 m pieces. Every piece is a continuous ramp whose
    /// endpoint heights come from TerrainQuery, and adjacent pieces share the same endpoint.
    /// This follows broad terrain without either failure mode of the earlier prototypes: independent
    /// flat tiles produced longitudinal steps, while one giant end-to-end ramp cut deep trenches
    /// through intervening hills. A Dirt carriageway keeps the authored width while thirty
    /// one-decimetre grassy bands per side grade its cut/fill edge back into the surrounding terrain.
    /// </summary>
    public static class KentridgeTownSurfaceCatalogue
    {
        private const int RoadSegmentDm = 128;
        private const int RoadFillDepthDm = 6;
        private const int PlazaFillDepthDm = 12;
        private const int SurfaceThicknessDm = 4;
        private const int ClearAboveDm = 24;
        private const int PlazaFootprintHeightDm = 52;
        private const int ShoulderBandCount = 30;
        private const int ShoulderBandWidthDm = 1;
        private const int ShoulderTotalRiseDm = 20;

        private readonly struct RoadSegmentBuild
        {
            public readonly FixedString64Bytes Name;
            public readonly int3 Footprint;
            public readonly ExplicitPlacement Placement;
            public readonly int CarriageWidth;
            public readonly int ShoulderWidth;
            public readonly int Length;
            public readonly int HeightDelta;
            public readonly byte Axis;

            public RoadSegmentBuild(FixedString64Bytes name, int3 footprint,
                                    ExplicitPlacement placement, int carriageWidth,
                                    int shoulderWidth, int length, int heightDelta, byte axis)
            {
                Name = name;
                Footprint = footprint;
                Placement = placement;
                CarriageWidth = carriageWidth;
                ShoulderWidth = shoulderWidth;
                Length = length;
                HeightDelta = heightDelta;
                Axis = axis;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            int scale = settings.VoxelsPerDecimetre;
            List<RoadSegmentBuild> roads = BuildRoadSegments(plan, seed, scale);
            int roadCount = roads.Count;
            int definitionCount = roadCount + 1;

            var programs = new int[definitionCount][];
            int programLength = 0;
            for (int i = 0; i < roadCount; i++)
            {
                programs[i] = RoadProgram(roads[i], settings);
                programLength += programs[i].Length;
            }
            programs[roadCount] = PlazaProgram(plan.Plaza, settings);
            programLength += programs[roadCount].Length;

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: definitionCount,
                rules: definitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: definitionCount,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int i = 0; i < roadCount; i++)
            {
                RoadSegmentBuild road = roads[i];
                int[] program = programs[i];
                CopyProgram(ref catalogue, programOffset, program);

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = road.Name,
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = road.Footprint,
                    MaxSlope = 16,
                    Precedence = 20,
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
                    MaxPrimitives = 1 + (1 + ShoulderBandCount * 2) * 2,
                };

                catalogue.ExplicitPlacements[i] = road.Placement;
                catalogue.Rules[i] = ExplicitRule(i, i, 1);
                programOffset += program.Length;
            }

            int plazaDefinition = roadCount;
            int[] plazaProgram = programs[plazaDefinition];
            CopyProgram(ref catalogue, programOffset, plazaProgram);
            catalogue.Definitions[plazaDefinition] = new FeatureDefinition
            {
                Name = new FixedString64Bytes("kentridge-market-square"),
                Kind = FeatureKind.Landform,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(
                    plan.Plaza.SizeDm.X * scale,
                    PlazaFootprintHeightDm * scale,
                    plan.Plaza.SizeDm.Y * scale),
                MaxSlope = 16,
                Precedence = 25,
                ParameterOffset = 0,
                ParameterCount = 0,
                AnchorOffset = 0,
                AnchorCount = 0,
                SlotOffset = 0,
                SlotCount = 0,
                ProgramOffset = programOffset,
                ProgramLength = plazaProgram.Length,
                MaterialOffset = 0,
                MaterialCount = 0,
                MaxPrimitives = 2,
            };
            catalogue.ExplicitPlacements[plazaDefinition] =
                ResolvePlazaPlacement(plan.Plaza, seed, scale);
            catalogue.Rules[plazaDefinition] =
                ExplicitRule(plazaDefinition, plazaDefinition, 1);

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge surface catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static List<RoadSegmentBuild> BuildRoadSegments(
            SettlementPlan plan, uint seed, int scale)
        {
            var result = new List<RoadSegmentBuild>();

            for (int streetIndex = 0; streetIndex < plan.Streets.Count; streetIndex++)
            {
                PlannedStreet street = plan.Streets[streetIndex];
                for (int p = 0; p + 1 < street.Points.Count; p++)
                {
                    Int2 a = street.Points[p];
                    Int2 b = street.Points[p + 1];
                    int dx = b.X - a.X;
                    int dz = b.Y - a.Y;
                    if (dx != 0 && dz != 0)
                        throw new InvalidOperationException(
                            "Kentridge roads must be orthogonal: " + street.Id);

                    int lengthDm = Math.Abs(dx != 0 ? dx : dz);
                    int pieces = Math.Max(1, (lengthDm + RoadSegmentDm - 1) / RoadSegmentDm);

                    for (int piece = 0; piece < pieces; piece++)
                    {
                        Int2 s0 = new Int2(
                            a.X + dx * piece / pieces,
                            a.Y + dz * piece / pieces);
                        Int2 s1 = new Int2(
                            a.X + dx * (piece + 1) / pieces,
                            a.Y + dz * (piece + 1) / pieces);
                        result.Add(ResolveRoadSegment(
                            street, streetIndex, p, piece, s0, s1, seed, scale));
                    }
                }
            }

            return result;
        }

        private static RoadSegmentBuild ResolveRoadSegment(
            PlannedStreet street, int streetIndex, int semanticSegment, int piece,
            Int2 a, Int2 b, uint seed, int scale)
        {
            int carriageWidth = street.WidthDm * scale;
            int shoulderWidth = ShoulderBandCount * ShoulderBandWidthDm * scale;
            int totalWidth = carriageWidth + shoulderWidth * 2;
            int fillHeight = (RoadFillDepthDm + SurfaceThicknessDm) * scale;
            int clearAbove = ClearAboveDm * scale;
            int maxShoulderRise = ShoulderTotalRiseDm * scale;
            string name = "kentridge-road-" + street.Id + "-" + semanticSegment + "-" + piece;

            if (a.X == b.X)
            {
                int minZ = Math.Min(a.Y, b.Y) * scale;
                int maxZ = Math.Max(a.Y, b.Y) * scale;
                int centreX = a.X * scale;
                int h0 = TerrainQuery.HeightAt(centreX, minZ, seed);
                int h1 = TerrainQuery.HeightAt(centreX, maxZ, seed);
                int low = Math.Min(h0, h1);
                int delta = Math.Abs(h1 - h0);
                byte orientation = h0 <= h1 ? (byte)0 : (byte)2;
                int length = maxZ - minZ + 1;

                return new RoadSegmentBuild(
                    new FixedString64Bytes(name),
                    new int3(
                        totalWidth,
                        fillHeight + delta + maxShoulderRise + clearAbove,
                        length),
                    new ExplicitPlacement
                    {
                        Position = new int3(
                            centreX - totalWidth / 2,
                            low - fillHeight,
                            minZ),
                        Orientation = orientation,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    },
                    carriageWidth, shoulderWidth, length, delta, axis: 2);
            }

            int minX = Math.Min(a.X, b.X) * scale;
            int maxX = Math.Max(a.X, b.X) * scale;
            int centreZ = a.Y * scale;
            int xh0 = TerrainQuery.HeightAt(minX, centreZ, seed);
            int xh1 = TerrainQuery.HeightAt(maxX, centreZ, seed);
            int xlow = Math.Min(xh0, xh1);
            int xdelta = Math.Abs(xh1 - xh0);
            byte xorientation = xh0 <= xh1 ? (byte)0 : (byte)2;
            int xlength = maxX - minX + 1;

            return new RoadSegmentBuild(
                new FixedString64Bytes(name),
                new int3(
                    xlength,
                    fillHeight + xdelta + maxShoulderRise + clearAbove,
                    totalWidth),
                new ExplicitPlacement
                {
                    Position = new int3(
                        minX,
                        xlow - fillHeight,
                        centreZ - totalWidth / 2),
                    Orientation = xorientation,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                },
                carriageWidth, shoulderWidth, xlength, xdelta, axis: 0);
        }

        private static ExplicitPlacement ResolvePlazaPlacement(
            PlannedPlaza plaza, uint seed, int scale)
        {
            int minX = plaza.CentreDm.X - plaza.SizeDm.X / 2;
            int minZ = plaza.CentreDm.Y - plaza.SizeDm.Y / 2;
            int fillHeight = (PlazaFillDepthDm + SurfaceThicknessDm) * scale;
            int targetY = TerrainQuery.HeightAt(
                plaza.CentreDm.X * scale, plaza.CentreDm.Y * scale, seed);

            return new ExplicitPlacement
            {
                Position = new int3(
                    minX * scale,
                    targetY - fillHeight,
                    minZ * scale),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };
        }

        private static int[] RoadProgram(RoadSegmentBuild road,
                                         VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int fillHeight = (RoadFillDepthDm + SurfaceThicknessDm) * s;
            int maxShoulderRise = ShoulderTotalRiseDm * s;
            int clearHeight = road.HeightDelta + maxShoulderRise + ClearAboveDm * s;
            int crossTotal = road.CarriageWidth + road.ShoulderWidth * 2;
            int sx = road.Axis == 0 ? road.Length : crossTotal;
            int sz = road.Axis == 2 ? road.Length : crossTotal;
            byte roadSurface = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte shoulderSurface = settings.Materials.Resolve(MaterialRole.Moss);

            var b = new ProgramBuilder();

            // Cut one corridor to the low carriageway endpoint, then fill the centre road and
            // progressively higher grassy shoulder bands back into it. Each strip keeps the same
            // longitudinal ramp as this 12.8 m road segment, so feathering does not reintroduce
            // tile-to-tile cliffs or a giant end-to-end road grade. The cross-slope begins flush
            // with the Dirt core and uses integer interpolation so no one-decimetre band jumps by
            // more than one decimetre at the canonical scale.
            b.Carve(0, fillHeight, 0, sx, clearHeight, sz);

            int centreCross = road.ShoulderWidth;
            AddLongitudinalStrip(
                b, road, centreCross, road.CarriageWidth, fillHeight, roadSurface);

            int bandWidth = ShoulderBandWidthDm * s;
            for (int band = 0; band < ShoulderBandCount; band++)
            {
                int rise = band * ShoulderTotalRiseDm * s / (ShoulderBandCount - 1);
                int leftCross = road.ShoulderWidth - (band + 1) * bandWidth;
                int rightCross = road.ShoulderWidth + road.CarriageWidth + band * bandWidth;
                AddLongitudinalStrip(
                    b, road, leftCross, bandWidth, fillHeight + rise, shoulderSurface);
                AddLongitudinalStrip(
                    b, road, rightCross, bandWidth, fillHeight + rise, shoulderSurface);
            }

            return b.Finish();
        }

        private static void AddLongitudinalStrip(
            ProgramBuilder b, RoadSegmentBuild road, int crossOffset, int crossWidth,
            int baseHeight, byte material)
        {
            if (road.Axis == 0)
            {
                b.Box(0, 0, crossOffset, road.Length, baseHeight, crossWidth, material);
                if (road.HeightDelta > 0)
                    b.Ramp(
                        0, baseHeight, crossOffset,
                        road.Length, road.HeightDelta, crossWidth,
                        axis: 0, material: material);
                return;
            }

            b.Box(crossOffset, 0, 0, crossWidth, baseHeight, road.Length, material);
            if (road.HeightDelta > 0)
                b.Ramp(
                    crossOffset, baseHeight, 0,
                    crossWidth, road.HeightDelta, road.Length,
                    axis: 2, material: material);
        }

        private static int[] PlazaProgram(PlannedPlaza plaza,
                                          VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int fillHeight = (PlazaFillDepthDm + SurfaceThicknessDm) * s;
            int clearHeight = ClearAboveDm * s;
            byte surface = settings.Materials.Resolve(MaterialRole.RoadSurface);

            var b = new ProgramBuilder();
            b.Carve(0, fillHeight, 0,
                    plaza.SizeDm.X * s, clearHeight, plaza.SizeDm.Y * s);
            b.Box(0, 0, 0,
                  plaza.SizeDm.X * s, fillHeight, plaza.SizeDm.Y * s, surface);
            return b.Finish();
        }

        private static PlacementRule ExplicitRule(int definitionId, int offset, int count)
        {
            return new PlacementRule
            {
                DefinitionId = definitionId,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 1024,
                MaxSlope = 16,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = offset,
                ExplicitCount = count,
            };
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (int i = 0; i < program.Length; i++) catalogue.Program[offset + i] = program[i];
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            PrimitiveMode mode = PrimitiveMode.Fill) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, 0, 0, (int)mode);

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

            public void Ramp(int x, int y, int z, int sx, int sy, int sz,
                             byte axis, byte material) =>
                Op(ShapeOp.EmitRamp, x, y, z, sx, sy, sz,
                   axis, material, 0, 0, (int)PrimitiveMode.Fill);

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
