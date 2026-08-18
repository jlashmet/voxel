using System;
using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Terrain.Runtime;

namespace VoxelEngine.CI
{
    internal static partial class KentridgeUnifiedCaptureV2
    {
        private static List<int3> ChunkSeeds(int minX, int maxX, int minZ, int maxZ)
        {
            int edge = CpuTransvoxelChunkCache.BaseVoxelsPerAxis;
            int bricks = CpuTransvoxelChunkCache.BaseBricksPerAxis;
            int interior = Math.Max(1, bricks / 2);
            int minCX = FloorDiv(minX, edge) - 1;
            int maxCX = FloorDiv(maxX, edge) + 1;
            int minCZ = FloorDiv(minZ, edge) - 1;
            int maxCZ = FloorDiv(maxZ, edge) + 1;
            int maxCY = FloorDiv(TerrainSampler.MaxHeight, edge);
            var result = new List<int3>();
            for (int cy = 0; cy <= maxCY; cy++)
            for (int cz = minCZ; cz <= maxCZ; cz++)
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                result.Add(new int3(
                    cx * bricks + interior,
                    cy * bricks + interior,
                    cz * bricks + interior));
            }
            return result;
        }

        private static void LoadTerrain(int minX, int maxX, int minZ, int maxZ,
                                        ref RegionTable table)
        {
            int minRX = (minX >> VoxelDimensions.RegionVoxelEdgeLog2) - 1;
            int maxRX = (maxX >> VoxelDimensions.RegionVoxelEdgeLog2) + 1;
            int minRZ = (minZ >> VoxelDimensions.RegionVoxelEdgeLog2) - 1;
            int maxRZ = (maxZ >> VoxelDimensions.RegionVoxelEdgeLog2) + 1;
            var generation = new RegionGenerationStore(in table);
            for (int rz = minRZ; rz <= maxRZ; rz++)
            for (int rx = minRX; rx <= maxRX; rx++)
            {
                int3 regionCoord = new int3(rx, 0, rz);
                TerrainGenerator.Generate(generation, regionCoord, Seed, CaptureTerrainMaterials.Default);
            }
        }

        private static float SurfaceY(int xDm, int zDm)
        {
            int natural = TerrainSampler.HeightAt(xDm, zDm, Seed);
            int authored = KentridgeVerticalProfile.SurfaceYAtDm(xDm, zDm, Seed, 1);
            return Math.Max(natural, authored) * VoxelSize;
        }

        private static MaterialPalette BuildMaterialPalette()
        {
            MaterialPalette palette = default;
            for (byte material = 1; material < MaterialCount; material++)
                palette.Register(material, 128, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            return palette;
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }

        private static void TownBounds(SettlementPlan plan,
                                       out int minX, out int maxX, out int minZ, out int maxZ)
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
                Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                minX = Math.Min(minX, plot.PositionDm.X);
                maxX = Math.Max(maxX, plot.PositionDm.X + footprint.X);
                minZ = Math.Min(minZ, plot.PositionDm.Y);
                maxZ = Math.Max(maxZ, plot.PositionDm.Y + footprint.Z);
            }
            minX -= 96;
            maxX += 96;
            minZ -= 96;
            maxZ += 96;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }
    }
}

namespace VoxelEngine.CI
{
    /// <summary>
    /// Terrain material slots for the capture tools.
    ///
    /// TerrainGenerator takes the material set explicitly now: the engine generates from opaque
    /// indices and the game owns their meaning. These captures render the game world, so the
    /// indices match Game.Materials.Runtime's GameTerrainMaterials.Default. They are duplicated
    /// rather than referenced because VoxelEngine.CI.Editor is an engine assembly and
    /// EngineGameDependencyBoundaryTests forbids a dependency on the game layer.
    /// </summary>
    internal static class CaptureTerrainMaterials
    {
        internal static readonly VoxelEngine.Terrain.Api.TerrainMaterialSet Default =
            new VoxelEngine.Terrain.Api.TerrainMaterialSet(5, 1, 3); // Bedrock, Stone, Sand
    }
}
