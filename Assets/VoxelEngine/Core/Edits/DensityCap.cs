using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Foundation;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// Per-region density cap: a static threshold check against a region's occupancy count
    /// that prevents any single region from exceeding a fraction of its capacity with mixed bricks.
    ///
    /// This is a hard limit — unlike the player budget in <see cref="AllocationBudget"/> which
    /// rolls over, the density cap is absolute within a session. Once a region reaches its cap,
    /// no further mixed-brick allocations are allowed in that region until existing mixed bricks
    /// are collapsed back to uniform or empty (freeing their pool slots).
    ///
    /// The cap fraction defaults to 75% of the region's total brick count — leaving headroom
    /// for transient state during edit expansion. This is Constitution Principle VI (Bounded Growth)
    /// applied at the per-region level.
    ///
    /// Checked after <see cref="AllocationBudget.TryConsume"/> in the edit pipeline: first verify
    /// the player's budget, then check region density before committing any voxel writes.
    /// </summary>
    public static class DensityCap
    {
        // -- constants -----------------------------------------------------------

        // Caps are expressed as integer percentages, not float fractions: the density cap
        // is a server-authoritative accept/reject threshold, and no authoritative decision
        // may derive from floating-point (Constitution Principle I). Two machines that
        // round a float ratio differently at the boundary would disagree on whether an
        // edit was legal.

        /// <summary>Default density cap: 75% of region capacity. The maximum proportion of
        /// bricks that may be mixed simultaneously.</summary>
        public const int DefaultPercent = 75;

        /// <summary>Minimum supported cap (25%). Below this, even minor edits cause rejection.</summary>
        public const int MinPercent = 25;

        /// <summary>Maximum supported cap (95%). Above this, little practical benefit over no cap.</summary>
        public const int MaxPercent = 95;

        // -- capacity calculation ------------------------------------------------

        /// <summary>
        /// Compute the maximum number of mixed bricks allowed in a region given its density cap fraction.
        /// </summary>
        /// <param name="region">The region to check.</param>
        /// <param name="percent">The density cap percentage. Clamped to [MinPercent, MaxPercent].</param>
        /// <returns>The integer count of mixed bricks that may be allocated. Always >= 1 for any valid region.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeCapacity(Region region, int percent = DefaultPercent)
        {
            if (percent < MinPercent) percent = MinPercent;
            if (percent > MaxPercent) percent = MaxPercent;

            int totalBricks = VoxelEngine.Core.Storage.VoxelDimensions.BricksPerRegion;
            return Math.Max(1, IntMath.MulDiv(totalBricks, percent, 100));
        }

        /// <summary>
        /// Compute the maximum number of mixed bricks for a region using the default 75% fraction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DefaultCapacity(in Region region) => ComputeCapacity(region, DefaultPercent);

        // -- density check -------------------------------------------------------

        /// <summary>
        /// Check whether a region is within its density cap.
        ///
        /// Counts the current number of mixed bricks in the region by scanning BrickRefs.
        /// This is O(N) where N = BricksPerRegion (262144 for 64-brick regions), so it should
        /// only be called during edit acceptance, not in the rendering or simulation loop.
        /// </summary>
        /// <param name="region">The region to check.</param>
        /// <param name="percent">Density cap percentage (default 75).</param>
        /// <returns>True if the region is below its cap and new mixed bricks can be allocated;
        /// false if the cap has been reached.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUnderCap(in Region region, int percent = DefaultPercent)
        {
            int capacity = ComputeCapacity(region, percent);

            int mixedCount = 0;
            for (int i = 0; i < VoxelEngine.Core.Storage.VoxelDimensions.BricksPerRegion; i++)
            {
                if (region.BrickRefs[i].IsMixed)
                    mixedCount++;
            }

            return mixedCount < capacity;
        }

        /// <summary>
        /// Current density for a region in permille (parts per thousand) of its cap.
        /// 1000 means the region has reached its density cap.
        ///
        /// Permille integer rather than a float ratio so that callers comparing against a
        /// threshold reach the same verdict on every machine.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DensityPermille(in Region region, int percent = DefaultPercent)
        {
            int capacity = ComputeCapacity(region, percent);

            int mixedCount = 0;
            for (int i = 0; i < VoxelEngine.Core.Storage.VoxelDimensions.BricksPerRegion; i++)
            {
                if (region.BrickRefs[i].IsMixed)
                    mixedCount++;
            }

            return IntMath.MulDiv(mixedCount, 1000, capacity);
        }

        /// <summary>
        /// Incremental version of IsUnderCap that checks whether adding N more mixed bricks
        /// would exceed the cap, without scanning all bricks. Requires the caller to track
        /// the current mixed brick count externally (e.g., via an incrementing counter during edits).
        /// </summary>
        /// <param name="currentMixed">The number of currently mixed bricks (tracked by the caller).</param>
        /// <param name="toAdd">Number of new mixed bricks that would be allocated.</param>
        /// <param name="region">The region being modified (needed for capacity computation).</param>
        /// <param name="percent">Density cap percentage.</param>
        /// <returns>True if currentMixed + toAdd is within the cap; false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanAllocate(int currentMixed, int toAdd, in Region region, int percent = DefaultPercent)
        {
            int capacity = ComputeCapacity(region, percent);
            return currentMixed + toAdd <= capacity;
        }

        // -- occupancy-based checks ----------------------------------------------

        /// <summary>
        /// Check density based on total occupied voxel count within a region rather than mixed-brick count.
        /// Useful for regions where the occupancyMips are available and a finer-grained cap is desired.
        /// The caller must supply per-brick occupancy counts via <paramref name="brickOccupancy"/> since
        /// DensityCap itself does not hold a BrickPool reference.
        /// </summary>
        /// <param name="region">The region to check.</param>
        /// <param name="brickOccupancy">Pre-computed per-brick occupied-voxel counts, one per brick index.</param>
        /// <param name="maxOccupiedVoxels">Maximum total occupied voxels allowed in the region.</param>
        /// <returns>True if the region's total occupancy is at or below the threshold.</returns>
        public static bool IsWithinVoxelCap(in Region region, NativeArray<int> brickOccupancy, ulong maxOccupiedVoxels)
        {
            ulong totalOccupied = 0;
            int edge = VoxelEngine.Core.Storage.VoxelDimensions.BricksPerRegion;

            for (int i = 0; i < edge; i++)
            {
                var ref_ = region.BrickRefs[i];
                if (ref_.IsMixed)
                    totalOccupied += (ulong)brickOccupancy[i];
                else if (!ref_.IsEmpty)
                    totalOccupied += (ulong)VoxelEngine.Core.Storage.VoxelDimensions.VoxelsPerBrick;
            }

            return totalOccupied <= maxOccupiedVoxels;
        }
    }
}
