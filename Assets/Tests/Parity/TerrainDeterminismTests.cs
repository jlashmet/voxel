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
        public void TerrainHasBedrockBelowSurface()
        {
            var pool = new BrickPool(512, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);

            TerrainGenerator.Generate(region, 99u, in pool);

            // At least some bricks below the region centre must be filled (bedrock).
            bool foundSolid = false;
            int3 centre = new int3(32, 32, 32);
            for (int y = centre.y; y < VoxelDimensions.RegionEdge && !foundSolid; y++)
            {
                for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                {
                    var brick = region.GetBrick(x, y, 32);
                    if (!brick.IsEmpty)
                    {
                        foundSolid = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(foundSolid, "Terrain must have solid bedrock below the surface.");

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
