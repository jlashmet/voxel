using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Occupancy
{
    /// <summary>
    /// Builds the occupancy mip hierarchy for a region by bitwise-OR-ing brick occupancy
    /// words up the chain from level 0 (full bricks) to the top level.
    ///
    /// Each mip level halves the dimensions of the previous level. For a 64-brick region
    /// edge this produces ten levels: level 0 is 64³ brick-sized cells, level 1 is 32³,
    /// level 2 is 16³, down to level 9 which is a single word — the always-resident far-
    /// field summary that never pages out (data-model.md: Region.occupancyMips invariant).
    ///
    /// Rebuild is a bitwise OR up the chain rather than a recompute from raw voxels, which
    /// keeps edit cost independent of world size and ensures every level derives from the
    /// same single source of truth — Constitution Principle II (Single Source of Truth).
    ///
    /// Supports two entry points: <see cref="RebuildFull"/> for initial population or total
    /// rebuild, and <see cref="RebuildDirty"/> for per-frame incremental rebuild over a dirty-
    /// brick set.
    /// </summary>
    public static class MipBuilder
    {
        // A 64-brick region edge produces floor(log2(64)) + 1 = 10 levels.
        // Level N covers (RegionEdge / 2^N)^3 bricks per cell.
        public const int MaxLevels = VoxelDimensions.RegionEdgeLog2 + 1;

        /// <summary>
        /// Build the complete mip hierarchy for a region from its brick occupancy arrays.
        ///
        /// The caller must allocate outputArray[level] with size (RegionEdge >> level)^3 ulong
        /// entries each. On return, outputArray[i][j] holds the OR-aggregate of all eight level-
        /// (i+1) children that map to cell j at level i. Level 0 is always BrickEdge^3 = region
        /// volume in bricks — effectively a copy of per-brick aggregates.
        ///
        /// For RegionEdge = 64:
        ///   level 0:  64³ × 8 B = 131 072 KB
        ///   level 1:  32³ × 8 B =  26 214 KB
        ///   ...
        ///   level 9:   1  × 8 B =       8 B
        /// Total ≈ 262 KB — negligible and fully bounded by region geometry, not content.
        /// </summary>
        public static void RebuildFull(in BrickPool pool, in Region region, int levelCount, NativeArray<ulong>[] outputArray)
        {
            if (levelCount <= 0 || levelCount > MaxLevels)
                throw new ArgumentOutOfRangeException(
                    nameof(levelCount), $"Must be in [1 .. MipBuilder.MaxLevels ({MaxLevels})].");

            var brickRefs = region.BrickRefs;

            // Level 0: one cell per brick — aggregate each brick's occupancy.
            int level0Cells = VoxelDimensions.BricksPerRegion;
            for (int i = 0; i < level0Cells; i++)
            {
                var ref_ = brickRefs[i];
                if (ref_.IsMixed)
                    outputArray[0][i] = AggregateBrick(pool, ref_.PoolIndex);
                else
                    outputArray[0][i] = ref_.IsEmpty ? 0UL : ulong.MaxValue;
            }

            // Levels 1 .. N: OR each parent's eight children.
            for (int l = 1; l < levelCount; l++)
            {
                int childCount = RegionEdgeForLevel(l - 1) * RegionEdgeForLevel(l - 1) *
                                 RegionEdgeForLevel(l - 1);
                int parentCount = childCount >> 3; // / 8

                for (int i = 0; i < parentCount; i++)
                {
                    ulong acc = 0UL;
                    for (int c = 0; c < 8; c++)
                        acc |= outputArray[l - 1][i * 8 + c];
                    outputArray[l][i] = acc;
                }
            }
        }

        /// <summary>
        /// Incrementally rebuild only the mip cells whose brick children changed.
        ///
        /// <paramref name="dirtyBricks"/> is a NativeHashSet of brick indices that were modified
        /// since the last rebuild. The caller populates it during the edit phase and passes it
        /// here before publishing.
        ///
        /// Returns a NativeList of cell indices at each level whose aggregate value actually
        /// changed — these are cells that downstream consumers (rendering, replication) need to
        /// re-process. Unchanged cells are skipped entirely.
        ///
        /// The caller must still provide the full mip array (outputArray); this method only
        /// recomputes entries within it rather than reallocating or scanning every cell.
        /// </summary>
        public static NativeList<int> RebuildDirty(in BrickPool pool, in Region region,
            in NativeHashSet<int> dirtyBricks, int levelCount,
            NativeArray<ulong>[] outputArray, Allocator allocator)
        {
            if (dirtyBricks.Count == 0)
                return new NativeList<int>(16, allocator);

            var brickRefs = region.BrickRefs;
            NativeList<int> dirtyCells = new NativeList<int>(256, allocator);

            // Recompute level-0 cells for dirty bricks in place.
            foreach (var bi in dirtyBricks)
            {
                ulong oldVal = outputArray[0][bi];
                var ref_ = brickRefs[bi];
                ulong newVal = ref_.IsMixed
                    ? AggregateBrick(pool, ref_.PoolIndex)
                    : (ref_.IsEmpty ? 0UL : ulong.MaxValue);
                outputArray[0][bi] = newVal;

                if (newVal != oldVal)
                    dirtyCells.Add(bi);
            }

            // Propagate dirty cells upward level by level. At each level, only re-aggregate
            // parents whose children appear in the current dirty set.
            for (int l = 1; l < levelCount; l++)
            {
                int parentEdge = RegionEdgeForLevel(l);

                if (dirtyCells.Length == 0)
                    break; // no changes propagate further

                var childToParentMap = new NativeArray<int>(dirtyCells.Length, Allocator.Temp);
                var newDirty = new NativeList<int>(dirtyCells.Length >> 1, allocator);
                int lastParent = -1;

                // Sort parents implicitly by iterating in order and de-duplicating adjacent.
                for (int i = 0; i < dirtyCells.Length; i++)
                {
                    int cellIdx = dirtyCells[i];

                    // Each mip level packs 8 children into one parent cell (2³).
                    int parentId = cellIdx >> 3;
                    childToParentMap[i] = parentId;

                    if (i == 0 || parentId != lastParent)
                    {
                        lastParent = parentId;

                        ulong oldVal = outputArray[l][parentId];
                        ulong acc = AggregateParent(l, parentId, outputArray[l - 1]);

                        outputArray[l][parentId] = acc;

                        if (acc != oldVal)
                            newDirty.Add(parentId);
                    }
                }

                dirtyCells.Dispose();
                childToParentMap.Dispose();
                dirtyCells = newDirty;
            }

            return dirtyCells;
        }

        /// <summary>Brick-edge length at a given mip level.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RegionEdgeForLevel(int level) => VoxelDimensions.RegionEdge >> level;

        /// <summary>Number of cells along one axis at a given mip level.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellCountPerAxis(int level) => RegionEdgeForLevel(level);

        /// <summary>Total number of cells across all axes at a given mip level.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TotalCellCount(int level)
        {
            int e = RegionEdgeForLevel(level);
            return e * e * e;
        }

        /// <summary>How many child cells feed into one parent cell at this level.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChildrenPerParent(int level) => 8; // (2^1)^3 = 8, constant for all levels.

        /// <summary>
        /// Aggregate a single mixed brick's occupancy into one ulong by OR-ing its eight words.
        /// The caller must ensure <paramref name="brickIndex"/> is valid in the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong AggregateBrick(in BrickPool pool, int brickIndex)
        {
            var occOffset = pool.OccupancyOffset(brickIndex);
            return OccupancyMask.Aggregate(pool.Occupancy, occOffset);
        }

        /// <summary>
        /// Recompute level-0 output for one mixed brick and write back into the mip array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecomputeBrickCell(in BrickPool pool, NativeArray<ulong> level0Output, int brickIndex)
        {
            level0Output[brickIndex] = AggregateBrick(pool, brickIndex);
        }

        /// <summary>
        /// Compute the mip cell (3D coordinate) that contains a given brick at a specific level.
        /// Returns (x, y, z) cell index within that level's grid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 BrickToCell(int brickX, int brickY, int brickZ, int level)
        {
            int s = VoxelDimensions.RegionEdgeLog2 - level;
            return new int3(
                brickX >> s,
                brickY >> s,
                brickZ >> s
            );
        }

        /// <summary>
        /// Convert a linear cell index at level L to its brick-edge coordinate along one axis.
        /// Useful for debugging and visualization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellToBrickAxis(int cellAxis, int level) => cellAxis << (VoxelDimensions.RegionEdgeLog2 - level);

        /// <summary>
        /// OR the eight children of a parent cell at the given mip level.
        /// Reads from outputArray[level] — the caller must have populated it first.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong AggregateParent(int level, int parentIdx, NativeArray<ulong> array)
        {
            // This helper is internal to MipBuilder and called during incremental rebuild
            // when we need to re-aggregate a parent without scanning all 262k cells.
            var acc = 0UL;
            for (int c = 0; c < 8; c++)
                acc |= array[parentIdx * 8 + c];
            return acc;
        }

        /// <summary>
        /// OR all eight words of one brick's occupancy into a single ulong.
        /// The caller must supply the brick's occupancy offset within its container.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong AggregateBrick(in NativeArray<ulong> occupancy, int occOffset) =>
            OccupancyMask.Aggregate(occupancy, occOffset);
    }
}
