using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Server-authoritative registry of protected zones and the validation predicate that
    /// enforces them (FR-019).
    ///
    /// A protected zone is a region-scoped bitmask over the region's logical read-block grid,
    /// one bit per block. Storing the mask per region rather than per voxel keeps the predicate
    /// a single bit test in the validation hot path without exposing Storage's physical layout.
    /// </summary>
    public struct ProtectedZones : IDisposable
    {
        private const int k_BlocksPerAxis = VoxelReadGrid.BlocksPerRegionEdge;
        private const int k_BlocksPerRegion =
            k_BlocksPerAxis * k_BlocksPerAxis * k_BlocksPerAxis;

        public const int WordsPerRegionMask = k_BlocksPerRegion / 64;

        private NativeHashMap<int3, int> _regionToMaskOffset;
        private NativeList<ulong> _maskWords;

        public bool IsCreated => _regionToMaskOffset.IsCreated;
        public int ProtectedRegionCount => _regionToMaskOffset.IsCreated ? _regionToMaskOffset.Count : 0;

        public ProtectedZones(int expectedRegions, Allocator allocator)
        {
            _regionToMaskOffset = new NativeHashMap<int3, int>(expectedRegions, allocator);
            _maskWords = new NativeList<ulong>(expectedRegions * WordsPerRegionMask, allocator);
        }

        public void ProtectBrick(int3 regionCoord, int brickX, int brickY, int brickZ)
        {
            if ((uint)brickX >= k_BlocksPerAxis ||
                (uint)brickY >= k_BlocksPerAxis ||
                (uint)brickZ >= k_BlocksPerAxis)
                throw new ArgumentOutOfRangeException(
                    nameof(brickX), $"Block coordinate ({brickX},{brickY},{brickZ}) is outside the region.");

            int offset = GetOrCreateMask(regionCoord);
            int bit = BlockIndex(brickX, brickY, brickZ);
            _maskWords[offset + (bit >> 6)] |= 1UL << (bit & 63);
        }

        public void ProtectBox(int3 regionCoord, int3 minBrick, int3 maxBrick)
        {
            for (int z = minBrick.z; z <= maxBrick.z; z++)
            for (int y = minBrick.y; y <= maxBrick.y; y++)
            for (int x = minBrick.x; x <= maxBrick.x; x++)
                ProtectBrick(regionCoord, x, y, z);
        }

        public void ClearRegion(int3 regionCoord)
        {
            if (!_regionToMaskOffset.TryGetValue(regionCoord, out int offset))
                return;

            for (int i = 0; i < WordsPerRegionMask; i++)
                _maskWords[offset + i] = 0UL;

            _regionToMaskOffset.Remove(regionCoord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsProtected(int3 worldVoxel)
        {
            if (!_regionToMaskOffset.IsCreated)
                return false;

            int3 regionCoord = worldVoxel >> VoxelGrid.RegionVoxelEdgeLog2;
            int3 localVoxel = worldVoxel - (regionCoord << VoxelGrid.RegionVoxelEdgeLog2);
            int3 blockCoord = localVoxel >> VoxelReadGrid.BlockEdgeLog2;

            if (!_regionToMaskOffset.TryGetValue(regionCoord, out int offset))
                return false;

            int bit = BlockIndex(blockCoord.x, blockCoord.y, blockCoord.z);
            return (_maskWords[offset + (bit >> 6)] & (1UL << (bit & 63))) != 0UL;
        }

        public bool IntersectsProtected(int3 minVoxel, int3 maxVoxel)
        {
            for (int z = minVoxel.z; z <= maxVoxel.z; z++)
            for (int y = minVoxel.y; y <= maxVoxel.y; y++)
            for (int x = minVoxel.x; x <= maxVoxel.x; x++)
                if (IsProtected(new int3(x, y, z)))
                    return true;

            return false;
        }

        private int GetOrCreateMask(int3 regionCoord)
        {
            if (_regionToMaskOffset.TryGetValue(regionCoord, out int existing))
                return existing;

            int offset = _maskWords.Length;
            for (int i = 0; i < WordsPerRegionMask; i++)
                _maskWords.Add(0UL);

            _regionToMaskOffset.Add(regionCoord, offset);
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BlockIndex(int x, int y, int z) =>
            x
            | (y << VoxelReadGrid.BlocksPerRegionEdgeLog2)
            | (z << (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2));

        public void Dispose()
        {
            if (_regionToMaskOffset.IsCreated) _regionToMaskOffset.Dispose();
            if (_maskWords.IsCreated) _maskWords.Dispose();
        }
    }
}
