using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Inclusive storage-region range intersected by a half-open world-voxel dependency envelope.
    /// This belongs to Composition because region sizing is a storage concern, not structure planning.
    /// </summary>
    public readonly struct ShowcaseCastleDependencyRegionRange
    {
        public readonly int3 Min;
        public readonly int3 MaxInclusive;

        private ShowcaseCastleDependencyRegionRange(int3 min, int3 maxInclusive)
        {
            Min = min;
            MaxInclusive = maxInclusive;
        }

        public static ShowcaseCastleDependencyRegionRange FromCastleBounds(
            in CastleBuildBounds bounds) =>
            FromVoxelBounds(bounds.Min, bounds.MaxExclusive);

        /// <summary>
        /// Converts inclusive-min/exclusive-max voxel bounds to an inclusive signed region range.
        /// Subtracting one from MaxExclusive is essential when the bound ends exactly on a region
        /// boundary; arithmetic right shift preserves floor division for negative coordinates.
        /// </summary>
        public static ShowcaseCastleDependencyRegionRange FromVoxelBounds(
            int3 min,
            int3 maxExclusive)
        {
            if (math.any(maxExclusive <= min))
                throw new ArgumentException("Dependency voxel bounds must have positive extent.");

            int shift = VoxelDimensions.RegionVoxelEdgeLog2;
            return new ShowcaseCastleDependencyRegionRange(
                min >> shift,
                (maxExclusive - 1) >> shift);
        }
    }
}
