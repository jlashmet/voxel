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
    /// Each semantic street is one continuous longitudinal grade rather than independently sampled
    /// flat tiles. A dirt carriageway occupies the authored width; five grassy shoulder bands rise
    /// 40 cm at a time on each side, turning a hard road cut into a roughly 2 m feathered terrain
    /// transition. The 4 dm vertical step matches the smooth renderer's four-voxel source step.
    /// The market square remains a shallow level cut/fill pad and is applied after the roads.
    /// </summary>
    public static class KentridgeTownSurfaceCatalogue
    {
        private const int RoadFillDepthDm = 6;
        private const int PlazaFillDepthDm = 12;
        private const int SurfaceThicknessDm = 2;
        private const int ClearAboveDm = 36;
        private const int PlazaFootprintHeightDm = 56;
        private const int ShoulderCount = 5;
        private const int ShoulderWidthDm = 6;
        private const int ShoulderRiseDm = 4;

        private readonly struct RoadBuild
        {
            public readonly int3 Footprint;
            public readonly ExplicitPlacement Placement;
            public readonly int CarriageWidth;
            public readonly int ShoulderWidth;
            public readonly int Length;
            public readonly int HeightDelta;
            public readonly byte Axis;

            public RoadBuild(int3 footprint, ExplicitPlacement placement,
                             int carriageWidth, int shoulderWidth, int length,
                             int heightDelta, byte axis)
            {
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
                    MaxSlope = 24,
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
                    MaxPrimitives = 1 + (1 + ShoulderCount * 2) * 2,
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
                    "The continuous Kentridge road backend expects one straight segment: "
                    + street.Id);

            Int2 a = street.Points[0];
            Int2 b = street.Points[1];
            int carriageWidth = street.WidthDm * scale;
            int shoulderWidth = ShoulderCount * ShoulderWidthDm * scale;
            int totalWidth = carriageWidth + shoulderWidth * 2;
            int fillDepth = RoadFillDepthDm * scale;
            int surfaceThickness = SurfaceThicknessDm * scale;
            int clearAbove = ClearAboveDm * scale;
            int maxShoulderRise = ShoulderCount * ShoulderRiseDm * scale;

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
                int vertical = fillDepth + surfaceThickness + delta
                             + maxShoulderRise + clearAbove;

                return new RoadBuild(
                    new int3(totalWidth, vertical, length),
                    new ExplicitPlacement
                    {
                        Position = new int3(
                            centreX - totalWidth / 2,
                            low - fillDepth,
                            minZ),
                        Orientation = orientation,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    },
                    carriageWidth, shoulderWidth, length, delta, axis: 2);
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
                int vertical = fillDepth + surfaceThickness + delta
                             + maxShoulderRise + clearAbove;

                return new RoadBuild(
                    new int3(length, vertical, totalWidth),
                    new ExplicitPlacement
                    {
                        Position = new int3(
                            minX,
                            low - fillDepth,
                            centreZ - totalWidth / 2),
                        Orientation = orientation,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    },
                    carriageWidth, shoulderWidth, length, delta, axis: 0);
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
            int fillHeight = (RoadFillDepthDm + SurfaceThicknessDm) * s;
            int maxShoulderRise = ShoulderCount * ShoulderRiseDm * s;
            int clearHeight = road.HeightDelta + maxShoulderRise + ClearAboveDm * s;
            int crossTotal = road.CarriageWidth + road.ShoulderWidth * 2;
            int sx = road.Axis == 0 ? road.Length : crossTotal;
            int sz = road.Axis == 2 ? road.Length : crossTotal;
            byte roadSurface = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte shoulderSurface = settings.Materials.Resolve(MaterialRole.Moss);

            var b = new ProgramBuilder();

            // First cut one broad corridor down to the carriageway's low endpoint. Shoulder bands
            // are filled back upward below, producing a stepped cross-slope without leaving the
            // original terrain suspended over the new road.
            b.Carve(0, fillHeight, 0, sx, clearHeight, sz);

            int centreCross = road.ShoulderWidth;
            AddLongitudinalStrip(b, road, centreCross, road.CarriageWidth,
                                 fillHeight, roadSurface);

            int bandWidth = ShoulderWidthDm * s;
            for (int band = 1; band <= ShoulderCount; band++)
            {
                int rise = band * ShoulderRiseDm * s;
                int leftCross = road.ShoulderWidth - band * bandWidth;
                int rightCross = road.ShoulderWidth + road.CarriageWidth
                               + (band - 1) * bandWidth;
                AddLongitudinalStrip(b, road, leftCross, bandWidth,
                                     fillHeight + rise, shoulderSurface);
                AddLongitudinalStrip(b, road, rightCross, bandWidth,
                                     fillHeight + rise, shoulderSurface);
            }

            return b.Finish();
        }

        private static void AddLongitudinalStrip(ProgramBuilder b, RoadBuild road,
                                                 int crossOffset, int crossWidth,
                                                 int baseHeight, byte material)
        {
            if (road.Axis == 0)
            {
                b.Box(0, 0, crossOffset, road.Length, baseHeight, crossWidth, material);
                if (road.HeightDelta > 0)
                    b.Ramp(0, baseHeight, crossOffset,
                           road.Length, road.HeightDelta, crossWidth,
                           axis: 0, material: material);
                return;
            }

            b.Box(crossOffset, 0, 0, crossWidth, baseHeight, road.Length, material);
            if (road.HeightDelta > 0)
                b.Ramp(crossOffset, baseHeight, 0,
                       crossWidth, road.HeightDelta, road.Length,
                       axis: 2, material: material);
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
                MaxSlope = 24,
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
