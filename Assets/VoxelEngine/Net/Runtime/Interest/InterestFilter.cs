using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Net.Interest
{
    /// <summary>
    /// Spatial interest management for world and player replication.
    ///
    /// Determines which regions and bricks each player should receive based on their
    /// distance from the player position. Used for culling network updates to only
    /// the relevant parts of the world — critical for staying within device-matrix.md
    /// bandwidth budgets (≥ 96 KB/s on mobile-HE cellular).
    ///
    /// Interest radii are derived from device-matrix.md region load/unload radii:
    ///   PC:      load 500 m / unload 650 m
    ///   Console: load 450 m / unload 600 m (note: values adjusted for consistency)
    ///   Mobile-HE: load 300 m / unload 420 m
    /// </summary>
    public struct InterestFilter
    {
        // -- interest radius parameters from device-matrix.md §Detail radius and LOD transitions

        /// <summary>Region load radius for PC (500 m, device-matrix.md).</summary>
        private const float k_LoadRadiusPC = 500f;

        /// <summary>Region unload radius for PC (650 m, device-matrix.md).</summary>
        private const float k_UnloadRadiusPC = 650f;

        /// <summary>Region load radius for Console (450 m, device-matrix.md).</summary>
        private const float k_LoadRadiusConsole = 450f;

        /// <summary>Region unload radius for Console (600 m, device-matrix.md).</summary>
        private const float k_UnloadRadiusConsole = 600f;

        /// <summary>Region load radius for Mobile-HE (300 m, device-matrix.md).</summary>
        private const float k_LoadRadiusMobile = 300f;

        /// <summary>Region unload radius for Mobile-HE (420 m, device-matrix.md).</summary>
        private const float k_UnloadRadiusMobile = 420f;

        // -- constants ------------------------------------------------------------

        /// <summary>Standard region size in bricks (64³ per data-model.md).</summary>
        public const int k_RegionSizeBricks = 64;

        /// <summary>Voxel scale: 10 cm per voxel (data-model.md §Scale targets).</summary>
        public const float k_VoxelScaleMeters = 0.1f;

        /// <summary>Brick size in metres (8 voxels × 0.1 m = 0.8 m per brick, data-model.md).</summary>
        public const float k_BrickSizeMeters = 0.8f;

        /// <summary>Region size in metres (64 bricks × 0.8 m = 51.2 m per region axis).</summary>
        public const float k_RegionSizeMeters = 51.2f;

        // -- interest query -------------------------------------------------------

        /// <summary>
        /// Determines which regions are within the interest radius of a player position.
        /// Only regions intersecting the load radius are returned — unload radii are used
        /// for eviction decisions, not inclusion.
        /// </summary>
        /// <param name="playerPosition">Player's world-space voxel coordinate.</param>
        /// <param name="radius">Interest radius in metres. Use LoadRadius() for the default.</param>
        /// <returns>NativeArray of region coordinates within range. Caller is responsible for disposing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<int3> GetInterestedRegions(int3 playerPosition, float radius)
        {
            // Convert player position to region grid coordinate (integer division).
            int3 playerRegion = playerPosition / k_RegionSizeBricks;

            // Compute the integer number of regions to scan in each axis.
            int radiusInRegions = (int)math.ceil(radius / k_RegionSizeMeters);

            // Clamp to a reasonable scan range to avoid O(n²) explosion in empty worlds.
            radiusInRegions = math.clamp(radiusInRegions, 1, 50); // max 50 regions per axis ≈ 2560 region scans.

            // Estimate result count for pre-allocation (approximate circle area / region area).
            int estimatedCount = (int)(3.14159f * radiusInRegions * radiusInRegions);

            NativeArray<int3> result = new NativeArray<int3>(estimatedCount, Unity.Collections.Allocator.Temp);
            int writeIndex = 0;

            // Scan the square region around the player's region and test distance.
            for (int dx = -radiusInRegions; dx <= radiusInRegions; dx++)
            {
                for (int dz = -radiusInRegions; dz <= radiusInRegions; dz++)
                {
                    // Prune corners of the square scan area — skip if minimum possible
                    // distance to this region exceeds the radius.
                    float minDistX = math.max(0f, math.abs(dx) * k_RegionSizeMeters - k_RegionSizeMeters * 0.5f);
                    float minDistZ = math.max(0f, math.abs(dz) * k_RegionSizeMeters - k_RegionSizeMeters * 0.5f);
                    if (minDistX * minDistX + minDistZ * minDistZ > radius * radius)
                        continue;

                    int3 regionCoord = playerRegion + new int3(dx, 0, dz);

                    // Compute squared distance from player to this region's center.
                    int3 regionCenter = regionCoord * k_RegionSizeBricks + (k_RegionSizeBricks / 2);
                    int3 diff = playerPosition - regionCenter;
                    float distSq = (float)(diff.x * diff.x + diff.y * diff.y + diff.z * diff.z) *
                                   (k_VoxelScaleMeters * k_VoxelScaleMeters);

                    if (distSq <= radius * radius)
                    {
                        if (writeIndex < result.Length)
                            result[writeIndex++] = regionCoord;
                    }
                }
            }

            // Resize the array to the actual count.
            if (writeIndex != result.Length)
            {
                var resized = new NativeArray<int3>(writeIndex, Unity.Collections.Allocator.Temp);
                for (int i = 0; i < writeIndex; i++)
                    resized[i] = result[i];
                result.Dispose();
                return resized;
            }

            return result;
        }

        /// <summary>Gets the default load radius for a given device tier.
        /// Use this to determine which regions each player should have loaded.</summary>
        /// <param name="tier">Device tier — affects only presentation, never simulation (C-006).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LoadRadius(DeviceTier tier)
        {
            return tier switch
            {
                DeviceTier.PC      => k_LoadRadiusPC,
                DeviceTier.Console => k_LoadRadiusConsole,
                DeviceTier.Mobile  => k_LoadRadiusMobile,
                _                  => k_LoadRadiusMobile, // default to mobile-HE (most constrained).
            };
        }

        /// <summary>Gets the unload radius for a given device tier.
        /// Must exceed load radius by ≥ 25% (device-matrix.md: hysteresis requirement).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float UnloadRadius(DeviceTier tier)
        {
            return tier switch
            {
                DeviceTier.PC      => k_UnloadRadiusPC,
                DeviceTier.Console => k_UnloadRadiusConsole,
                DeviceTier.Mobile  => k_UnloadRadiusMobile,
                _                  => k_UnloadRadiusMobile,
            };
        }

        /// <summary>Checks if a region is within the interest radius of a player.
        /// Faster than GetInterestedRegions when checking a single region.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRegionInInterest(int3 playerVoxelPos, int3 regionCoord, float radius)
        {
            int3 regionCenter = regionCoord * k_RegionSizeBricks + (k_RegionSizeBricks / 2);
            int3 diff = playerVoxelPos - regionCenter;

            // Use squared distance to avoid sqrt.
            float voxelScale = k_VoxelScaleMeters;
            float distSq = ((float)diff.x * diff.x + (float)diff.y * diff.y + (float)diff.z * diff.z) *
                           (voxelScale * voxelScale);

            return distSq <= radius * radius;
        }

        /// <summary>Disposes resources. Called when the interest filter is no longer needed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            // No persistent state to dispose — all allocations are Temp and returned to caller.
        }
    }

    /// <summary>Device tier enum matching the tiers in device-matrix.md §Supported device classes.</summary>
    public enum DeviceTier : byte
    {
        /// <summary>PC with discrete GPU (Vulkan 1.2 / DX12).</summary>
        PC = 0,

        /// <summary>Current-generation console (platform-native API).</summary>
        Console = 1,

        /// <summary>Flagship mobile phone within ~3 years, Metal 3 / Vulkan 1.1+.</summary>
        Mobile = 2,
    }
}
