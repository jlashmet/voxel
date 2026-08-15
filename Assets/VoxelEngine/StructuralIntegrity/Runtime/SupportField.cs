using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.StructuralIntegrity.Runtime
{
    /// <summary>
    /// Deterministic support propagation over logical Storage read blocks. Physical voxel storage
    /// stays owned by Storage; this algorithm only asks whether logical blocks are occupied.
    /// </summary>
    public static class SupportField
    {
        public const byte NBrickReach = 128;
        private const int BlocksPerRegion = VoxelReadGrid.BlocksPerRegionEdge
                                          * VoxelReadGrid.BlocksPerRegionEdge
                                          * VoxelReadGrid.BlocksPerRegionEdge;

        public static void ComputeSupport(
            IRegionReadSource storage,
            int3 regionCoord,
            NativeArray<byte> supportValues,
            Allocator allocator)
        {
            if (supportValues.Length != BlocksPerRegion)
                throw new ArgumentException(
                    $"supportValues must have length {BlocksPerRegion}.", nameof(supportValues));
            if (storage == null || !storage.TryAcquireRegion(regionCoord, out RegionReadView region))
                return;

            for (int i = 0; i < BlocksPerRegion; i++) supportValues[i] = 0;

            var visited = new NativeBitArray(BlocksPerRegion, Allocator.Temp);
            var bfsQueue = new NativeList<int>(BlocksPerRegion >> 2, allocator);
            var bfsSupport = new NativeList<byte>(BlocksPerRegion >> 2, allocator);

            if (regionCoord.y == 0)
            {
                for (int x = 0; x < VoxelReadGrid.BlocksPerRegionEdge; x++)
                for (int z = 0; z < VoxelReadGrid.BlocksPerRegionEdge; z++)
                {
                    int idx = BlockIndex(x, 0, z);
                    if (visited.IsSet(idx) || !region.IsBlockOccupied(new int3(x, 0, z))) continue;
                    visited.Set(idx, true);
                    supportValues[idx] = 0;
                    bfsQueue.Add(idx);
                    bfsSupport.Add(0);
                }
            }

            for (int axis = 0; axis < 3; axis++)
            for (int dir = -1; dir <= 1; dir += 2)
            for (int i = 0; i < BlocksPerRegion; i++)
            {
                if (visited.IsSet(i) || !BlockTouchesBorder(i, axis, dir)) continue;
                int3 block = BlockCoords(i);
                if (!region.IsBlockOccupied(block)) continue;
                visited.Set(i, true);
                supportValues[i] = NBrickReach;
                bfsQueue.Add(i);
                bfsSupport.Add(NBrickReach);
            }

            int head = 0;
            while (head < bfsQueue.Length)
            {
                int curIdx = bfsQueue[head];
                byte curSupport = bfsSupport[head];
                head++;
                if (curSupport == 0) continue;

                int3 c = BlockCoords(curIdx);
                byte next = (byte)(curSupport - 1);
                if (c.x < VoxelReadGrid.BlocksPerRegionEdgeMask) Push(curIdx + 1, next);
                if (c.x > 0) Push(curIdx - 1, next);
                int yStride = VoxelReadGrid.BlocksPerRegionEdge;
                if (c.y < VoxelReadGrid.BlocksPerRegionEdgeMask) Push(curIdx + yStride, next);
                if (c.y > 0) Push(curIdx - yStride, next);
                int zStride = VoxelReadGrid.BlocksPerRegionEdge << VoxelReadGrid.BlocksPerRegionEdgeLog2;
                if (c.z < VoxelReadGrid.BlocksPerRegionEdgeMask) Push(curIdx + zStride, next);
                if (c.z > 0) Push(curIdx - zStride, next);
            }

            visited.Dispose();
            bfsQueue.Dispose();
            bfsSupport.Dispose();

            void Push(int index, byte value)
            {
                if (visited.IsSet(index) || !region.IsBlockOccupied(BlockCoords(index))) return;
                visited.Set(index, true);
                supportValues[index] = value;
                bfsQueue.Add(index);
                bfsSupport.Add(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte MinBrickSupport(int blockIndex, in NativeArray<byte> supportValues) =>
            supportValues[blockIndex];

        public static bool HasUnsupportedBricks(
            int regionCoordX, int regionCoordY, int regionCoordZ,
            in NativeArray<byte> supportValues,
            byte threshold)
        {
            if (supportValues.Length != BlocksPerRegion) return false;
            for (int i = 0; i < BlocksPerRegion; i++)
                if (supportValues[i] <= threshold) return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BlockIndex(int x, int y, int z) =>
            x | (y << VoxelReadGrid.BlocksPerRegionEdgeLog2)
              | (z << (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 BlockCoords(int index) => new int3(
            index & VoxelReadGrid.BlocksPerRegionEdgeMask,
            (index >> VoxelReadGrid.BlocksPerRegionEdgeLog2) & VoxelReadGrid.BlocksPerRegionEdgeMask,
            (index >> (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2)) & VoxelReadGrid.BlocksPerRegionEdgeMask);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool BlockTouchesBorder(int index, int axis, int dir)
        {
            int3 c = BlockCoords(index);
            switch (axis)
            {
                case 0: return dir > 0 ? c.x == VoxelReadGrid.BlocksPerRegionEdgeMask : c.x == 0;
                case 1: return dir > 0 ? c.y == VoxelReadGrid.BlocksPerRegionEdgeMask : c.y == 0;
                case 2: return dir > 0 ? c.z == VoxelReadGrid.BlocksPerRegionEdgeMask : c.z == 0;
                default: return false;
            }
        }
    }
}
