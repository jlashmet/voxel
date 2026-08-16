using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Converts a conservative castle voxel envelope into the complete region dependency set that
    /// must be resident before atomic realization starts. This is pure coordinate math; it owns no
    /// storage or streaming state.
    /// </summary>
    public static class CastleBuildRegionDependencies
    {
        public static int3[] Enumerate(in CastleBuildBounds bounds)
        {
            int3 minRegion = bounds.Min >> VoxelGrid.RegionVoxelEdgeLog2;
            int3 maxRegion = (bounds.MaxExclusive - 1) >> VoxelGrid.RegionVoxelEdgeLog2;

            long countX = (long)maxRegion.x - minRegion.x + 1;
            long countY = (long)maxRegion.y - minRegion.y + 1;
            long countZ = (long)maxRegion.z - minRegion.z + 1;
            long count = countX * countY * countZ;
            if (count <= 0 || count > int.MaxValue)
                throw new InvalidOperationException($"Castle region dependency count is invalid: {count}.");

            var regions = new int3[(int)count];
            int cursor = 0;
            for (int y = minRegion.y; y <= maxRegion.y; y++)
            for (int z = minRegion.z; z <= maxRegion.z; z++)
            for (int x = minRegion.x; x <= maxRegion.x; x++)
                regions[cursor++] = new int3(x, y, z);

            return regions;
        }
    }
}
