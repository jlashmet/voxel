using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Occupancy
{
    /// <summary>
    /// Builds the voxel mip hierarchy for a region: an occupancy pyramid plus a parallel
    /// material pyramid, aggregated up the chain from level 0 (one cell per brick) to the top.
    ///
    /// Each mip level halves the dimensions of the previous level. For a 64-brick region
    /// edge this produces seven levels: level 0 is 64³ brick-sized cells, level 1 is 32³,
    /// level 2 is 16³, down to level 6 which is a single cell — the always-resident far-
    /// field summary that never pages out (data-model.md: Region.occupancyMips invariant).
    ///
    /// Rebuild is an aggregate up the chain rather than a recompute from raw voxels, which
    /// keeps edit cost independent of world size and ensures every level derives from the
    /// same single source of truth — Constitution Principle II (Single Source of Truth).
    ///
    /// <para><b>Indexing.</b> Every level uses the same 3D linearization as
    /// <see cref="Region.BrickIndex"/>: <c>x + e*(y + e*z)</c> for that level's edge
    /// <c>e = RegionEdge >> level</c>. The eight children of parent <c>(px,py,pz)</c> are the
    /// 2×2×2 block at <c>(2px+dx, 2py+dy, 2pz+dz)</c>, whose linear indices are *not*
    /// contiguous. Aggregation must gather them by coordinate; treating a parent's children as
    /// eight consecutive linear entries walks an 8×1×1 stripe along x and silently mixes
    /// unrelated bricks. See <see cref="ChildIndices"/>, which is the single place that
    /// mapping is expressed.</para>
    ///
    /// <para><b>Materials.</b> Occupancy alone cannot be shaded, so distant chunks need a
    /// representative material per cell. Level 0 takes each brick's dominant material; a
    /// parent inherits the material of whichever child carries the most set occupancy bits.
    /// "Most solid child wins" is integer-only and order-independent, so it is deterministic
    /// across clients (Constitution Principle I) and reads correctly at range, where a cell
    /// covers metres and the majority constituent is the one the eye resolves.</para>
    ///
    /// Supports two entry points: <see cref="RebuildFull"/> for initial population or total
    /// rebuild, and <see cref="RebuildDirty"/> for per-frame incremental rebuild over a dirty-
    /// brick set.
    /// </summary>
    public static class MipBuilder
    {
        // A 64-brick region edge produces log2(64) + 1 = 7 levels: 64³, 32³, ... , 2³, 1³.
        public const int MaxLevels = VoxelDimensions.RegionEdgeLog2 + 1;

        /// <summary>
        /// Build the complete mip hierarchy for a region from its brick occupancy and materials.
        ///
        /// The caller must allocate <paramref name="occupancy"/>[level] and
        /// <paramref name="materials"/>[level] with <see cref="TotalCellCount"/> entries each.
        /// On return, level i holds the aggregate of the eight level-(i-1) children that map to
        /// each cell. Level 0 is one cell per brick.
        ///
        /// For RegionEdge = 64 the occupancy pyramid totals ~2.4 MB and the material pyramid
        /// ~300 KB per region — bounded by region geometry, not content.
        /// </summary>
        public static void RebuildFull(in BrickPool pool, in Region region, int levelCount,
                                       NativeArray<ulong>[] occupancy,
                                       NativeArray<byte>[] materials)
        {
            if (levelCount <= 0 || levelCount > MaxLevels)
                throw new ArgumentOutOfRangeException(
                    nameof(levelCount), $"Must be in [1 .. MipBuilder.MaxLevels ({MaxLevels})].");
            if (occupancy == null) throw new ArgumentNullException(nameof(occupancy));
            if (materials == null) throw new ArgumentNullException(nameof(materials));

            var brickRefs = region.BrickRefs;

            // Level 0: one cell per brick.
            int level0Cells = VoxelDimensions.BricksPerRegion;
            for (int i = 0; i < level0Cells; i++)
            {
                BrickRef brick = brickRefs[i];
                if (brick.IsMixed)
                {
                    occupancy[0][i] = AggregateBrick(pool, brick.PoolIndex);
                    materials[0][i] = DominantMixedMaterial(pool, brick.PoolIndex);
                }
                else if (brick.IsEmpty)
                {
                    occupancy[0][i] = 0UL;
                    materials[0][i] = VoxelDimensions.MaterialEmpty;
                }
                else
                {
                    occupancy[0][i] = ulong.MaxValue;
                    materials[0][i] = brick.UniformMaterial;
                }
            }

            // Levels 1..N: aggregate each parent's 2×2×2 block of children.
            for (int level = 1; level < levelCount; level++)
            {
                int parentEdge = RegionEdgeForLevel(level);
                NativeArray<ulong> childOcc = occupancy[level - 1];
                NativeArray<byte> childMat = materials[level - 1];

                for (int pz = 0; pz < parentEdge; pz++)
                for (int py = 0; py < parentEdge; py++)
                for (int px = 0; px < parentEdge; px++)
                {
                    int parentIndex = CellIndex(px, py, pz, parentEdge);
                    AggregateChildren(px, py, pz, level, childOcc, childMat,
                                      out ulong acc, out byte material);
                    occupancy[level][parentIndex] = acc;
                    materials[level][parentIndex] = material;
                }
            }
        }

        /// <summary>
        /// Incrementally rebuild only the mip cells whose brick children changed.
        ///
        /// <paramref name="dirtyBricks"/> is a set of level-0 brick indices modified since the
        /// last rebuild. The caller populates it during the edit phase and passes it here
        /// before publishing.
        ///
        /// Returns the level-0 cell indices whose aggregate actually changed. Propagation stops
        /// early at any level where no parent's value changed, which is what keeps distant
        /// destruction nearly free: an edit that does not alter a coarse cell's aggregate does
        /// not invalidate anything above it.
        ///
        /// The caller must still provide the full pyramid; this method recomputes entries
        /// within it rather than reallocating or scanning every cell.
        /// </summary>
        public static NativeList<int> RebuildDirty(in BrickPool pool, in Region region,
            in NativeHashSet<int> dirtyBricks, int levelCount,
            NativeArray<ulong>[] occupancy, NativeArray<byte>[] materials, Allocator allocator)
        {
            var changedLevel0 = new NativeList<int>(16, allocator);
            if (dirtyBricks.Count == 0) return changedLevel0;
            if (levelCount <= 0 || levelCount > MaxLevels)
                throw new ArgumentOutOfRangeException(
                    nameof(levelCount), $"Must be in [1 .. MipBuilder.MaxLevels ({MaxLevels})].");

            var brickRefs = region.BrickRefs;

            // Recompute level-0 cells for dirty bricks in place.
            foreach (int brickIndex in dirtyBricks)
            {
                ulong oldOcc = occupancy[0][brickIndex];
                byte oldMat = materials[0][brickIndex];

                BrickRef brick = brickRefs[brickIndex];
                ulong newOcc;
                byte newMat;
                if (brick.IsMixed)
                {
                    newOcc = AggregateBrick(pool, brick.PoolIndex);
                    newMat = DominantMixedMaterial(pool, brick.PoolIndex);
                }
                else if (brick.IsEmpty)
                {
                    newOcc = 0UL;
                    newMat = VoxelDimensions.MaterialEmpty;
                }
                else
                {
                    newOcc = ulong.MaxValue;
                    newMat = brick.UniformMaterial;
                }

                occupancy[0][brickIndex] = newOcc;
                materials[0][brickIndex] = newMat;
                if (newOcc != oldOcc || newMat != oldMat) changedLevel0.Add(brickIndex);
            }

            // Propagate upward. Each level maps its changed children to the distinct parents
            // that contain them, re-aggregates those parents in full, and carries forward only
            // the parents whose value actually moved.
            var frontier = new NativeHashSet<int>(math.max(16, changedLevel0.Length),
                                                  Allocator.Temp);
            for (int i = 0; i < changedLevel0.Length; i++) frontier.Add(changedLevel0[i]);

            for (int level = 1; level < levelCount && frontier.Count > 0; level++)
            {
                int childEdge = RegionEdgeForLevel(level - 1);
                int parentEdge = RegionEdgeForLevel(level);
                NativeArray<ulong> childOcc = occupancy[level - 1];
                NativeArray<byte> childMat = materials[level - 1];

                var parents = new NativeHashSet<int>(frontier.Count, Allocator.Temp);
                foreach (int childIndex in frontier)
                {
                    CellCoordinate(childIndex, childEdge, out int cx, out int cy, out int cz);
                    parents.Add(CellIndex(cx >> 1, cy >> 1, cz >> 1, parentEdge));
                }

                var nextFrontier = new NativeHashSet<int>(parents.Count, Allocator.Temp);
                foreach (int parentIndex in parents)
                {
                    CellCoordinate(parentIndex, parentEdge, out int px, out int py, out int pz);
                    AggregateChildren(px, py, pz, level, childOcc, childMat,
                                      out ulong acc, out byte material);
                    if (occupancy[level][parentIndex] == acc
                        && materials[level][parentIndex] == material) continue;
                    occupancy[level][parentIndex] = acc;
                    materials[level][parentIndex] = material;
                    nextFrontier.Add(parentIndex);
                }

                parents.Dispose();
                frontier.Dispose();
                frontier = nextFrontier;
            }

            frontier.Dispose();
            return changedLevel0;
        }

        /// <summary>
        /// Reads a level-0 cell — one brick — directly from the region's brick references and
        /// the pool. Level 0 is derived rather than stored so the pyramid never duplicates the
        /// authoritative voxel data (Constitution Principle II); see <see cref="RegionMipLayout"/>.
        /// </summary>
        public static void ReadLevel0(in BrickPool pool, in Region region, int brickIndex,
                                      out ulong occupancy, out byte material)
        {
            BrickRef brick = region.BrickRefs[brickIndex];
            if (brick.IsMixed)
            {
                occupancy = AggregateBrick(pool, brick.PoolIndex);
                material = DominantMixedMaterial(pool, brick.PoolIndex);
            }
            else if (brick.IsEmpty)
            {
                occupancy = 0UL;
                material = VoxelDimensions.MaterialEmpty;
            }
            else
            {
                occupancy = ulong.MaxValue;
                material = brick.UniformMaterial;
            }
        }

        /// <summary>
        /// Builds the region's own flattened pyramid (levels 1..N) from its bricks, deriving
        /// level 0 into temporary scratch. This is the entry point the streaming and edit paths
        /// use; the array-based <see cref="RebuildFull"/> overload remains for tests and tools
        /// that want to inspect every level including level 0.
        /// </summary>
        public static void RebuildRegion(in BrickPool pool, ref Region region,
                                         Allocator scratchAllocator = Allocator.Temp)
        {
            if (!region.HasMips)
                throw new InvalidOperationException(
                    "Region has no mip storage; call Region.AllocateMips first.");

            int levelCount = region.MipLevelCount;
            int level0Cells = VoxelDimensions.BricksPerRegion;

            var level0Occupancy = new NativeArray<ulong>(level0Cells, scratchAllocator,
                                                         NativeArrayOptions.UninitializedMemory);
            var level0Materials = new NativeArray<byte>(level0Cells, scratchAllocator,
                                                        NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < level0Cells; i++)
            {
                ReadLevel0(in pool, in region, i, out ulong occupancy, out byte material);
                level0Occupancy[i] = occupancy;
                level0Materials[i] = material;
            }

            NativeArray<ulong> storedOccupancy = region.OccupancyMips;
            NativeArray<byte> storedMaterials = region.MaterialMips;

            for (int level = RegionMipLayout.FirstStoredLevel; level < levelCount; level++)
            {
                int parentEdge = RegionEdgeForLevel(level);
                int levelOffset = RegionMipLayout.LevelOffset(level);
                bool childIsLevel0 = level == RegionMipLayout.FirstStoredLevel;
                int childOffset = childIsLevel0 ? 0 : RegionMipLayout.LevelOffset(level - 1);
                NativeArray<ulong> childOccupancy =
                    childIsLevel0 ? level0Occupancy : storedOccupancy;
                NativeArray<byte> childMaterials =
                    childIsLevel0 ? level0Materials : storedMaterials;

                for (int pz = 0; pz < parentEdge; pz++)
                for (int py = 0; py < parentEdge; py++)
                for (int px = 0; px < parentEdge; px++)
                {
                    AggregateChildrenAt(px, py, pz, level, childOccupancy, childMaterials,
                                        childOffset, out ulong acc, out byte material);
                    int index = levelOffset + CellIndex(px, py, pz, parentEdge);
                    storedOccupancy[index] = acc;
                    storedMaterials[index] = material;
                }
            }

            level0Occupancy.Dispose();
            level0Materials.Dispose();
        }

        /// <summary>
        /// Aggregate the 2×2×2 block of level-(<paramref name="level"/>-1) children under parent
        /// cell (<paramref name="px"/>, <paramref name="py"/>, <paramref name="pz"/>).
        /// Occupancy is a bitwise OR; the material is taken from the child with the most set
        /// occupancy bits, with ties broken by child order so the result is deterministic.
        /// </summary>
        private static void AggregateChildren(int px, int py, int pz, int level,
                                              NativeArray<ulong> childOccupancy,
                                              NativeArray<byte> childMaterials,
                                              out ulong occupancy, out byte material) =>
            AggregateChildrenAt(px, py, pz, level, childOccupancy, childMaterials, 0,
                                out occupancy, out material);

        /// <summary>
        /// As <see cref="AggregateChildren"/>, but with the child level's cells starting at
        /// <paramref name="childOffset"/> within a flattened multi-level array.
        /// </summary>
        private static void AggregateChildrenAt(int px, int py, int pz, int level,
                                                NativeArray<ulong> childOccupancy,
                                                NativeArray<byte> childMaterials,
                                                int childOffset,
                                                out ulong occupancy, out byte material)
        {
            int childEdge = RegionEdgeForLevel(level - 1);
            occupancy = 0UL;
            material = VoxelDimensions.MaterialEmpty;
            int bestPopCount = 0;

            for (int dz = 0; dz < 2; dz++)
            for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
            {
                int childIndex = childOffset
                               + CellIndex((px << 1) + dx, (py << 1) + dy, (pz << 1) + dz,
                                           childEdge);
                ulong childBits = childOccupancy[childIndex];
                occupancy |= childBits;

                byte childMaterial = childMaterials[childIndex];
                if (childMaterial == VoxelDimensions.MaterialEmpty) continue;
                int popCount = math.countbits(childBits);
                if (popCount <= bestPopCount) continue;
                bestPopCount = popCount;
                material = childMaterial;
            }
        }

        /// <summary>
        /// The linear indices at level <paramref name="level"/>-1 of the eight children under
        /// parent cell <paramref name="parentIndex"/>. Exposed so tests and tools share the one
        /// authoritative parent/child mapping rather than restating the arithmetic.
        /// </summary>
        public static void ChildIndices(int parentIndex, int level, Span<int> destination)
        {
            if (destination.Length < 8)
                throw new ArgumentException("Destination must hold eight children.",
                                            nameof(destination));
            int parentEdge = RegionEdgeForLevel(level);
            int childEdge = RegionEdgeForLevel(level - 1);
            CellCoordinate(parentIndex, parentEdge, out int px, out int py, out int pz);
            int n = 0;
            for (int dz = 0; dz < 2; dz++)
            for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
                destination[n++] = CellIndex((px << 1) + dx, (py << 1) + dy, (pz << 1) + dz,
                                             childEdge);
        }

        /// <summary>Dominant material of a mixed brick: the occupied material with the most
        /// voxels. Ties resolve to the lowest material id so the result is order-independent.
        /// </summary>
        private static byte DominantMixedMaterial(in BrickPool pool, int brickIndex)
        {
            int voxelOffset = pool.VoxelOffset(brickIndex);
            // 256 counters covers the material id space and avoids a second pass over voxels.
            Span<int> counts = stackalloc int[256];
            counts.Clear();
            for (int i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
                counts[pool.Voxels[voxelOffset + i]]++;

            byte best = VoxelDimensions.MaterialEmpty;
            int bestCount = 0;
            // Start at 1: material 0 is empty space and never represents a cell.
            for (int m = 1; m < 256; m++)
            {
                if (counts[m] <= bestCount) continue;
                bestCount = counts[m];
                best = (byte)m;
            }
            return best;
        }

        /// <summary>Linear index of a cell within a level of the given edge length.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellIndex(int x, int y, int z, int edge) => x + edge * (y + edge * z);

        /// <summary>Inverse of <see cref="CellIndex"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CellCoordinate(int index, int edge, out int x, out int y, out int z)
        {
            x = index % edge;
            y = index / edge % edge;
            z = index / (edge * edge);
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
        /// Compute the mip cell (3D coordinate) that contains a given brick at a specific level.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 BrickToCell(int brickX, int brickY, int brickZ, int level) =>
            new int3(brickX >> level, brickY >> level, brickZ >> level);

        /// <summary>
        /// Convert a cell coordinate at level L back to the brick coordinate of its origin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellToBrickAxis(int cellAxis, int level) => cellAxis << level;
    }
}
