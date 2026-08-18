using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Occupancy-summary invalidation under edits.
    ///
    /// The property that matters is that destruction cost scales with the region touched rather
    /// than the number of bricks in it: a collapsing wall shares ancestors, and rebuilding those
    /// once per falling brick is how a cheap bitwise OR turns into a frame spike.
    /// </summary>
    public sealed class GpuBrickMipInvalidationTests
    {
        [Test]
        public void EachLevelHalvesResolution()
        {
            Assert.AreEqual(new int3(1, 1, 1), GpuBrickMipInvalidation.AncestorOf(new int3(2, 3, 2), 0));
            Assert.AreEqual(new int3(0, 0, 0), GpuBrickMipInvalidation.AncestorOf(new int3(3, 3, 3), 1));
            Assert.AreEqual(new int3(1, 0, 0), GpuBrickMipInvalidation.AncestorOf(new int3(4, 0, 0), 1));
        }

        [Test]
        public void NegativeCoordinatesDoNotFoldOntoTheOrigin()
        {
            // The world extends both ways from the origin. A divide would put -1 and 0 in the same
            // parent and quietly merge two halves of the world into one summary.
            Assert.AreNotEqual(
                GpuBrickMipInvalidation.AncestorOf(new int3(-1, 0, 0), 0),
                GpuBrickMipInvalidation.AncestorOf(new int3(0, 0, 0), 0));
            Assert.AreEqual(new int3(-1, -1, -1),
                GpuBrickMipInvalidation.AncestorOf(new int3(-1, -1, -1), 0));
            Assert.AreEqual(new int3(-1, 0, 0),
                GpuBrickMipInvalidation.AncestorOf(new int3(-2, 0, 0), 0));
        }

        [Test]
        public void OneEditMarksItsWholeAncestorChain()
        {
            var invalidation = new GpuBrickMipInvalidation(levelCount: 4);

            invalidation.MarkBrick(new int3(5, 5, 5));

            Assert.AreEqual(4, invalidation.PendingCount);
            for (int level = 0; level < 4; level++)
                Assert.IsTrue(invalidation.IsPending(
                    level, GpuBrickMipInvalidation.AncestorOf(new int3(5, 5, 5), level)));
        }

        [Test]
        public void NeighbouringBricksShareAncestorsInsteadOfMultiplyingWork()
        {
            var invalidation = new GpuBrickMipInvalidation(levelCount: 4);

            // Eight bricks in one 2x2x2 cell: level 0 collapses them to a single node.
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
                invalidation.MarkBrick(new int3(x, y, z));

            Assert.AreEqual(1, invalidation.PendingAt(0),
                "Eight bricks in one cell share one summary; rebuilding it eight times is the "
              + "difference between a bitwise OR and a frame spike.");
            Assert.AreEqual(4, invalidation.PendingCount);
            Assert.AreEqual(28UL, invalidation.CoalescedCount, "8 bricks x 4 levels, 4 distinct");
        }

        [Test]
        public void DrainRespectsItsBudgetAndRemovesWhatItTook()
        {
            var invalidation = new GpuBrickMipInvalidation(levelCount: 2);
            for (int x = 0; x < 10; x++) invalidation.MarkBrick(new int3(x * 2, 0, 0));

            int atLevelZero = invalidation.PendingAt(0);
            var drained = new List<int3>();
            int count = invalidation.Drain(0, 3, drained);

            Assert.AreEqual(3, count);
            Assert.AreEqual(3, drained.Count);
            Assert.AreEqual(atLevelZero - 3, invalidation.PendingAt(0));
        }

        [Test]
        public void WhatIsNotDrainedSimplyWaits()
        {
            var invalidation = new GpuBrickMipInvalidation(levelCount: 1);
            for (int x = 0; x < 5; x++) invalidation.MarkBrick(new int3(x * 2, 0, 0));

            var drained = new List<int3>();
            invalidation.Drain(0, 2, drained);

            Assert.AreEqual(3, invalidation.PendingAt(0),
                "A stale coarse summary over-reports occupancy, costing a few wasted ray steps. "
              + "It never under-reports into a hole, so deferring is safe.");
        }

        [Test]
        public void CoarsestLevelsAreRebuiltFirstUnderATightBudget()
        {
            var invalidation = new GpuBrickMipInvalidation(levelCount: 3);
            invalidation.MarkBrick(new int3(1, 1, 1));

            var destinations = new[] { new List<int3>(), new List<int3>(), new List<int3>() };
            int drained = invalidation.DrainCoarsestFirst(1, destinations);

            Assert.AreEqual(1, drained);
            Assert.AreEqual(1, destinations[2].Count,
                "The coarse node is what covers the view while finer detail is pending, so a tight "
              + "budget should buy that one first.");
            Assert.AreEqual(0, destinations[0].Count);
        }

        [Test]
        public void DrainingEverythingLeavesNothingPending()
        {
            var invalidation = new GpuBrickMipInvalidation(levelCount: 3);
            invalidation.MarkBrick(new int3(7, 7, 7));
            invalidation.MarkBrick(new int3(70, 2, 9));

            var destinations = new[] { new List<int3>(), new List<int3>(), new List<int3>() };
            invalidation.DrainCoarsestFirst(1000, destinations);

            Assert.AreEqual(0, invalidation.PendingCount);
        }

        [Test]
        public void AWallCollapseCostsTheRegionItTouchedNotTheBrickCount()
        {
            var invalidation = new GpuBrickMipInvalidation(levelCount: 6);

            // 16x16 bricks of wall coming down at once.
            for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                invalidation.MarkBrick(new int3(x, y, 0));

            Assert.AreEqual(256 * 6, (int)invalidation.CoalescedCount + invalidation.PendingCount);
            Assert.Less(invalidation.PendingCount, 256,
                "256 bricks fell, but the summaries above them overlap heavily, so the rebuild "
              + "cost tracks the volume touched rather than the number of bricks in it.");
        }

        [Test]
        public void ClearDropsEverything()
        {
            var invalidation = new GpuBrickMipInvalidation(levelCount: 3);
            invalidation.MarkBrick(new int3(1, 2, 3));

            invalidation.Clear();

            Assert.AreEqual(0, invalidation.PendingCount);
            Assert.AreEqual(0UL, invalidation.CoalescedCount);
        }
    }
}
