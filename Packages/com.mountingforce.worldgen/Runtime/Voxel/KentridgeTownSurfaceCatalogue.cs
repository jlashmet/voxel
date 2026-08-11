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
    /// Voxel backend for Kentridge's public-space plan. Streets are tessellated into small,
    /// terrain-following tiles and the market square gets one shallow cut/fill pad. This pass is
    /// intentionally separate from building grammar and is composed before buildings.
    /// </summary>
    public static class KentridgeTownSurfaceCatalogue
    {
        private const int RoadTileDm = 64;
        private const int RoadStepDm = 56;
        private const int RoadFillDepthDm = 6;
        private const int PlazaFillDepthDm = 12;
        private const int SurfaceThicknessDm = 2;
        private const int ClearAboveDm = 36;
        private const int SurfaceFootprintHeightDm = 56;

        private readonly struct SurfaceTile
        {
            public readonly Int2 OriginDm;
            public readonly byte Orientation;

            public SurfaceTile(Int2 originDm, byte orientation)
            {
                OriginDm = originDm;
                Orientation = orientation;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            int streetCount = plan.Streets.Count;
            int definitionCount = streetCount + 1;
            int scale = settings.VoxelsPerDecimetre;

            var programs = new int[definitionCount][];
            var tiles = new List<SurfaceTile>[streetCount];
            int totalPlacements = 1;
            int programLength = 0;

            for (int i = 0; i < streetCount; i++)
            {
                PlannedStreet street = plan.Streets[i];
                programs[i] = RoadProgram(street.WidthDm, settings);
                programLength += programs[i].Length;
                tiles[i] = Tessellate(street);
                totalPlacements += tiles[i].Count;
            }

            programs[streetCount] = PlazaProgram(plan.Plaza, settings);
            programLength += programs[streetCount].Length;

            var catalogue = CatalogueLoader.Allocate(
                definitions: definitionCount,
                rules: definitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: totalPlacements,
                overrides: 0,
                allocator);

            int programOffset = 0;
            int placementOffset = 0;

            for (int i = 0; i < streetCount; i++)
            {
                PlannedStreet street = plan.Streets[i];
                int[] program = programs[i];
                CopyProgram(ref catalogue, programOffset, program);

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-road-" + street.Id),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(
                        RoadTileDm * scale,
                        SurfaceFootprintHeightDm * scale,
                        RoadTileDm * scale),
                    MaxSlope = 8,
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
                    MaxPrimitives = 2,
                };

                List<SurfaceTile> streetTiles = tiles[i];
                for (int t = 0; t < streetTiles.Count; t++)
                    catalogue.ExplicitPlacements[placementOffset + t] =
                        ResolveRoadPlacement(streetTiles[t], seed, scale);

                catalogue.Rules[i] = ExplicitRule(i, placementOffset, streetTiles.Count);
                placementOffset += streetTiles.Count;
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
                    SurfaceFootprintHeightDm * scale,
                    plan.Plaza.SizeDm.Y * scale),
                MaxSlope = 8,
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

            catalogue.ExplicitPlacements[placementOffset] =
                ResolvePlazaPlacement(plan.Plaza, seed, scale);
            catalogue.Rules[plazaDefinition] = ExplicitRule(plazaDefinition, placementOffset, 1);

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge surface catalogue failed validation: " + result);
            }

            return catalogue;
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
                MaxSlope = 8,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = offset,
                ExplicitCount = count,
            };
        }

        private static List<SurfaceTile> Tessellate(PlannedStreet street)
        {
            var result = new List<SurfaceTile>();

            for (int p = 0; p + 1 < street.Points.Count; p++)
            {
                Int2 a = street.Points[p];
                Int2 b = street.Points[p + 1];

                if (a.X == b.X)
                {
                    AddVerticalTiles(result, a.X, a.Y, b.Y);
                }
                else if (a.Y == b.Y)
                {
                    AddHorizontalTiles(result, a.Y, a.X, b.X);
                }
                else
                {
                    throw new InvalidOperationException(
                        "The first Kentridge road backend supports orthogonal street segments only: "
                        + street.Id);
                }
            }

            return result;
        }

        private static void AddVerticalTiles(List<SurfaceTile> result, int centreX, int z0, int z1)
        {
            int min = Math.Min(z0, z1);
            int max = Math.Max(z0, z1);
            int cross = centreX - RoadTileDm / 2;
            AddAlong(result, min, max, along =>
                new SurfaceTile(new Int2(cross, along - RoadTileDm / 2), 0));
        }

        private static void AddHorizontalTiles(List<SurfaceTile> result, int centreZ, int x0, int x1)
        {
            int min = Math.Min(x0, x1);
            int max = Math.Max(x0, x1);
            int cross = centreZ - RoadTileDm / 2;
            AddAlong(result, min, max, along =>
                new SurfaceTile(new Int2(along - RoadTileDm / 2, cross), 1));
        }

        private static void AddAlong(List<SurfaceTile> result, int min, int max,
                                     Func<int, SurfaceTile> makeTile)
        {
            int last = int.MinValue;

            for (int along = min; along <= max; along += RoadStepDm)
            {
                result.Add(makeTile(along));
                last = along;
            }

            if (last != max)
                result.Add(makeTile(max));
        }

        private static ExplicitPlacement ResolveRoadPlacement(
            SurfaceTile tile, uint seed, int scale)
        {
            int centreX = (tile.OriginDm.X + RoadTileDm / 2) * scale;
            int centreZ = (tile.OriginDm.Y + RoadTileDm / 2) * scale;
            int targetY = TerrainSampler.HeightAt(centreX, centreZ, seed);

            return new ExplicitPlacement
            {
                Position = new int3(
                    tile.OriginDm.X * scale,
                    targetY - RoadFillDepthDm * scale,
                    tile.OriginDm.Y * scale),
                Orientation = tile.Orientation,
                OverrideOffset = 0,
                OverrideCount = 0,
            };
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

        private static int[] RoadProgram(int widthDm, VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int x = (RoadTileDm - widthDm) * s / 2;
            int width = widthDm * s;
            int length = RoadTileDm * s;
            int fillHeight = (RoadFillDepthDm + SurfaceThicknessDm) * s;
            int clearHeight = ClearAboveDm * s;
            byte surface = settings.Materials.Resolve(MaterialRole.RoadSurface);

            var b = new ProgramBuilder();
            b.Carve(x, fillHeight, 0, width, clearHeight, length);
            b.Box(x, 0, 0, width, fillHeight, length, surface);
            return b.Finish();
        }

        private static int[] PlazaProgram(PlannedPlaza plaza, VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int fillHeight = (PlazaFillDepthDm + SurfaceThicknessDm) * s;
            int clearHeight = ClearAboveDm * s;
            byte surface = settings.Materials.Resolve(MaterialRole.RoadSurface);

            var b = new ProgramBuilder();
            b.Carve(0, fillHeight, 0, plaza.SizeDm.X * s, clearHeight, plaza.SizeDm.Y * s);
            b.Box(0, 0, 0, plaza.SizeDm.X * s, fillHeight, plaza.SizeDm.Y * s, surface);
            return b.Finish();
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (int i = 0; i < program.Length; i++)
                catalogue.Program[offset + i] = program[i];
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            PrimitiveMode mode = PrimitiveMode.Fill) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)mode);

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

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
