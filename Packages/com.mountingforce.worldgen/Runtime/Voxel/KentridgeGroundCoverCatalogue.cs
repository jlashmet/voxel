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
    /// Repaints the existing terrain surface around Kentridge without changing occupancy.
    ///
    /// Generic terrain intentionally knows nothing about individual fantasy lands, so its native
    /// sand/stone/grass mix can run straight through a settlement. Kentridge wants a coherent
    /// temperate ground vocabulary. This pass adaptively tiles a rounded town envelope and uses
    /// PaintSurface: the rasteriser finds each column's true top solid and repaints only its top
    /// four voxels. Density, collision, caves, and silhouette stay identical while Transvoxel sees
    /// the themed material at every surface sample and still finds mineral support immediately
    /// underneath.
    ///
    /// This catalogue runs before roads, plot grading, and structures. Those later semantic layers
    /// remain authoritative where they overlap the themed ground.
    /// </summary>
    public static class KentridgeGroundCoverCatalogue
    {
        private const int LargestTileDm = 32;
        private const int MaxSurfaceVariationDm = 16;
        private const int SearchBelowDm = 4;
        private const int SearchAboveDm = 4;
        private const int SearchHeightDm =
            SearchBelowDm + MaxSurfaceVariationDm + SearchAboveDm + 1;
        private const int PaddingDm = 72;

        private static readonly int[] s_TileSizesDm = { 32, 16, 8, 4, 2, 1 };

        private readonly struct PaintTile
        {
            public readonly int SizeDm;
            public readonly int Xdm;
            public readonly int Zdm;
            public readonly int BaseY;

            public PaintTile(int sizeDm, int xDm, int zDm, int baseY)
            {
                SizeDm = sizeDm;
                Xdm = xDm;
                Zdm = zDm;
                BaseY = baseY;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            int scale = settings.VoxelsPerDecimetre;
            TownEnvelope(plan, out int minXdm, out int maxXdm,
                         out int minZdm, out int maxZdm);

            int centreXdm = (minXdm + maxXdm) / 2;
            int centreZdm = (minZdm + maxZdm) / 2;
            int radiusXdm = Math.Max(1, (maxXdm - minXdm) / 2);
            int radiusZdm = Math.Max(1, (maxZdm - minZdm) / 2);

            int alignedMinX = FloorTo(minXdm, LargestTileDm);
            int alignedMaxX = CeilTo(maxXdm, LargestTileDm);
            int alignedMinZ = FloorTo(minZdm, LargestTileDm);
            int alignedMaxZ = CeilTo(maxZdm, LargestTileDm);

            var bySize = new List<PaintTile>[s_TileSizesDm.Length];
            for (int i = 0; i < bySize.Length; i++) bySize[i] = new List<PaintTile>();

            for (int z = alignedMinZ; z < alignedMaxZ; z += LargestTileDm)
            for (int x = alignedMinX; x < alignedMaxX; x += LargestTileDm)
            {
                AddAdaptiveTile(bySize, 0, x, z, LargestTileDm,
                                centreXdm, centreZdm, radiusXdm, radiusZdm,
                                seed, scale);
            }

            int placementCount = 0;
            for (int i = 0; i < bySize.Length; i++) placementCount += bySize[i].Count;
            if (placementCount == 0)
                throw new InvalidOperationException("Kentridge ground cover produced no paint tiles.");

            var programs = new int[s_TileSizesDm.Length][];
            int programLength = 0;
            for (int i = 0; i < programs.Length; i++)
            {
                programs[i] = PaintProgram(s_TileSizesDm[i], settings);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: s_TileSizesDm.Length,
                rules: s_TileSizesDm.Length,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: placementCount,
                overrides: 0,
                allocator);

            int programOffset = 0;
            int placementOffset = 0;
            int searchHeight = SearchHeightDm * scale;

            for (int id = 0; id < s_TileSizesDm.Length; id++)
            {
                int sizeDm = s_TileSizesDm[id];
                int[] program = programs[id];
                CopyProgram(ref catalogue, programOffset, program);

                catalogue.Definitions[id] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-ground-" + sizeDm + "dm"),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(sizeDm * scale, searchHeight, sizeDm * scale),
                    MaxSlope = 32,
                    Precedence = 5,
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
                    MaxPrimitives = 1,
                };

                List<PaintTile> tiles = bySize[id];
                for (int i = 0; i < tiles.Count; i++)
                {
                    PaintTile tile = tiles[i];
                    catalogue.ExplicitPlacements[placementOffset + i] = new ExplicitPlacement
                    {
                        Position = new int3(tile.Xdm * scale, tile.BaseY, tile.Zdm * scale),
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    };
                }

                catalogue.Rules[id] = ExplicitRule(id, placementOffset, tiles.Count);
                placementOffset += tiles.Count;
                programOffset += program.Length;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge ground cover catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static void AddAdaptiveTile(
            List<PaintTile>[] bySize, int sizeIndex,
            int xDm, int zDm, int sizeDm,
            int centreXdm, int centreZdm, int radiusXdm, int radiusZdm,
            uint seed, int scale)
        {
            int half = sizeDm / 2;
            int centreX = xDm + half;
            int centreZ = zDm + half;
            if (!InsideEnvelope(centreX, centreZ,
                                centreXdm, centreZdm, radiusXdm, radiusZdm))
                return;

            SurfaceRange(xDm, zDm, sizeDm, seed, scale, out int minY, out int maxY);
            int allowedVariation = MaxSurfaceVariationDm * scale;
            if (maxY - minY <= allowedVariation
                || sizeIndex == s_TileSizesDm.Length - 1)
            {
                bySize[sizeIndex].Add(new PaintTile(
                    sizeDm, xDm, zDm, minY - SearchBelowDm * scale));
                return;
            }

            int childSize = s_TileSizesDm[sizeIndex + 1];
            for (int dz = 0; dz < sizeDm; dz += childSize)
            for (int dx = 0; dx < sizeDm; dx += childSize)
                AddAdaptiveTile(bySize, sizeIndex + 1,
                                xDm + dx, zDm + dz, childSize,
                                centreXdm, centreZdm, radiusXdm, radiusZdm,
                                seed, scale);
        }

        private static bool InsideEnvelope(int xDm, int zDm,
                                           int centreXdm, int centreZdm,
                                           int radiusXdm, int radiusZdm)
        {
            long dx = xDm - centreXdm;
            long dz = zDm - centreZdm;
            long rx2 = (long)radiusXdm * radiusXdm;
            long rz2 = (long)radiusZdm * radiusZdm;

            return dx * dx * rz2 + dz * dz * rx2 <= rx2 * rz2;
        }

        private static void SurfaceRange(int xDm, int zDm, int sizeDm,
                                         uint seed, int scale,
                                         out int minY, out int maxY)
        {
            int x0 = xDm * scale;
            int z0 = zDm * scale;
            int edge = Math.Max(1, sizeDm * scale);
            int x1 = x0 + edge - 1;
            int z1 = z0 + edge - 1;
            int xm = (x0 + x1) / 2;
            int zm = (z0 + z1) / 2;

            minY = int.MaxValue;
            maxY = int.MinValue;
            Sample(x0, z0, seed, ref minY, ref maxY);
            Sample(x1, z0, seed, ref minY, ref maxY);
            Sample(x0, z1, seed, ref minY, ref maxY);
            Sample(x1, z1, seed, ref minY, ref maxY);
            Sample(xm, zm, seed, ref minY, ref maxY);
            Sample(xm, z0, seed, ref minY, ref maxY);
            Sample(xm, z1, seed, ref minY, ref maxY);
            Sample(x0, zm, seed, ref minY, ref maxY);
            Sample(x1, zm, seed, ref minY, ref maxY);
        }

        private static void Sample(int x, int z, uint seed, ref int minY, ref int maxY)
        {
            int y = TerrainQuery.HeightAt(x, z, seed);
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        private static void TownEnvelope(SettlementPlan plan,
                                         out int minX, out int maxX,
                                         out int minZ, out int maxZ)
        {
            minX = plan.Plaza.CentreDm.X - plan.Plaza.SizeDm.X / 2;
            maxX = plan.Plaza.CentreDm.X + plan.Plaza.SizeDm.X / 2;
            minZ = plan.Plaza.CentreDm.Y - plan.Plaza.SizeDm.Y / 2;
            maxZ = plan.Plaza.CentreDm.Y + plan.Plaza.SizeDm.Y / 2;

            for (int i = 0; i < plan.Streets.Count; i++)
            {
                PlannedStreet street = plan.Streets[i];
                int radius = street.WidthDm / 2;
                for (int p = 0; p < street.Points.Count; p++)
                {
                    Int2 point = street.Points[p];
                    minX = Math.Min(minX, point.X - radius);
                    maxX = Math.Max(maxX, point.X + radius);
                    minZ = Math.Min(minZ, point.Y - radius);
                    maxZ = Math.Max(maxZ, point.Y + radius);
                }
            }

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
                minX = Math.Min(minX, plot.PositionDm.X);
                maxX = Math.Max(maxX, plot.PositionDm.X + footprint.X);
                minZ = Math.Min(minZ, plot.PositionDm.Y);
                maxZ = Math.Max(maxZ, plot.PositionDm.Y + footprint.Z);
            }

            minX -= PaddingDm;
            maxX += PaddingDm;
            minZ -= PaddingDm;
            maxZ += PaddingDm;
        }

        private static int[] PaintProgram(int sizeDm, VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte ground = settings.Materials.Resolve(MaterialRole.Moss);
            var b = new ProgramBuilder();
            b.PaintSurface(0, 0, 0,
                           sizeDm * s, SearchHeightDm * s, sizeDm * s, ground);
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
                MaxSlope = 32,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = offset,
                ExplicitCount = count,
            };
        }

        private static int FloorTo(int value, int step)
        {
            int quotient = value / step;
            int remainder = value % step;
            if (remainder < 0) quotient--;
            return quotient * step;
        }

        private static int CeilTo(int value, int step)
        {
            int floor = FloorTo(value, step);
            return floor == value ? value : floor + step;
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (int i = 0; i < program.Length; i++) catalogue.Program[offset + i] = program[i];
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void PaintSurface(int x, int y, int z,
                                     int sx, int sy, int sz, byte material) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   material, 0, 0, (int)PrimitiveMode.PaintSurface);

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
