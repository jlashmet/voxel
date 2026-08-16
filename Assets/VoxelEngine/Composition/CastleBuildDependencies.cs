using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Composition-owned dependency projection for a runtime-ready castle build. Structure planning
    /// decides the voxel envelope; this layer translates that envelope into Storage region
    /// coordinates so applications never reproduce signed-region arithmetic or castle reach rules.
    /// </summary>
    public static class CastleBuildDependencies
    {
        public static CastleBuildBounds ResolveBounds(in PlannedCastleBuild planned)
        {
            CastlePlan dimensions = planned.Dimensions;
            CastleSpatialPlan spatial = planned.Spatial;
            if (spatial == null)
                throw new InvalidOperationException("Castle build dependencies require a planned castle.");

            return CastleBuildBoundsResolver.Resolve(in dimensions, spatial);
        }

        /// <summary>
        /// Returns every Storage region intersected by the conservative castle build envelope,
        /// including underground negative-Y and upper-structure positive-Y layers.
        /// </summary>
        public static int3[] RequiredRegions(in PlannedCastleBuild planned)
        {
            CastleBuildBounds bounds = ResolveBounds(in planned);
            int3 minRegion = bounds.Min >> VoxelGrid.RegionVoxelEdgeLog2;
            int3 maxRegion = (bounds.MaxExclusive - 1) >> VoxelGrid.RegionVoxelEdgeLog2;

            long countX = (long)maxRegion.x - minRegion.x + 1L;
            long countY = (long)maxRegion.y - minRegion.y + 1L;
            long countZ = (long)maxRegion.z - minRegion.z + 1L;
            long count = countX * countY * countZ;
            if (count <= 0L || count > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Castle dependency envelope expands to an unsupported {count:N0} regions.");
            }

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
