using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime.Occupancy;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Ensures MipBuilder aggregates the correct 2x2x2 children and builds incrementally — a
    /// single brick dirty-flag triggers only the affected mip levels, never a full recompute.
    /// This is what keeps edit cost independent of world size (Constitution Principle V).
    ///
    /// Mip storage is caller-owned: MipBuilder writes into a jagged array with one
    /// NativeArray per level, sized (RegionEdge >> level)^3.
    ///
    /// <para><b>Every spatial test here places bricks away from the origin on purpose.</b>
    /// An earlier implementation gathered a parent's children as eight *linearly consecutive*
    /// cells — an 8x1x1 stripe along x rather than a 2x2x2 block. At brick (0,0,0) the stripe
    /// and the true block both contain cell 0, so an origin-only suite passed while the
    /// aggregation was wrong everywhere else. Tests that only exercise the origin cannot
    /// distinguish the two mappings; these do.</para>
    /// </summary>
    public sealed class MipBuilderTests
    {
        private const int Levels = 4;

        private static NativeArray<ulong>[] AllocateOccupancy(int levels)
        {
            var mips = new NativeArray<ulong>[levels];
            for (int l = 0; l < levels; l++)
                mips[l] = new NativeArray<ulong>(MipBuilder.TotalCellCount(l), Allocator.Temp);
            return mips;
        }

        private static NativeArray<byte>[] AllocateMaterials(int levels)
        {
            var mips = new NativeArray<byte>[levels];
            for (int l = 0; l < levels; l++)
                mips[l] = new NativeArray<byte>(MipBuilder.TotalCellCount(l), Allocator.Temp);
            return mips;
        }

        private static void DisposeOccupancy(NativeArray<ulong>[] mips)
        {
            for (int l = 0; l < mips.Length; l++)
                if (mips[l].IsCreated) mips[l].Dispose();
        }

        private static void DisposeMaterials(NativeArray<byte>[] mips)
        {
            for (int l = 0; l < mips.Length; l++)
                if (mips[l].IsCreated) mips[l].Dispose();
        }

        /// <summary>Places one fully occupied mixed brick of the given material at a brick coord.</summary>
        private static int AddFullBrick(ref BrickPool pool, ref Region region,
                                        int bx, int by, int bz, byte material)
        {
            int poolIndex = pool.Allocate();
            pool.FillBrick(poolIndex, material);
            int brickIndex = Region.BrickIndex(bx, by, bz);
            region.BrickRefs[brickIndex] = BrickRef.FromPoolIndex(poolIndex);
            return brickIndex;
        }

        [Test]
        public void FullBuildPropagatesOccupancyToEveryAncestorOfAnOffOriginBrick()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            // (3, 5, 9) shares no ancestor with the origin at any level.
            int brickIndex = AddFullBrick(ref pool, ref region, 3, 5, 9, 1);
            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);

            Assert.AreNotEqual(0UL, occupancy[0][brickIndex],
                "Level-0 cell for an occupied brick must be non-zero.");

            for (int level = 1; level < Levels; level++)
            {
                int3 cell = MipBuilder.BrickToCell(3, 5, 9, level);
                int edge = MipBuilder.RegionEdgeForLevel(level);
                int index = MipBuilder.CellIndex(cell.x, cell.y, cell.z, edge);
                Assert.AreNotEqual(0UL, occupancy[level][index],
                    $"Level-{level} ancestor cell {cell} must carry the brick's occupancy.");
            }

            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void FullBuildLeavesNonAncestorCellsEmpty()
        {
            // The stripe bug's signature: occupancy leaking into cells that are not ancestors.
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            AddFullBrick(ref pool, ref region, 3, 5, 9, 1);
            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);

            for (int level = 1; level < Levels; level++)
            {
                int edge = MipBuilder.RegionEdgeForLevel(level);
                int3 ancestor = MipBuilder.BrickToCell(3, 5, 9, level);
                int ancestorIndex = MipBuilder.CellIndex(ancestor.x, ancestor.y, ancestor.z, edge);

                for (int i = 0; i < occupancy[level].Length; i++)
                {
                    if (i == ancestorIndex) continue;
                    Assert.AreEqual(0UL, occupancy[level][i],
                        $"Level-{level} cell {i} is not an ancestor of brick (3,5,9) and must "
                      + "stay empty; a non-zero value means aggregation gathered the wrong "
                      + "children.");
                }
            }

            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void SiblingsInTheSameParentMergeButNeighboursInXDoNot()
        {
            // Bricks (0,0,0) and (1,1,1) are siblings under level-1 cell (0,0,0).
            // Brick (8,0,0) sits in a different level-1 parent but would fall inside the
            // 8-wide x-stripe that the old aggregation walked.
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            AddFullBrick(ref pool, ref region, 0, 0, 0, 1);
            AddFullBrick(ref pool, ref region, 1, 1, 1, 1);
            AddFullBrick(ref pool, ref region, 8, 0, 0, 1);
            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);

            int edge1 = MipBuilder.RegionEdgeForLevel(1);
            Assert.AreNotEqual(0UL, occupancy[1][MipBuilder.CellIndex(0, 0, 0, edge1)],
                "Level-1 cell (0,0,0) must contain bricks (0,0,0) and (1,1,1).");
            Assert.AreNotEqual(0UL, occupancy[1][MipBuilder.CellIndex(4, 0, 0, edge1)],
                "Brick (8,0,0) belongs to level-1 cell (4,0,0).");
            Assert.AreEqual(0UL, occupancy[1][MipBuilder.CellIndex(1, 0, 0, edge1)],
                "Level-1 cell (1,0,0) covers bricks (2..3,0..1,0..1) and must stay empty.");

            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void ChildIndicesFormA2x2x2BlockNotALinearRun()
        {
            Span<int> children = stackalloc int[8];
            MipBuilder.ChildIndices(MipBuilder.CellIndex(1, 1, 1, MipBuilder.RegionEdgeForLevel(1)),
                                    1, children);

            int childEdge = MipBuilder.RegionEdgeForLevel(0);
            var expected = new System.Collections.Generic.HashSet<int>();
            for (int dz = 0; dz < 2; dz++)
            for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
                expected.Add(MipBuilder.CellIndex(2 + dx, 2 + dy, 2 + dz, childEdge));

            for (int i = 0; i < 8; i++)
                Assert.IsTrue(expected.Contains(children[i]),
                    $"Child index {children[i]} is outside the 2x2x2 block under parent (1,1,1).");
        }

        [Test]
        public void EmptyRegionProducesEmptyMipChain()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);

            for (int level = 0; level < Levels; level++)
            {
                for (int i = 0; i < occupancy[level].Length; i++)
                    Assert.AreEqual(0UL, occupancy[level][i],
                                    $"Level-{level} cell {i} must be empty.");
                for (int i = 0; i < materials[level].Length; i++)
                    Assert.AreEqual(VoxelDimensions.MaterialEmpty, materials[level][i],
                                    $"Level-{level} material {i} must be empty.");
            }

            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
            region.Dispose();
            pool.Dispose();
        }

        // -------------------------------------------------------------------------
        // Material pyramid
        // -------------------------------------------------------------------------

        [Test]
        public void UniformBrickMaterialPropagatesUpTheChain()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            AddFullBrick(ref pool, ref region, 3, 5, 9, 7);
            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);

            for (int level = 0; level < Levels; level++)
            {
                int3 cell = MipBuilder.BrickToCell(3, 5, 9, level);
                int edge = MipBuilder.RegionEdgeForLevel(level);
                int index = MipBuilder.CellIndex(cell.x, cell.y, cell.z, edge);
                Assert.AreEqual((byte)7, materials[level][index],
                    $"Level-{level} cell must carry the only present material.");
            }

            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void MostSolidChildWinsTheParentMaterial()
        {
            // Two siblings under level-1 cell (0,0,0): one fully solid, one half solid.
            // The fully solid child has the higher occupancy popcount and must win.
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            AddFullBrick(ref pool, ref region, 0, 0, 0, 4);

            int partialPool = pool.Allocate();
            for (int i = 0; i < VoxelDimensions.VoxelsPerBrick / 2; i++)
                pool.SetVoxel(partialPool, i, 9);
            region.BrickRefs[Region.BrickIndex(1, 0, 0)] = BrickRef.FromPoolIndex(partialPool);

            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);

            int edge1 = MipBuilder.RegionEdgeForLevel(1);
            Assert.AreEqual((byte)4, materials[1][MipBuilder.CellIndex(0, 0, 0, edge1)],
                "The child with the most set occupancy bits must supply the parent material.");

            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void EmptyChildrenNeverSupplyAParentMaterial()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            // A single occupied brick among seven empty siblings.
            AddFullBrick(ref pool, ref region, 1, 1, 1, 12);
            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);

            int edge1 = MipBuilder.RegionEdgeForLevel(1);
            Assert.AreEqual((byte)12, materials[1][MipBuilder.CellIndex(0, 0, 0, edge1)],
                "Empty siblings must not dilute the parent material to empty.");

            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
            region.Dispose();
            pool.Dispose();
        }

        // -------------------------------------------------------------------------
        // Incremental rebuild
        // -------------------------------------------------------------------------

        [Test]
        public void DirtyRebuildClearsAncestorsOfARemovedBrick()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            int brickIndex = AddFullBrick(ref pool, ref region, 3, 5, 9, 1);
            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);

            pool.Free(region.BrickRefs[brickIndex].PoolIndex);
            region.BrickRefs[brickIndex] = BrickRef.Empty;

            var dirty = new NativeHashSet<int>(4, Allocator.Temp);
            dirty.Add(brickIndex);

            using var changed = MipBuilder.RebuildDirty(
                in pool, region, in dirty, Levels, occupancy, materials, Allocator.Temp);

            Assert.AreEqual(0UL, occupancy[0][brickIndex],
                "Clearing a brick must zero its level-0 cell.");

            // The whole ancestor chain must clear too — this is what makes distant
            // destruction visible in the far field.
            for (int level = 1; level < Levels; level++)
            {
                int3 cell = MipBuilder.BrickToCell(3, 5, 9, level);
                int edge = MipBuilder.RegionEdgeForLevel(level);
                int index = MipBuilder.CellIndex(cell.x, cell.y, cell.z, edge);
                Assert.AreEqual(0UL, occupancy[level][index],
                    $"Level-{level} ancestor must clear when its only occupied child is removed.");
                Assert.AreEqual(VoxelDimensions.MaterialEmpty, materials[level][index],
                    $"Level-{level} ancestor material must clear with its occupancy.");
            }

            dirty.Dispose();
            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void DirtyRebuildMatchesFullRebuild()
        {
            // The strongest available check: after an edit, incremental and full rebuild must
            // agree cell for cell across every level.
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            AddFullBrick(ref pool, ref region, 3, 5, 9, 2);
            AddFullBrick(ref pool, ref region, 2, 5, 9, 3);
            AddFullBrick(ref pool, ref region, 17, 0, 4, 5);

            var incrementalOcc = AllocateOccupancy(Levels);
            var incrementalMat = AllocateMaterials(Levels);
            MipBuilder.RebuildFull(in pool, region, Levels, incrementalOcc, incrementalMat);

            // Edit: remove one brick, then refresh incrementally.
            int removed = Region.BrickIndex(2, 5, 9);
            pool.Free(region.BrickRefs[removed].PoolIndex);
            region.BrickRefs[removed] = BrickRef.Empty;

            var dirty = new NativeHashSet<int>(4, Allocator.Temp);
            dirty.Add(removed);
            using var changed = MipBuilder.RebuildDirty(
                in pool, region, in dirty, Levels, incrementalOcc, incrementalMat, Allocator.Temp);

            // Reference: a full rebuild of the same post-edit state.
            var referenceOcc = AllocateOccupancy(Levels);
            var referenceMat = AllocateMaterials(Levels);
            MipBuilder.RebuildFull(in pool, region, Levels, referenceOcc, referenceMat);

            for (int level = 0; level < Levels; level++)
            {
                for (int i = 0; i < referenceOcc[level].Length; i++)
                {
                    Assert.AreEqual(referenceOcc[level][i], incrementalOcc[level][i],
                        $"Occupancy diverged at level {level} cell {i}.");
                    Assert.AreEqual(referenceMat[level][i], incrementalMat[level][i],
                        $"Material diverged at level {level} cell {i}.");
                }
            }

            dirty.Dispose();
            DisposeOccupancy(incrementalOcc);
            DisposeMaterials(incrementalMat);
            DisposeOccupancy(referenceOcc);
            DisposeMaterials(referenceMat);
            region.Dispose();
            pool.Dispose();
        }

        [Test]
        public void DirtyRebuildWithNoDirtyBricksDoesNothing()
        {
            // The incremental guarantee: an empty dirty set must not trigger a full
            // recompute. If it did, edit cost would scale with world size.
            var pool = new BrickPool(256, Allocator.Temp);
            var region = new Region(int3.zero, Allocator.Temp);
            int brickIndex = AddFullBrick(ref pool, ref region, 3, 5, 9, 1);
            var occupancy = AllocateOccupancy(Levels);
            var materials = AllocateMaterials(Levels);

            MipBuilder.RebuildFull(in pool, region, Levels, occupancy, materials);
            ulong before = occupancy[0][brickIndex];

            var dirty = new NativeHashSet<int>(4, Allocator.Temp);

            using var changed = MipBuilder.RebuildDirty(
                in pool, region, in dirty, Levels, occupancy, materials, Allocator.Temp);

            Assert.AreEqual(0, changed.Length, "No dirty bricks means no changed cells.");
            Assert.AreEqual(before, occupancy[0][brickIndex], "Mip data must be untouched.");

            dirty.Dispose();
            DisposeOccupancy(occupancy);
            DisposeMaterials(materials);
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

        [Test]
        public void BrickToCellAndCellToBrickAxisRoundTrip()
        {
            for (int level = 0; level < Levels; level++)
            {
                int3 cell = MipBuilder.BrickToCell(37, 5, 60, level);
                Assert.AreEqual(37 >> level, cell.x, $"Level {level} x.");
                Assert.AreEqual(MipBuilder.CellToBrickAxis(cell.x, level), (37 >> level) << level,
                    $"Level {level} round trip must land on the cell's brick origin.");
            }
        }

        [Test]
        public void CellIndexAndCellCoordinateRoundTrip()
        {
            int edge = MipBuilder.RegionEdgeForLevel(2);
            int index = MipBuilder.CellIndex(5, 9, 14, edge);
            MipBuilder.CellCoordinate(index, edge, out int x, out int y, out int z);
            Assert.AreEqual(5, x);
            Assert.AreEqual(9, y);
            Assert.AreEqual(14, z);
        }
    }
}
