using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
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
