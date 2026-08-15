using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Collision.Runtime
{
    /// <summary>Converts authoritative Storage read views into simple physics hulls.</summary>
    public static class HullExport
    {
        private const int ReadBlockEdgeLog2 = 3;
        private const int RegionReadBlockEdgeLog2 = VoxelGrid.RegionVoxelEdgeLog2 - ReadBlockEdgeLog2;
        private const int RegionReadBlockEdgeMask = (1 << RegionReadBlockEdgeLog2) - 1;

        /// <summary>
        /// Export all solid logical blocks in one resident region as an eight-corner bounding hull.
        /// Preserves the existing behavior that any mixed block counts as solid in this overload.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<float3> ExportHulls(in RegionReadView region, Allocator allocator)
        {
            var bounds = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            var extents = new float3(float.MinValue, float.MinValue, float.MinValue);
            bool found = false;

            int blocksPerAxis = region.BlockEdgeCount;
            for (int bx = 0; bx < blocksPerAxis; bx++)
            for (int by = 0; by < blocksPerAxis; by++)
            for (int bz = 0; bz < blocksPerAxis; bz++)
            {
                if (!region.TryGetBlock(new int3(bx, by, bz), out VoxelReadBlock block)
                    || !IsRegionBlockSolid(block))
                    continue;

                found = true;
                float3 center = new float3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                bounds = math.min(bounds, center);
                extents = math.max(extents, center);
            }

            if (!found)
                return new NativeArray<float3>(0, allocator);

            return BuildHull(bounds, extents, allocator);
        }

        /// <summary>
        /// Export a world-space bounding volume. Preserves the existing ranged-overload behavior:
        /// mixed blocks only count when at least one occupancy bit is set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<float3> ExportHulls(
            IRegionReadSource source,
            int3 min,
            int3 max,
            Allocator allocator)
        {
            var bounds = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            var extents = new float3(float.MinValue, float.MinValue, float.MinValue);

            RegionReadView region = default;
            int3 cachedRegionCoord = new int3(int.MinValue);

            for (int bx = min.x; bx <= max.x; bx++)
            for (int by = min.y; by <= max.y; by++)
            for (int bz = min.z; bz <= max.z; bz++)
            {
                int3 coordinate = new int3(bx, by, bz);
                if (!IsSolidAtCoordinate(source, coordinate, ref cachedRegionCoord, ref region))
                    continue;

                float3 center = new float3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                bounds = math.min(bounds, center);
                extents = math.max(extents, center);
            }

            if (bounds.x > extents.x)
                return new NativeArray<float3>(0, allocator);

            return BuildHull(bounds, extents, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRegionBlockSolid(VoxelReadBlock block) =>
            block.Kind == VoxelReadBlockKind.Mixed
            || block.Kind == VoxelReadBlockKind.Uniform
               && block.UniformMaterial != VoxelGrid.MaterialEmpty;

        private static bool IsSolidAtCoordinate(
            IRegionReadSource source,
            int3 coordinate,
            ref int3 cachedRegionCoord,
            ref RegionReadView region)
        {
            // Preserve the pre-refactor coordinate interpretation exactly. This overload's
            // historical API uses coordinates that are shifted once more to choose a local block.
            int3 regionCoord = coordinate >> RegionReadBlockEdgeLog2;
            if (!region.IsCreated || math.any(regionCoord != cachedRegionCoord))
            {
                if (!source.TryAcquireRegion(regionCoord, out region))
                {
                    region = default;
                    cachedRegionCoord = regionCoord;
                    return false;
                }
                cachedRegionCoord = regionCoord;
            }

            int3 localBlock = (coordinate >> ReadBlockEdgeLog2) & RegionReadBlockEdgeMask;
            if (!region.TryGetBlock(localBlock, out VoxelReadBlock block)) return false;
            if (block.Kind == VoxelReadBlockKind.Uniform)
                return block.UniformMaterial != VoxelGrid.MaterialEmpty;
            return block.Kind == VoxelReadBlockKind.Mixed && region.IsBlockOccupied(localBlock);
        }

        private static NativeArray<float3> BuildHull(
            float3 bounds, float3 extents, Allocator allocator)
        {
            var hull = new NativeArray<float3>(8, allocator, NativeArrayOptions.ClearMemory);
            hull[0] = new float3(bounds.x, bounds.y, bounds.z);
            hull[1] = new float3(extents.x, bounds.y, bounds.z);
            hull[2] = new float3(bounds.x, extents.y, bounds.z);
            hull[3] = new float3(extents.x, extents.y, bounds.z);
            hull[4] = new float3(bounds.x, bounds.y, extents.z);
            hull[5] = new float3(extents.x, bounds.y, extents.z);
            hull[6] = new float3(bounds.x, extents.y, extents.z);
            hull[7] = new float3(extents.x, extents.y, extents.z);
            return hull;
        }
    }
}
