using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Runtime.Interest
{
    /// <summary>
    /// Platform-neutral simulation/replication interest.
    ///
    /// Presentation distance may vary by device tier, but authoritative gameplay interest must not.
    /// The initial common radius intentionally matches the previous constrained-client baseline;
    /// tune it from gameplay/network captures, never from GPU tier.
    /// </summary>
    public static class SimulationInterest
    {
        public const float LoadRadiusMeters = 300f;
        public const float UnloadRadiusMeters = 420f;
        public const float VoxelScaleMeters = 0.1f;

        private const int RegionVoxelEdge = 1 << VoxelGrid.RegionVoxelEdgeLog2;

        /// <summary>Collect every 3D region whose AABB intersects the common simulation load radius.</summary>
        public static void CollectLoadRegions(int3 playerVoxelPosition, List<int3> destination)
        {
            CollectRegions(playerVoxelPosition, LoadRadiusMeters, destination);
        }

        /// <summary>True while an already-subscribed region remains inside unload hysteresis.</summary>
        public static bool IsWithinUnloadRadius(int3 playerVoxelPosition, int3 regionCoord)
        {
            return IsWithinRadius(playerVoxelPosition, regionCoord, UnloadRadiusMeters);
        }

        /// <summary>Convert a world voxel coordinate to the containing region using floor division.</summary>
        public static int3 WorldVoxelToRegion(int3 worldVoxelPosition)
        {
            return new int3(
                worldVoxelPosition.x >> VoxelGrid.RegionVoxelEdgeLog2,
                worldVoxelPosition.y >> VoxelGrid.RegionVoxelEdgeLog2,
                worldVoxelPosition.z >> VoxelGrid.RegionVoxelEdgeLog2);
        }

        private static void CollectRegions(int3 playerVoxelPosition, float radiusMeters, List<int3> destination)
        {
            destination.Clear();

            int radiusVoxels = (int)math.ceil(radiusMeters / VoxelScaleMeters);
            int scanRadius = ((radiusVoxels + RegionVoxelEdge - 1) / RegionVoxelEdge) + 1;
            int3 centerRegion = WorldVoxelToRegion(playerVoxelPosition);
            long radiusSq = (long)radiusVoxels * radiusVoxels;

            for (int x = -scanRadius; x <= scanRadius; x++)
            {
                for (int y = -scanRadius; y <= scanRadius; y++)
                {
                    for (int z = -scanRadius; z <= scanRadius; z++)
                    {
                        int3 region = centerRegion + new int3(x, y, z);
                        if (DistanceSqToRegionAabb(playerVoxelPosition, region) <= radiusSq)
                            destination.Add(region);
                    }
                }
            }
        }

        private static bool IsWithinRadius(int3 playerVoxelPosition, int3 regionCoord, float radiusMeters)
        {
            int radiusVoxels = (int)math.ceil(radiusMeters / VoxelScaleMeters);
            long radiusSq = (long)radiusVoxels * radiusVoxels;
            return DistanceSqToRegionAabb(playerVoxelPosition, regionCoord) <= radiusSq;
        }

        private static long DistanceSqToRegionAabb(int3 point, int3 regionCoord)
        {
            int3 min = regionCoord << VoxelGrid.RegionVoxelEdgeLog2;
            int3 max = min + new int3(RegionVoxelEdge - 1);

            long dx = AxisDistance(point.x, min.x, max.x);
            long dy = AxisDistance(point.y, min.y, max.y);
            long dz = AxisDistance(point.z, min.z, max.z);
            return dx * dx + dy * dy + dz * dz;
        }

        private static long AxisDistance(int value, int min, int max)
        {
            if (value < min)
                return (long)min - value;
            if (value > max)
                return (long)value - max;
            return 0;
        }
    }
}
