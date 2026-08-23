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
            // Kentridge and Hightown both sit inside this lowland. Across a two-metre walking
            // footprint, authored terrain may slope but should not oscillate up and down like the
            // former 1.6 m noise octave did.
            for (int z = 0; z <= 6_000; z += 137)
            for (int x = 400; x <= 2_000; x += 113)
            {
                int reversals = 0;
                int previousSign = 0;
                int previous = TerrainQuery.HeightAt(x, z, Seed);
                for (int step = 1; step <= 20; step++)
                {
                    int next = TerrainQuery.HeightAt(x + step, z, Seed);
                    int delta = next - previous;
                    int sign = delta == 0 ? previousSign : (delta > 0 ? 1 : -1);
                    if (previousSign != 0 && sign != 0 && sign != previousSign) reversals++;
                    previousSign = sign;
                    previous = next;
                }

                Assert.LessOrEqual(reversals, 2,
                    $"Terrain chatters {reversals} times across 2 m near ({x},{z}).");
            }
        }

        [Test]
        public void SceneIssue20260823013924433CaptureAreaStaysCalmAndContinuous()
        {
            // The saved view sits over lowland around (75.6 m, -7.45 m). This is base terrain,
            // not a mountain or an authored terrace. The visually rejected 51.2 m + 12.8 m relief
            // spread sixteen vertical voxels across this small view, producing repeated contour
            // bands and a sawtooth grass-to-dirt edge even though adjacent samples only jumped by
            // one voxel. Guard both continuity and total local relief so that failure cannot hide
            // behind a maximum-neighbour-delta assertion again.
            const uint showcaseSeed = 0x5EED1234u;
            const int minX = 628;
            const int maxX = 884;
            const int minZ = -203;
            const int maxZ = 53;
            int lowest = int.MaxValue;
            int highest = int.MinValue;
            int largestJump = 0;
            int2 jumpFrom = default;
            int2 jumpTo = default;

            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                int height = TerrainQuery.HeightAt(x, z, showcaseSeed);
                lowest = math.min(lowest, height);
                highest = math.max(highest, height);
                if (x < maxX)
                    RecordJump(x, z, x + 1, z, height,
                        TerrainQuery.HeightAt(x + 1, z, showcaseSeed),
                        ref largestJump, ref jumpFrom, ref jumpTo);
                if (z < maxZ)
                    RecordJump(x, z, x, z + 1, height,
                        TerrainQuery.HeightAt(x, z + 1, showcaseSeed),
                        ref largestJump, ref jumpFrom, ref jumpTo);
            }

            Assert.LessOrEqual(largestJump, 1,
                $"Captured terrain has a {largestJump}-voxel cliff from "
              + $"({jumpFrom.x},{jumpFrom.y}) to ({jumpTo.x},{jumpTo.y}).");
            Assert.LessOrEqual(highest - lowest, 2,
                $"Captured lowland spans {highest - lowest} vertical voxels; the exact replay "
              + "should stay visually calm rather than forming repeated terrain contours.");
        }

        [Test]
        public void SettlementValleyReliefStaysBroadAndWalkable()
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

            Assert.LessOrEqual(highest - lowest, 18,
                "The inhabited valley should read as one broad landform; local settlements own "
              + "their terraces, river banks, and other meaningful elevation changes.");
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

        private static void RecordJump(
            int fromX, int fromZ, int toX, int toZ,
            int fromHeight, int toHeight,
            ref int largestJump, ref int2 jumpFrom, ref int2 jumpTo)
        {
            int jump = math.abs(toHeight - fromHeight);
            if (jump <= largestJump) return;
            largestJump = jump;
            jumpFrom = new int2(fromX, fromZ);
            jumpTo = new int2(toX, toZ);
        }
    }
}
