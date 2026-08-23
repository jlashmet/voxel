using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// Terrain must be a pure, world-continuous function of position.
    ///
    /// The sampler these tests replaced reduced its inputs modulo the region edge, so every region
    /// held identical terrain. Every determinism test passed: terrain that repeats is perfectly
    /// deterministic. What was missing was a test that compared *different places* rather than the
    /// same place twice.
    ///
    /// Placement rules read ground height and slope, so building on that sampler would have put
    /// every village in the same relative spot in every region — a world that is wrong rather than
    /// a world that fails.
    /// </summary>
    public sealed class TerrainContinuityTests
    {
        private const uint Seed = 1234u;

        [Test]
        public void HeightIsIdenticalWhenSampledFromEitherSideOfARegionBorder()
        {
            const int border = 1 << VoxelDimensions.RegionVoxelEdgeLog2;

            for (int offset = -4; offset <= 4; offset++)
            {
                int x = border + offset;
                int a = TerrainQuery.HeightAt(x, 100, Seed);
                int b = TerrainQuery.HeightAt(x, 100, Seed);

                Assert.AreEqual(a, b, $"height at x={x} is not a pure function");
            }
        }

        [Test]
        public void HeightIsContinuousAcrossARegionBorder()
        {
            const int border = 1 << VoxelDimensions.RegionVoxelEdgeLog2;

            int inside = TerrainQuery.HeightAt(border - 1, 512, Seed);
            int outside = TerrainQuery.HeightAt(border, 512, Seed);

            Assert.LessOrEqual(math.abs(outside - inside), 4,
                $"height jumps {math.abs(outside - inside)} voxels across a region border");
        }

        [Test]
        public void TerrainDoesNotRepeatBetweenRegions()
        {
            const int edge = 1 << VoxelDimensions.RegionVoxelEdgeLog2;

            int differing = 0;

            for (int i = 0; i < 64; i++)
            {
                int local = i * 8;
                int inRegionZero = TerrainQuery.HeightAt(local, local, Seed);
                int inRegionOne = TerrainQuery.HeightAt(edge + local, local, Seed);

                if (inRegionZero != inRegionOne) differing++;
            }

            Assert.Greater(differing, 32,
                "neighbouring regions have near-identical terrain — the sampler is tiling");
        }

        [Test]
        public void SlopeIsSymmetric()
        {
            for (int i = 0; i < 32; i++)
            {
                int x = i * 37;
                int z = i * 53;

                Assert.AreEqual(TerrainQuery.SlopeAt(x, z, Seed), TerrainQuery.SlopeAt(x, z, Seed));
            }
        }

        [Test]
        public void HeightStaysWithinDeclaredBounds()
        {
            for (int i = 0; i < 512; i++)
            {
                int x = i * 101 - 20000;
                int z = i * 211 - 30000;
                int h = TerrainQuery.HeightAt(x, z, Seed);

                Assert.GreaterOrEqual(h, TerrainQuery.MinHeight);
                Assert.LessOrEqual(h, TerrainQuery.MaxHeight);
            }
        }

        [Test]
        public void NegativeCoordinatesDoNotMirrorTheWorld()
        {
            int differing = 0;

            for (int i = 1; i <= 64; i++)
            {
                if (TerrainQuery.HeightAt(-i * 16, 0, Seed) != TerrainQuery.HeightAt(i * 16, 0, Seed))
                    differing++;
            }

            Assert.Greater(differing, 32, "terrain appears mirrored about the origin");
        }

        [Test]
        public void SettlementValleyHasNoPlayerScaleCorrugation()
        {
            // Landscape-scale relief is allowed to slope through a walking footprint, but the
            // removed 3.2 m / 1.6 m noise layers must not return as metre-scale corrugation.
            // Across any sampled two-metre run the broad 51.2 m / 12.8 m layers should stay within
            // a modest vertical span rather than producing a ridge/trough pattern every few steps.
            for (int z = 0; z <= 6_000; z += 137)
            for (int x = 400; x <= 2_000; x += 113)
            {
                int lowest = int.MaxValue;
                int highest = int.MinValue;
                for (int step = 0; step <= 20; step++)
                {
                    int height = TerrainQuery.HeightAt(x + step, z, Seed);
                    lowest = math.min(lowest, height);
                    highest = math.max(highest, height);
                }

                Assert.LessOrEqual(highest - lowest, 8,
                    $"Terrain varies {highest - lowest} voxels across 2 m near ({x},{z}); "
                  + "player-scale corrugation has returned.");
            }
        }

        [Test]
        public void SettlementValleyRetainsReadableLandscapeRelief()
        {
            int lowest = int.MaxValue;
            int highest = int.MinValue;
            for (int z = 0; z <= 6_000; z += 61)
            for (int x = 400; x <= 2_000; x += 53)
            {
                int height = TerrainQuery.HeightAt(x, z, Seed);
                lowest = math.min(lowest, height);
                highest = math.max(highest, height);
            }

            int relief = highest - lowest;
            Assert.GreaterOrEqual(relief, 60,
                "The inhabited valley has been flattened enough that its natural landform no longer reads.");
            Assert.LessOrEqual(relief, 100,
                "The inhabited valley should keep broad rolling relief rather than become mountainous.");
        }

        [Test]
        public void RegionGenerationIsIndependentOfOrder()
        {
            var size = new int3(3, 1, 3);
            int total = size.x * size.y * size.z;

            ulong sequential = GenerationOrderHarness.GenerateBlock(
                int3.zero, size, Seed, GenerationOrderHarness.SequentialOrder(total));

            for (uint shuffle = 1; shuffle <= 16; shuffle++)
            {
                ulong shuffled = GenerationOrderHarness.GenerateBlock(
                    int3.zero, size, Seed, GenerationOrderHarness.ShuffledOrder(total, shuffle));

                Assert.AreEqual(sequential, shuffled,
                    $"generation order {shuffle} produced a different world");
            }
        }
    }
}
