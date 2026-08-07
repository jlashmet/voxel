using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Structure
{
    /// <summary>
    /// Detects structural failure: identifies bricks whose support value falls below the
    /// configured threshold after a change (destruction, placement removal).
    ///
    /// Returns the list of affected brick coordinates that should collapse into debris bodies.
    /// Only mixed bricks can produce debris; uniform/empty bricks are skipped because they
    /// have no voxel-level content to scatter and their pool slots are already freed when
    /// they become uniform (per <see cref="BrickPool.Free"/>).
    ///
    /// The three-phase workflow is:
    ///   Phase 1 (FindCollapseTargets): scan support field, identify unsupported bricks.
    ///   Phase 2 (ApplyCollapse): apply changes — free pool slots, mark dirty, set refs empty.
    ///   Phase 3 (propagate to debris/mip layers): caller iterates the returned brick list.
    /// </summary>
    public static class CollapseDetection
    {
        /// <summary>Default support threshold: bricks with minSupport below this collapse.</summary>
        public const byte DefaultThreshold = 2;

        // -- FindCollapseTargets ----------------------------------------------------

        /// <summary>
        /// Identify all unsupported bricks in a region and return their voxel-level coordinates.
        /// Each brick produces up to 512 voxel entries (one per occupied voxel).
        /// For efficiency, returns brick-level first (caller can expand if needed).
        ///
        /// A brick is "unsupported" when its support value (from <see cref="SupportField"/> computation)
        /// falls at or below the threshold — it has no structural path to a grounded anchor source.
        /// Only mixed bricks are returned because:
        ///   - Uniform bricks have already been collapsed to their uniform state.
        ///   - Empty bricks have nothing to collapse into debris.
        /// </summary>
        /// <param name="brickRefs">The region's brick reference array.</param>
        /// <param name="pool">Brick pool for mixed-brick occupancy data.</param>
        /// <param name="supportValues">Computed support values — one byte per brick.</param>
        /// <param name="threshold">Bricks with minSupport at or below this value are unsupported.</param>
        public static NativeList<int3> FindCollapseTargets(
            in NativeArray<BrickRef> brickRefs,
            in BrickPool pool,
            in NativeArray<byte> supportValues,
            byte threshold)
        {
            if (supportValues.Length != VoxelDimensions.BricksPerRegion)
                throw new ArgumentException(
                    $"supportValues must have length {VoxelDimensions.BricksPerRegion}.",
                    nameof(supportValues));

            var result = new NativeList<int3>(64, Allocator.Temp);

            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                // Only mixed bricks can collapse into debris.
                if (!brickRefs[i].IsMixed) continue;

                // Check support: at or below threshold means unsupported.
                byte sup = supportValues[i];
                if (sup > threshold) continue; // supported — skip.

                result.Add(BrickCoords(i));
            }

            return result;
        }

        // -- ApplyCollapse ----------------------------------------------------------

        /// <summary>
        /// Apply collapse: for each unsupported brick, free its mixed pool slot to the
        /// pool (it becomes uniform empty), and mark it dirty. Called by the server tick loop.
        /// Returns the list of affected bricks for downstream processing (mip rebuild, debris).
        /// </summary>
        /// <param name="table">Region table — ref to allow CommitRegion after modification.</param>
        /// <param name="pool">Brick pool — ref to free pool slots and update occupancy state.</param>
        /// <param name="brickRefs">The region's brick reference array — modified in place.</param>
        /// <param name="supportValues">Computed support values (from <see cref="SupportField"/>).</param>
        /// <param name="regionCoordX">X coordinate of the region in world space.</param>
        /// <param name="regionCoordY">Y coordinate of the region in world space.</param>
        /// <param name="regionCoordZ">Z coordinate of the region in world space.</param>
        /// <param name="threshold">Bricks with minSupport at or below this value collapse.</param>
        public static NativeList<int3> ApplyCollapse(
            ref RegionTable table,
            ref BrickPool pool,
            // Not `in`: this array is written back in place (see the param doc above).
            // NativeArray is a handle, so by-value still mutates the shared buffer.
            NativeArray<BrickRef> brickRefs,
            in NativeArray<byte> supportValues,
            int regionCoordX, int regionCoordY, int regionCoordZ,
            byte threshold)
        {
            int3 coord = new int3(regionCoordX, regionCoordY, regionCoordZ);

            // Phase 1: find unsupported bricks (don't modify yet — need old brick refs).
            var targets = FindCollapseTargets(brickRefs, pool, supportValues, threshold);
            if (targets.Length == 0)
                return targets; // nothing to collapse.

            // Phase 2: apply each collapse — free pool slot, then set BrickRef to Empty.
            for (int i = 0; i < targets.Length; i++)
            {
                int3 bc = targets[i];
                int linearIdx = Region.BrickIndex(bc.x, bc.y, bc.z);

                // Free the pool slot BEFORE clearing the brick ref — we need the old value.
                var oldRef = brickRefs[linearIdx];
                if (oldRef.IsMixed)
                    pool.Free(oldRef.PoolIndex);

                // Set brick to empty (uniform empty).
                brickRefs[linearIdx] = BrickRef.Empty;
            }

            // Mark the region dirty so it gets committed and synced.
            int3 slotCoord = new int3(regionCoordX, regionCoordY, regionCoordZ);
            if (table.IsResident(slotCoord))
            {
                var existing = table.LoadRegion(slotCoord);
                existing.Dirty = true;
                table.CommitRegion(existing);
            }

            return targets;
        }

        // -- FindUnsupportedBuilds --------------------------------------------------

        /// <summary>
        /// Collapse detection for player-built structures (US3): any material placed without
        /// an anchoring path to the ground should immediately collapse. Same algorithm as T088
        /// but with a lower threshold (1) for stricter building rules.
        /// </summary>
        /// <param name="brickRefs">The region's brick reference array.</param>
        /// <param name="pool">Brick pool providing occupancy data for mixed bricks.</param>
        /// <param name="supportValues">Computed support values (from <see cref="SupportField"/>).</param>
        public static NativeList<int3> FindUnsupportedBuilds(
            in NativeArray<BrickRef> brickRefs,
            in BrickPool pool,
            in NativeArray<byte> supportValues)
        {
            // Player-built bricks collapse when unsupported by ANY structural path.
            byte playerThreshold = 1; // stricter than DefaultThreshold (2).

            return FindCollapseTargets(brickRefs, pool, supportValues, playerThreshold);
        }

        // -- private helpers --------------------------------------------------------

        /// <summary>Convert linear brick index to int3 coordinates within the region.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 BrickCoords(int linearIndex) => new(
            linearIndex & VoxelDimensions.RegionEdgeMask,
            (linearIndex >> VoxelDimensions.RegionEdgeLog2) & VoxelDimensions.RegionEdgeMask,
            (linearIndex >> (VoxelDimensions.RegionEdgeLog2 * 2)) & VoxelDimensions.RegionEdgeMask);
    }
}
