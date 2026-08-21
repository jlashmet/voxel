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
    /// Public-space backend for the macro-vertical Kentridge layout.
    ///
    /// Horizontal streets sit on authored district terraces while the two north/south routes become
    /// long, continuous ascents between them. When an authored road rises above analytic terrain the
    /// difference becomes a solid masonry causeway instead of a floating ribbon; where the profile
    /// cuts through a natural shoulder the ordinary carve volume opens the route. The result is a
    /// readable low-to-high journey through the town rather than four roads independently following
    /// whatever elevation noise happened to be underneath them.
    /// </summary>
    public static class KentridgeVerticalTownSurfaceCatalogue
    {
        private const int RoadSegmentDm = 96;
        private const int RoadFillDepthDm = 6;
        private const int RoadBuriedFootingDm = 6;
        private const int PlazaFillDepthDm = 10;
        private const int PlazaBuriedFootingDm = 8;
        private const int SurfaceThicknessDm = 4;
        private const int ClearAboveDm = 28;

        private readonly struct RoadSegmentBuild
        {
            public readonly FixedString64Bytes Name;
            public readonly int3 Footprint;
            public readonly ExplicitPlacement Placement;
            public readonly int Width;
            public readonly int Length;
            public readonly int HeightDelta;
            public readonly int SupportDepth;
            public readonly int ClearHeight;
            public readonly byte Axis;

            public RoadSegmentBuild(
                FixedString64Bytes name, int3 footprint, ExplicitPlacement placement,
                int width, int length, int heightDelta, int supportDepth,
                int clearHeight, byte axis)
            {
                Name = name;
                Footprint = footprint;
                Placement = placement;
                Width = width;
                Length = length;
                HeightDelta = heightDelta;
                SupportDepth = supportDepth;
                ClearHeight = clearHeight;
                Axis = axis;
            }
        }

        private readonly struct PlazaBuild
        {
            public readonly int3 Footprint;
            public readonly ExplicitPlacement Placement;
            public readonly int Width;
            public readonly int Depth;
            public readonly int SupportDepth;
            public readonly int ClearHeight;

            public PlazaBuild(int3 footprint, ExplicitPlacement placement, int width, int depth,
                              int supportDepth, int clearHeight)
            {
                Footprint = footprint;
                Placement = placement;
                Width = width;
                Depth = depth;
                SupportDepth = supportDepth;
                ClearHeight = clearHeight;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            int scale = settings.VoxelsPerDecimetre;
            List<RoadSegmentBuild> roads = BuildRoadSegments(plan, seed, scale);
            PlazaBuild plaza = BuildPlaza(plan.Plaza, seed, scale);
            int roadCount = roads.Count;
            int definitionCount = roadCount + 1;

            var programs = new int[definitionCount][];
            int programLength = 0;
            for (int i = 0; i < roadCount; i++)
            {
                programs[i] = RoadProgram(roads[i], settings);
                programLength += programs[i].Length;
            }
            programs[roadCount] = PlazaProgram(plaza, settings);
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
                    MaxSlope = 32,
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
                    MaxPrimitives = 4,
                };

                catalogue.ExplicitPlacements[i] = road.Placement;
                catalogue.Rules[i] = ExplicitRule(i, i);
                programOffset += program.Length;
            }

            int plazaDefinition = roadCount;
            int[] plazaProgram = programs[plazaDefinition];
            CopyProgram(ref catalogue, programOffset, plazaProgram);
            catalogue.Definitions[plazaDefinition] = new FeatureDefinition
            {
                Name = new FixedString64Bytes("kentridge-vertical-market-square"),
                Kind = FeatureKind.Landform,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = plaza.Footprint,
                MaxSlope = 32,
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
                MaxPrimitives = 3,
            };
            catalogue.ExplicitPlacements[plazaDefinition] = plaza.Placement;
            catalogue.Rules[plazaDefinition] = ExplicitRule(plazaDefinition, plazaDefinition);

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Vertical Kentridge surface catalogue failed validation: " + result);
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
            int width = street.WidthDm * scale;
            int fillHeight = (RoadFillDepthDm + SurfaceThicknessDm) * scale;
            int buried = RoadBuriedFootingDm * scale;
            int clearBase = ClearAboveDm * scale;
            string name = "kentridge-vertical-road-" + street.Id + "-" + semanticSegment + "-" + piece;

            int targetA = KentridgeVerticalProfile.SurfaceYAtDm(a.X, a.Y, seed, scale);
            int targetB = KentridgeVerticalProfile.SurfaceYAtDm(b.X, b.Y, seed, scale);
            int lowTarget = Math.Min(targetA, targetB);
            int highTarget = Math.Max(targetA, targetB);
            int delta = highTarget - lowTarget;
            Int2 mid = new Int2((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            int naturalA = TerrainQuery.HeightAt(a.X * scale, a.Y * scale, seed);
            int naturalB = TerrainQuery.HeightAt(b.X * scale, b.Y * scale, seed);
            int naturalMid = TerrainQuery.HeightAt(mid.X * scale, mid.Y * scale, seed);
            int minNatural = Math.Min(naturalA, Math.Min(naturalB, naturalMid));
            int maxNatural = Math.Max(naturalA, Math.Max(naturalB, naturalMid));

            int supportDepth = Math.Max(0, lowTarget - minNatural) + buried;
            int clearHeight = delta + clearBase + Math.Max(0, maxNatural - lowTarget);
            byte orientation = targetA <= targetB ? (byte)0 : (byte)2;

            if (a.X == b.X)
            {
                int minZ = Math.Min(a.Y, b.Y) * scale;
                int maxZ = Math.Max(a.Y, b.Y) * scale;
                int centreX = a.X * scale;
                int length = maxZ - minZ + 1;
                return new RoadSegmentBuild(
                    new FixedString64Bytes(name),
                    new int3(width, supportDepth + fillHeight + clearHeight, length),
                    new ExplicitPlacement
                    {
                        Position = new int3(
                            centreX - width / 2,
                            lowTarget - fillHeight - supportDepth,
                            minZ),
                        Orientation = orientation,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    },
                    width, length, delta, supportDepth, clearHeight, axis: 2);
            }

            int minX = Math.Min(a.X, b.X) * scale;
            int maxX = Math.Max(a.X, b.X) * scale;
            int centreZ = a.Y * scale;
            int xlength = maxX - minX + 1;
            return new RoadSegmentBuild(
                new FixedString64Bytes(name),
                new int3(xlength, supportDepth + fillHeight + clearHeight, width),
                new ExplicitPlacement
                {
                    Position = new int3(
                        minX,
                        lowTarget - fillHeight - supportDepth,
                        centreZ - width / 2),
                    Orientation = orientation,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                },
                width, xlength, delta, supportDepth, clearHeight, axis: 0);
        }

        private static PlazaBuild BuildPlaza(PlannedPlaza plaza, uint seed, int scale)
        {
            int minXDm = plaza.CentreDm.X - plaza.SizeDm.X / 2;
            int maxXDm = plaza.CentreDm.X + plaza.SizeDm.X / 2;
            int minZDm = plaza.CentreDm.Y - plaza.SizeDm.Y / 2;
            int maxZDm = plaza.CentreDm.Y + plaza.SizeDm.Y / 2;
            int target = KentridgeVerticalProfile.SurfaceYAtDm(
                plaza.CentreDm.X, plaza.CentreDm.Y, seed, scale);

            int naturalCentre = TerrainQuery.HeightAt(
                plaza.CentreDm.X * scale, plaza.CentreDm.Y * scale, seed);
            int natural00 = TerrainQuery.HeightAt(minXDm * scale, minZDm * scale, seed);
            int natural10 = TerrainQuery.HeightAt(maxXDm * scale, minZDm * scale, seed);
            int natural01 = TerrainQuery.HeightAt(minXDm * scale, maxZDm * scale, seed);
            int natural11 = TerrainQuery.HeightAt(maxXDm * scale, maxZDm * scale, seed);
            int minNatural = Math.Min(naturalCentre,
                Math.Min(Math.Min(natural00, natural10), Math.Min(natural01, natural11)));
            int maxNatural = Math.Max(naturalCentre,
                Math.Max(Math.Max(natural00, natural10), Math.Max(natural01, natural11)));

            int fillHeight = (PlazaFillDepthDm + SurfaceThicknessDm) * scale;
            int supportDepth = Math.Max(0, target - minNatural) + PlazaBuriedFootingDm * scale;
            int clearHeight = ClearAboveDm * scale + Math.Max(0, maxNatural - target);
            int width = plaza.SizeDm.X * scale;
            int depth = plaza.SizeDm.Y * scale;

            return new PlazaBuild(
                new int3(width, supportDepth + fillHeight + clearHeight, depth),
                new ExplicitPlacement
                {
                    Position = new int3(
                        minXDm * scale,
                        target - fillHeight - supportDepth,
                        minZDm * scale),
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                },
                width, depth, supportDepth, clearHeight);
        }

        private static int[] RoadProgram(RoadSegmentBuild road,
                                         VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int fillHeight = (RoadFillDepthDm + SurfaceThicknessDm) * s;
            int sx = road.Axis == 0 ? road.Length : road.Width;
            int sz = road.Axis == 2 ? road.Length : road.Width;
            byte roadSurface = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte supportStone = settings.Materials.Resolve(MaterialRole.FoundationStone);

            var b = new ProgramBuilder();
            // Clear first; later fills are authoritative at the road surface and cannot be erased by
            // the same instance's excavation primitive.
            b.Carve(0, road.SupportDepth + fillHeight, 0,
                    sx, road.ClearHeight, sz);
            if (road.SupportDepth > 0)
                b.Box(0, 0, 0, sx, road.SupportDepth, sz, supportStone);
            b.Box(0, road.SupportDepth, 0, sx, fillHeight, sz, roadSurface);
            if (road.HeightDelta > 0)
                b.Ramp(0, road.SupportDepth + fillHeight, 0,
                       sx, road.HeightDelta, sz, road.Axis, roadSurface);
            return b.Finish();
        }

        private static int[] PlazaProgram(PlazaBuild plaza,
                                          VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int fillHeight = (PlazaFillDepthDm + SurfaceThicknessDm) * s;
            byte roadSurface = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte supportStone = settings.Materials.Resolve(MaterialRole.FoundationStone);

            var b = new ProgramBuilder();
            b.Carve(0, plaza.SupportDepth + fillHeight, 0,
                    plaza.Width, plaza.ClearHeight, plaza.Depth);
            if (plaza.SupportDepth > 0)
                b.Box(0, 0, 0, plaza.Width, plaza.SupportDepth, plaza.Depth, supportStone);
            b.Box(0, plaza.SupportDepth, 0,
                  plaza.Width, fillHeight, plaza.Depth, roadSurface);
            return b.Finish();
        }

        private static PlacementRule ExplicitRule(int definitionId, int offset)
        {
            return new PlacementRule
            {
                DefinitionId = definitionId,
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
                ExplicitOffset = offset,
                ExplicitCount = 1,
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