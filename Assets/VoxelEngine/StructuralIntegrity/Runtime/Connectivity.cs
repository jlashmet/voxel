using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.StructuralIntegrity.Runtime
{
    /// <summary>
    /// Deterministic 6-connected component analysis over logical Storage read blocks.
    /// Physical voxel allocation and occupancy representation remain owned by Storage.
    /// </summary>
    public static class Connectivity
    {
        private const int BlocksPerRegion = VoxelReadGrid.BlocksPerRegionEdge
                                          * VoxelReadGrid.BlocksPerRegionEdge
                                          * VoxelReadGrid.BlocksPerRegionEdge;

        /// <summary>
        /// Flood-fill from a logical block index and return block coordinates in the connected
        /// occupied component. Neighbors are visited in fixed +X, -X, +Y, -Y, +Z, -Z order.
        /// </summary>
        public static NativeList<int3> FloodFill(
            IRegionReadSource storage,
            int3 regionCoord,
            int startBlockIndex,
            Allocator allocator)
        {
            if ((uint)startBlockIndex >= (uint)BlocksPerRegion)
                throw new ArgumentOutOfRangeException(
                    nameof(startBlockIndex), "Must be in [0 .. BlocksPerRegion).");

            var result = new NativeList<int3>(BlocksPerRegion >> 2, allocator);
            if (storage == null
                || !storage.TryAcquireRegion(regionCoord, out RegionReadView region)
                || !region.IsBlockOccupied(BlockCoords(startBlockIndex)))
                return result;

            var visited = new NativeBitArray(BlocksPerRegion, Allocator.Temp);
            var bfs = new NativeList<int>(BlocksPerRegion >> 2, Allocator.Temp);

            visited.Set(startBlockIndex, true);
            bfs.Add(startBlockIndex);

            int head = 0;
            while (head < bfs.Length)
            {
                int current = bfs[head++];
                result.Add(BlockCoords(current));
                EnqueueNeighbors(region, current, visited, ref bfs);
            }

            visited.Dispose();
            bfs.Dispose();
            return result;
        }

        /// <summary>
        /// Label all occupied logical blocks in a region with deterministic component IDs.
        /// Empty blocks retain ID 0. The caller owns and reuses the component array.
        /// </summary>
        public static int LabelComponents(
            IRegionReadSource storage,
            int3 regionCoord,
            NativeArray<int> componentIds)
        {
            if (componentIds.Length != BlocksPerRegion)
                throw new ArgumentException(
                    $"componentIds must have length {BlocksPerRegion}.", nameof(componentIds));

            if (storage == null || !storage.TryAcquireRegion(regionCoord, out RegionReadView region))
                return 0;

            int componentCount = 0;
            var visited = new NativeBitArray(BlocksPerRegion, Allocator.Temp);
            var bfs = new NativeList<int>(64, Allocator.Temp);

            for (int i = 0; i < BlocksPerRegion; i++)
            {
                if (componentIds[i] != 0 || !region.IsBlockOccupied(BlockCoords(i)))
                    continue;

                componentCount++;
                int componentId = componentCount;
                visited.Set(i, true);
                componentIds[i] = componentId;
                bfs.Add(i);

                int head = 0;
                while (head < bfs.Length)
                {
                    int current = bfs[head++];
                    TagNeighbors(region, current, visited, ref bfs, componentIds, componentId);
                }

                bfs.Clear();
            }

            visited.Dispose();
            bfs.Dispose();
            return componentCount;
        }

        /// <summary>
        /// A component is anchored when one of its occupied blocks touches the ground plane or a
        /// region border. Border contact retains the existing conservative R-011 behavior: the
        /// component remains anchored rather than collapsing while cross-region support is unknown.
        /// </summary>
        public static bool IsComponentAnchored(
            IRegionReadSource storage,
            int3 regionCoord,
            in NativeArray<int> componentIds,
            int componentId)
        {
            if (componentId <= 0 || componentIds.Length != BlocksPerRegion
                || storage == null
                || !storage.TryAcquireRegion(regionCoord, out RegionReadView region))
                return false;

            for (int i = 0; i < BlocksPerRegion; i++)
            {
                if (componentIds[i] != componentId) continue;

                int3 block = BlockCoords(i);
                if (!region.IsBlockOccupied(block)) continue;

                if (regionCoord.y == 0 && block.y == 0)
                    return true;

                if (TouchesRegionBorder(block))
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetComponentId(int blockIndex, in NativeArray<int> componentIds) =>
            componentIds[blockIndex];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnqueueNeighbors(
            RegionReadView region,
            int current,
            NativeBitArray visited,
            ref NativeList<int> bfs)
        {
            int3 block = BlockCoords(current);
            if (block.x < VoxelReadGrid.BlocksPerRegionEdgeMask)
                EnqueueIfOccupied(region, visited, ref bfs, current + 1);
            if (block.x > 0)
                EnqueueIfOccupied(region, visited, ref bfs, current - 1);

            int yStride = VoxelReadGrid.BlocksPerRegionEdge;
            if (block.y < VoxelReadGrid.BlocksPerRegionEdgeMask)
                EnqueueIfOccupied(region, visited, ref bfs, current + yStride);
            if (block.y > 0)
                EnqueueIfOccupied(region, visited, ref bfs, current - yStride);

            int zStride = VoxelReadGrid.BlocksPerRegionEdge << VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (block.z < VoxelReadGrid.BlocksPerRegionEdgeMask)
                EnqueueIfOccupied(region, visited, ref bfs, current + zStride);
            if (block.z > 0)
                EnqueueIfOccupied(region, visited, ref bfs, current - zStride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TagNeighbors(
            RegionReadView region,
            int current,
            NativeBitArray visited,
            ref NativeList<int> bfs,
            NativeArray<int> componentIds,
            int componentId)
        {
            int3 block = BlockCoords(current);
            if (block.x < VoxelReadGrid.BlocksPerRegionEdgeMask)
                TagIfOccupied(region, visited, ref bfs, componentIds, current + 1, componentId);
            if (block.x > 0)
                TagIfOccupied(region, visited, ref bfs, componentIds, current - 1, componentId);

            int yStride = VoxelReadGrid.BlocksPerRegionEdge;
            if (block.y < VoxelReadGrid.BlocksPerRegionEdgeMask)
                TagIfOccupied(region, visited, ref bfs, componentIds, current + yStride, componentId);
            if (block.y > 0)
                TagIfOccupied(region, visited, ref bfs, componentIds, current - yStride, componentId);

            int zStride = VoxelReadGrid.BlocksPerRegionEdge << VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (block.z < VoxelReadGrid.BlocksPerRegionEdgeMask)
                TagIfOccupied(region, visited, ref bfs, componentIds, current + zStride, componentId);
            if (block.z > 0)
                TagIfOccupied(region, visited, ref bfs, componentIds, current - zStride, componentId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnqueueIfOccupied(
            RegionReadView region,
            NativeBitArray visited,
            ref NativeList<int> bfs,
            int index)
        {
            if (visited.IsSet(index) || !region.IsBlockOccupied(BlockCoords(index))) return;
            visited.Set(index, true);
            bfs.Add(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TagIfOccupied(
            RegionReadView region,
            NativeBitArray visited,
            ref NativeList<int> bfs,
            NativeArray<int> componentIds,
            int index,
            int componentId)
        {
            if (visited.IsSet(index) || !region.IsBlockOccupied(BlockCoords(index))) return;
            visited.Set(index, true);
            componentIds[index] = componentId;
            bfs.Add(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 BlockCoords(int index) => new int3(
            index & VoxelReadGrid.BlocksPerRegionEdgeMask,
            (index >> VoxelReadGrid.BlocksPerRegionEdgeLog2) & VoxelReadGrid.BlocksPerRegionEdgeMask,
            (index >> (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2)) & VoxelReadGrid.BlocksPerRegionEdgeMask);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TouchesRegionBorder(int3 block) =>
            block.x == 0 || block.x == VoxelReadGrid.BlocksPerRegionEdgeMask
            || block.y == 0 || block.y == VoxelReadGrid.BlocksPerRegionEdgeMask
            || block.z == 0 || block.z == VoxelReadGrid.BlocksPerRegionEdgeMask;
    }
}
