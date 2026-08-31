using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime.Occupancy;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Covers the region-owned mip pyramid: its flattened layout, its lifetime, and the
    /// sampler the LOD rings read it through.
    ///
    /// The layout deliberately omits level 0. That is the load-bearing memory decision — a
    /// stored level 0 would be 2.4 MB per region and 87% of the pyramid — so these tests pin
    /// both the omission and the derivation that compensates for it.
    /// </summary>
    public sealed class RegionMipStorageTests
    {
        private const int Levels = 4;

        // -------------------------------------------------------------------------
        // Layout
        // -------------------------------------------------------------------------

        [Test]
        public void LayoutExcludesLevelZero()
        {
            int stored = RegionMipLayout.TotalStoredCells(Levels);
            int withLevel0 = stored + MipBuilder.TotalCellCount(0);

            Assert.AreEqual(0, RegionMipLayout.LevelOffset(RegionMipLayout.FirstStoredLevel),
                "The first stored level must sit at the front of the flattened array.");
            Assert.Less(stored, withLevel0 / 2,
                "Omitting level 0 must remove the bulk of the pyramid; if this ratio drifts, "
              + "the memory argument in RegionMipLayout no longer holds.");
        }

        [Test]
        public void LayoutLevelsAreContiguousAndNonOverlapping()
        {
            int expected = 0;
            for (int level = RegionMipLayout.FirstStoredLevel; level < Levels; level++)
            {
                Assert.AreEqual(expected, RegionMipLayout.LevelOffset(level),
                    $"Level {level} must begin where level {level - 1} ended.");
                expected += MipBuilder.TotalCellCount(level);
            }
            Assert.AreEqual(expected, RegionMipLayout.TotalStoredCells(Levels));
        }

        [Test]
        public void LayoutIndexIsUniquePerCell()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int level = RegionMipLayout.FirstStoredLevel; level < Levels; level++)
            {
                int edge = MipBuilder.RegionEdgeForLevel(level);
                for (int z = 0; z < edge; z++)
                for (int y = 0; y < edge; y++)
                for (int x = 0; x < edge; x++)
                {
                    int index = RegionMipLayout.Index(level, x, y, z);
                    Assert.IsTrue(seen.Add(index),
                        $"Flattened index {index} collides at level {level} cell ({x},{y},{z}).");
                }
            }
            Assert.AreEqual(RegionMipLayout.TotalStoredCells(Levels), seen.Count);
        }

        // -------------------------------------------------------------------------
        // Region lifetime
        // -------------------------------------------------------------------------

        [Test]
        public void RegionStartsWithoutMips()
        {
            var region = new Region(int3.zero, Allocator.Temp);
            Assert.IsFalse(region.HasMips,
                "A region used only for near-field collision must not pay for a pyramid.");
            region.Dispose();
        }

        [Test]
        public void AllocateMipsIsIdempotentForTheSameLevelCount()
        {
            var region = new Region(int3.zero, Allocator.Temp);
            region.AllocateMips(Levels, Allocator.Temp);
            region.OccupancyMips[0] = 1234UL;

            region.AllocateMips(Levels, Allocator.Temp);
            Assert.AreEqual(1234UL, region.OccupancyMips[0],
                "Re-allocating at the same level count must not discard existing data.");

            region.Dispose();
        }

        [Test]
        public void AllocateMipsRejectsOutOfRangeLevelCounts()
        {
            var region = new Region(int3.zero, Allocator.Temp);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => region.AllocateMips(1, Allocator.Temp),
                "A level count leaving no stored level is meaningless.");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => region.AllocateMips(MipBuilder.MaxLevels + 1, Allocator.Temp));
            region.Dispose();
        }

        [Test]
        public void ReleaseMipsLeavesBricksIntact()
        {
            var region = new Region(int3.zero, Allocator.Temp);
            region.AllocateMips(Levels, Allocator.Temp);
            region.ReleaseMips();

            Assert.IsFalse(region.HasMips);
            Assert.IsTrue(region.IsCreated,
                "Dropping the pyramid must not disturb brick storage — a near region keeps "
              + "its voxels when far-field summaries are reclaimed.");
            region.Dispose();
        }

        [Test]
        public void DisposeReleasesMips()
        {
            var region = new Region(int3.zero, Allocator.Temp);
            region.AllocateMips(Levels, Allocator.Temp);
            region.Dispose();
            Assert.IsFalse(region.HasMips);
        }

        // -------------------------------------------------------------------------
        // RebuildRegion against the array-based reference
        // -------------------------------------------------------------------------

        [Test]
        public void RebuildRegionMatchesTheArrayBasedBuilder()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);

            int poolIndex = pool.Allocate();
            pool.FillBrick(poolIndex, 6);
            region.BrickRefs[Region.BrickIndex(3, 5, 9)] = BrickRef.FromPoolIndex(poolIndex);

            int second = pool.Allocate();
            pool.FillBrick(second, 11);
            region.BrickRefs[Region.BrickIndex(2, 5, 9)] = BrickRef.FromPoolIndex(second);

            // Reference: the jagged array form, which the MipBuilder tests cover directly.
            var refOcc = new NativeArray<ulong>[Levels];
            var refMat = new NativeArray<byte>[Levels];
            for (int l = 0; l < Levels; l++)
            {
                refOcc[l] = new NativeArray<ulong>(MipBuilder.TotalCellCount(l), Allocator.Temp);
                refMat[l] = new NativeArray<byte>(MipBuilder.TotalCellCount(l), Allocator.Temp);
            }
            MipBuilder.RebuildFull(in pool, region, Levels, refOcc, refMat);

            region.AllocateMips(Levels, Allocator.Temp);
            MipBuilder.RebuildRegion(in pool, ref region);

            for (int level = RegionMipLayout.FirstStoredLevel; level < Levels; level++)
            {
                int edge = MipBuilder.RegionEdgeForLevel(level);
                for (int z = 0; z < edge; z++)
                for (int y = 0; y < edge; y++)
                for (int x = 0; x < edge; x++)
                {
                    int flat = RegionMipLayout.Index(level, x, y, z);
                    int linear = MipBuilder.CellIndex(x, y, z, edge);
                    Assert.AreEqual(refOcc[level][linear], region.OccupancyMips[flat],
                        $"Occupancy mismatch at level {level} cell ({x},{y},{z}).");
                    Assert.AreEqual(refMat[level][linear], region.MaterialMips[flat],
                        $"Material mismatch at level {level} cell ({x},{y},{z}).");
                }
            }

            for (int l = 0; l < Levels; l++) { refOcc[l].Dispose(); refMat[l].Dispose(); }
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void RebuildRegionRequiresAllocatedMips()
        {
            var pool = new BrickPool(16, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            Assert.Throws<InvalidOperationException>(
                () => MipBuilder.RebuildRegion(in pool, ref region));
            region.Dispose();
            pool.Dispose();
        }

        // -------------------------------------------------------------------------
        // Stride to level
        // -------------------------------------------------------------------------

        [Test]
        public void StridesFinerThanABrickHaveNoMipLevel()
        {
            // Rings at steps 1, 2 and 4 sample inside a brick, so no pyramid level can serve
            // them; they require resident voxels. This is the constraint that decides which
            // rings can render a region whose bricks have been evicted.
            Assert.AreEqual(-1, VoxelMipSampler.LevelForStride(1));
            Assert.AreEqual(-1, VoxelMipSampler.LevelForStride(2));
            Assert.AreEqual(-1, VoxelMipSampler.LevelForStride(4));
        }

        [Test]
        public void BrickSizedAndCoarserStridesMapToAscendingLevels()
        {
            Assert.AreEqual(0, VoxelMipSampler.LevelForStride(VoxelDimensions.BrickEdge));
            Assert.AreEqual(1, VoxelMipSampler.LevelForStride(VoxelDimensions.BrickEdge * 2));
            Assert.AreEqual(2, VoxelMipSampler.LevelForStride(VoxelDimensions.BrickEdge * 4));
            Assert.AreEqual(3, VoxelMipSampler.LevelForStride(VoxelDimensions.BrickEdge * 8));
        }

        [Test]
        public void VoxelsPerCellInvertsLevelForStride()
        {
            for (int level = 0; level < 4; level++)
            {
                int span = VoxelMipSampler.VoxelsPerCell(level);
                Assert.AreEqual(level, VoxelMipSampler.LevelForStride(span),
                    $"Level {level} spans {span} voxels and must round-trip.");
            }
        }

        // -------------------------------------------------------------------------
        // Sampling
        // -------------------------------------------------------------------------

        [Test]
        public void SamplerReadsLevelZeroFromBricksWithoutAPyramid()
        {
            var pool = new BrickPool(64, Allocator.Temp);
            var table = new RegionTable(4, Allocator.Temp);
            var region = table.LoadRegion(int3.zero);

            int poolIndex = pool.Allocate();
            pool.FillBrick(poolIndex, 5);
            region.BrickRefs[Region.BrickIndex(3, 1, 2)] = BrickRef.FromPoolIndex(poolIndex);
            table.CommitRegion(in region);

            int3 worldVoxel = new int3(3, 1, 2) * VoxelDimensions.BrickEdge;
            Assert.IsTrue(VoxelMipSampler.TrySample(ref table, in pool, worldVoxel, 0,
                                                    out bool occupied, out byte material),
                "Level 0 is derived from bricks and must answer without an allocated pyramid.");
            Assert.IsTrue(occupied);
            Assert.AreEqual((byte)5, material);

            table.Dispose();
            pool.Dispose();
        }

        [Test]
        public void SamplerReadsCoarseLevelsFromThePyramid()
        {
            var pool = new BrickPool(64, Allocator.Temp);
            var table = new RegionTable(4, Allocator.Temp);
            var region = table.LoadRegion(int3.zero);

            int poolIndex = pool.Allocate();
            pool.FillBrick(poolIndex, 5);
            region.BrickRefs[Region.BrickIndex(3, 1, 2)] = BrickRef.FromPoolIndex(poolIndex);
            region.AllocateMips(Levels, Allocator.Temp);
            MipBuilder.RebuildRegion(in pool, ref region);
            table.CommitRegion(in region);

            int3 worldVoxel = new int3(3, 1, 2) * VoxelDimensions.BrickEdge;
            Assert.IsTrue(VoxelMipSampler.TrySample(ref table, in pool, worldVoxel, 1,
                                                    out bool occupied, out byte material));
            Assert.IsTrue(occupied, "The level-1 ancestor of an occupied brick must read solid.");
            Assert.AreEqual((byte)5, material);

            table.Dispose();
            pool.Dispose();
        }

        [Test]
        public void SamplerRefusesLevelsThePyramidDoesNotHold()
        {
            var pool = new BrickPool(64, Allocator.Temp);
            var table = new RegionTable(4, Allocator.Temp);
            var region = table.LoadRegion(int3.zero);
            region.AllocateMips(Levels, Allocator.Temp);
            MipBuilder.RebuildRegion(in pool, ref region);
            table.CommitRegion(in region);

            Assert.IsFalse(VoxelMipSampler.TrySample(ref table, in pool, int3.zero, Levels,
                                                     out _, out _),
                "Asking for a level beyond the pyramid must fail rather than read garbage.");

            table.Dispose();
            pool.Dispose();
        }

        [Test]
        public void SamplerRefusesRegionsThatAreNotResident()
        {
            var pool = new BrickPool(64, Allocator.Temp);
            var table = new RegionTable(4, Allocator.Temp);

            Assert.IsFalse(VoxelMipSampler.TrySample(
                ref table, in pool, new int3(9999, 0, 0), 0, out _, out _));

            table.Dispose();
            pool.Dispose();
        }

        [Test]
        public void SamplerHandlesNegativeWorldCoordinates()
        {
            // Region coordinates go negative around the origin; floor division must not
            // truncate toward zero or samples land in the wrong region.
            var pool = new BrickPool(64, Allocator.Temp);
            var table = new RegionTable(4, Allocator.Temp);
            var region = table.LoadRegion(new int3(-1, -1, -1));

            int poolIndex = pool.Allocate();
            pool.FillBrick(poolIndex, 3);
            region.BrickRefs[Region.BrickIndex(0, 0, 0)] = BrickRef.FromPoolIndex(poolIndex);
            table.CommitRegion(in region);

            int3 worldVoxel = new(-VoxelDimensions.RegionVoxelEdge,
                                  -VoxelDimensions.RegionVoxelEdge,
                                  -VoxelDimensions.RegionVoxelEdge);
            Assert.IsTrue(VoxelMipSampler.TrySample(ref table, in pool, worldVoxel, 0,
                                                    out bool occupied, out byte material));
            Assert.IsTrue(occupied);
            Assert.AreEqual((byte)3, material);

            table.Dispose();
            pool.Dispose();
        }
    }
}
