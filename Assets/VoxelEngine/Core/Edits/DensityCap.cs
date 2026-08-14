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
    /// Authoritative ratios remain integer-only so every machine reaches the same decision.
    /// </summary>
    public static class DensityCap
    {
        public const int DefaultPercent = 75;
        public const int MinPercent = 25;
        public const int MaxPercent = 95;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeCapacity(Region region, int percent = DefaultPercent)
        {
            if (percent < MinPercent) percent = MinPercent;
            if (percent > MaxPercent) percent = MaxPercent;

            int totalBricks = VoxelDimensions.BricksPerRegion;
            return Math.Max(1, IntMath.MulDiv(totalBricks, percent, 100));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DefaultCapacity(in Region region) => ComputeCapacity(region, DefaultPercent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUnderCap(in Region region, int percent = DefaultPercent)
        {
            int capacity = ComputeCapacity(region, percent);
            int mixedCount = 0;
            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                if (region.BrickRefs[i].IsMixed)
                    mixedCount++;
            }

            return mixedCount < capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DensityPermille(in Region region, int percent = DefaultPercent)
        {
            int capacity = ComputeCapacity(region, percent);
            int mixedCount = 0;
            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                if (region.BrickRefs[i].IsMixed)
                    mixedCount++;
            }

            return IntMath.MulDiv(mixedCount, 1000, capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanAllocate(int currentMixed, int toAdd, in Region region, int percent = DefaultPercent)
        {
            int capacity = ComputeCapacity(region, percent);
            return currentMixed + toAdd <= capacity;
        }

        public static bool IsWithinVoxelCap(
            in Region region,
            NativeArray<int> brickOccupancy,
            ulong maxOccupiedVoxels)
        {
            ulong totalOccupied = 0;
            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                var brickRef = region.BrickRefs[i];
                if (brickRef.IsMixed)
                    totalOccupied += (ulong)brickOccupancy[i];
                else if (!brickRef.IsEmpty)
                    totalOccupied += (ulong)VoxelDimensions.VoxelsPerBrick;
            }

            return totalOccupied <= maxOccupiedVoxels;
        }
    }
}
