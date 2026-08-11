using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Terrain;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Voxel backend for Kentridge's public-space plan.
    ///
    /// Each semantic street is compiled to one continuous longitudinal grade rather than a chain
    /// of independently sampled flat tiles. The road therefore meets the original terrain at both
    /// endpoints and changes altitude monotonically between them without tile-to-tile cliffs.
    /// The market square remains a shallow level cut/fill pad and is applied after the roads.
    /// </summary>
    public static class KentridgeTownSurfaceCatalogue
    {
        private const int RoadFillDepthDm = 6;
        private const int PlazaFillDepthDm = 12;
        private const int SurfaceThicknessDm = 2;
        private const int ClearAboveDm = 36;
        private const int PlazaFootprintHeightDm = 56;

        private readonly struct RoadBuild
        {
            public readonly int3 Footprint;
            public readonly ExplicitPlacement Placement;
            public readonly int Width;
            public readonly int Length;
            public readonly int HeightDelta;
            public readonly byte Axis;

            public RoadBuild(int3 footprint, ExplicitPlacement placement,
                             int width, int length, int heightDelta, byte axis)
            {
                Footprint = footprint;
                Placement = placement;
                Width = width;
                Length = length;
                HeightDelta = heightDelta;
                Axis = axis;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            int streetCount = plan.Streets.Count;
            int definitionCount = streetCount + 1;
            int scale = settings.VoxelsPerDecimetre;

            var roads = new RoadBuild[streetCount];
            var programs = new int[definitionCount][];
            int programLength = 0;

            for (int i = 0; i < streetCount; i++)
            {
                roads[i] = ResolveRoad(plan.Streets[i], seed, scale);
                programs[i] = RoadProgram(roads[i], settings);
                programLength += programs[i].Length;
            }

            programs[streetCount] = PlazaProgram(plan.Plaza, settings);
            programLength += programs[streetCount].Length;

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
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

            for (int i = 0; i < streetCount; i++)
            {
                PlannedStreet street = plan.Streets[i];
                RoadBuild road = roads[i];
                int[] program = programs[i];
                CopyProgram(ref catalogue, programOffset, program);

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-road-" + street.Id),
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
                    MaxPrimitives = 3,
                };

                catalogue.ExplicitPlacements[i] = road.Placement;
                catalogue.Rules[i] = ExplicitRule(i, i, 1);
                programOffset += program.Length;
            }

            int plazaDefinition = streetCount;
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

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge surface catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static RoadBuild ResolveRoad(PlannedStreet street, uint seed, int scale)
        {
            if (street.Points.Count != 2)
                throw new InvalidOperationException(
                    "The first continuous Kentridge road backend expects one straight segment: "
                    + street.Id);

            Int2 a = street.Points[0];
            Int2 b = street.Points[1];
            int width = street.WidthDm * scale;
            int fillDepth = RoadFillDepthDm * scale;
            int surfaceThickness = SurfaceThicknessDm * scale;
            int clearAbove = ClearAboveDm * scale;

            if (a.X == b.X)
            {
                int minZ = Math.Min(a.Y, b.Y) * scale;
                int maxZ = Math.Max(a.Y, b.Y) * scale;
                int centreX = a.X * scale;
                int h0 = TerrainSampler.HeightAt(centreX, minZ, seed);
                int h1 = TerrainSampler.HeightAt(centreX, maxZ, seed);
                int low = Math.Min(h0, h1);
                int delta = Math.Abs(h1 - h0);
                byte orientation = h0 <= h1 ? (byte)0 : (byte)2;
                int length = maxZ - minZ + 1;
                int vertical = fillDepth + surfaceThickness + delta + clearAbove;

                return new RoadBuild(
                    new int3(width, vertical, length),
                    new ExplicitPlacement
                    {
                        Position = new int3(
                            centreX - width / 2,
                            low - fillDepth,
                            minZ),
                        Orientation = orientation,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    },
                    width, length, delta, axis: 2);
            }

            if (a.Y == b.Y)
            {
                int minX = Math.Min(a.X, b.X) * scale;
                int maxX = Math.Max(a.X, b.X) * scale;
                int centreZ = a.Y * scale;
                int h0 = TerrainSampler.HeightAt(minX, centreZ, seed);
                int h1 = TerrainSampler.HeightAt(maxX, centreZ, seed);
                int low = Math.Min(h0, h1);
                int delta = Math.Abs(h1 - h0);
                byte orientation = h0 <= h1 ? (byte)0 : (byte)2;
                int length = maxX - minX + 1;
                int vertical = fillDepth + surfaceThickness + delta + clearAbove;

                return new RoadBuild(
                    new int3(length, vertical, width),
                    new ExplicitPlacement
                    {
                        Position = new int3(
                            minX,
                            low - fillDepth,
                            centreZ - width / 2),
                        Orientation = orientation,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    },
                    width, length, delta, axis: 0);
            }

            throw new InvalidOperationException(
                "Kentridge roads must be orthogonal: " + street.Id);
        }

        private static ExplicitPlacement ResolvePlazaPlacement(
            PlannedPlaza plaza, uint seed, int scale)
        {
            int minX = plaza.CentreDm.X - plaza.SizeDm.X / 2;
            int minZ = plaza.CentreDm.Y - plaza.SizeDm.Y / 2;
            long total = 0;
            const int samples = 5;

            for (int iz = 0; iz < samples; iz++)
            for (int ix = 0; ix < samples; ix++)
            {
                int xDm = minX + plaza.SizeDm.X * ix / (samples - 1);
                int zDm = minZ + plaza.SizeDm.Y * iz / (samples - 1);
                total += TerrainSampler.HeightAt(xDm * scale, zDm * scale, seed);
            }

            int targetY = (int)(total / (samples * samples));

            return new ExplicitPlacement
            {
                Position = new int3(
                    minX * scale,
                    targetY - PlazaFillDepthDm * scale,
                    minZ * scale),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };
        }

        private static int[] RoadProgram(RoadBuild road, VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int baseHeight = (RoadFillDepthDm + SurfaceThicknessDm) * s;
            int clearHeight = road.HeightDelta + ClearAboveDm * s;
            int sx = road.Axis == 0 ? road.Length : road.Width;
            int sz = road.Axis == 2 ? road.Length : road.Width;
            byte surface = settings.Materials.Resolve(MaterialRole.RoadSurface);

            var b = new ProgramBuilder();
            b.Carve(0, baseHeight, 0, sx, clearHeight, sz);
            b.Box(0, 0, 0, sx, baseHeight, sz, surface);
            if (road.HeightDelta > 0)
                b.Ramp(0, baseHeight, 0, sx, road.HeightDelta, sz,
                       road.Axis, surface);
            return b.Finish();
        }

        private static int[] PlazaProgram(PlannedPlaza plaza, VoxelWorldGenSettings settings)
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
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)mode);

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

            public void Ramp(int x, int y, int z, int sx, int sy, int sz,
                             byte axis, byte material) =>
                Op(ShapeOp.EmitRamp, x, y, z, sx, sy, sz,
                   axis, material, (int)PrimitiveMode.Fill);

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
