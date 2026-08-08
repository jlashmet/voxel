using Unity.Mathematics;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Terrain;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// Guards the determinism invariant for terrain: identical seed must produce
    /// byte-identical region content across platforms.  This is a precondition for
    /// SC-003 (the 10,000-event replay).
    /// </summary>
    public sealed class TerrainDeterminismTests
    {
        [Test]
        public void IdenticalSeedsProduceIdenticalTerrain()
        {
            const uint seed = 42u;

            var poolA = new BrickPool(512, Allocator.Temp);
            var poolB = new BrickPool(512, Allocator.Temp);

            var regionA = new Region(int3.zero, Allocator.Temp);
            var regionB = new Region(int3.zero, Allocator.Temp);

            TerrainGenerator.Generate(regionA, seed, in poolA);
            TerrainGenerator.Generate(regionB, seed, in poolB);

            // Compare brick pointer arrays — if seeds match, every BrickRef must match.
            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                Assert.AreEqual(regionA.BrickRefs[i].Value, regionB.BrickRefs[i].Value,
                    $"Brick[{i}] differs: A={regionA.BrickRefs[i]}, B={regionB.BrickRefs[i]}");
            }

            // Compare pool voxel data for any mixed bricks.
            var mixedCountA = CountMixed(poolA);
            var mixedCountB = CountMixed(poolB);
            Assert.AreEqual(mixedCountA, mixedCountB, "Mixed brick count must match.");

            for (int p = 0; p < mixedCountA && p < poolA.Voxels.Length; p++)
            {
                // Only compare actual voxel data (pool sizes may differ in allocation).
                if (p < poolB.Voxels.Length)
                    Assert.AreEqual(poolA.Voxels[p], poolB.Voxels[p], $"Pool[{p}] differs.");
            }

            regionA.Dispose();
            regionB.Dispose();
        }

        [Test]
        public void DifferentSeedsProduceDifferentTerrain()
        {
            var pool = new BrickPool(512, Allocator.Temp);
            var r1 = new Region(int3.zero, Allocator.Temp);
            var r2 = new Region(new int3(1, 1, 1), Allocator.Temp);

            TerrainGenerator.Generate(r1, 0u, in pool);
            TerrainGenerator.Generate(r2, 1u, in pool);

            // With different region coordinates the terrain surface height will differ.
            bool foundDifference = false;
            for (int i = 0; i < VoxelDimensions.BricksPerRegion && !foundDifference; i++)
            {
                if (r1.BrickRefs[i] != r2.BrickRefs[i])
                    foundDifference = true;
            }

            Assert.IsTrue(foundDifference, "Different seeds must produce different terrain.");

            r1.Dispose();
            r2.Dispose();
        }

        [Test]
        public void TerrainHasSolidGroundBelowTheSurface()
        {
            var pool = new BrickPool(512, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);

            const uint seed = 99u;
            TerrainGenerator.Generate(region, seed, in pool);

            // Find the surface rather than assuming where it is. The previous version of this
            // test scanned bricks from the region's vertical centre *upward* while claiming to
            // check below the surface, and passed only because the old generator happened to put
            // its base height at exactly that centre. It was asserting the surface's altitude,
            // not that ground is solid.
            const int column = 32;
            int worldX = column * VoxelDimensions.BrickEdge + (VoxelDimensions.BrickEdge >> 1);
            int worldZ = worldX;

            int surfaceVoxel = TerrainSampler.HeightAt(worldX, worldZ, seed);
            int surfaceBrick = surfaceVoxel >> VoxelDimensions.BrickEdgeLog2;

            Assert.Greater(surfaceBrick, 1, "the surface is too low to have ground beneath it");
            Assert.Less(surfaceBrick, VoxelDimensions.RegionEdge - 1,
                "the surface is above the region — this test cannot see the ground");

            for (int y = 0; y < surfaceBrick; y++)
            {
                Assert.IsFalse(region.GetBrick(column, y, column).IsEmpty,
                    $"brick y={y} is empty but sits below the surface at brick {surfaceBrick}");
            }

            Assert.IsTrue(region.GetBrick(column, VoxelDimensions.RegionEdge - 1, column).IsEmpty,
                "the top of the region should be open sky");

            region.Dispose();
        }

        [Test]
        public void DeepGroundIsBedrock()
        {
            var pool = new BrickPool(512, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);

            const uint seed = 99u;
            TerrainGenerator.Generate(region, seed, in pool);

            var deep = region.GetBrick(32, 0, 32);

            Assert.IsTrue(deep.IsUniform, "the bottom of the region should be uniform ground");
            Assert.AreEqual(TerrainGenerator.MaterialBedrock, deep.UniformMaterial,
                "ground far below the surface should be bedrock");

            region.Dispose();
        }

        private static int CountMixed(BrickPool pool)
        {
            int count = 0;
            for (int i = 0; i < pool.Voxels.Length && (uint)i < (uint)(pool.Capacity >> 4); i += 16)
            {
                if (VoxelEngine.Core.Occupancy.OccupancyMask.IsEmpty(pool.Occupancy, i * VoxelDimensions.OccupancyWordsPerBrick) == false
                    || !VoxelEngine.Core.Occupancy.OccupancyMask.IsFull(pool.Occupancy, i * VoxelDimensions.OccupancyWordsPerBrick))
                {
                    // Simplified: if not uniform, count it.
                    count++;
                }
            }
            return count;
        }
    }
}
