using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Ensures MipBuilder builds incrementally — a single brick dirty-flag triggers
    /// only the affected mip levels, never a full recompute.  This is what keeps edit
    /// cost independent of world size (Constitution Principle V).
    ///
    /// Mip storage is caller-owned: MipBuilder writes into a jagged array with one
    /// NativeArray per level, sized (RegionEdge >> level)^3. Region itself holds no mips.
    /// </summary>
    public sealed class MipBuilderTests
    {
        private const int Levels = 4;

        /// <summary>Allocates per-level mip storage for a region.</summary>
        private static NativeArray<ulong>[] AllocateMips(int levels)
        {
            var mips = new NativeArray<ulong>[levels];
            for (int l = 0; l < levels; l++)
                mips[l] = new NativeArray<ulong>(MipBuilder.TotalCellCount(l), Allocator.Temp);

            return mips;
        }

        private static void DisposeMips(NativeArray<ulong>[] mips)
        {
            for (int l = 0; l < mips.Length; l++)
                if (mips[l].IsCreated) mips[l].Dispose();
        }

        /// <summary>A region with exactly one fully occupied mixed brick at the origin.</summary>
        private static Region MakeRegionWithOneFullBrick(ref BrickPool pool, out int brickIndex)
        {
            var region = new Region(int3.zero, Allocator.Temp);

            var poolIndex = pool.Allocate();
            pool.FillBrick(poolIndex, 1);

            brickIndex = Region.BrickIndex(0, 0, 0);
            region.BrickRefs[brickIndex] = BrickRef.FromPoolIndex(poolIndex);

            return region;
        }

        [Test]
        public void FullBuildProducesCorrectMipChain()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = MakeRegionWithOneFullBrick(ref pool, out int brickIndex);
            var mips = AllocateMips(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, mips);

            Assert.AreNotEqual(0UL, mips[0][brickIndex],
                "Level-0 cell for an occupied brick must be non-zero.");

            // Occupancy must propagate all the way up the chain by bitwise OR.
            for (int lvl = 1; lvl < Levels; lvl++)
            {
                Assert.AreNotEqual(0UL, mips[lvl][0],
                    $"Level-{lvl} parent of an occupied brick must be non-zero.");
            }

            DisposeMips(mips);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void EmptyRegionProducesEmptyMipChain()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            var mips = AllocateMips(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, mips);

            for (int lvl = 0; lvl < Levels; lvl++)
            {
                for (int i = 0; i < mips[lvl].Length; i++)
                    Assert.AreEqual(0UL, mips[lvl][i], $"Level-{lvl} cell {i} must be empty.");
            }

            DisposeMips(mips);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void DirtyRebuildTouchesOnlyAffectedCells()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = MakeRegionWithOneFullBrick(ref pool, out int brickIndex);
            var mips = AllocateMips(Levels);

            // Establish a clean baseline.
            MipBuilder.RebuildFull(in pool, region, Levels, mips);

            // Now clear that brick and rebuild only it.
            pool.Free(region.BrickRefs[brickIndex].PoolIndex);
            region.BrickRefs[brickIndex] = BrickRef.Empty;

            var dirty = new NativeHashSet<int>(4, Allocator.Temp);
            dirty.Add(brickIndex);

            using var changed = MipBuilder.RebuildDirty(
                in pool, region, in dirty, Levels, mips, Allocator.Temp);

            Assert.AreEqual(0UL, mips[0][brickIndex],
                "Clearing a brick must zero its level-0 cell.");

            dirty.Dispose();
            DisposeMips(mips);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void DirtyRebuildWithNoDirtyBricksDoesNothing()
        {
            // The incremental guarantee: an empty dirty set must not trigger a full
            // recompute. If it did, edit cost would scale with world size.
            var pool = new BrickPool(256, Allocator.Temp);
            var region = MakeRegionWithOneFullBrick(ref pool, out int brickIndex);
            var mips = AllocateMips(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, mips);
            ulong before = mips[0][brickIndex];

            var dirty = new NativeHashSet<int>(4, Allocator.Temp);

            using var changed = MipBuilder.RebuildDirty(
                in pool, region, in dirty, Levels, mips, Allocator.Temp);

            Assert.AreEqual(0, changed.Length, "No dirty bricks means no changed cells.");
            Assert.AreEqual(before, mips[0][brickIndex], "Mip data must be untouched.");

            dirty.Dispose();
            DisposeMips(mips);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void BrickToCellCollapsesTowardTheTop()
        {
            // Each level halves the grid, so distinct bricks converge on a shared parent.
            var a = MipBuilder.BrickToCell(0, 0, 0, 1);
            var b = MipBuilder.BrickToCell(1, 0, 0, 1);

            Assert.IsTrue(math.all(a == b),
                "Adjacent bricks must share a level-1 parent cell.");

            var far = MipBuilder.BrickToCell(VoxelDimensions.RegionEdge - 1, 0, 0, 1);
            Assert.IsFalse(math.all(a == far),
                "Bricks at opposite ends of the region must not share a level-1 cell.");
        }
    }
}
