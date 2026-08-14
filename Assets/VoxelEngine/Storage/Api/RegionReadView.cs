using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>Logical state of one Storage read block. No allocator or pool identity is exposed.</summary>
    public enum VoxelReadBlockKind : byte
    {
        Empty = 0,
        Uniform = 1,
        Mixed = 2,
    }

    /// <summary>
    /// Lightweight description of one logical read block. Mixed blocks deliberately expose no
    /// backing-storage index; callers copy/read their payload through <see cref="RegionReadView"/>.
    /// </summary>
    public readonly struct VoxelReadBlock
    {
        public readonly VoxelReadBlockKind Kind;
        public readonly byte UniformMaterial;

        internal VoxelReadBlock(VoxelReadBlockKind kind, byte uniformMaterial)
        {
            Kind = kind;
            UniformMaterial = uniformMaterial;
        }

        public bool IsSolid => Kind == VoxelReadBlockKind.Mixed
                            || Kind == VoxelReadBlockKind.Uniform
                               && UniformMaterial != VoxelGrid.MaterialEmpty;
    }

    /// <summary>
    /// Borrowed, zero-copy read access to one resident voxel region.
    ///
    /// The arrays backing this struct are owned by Storage. A consumer must never dispose them,
    /// retain the view across region unload, or assume it is still current after the source
    /// version changes. Sparse-world lookup happens before this view is acquired; hot reads are
    /// direct native-array operations with no interface dispatch.
    /// </summary>
    public readonly struct RegionReadView
    {
        // These constants describe the current read granule only inside the API implementation.
        // They are intentionally not public world-layout vocabulary and do not expose pool slots.
        private const int BlockEdgeLog2 = 3;
        private const int BlockEdge = 1 << BlockEdgeLog2;
        private const int BlockEdgeMask = BlockEdge - 1;
        private const int VoxelsPerBlock = BlockEdge * BlockEdge * BlockEdge;
        private const int OccupancyWordsPerBlock = VoxelsPerBlock / 64;
        private const int RegionBlockEdgeLog2 = VoxelGrid.RegionVoxelEdgeLog2 - BlockEdgeLog2;
        private const int RegionBlockEdge = 1 << RegionBlockEdgeLog2;
        private const int RegionBlockEdgeMask = RegionBlockEdge - 1;

        private readonly NativeArray<int> _encodedBlockRefs;
        private readonly NativeArray<ulong> _hardSurfaceWords;
        private readonly NativeArray<ulong> _occupancyMips;
        private readonly NativeArray<byte> _materialMips;
        private readonly int _mipLevelCount;
        private readonly NativeArray<byte> _mixedVoxels;
        private readonly NativeArray<ushort> _mixedSurfaceSemantics;
        private readonly NativeArray<byte> _mixedBoundarySamples;
        private readonly NativeArray<ulong> _mixedOccupancy;

        public int3 RegionCoord { get; }
        public ulong Version { get; }
        public bool IsCreated => _encodedBlockRefs.IsCreated;
        public bool HasMips => _occupancyMips.IsCreated && _mipLevelCount > 0;
        public int MipLevelCount => _mipLevelCount;

        internal RegionReadView(
            int3 regionCoord,
            ulong version,
            NativeArray<int> encodedBlockRefs,
            NativeArray<ulong> hardSurfaceWords,
            NativeArray<ulong> occupancyMips,
            NativeArray<byte> materialMips,
            int mipLevelCount,
            NativeArray<byte> mixedVoxels,
            NativeArray<ushort> mixedSurfaceSemantics,
            NativeArray<byte> mixedBoundarySamples,
            NativeArray<ulong> mixedOccupancy)
        {
            RegionCoord = regionCoord;
            Version = version;
            _encodedBlockRefs = encodedBlockRefs;
            _hardSurfaceWords = hardSurfaceWords;
            _occupancyMips = occupancyMips;
            _materialMips = materialMips;
            _mipLevelCount = mipLevelCount;
            _mixedVoxels = mixedVoxels;
            _mixedSurfaceSemantics = mixedSurfaceSemantics;
            _mixedBoundarySamples = mixedBoundarySamples;
            _mixedOccupancy = mixedOccupancy;
        }

        /// <summary>Describes a local read block without revealing a Storage pool slot.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetBlock(int3 localBlock, out VoxelReadBlock block)
        {
            if (!IsCreated || !IsLocalBlock(localBlock))
            {
                block = default;
                return false;
            }

            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded == -1)
            {
                block = new VoxelReadBlock(VoxelReadBlockKind.Empty, VoxelGrid.MaterialEmpty);
                return true;
            }

            if (encoded < 0)
            {
                block = new VoxelReadBlock(VoxelReadBlockKind.Uniform,
                                          (byte)(-encoded - 1));
                return true;
            }

            block = new VoxelReadBlock(VoxelReadBlockKind.Mixed, VoxelGrid.MaterialEmpty);
            return true;
        }

        /// <summary>Reads one local voxel cell directly from the borrowed native storage.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCell(int3 localVoxel, out VoxelCell cell)
        {
            if (!IsCreated || !IsLocalVoxel(localVoxel))
            {
                cell = default;
                return false;
            }

            int3 localBlock = localVoxel >> BlockEdgeLog2;
            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded == -1)
            {
                cell = default;
                return true;
            }

            if (encoded < 0)
            {
                cell = new VoxelCell
                {
                    BaseMaterialId = (byte)(-encoded - 1),
                    Surface = default,
                    Boundary = default,
                };
                return true;
            }

            int3 inner = localVoxel & BlockEdgeMask;
            int voxelIndex = inner.x | (inner.y << BlockEdgeLog2)
                                    | (inner.z << (BlockEdgeLog2 * 2));
            int offset = encoded * VoxelsPerBlock + voxelIndex;
            byte material = _mixedVoxels[offset];
            cell = new VoxelCell
            {
                BaseMaterialId = material,
                Surface = material == VoxelGrid.MaterialEmpty
                    ? default
                    : VoxelSurfaceSemantics.FromStorage(_mixedSurfaceSemantics[offset]),
                Boundary = material == VoxelGrid.MaterialEmpty
                    ? default
                    : new VoxelBoundarySample { Packed = _mixedBoundarySamples[offset] },
            };
            return true;
        }

        /// <summary>
        /// Copies one mixed block into caller-owned immutable snapshot buffers. Uniform and empty
        /// blocks return false because their complete value is already in <see cref="VoxelReadBlock"/>.
        /// </summary>
        public bool TryCopyMixedBlock(
            int3 localBlock,
            NativeArray<byte> materials,
            NativeArray<ushort> surfaceSemantics,
            NativeArray<byte> boundarySamples,
            int destinationOffset)
        {
            if (!IsCreated || !IsLocalBlock(localBlock) || destinationOffset < 0)
                return false;
            if (destinationOffset + VoxelsPerBlock > materials.Length
                || destinationOffset + VoxelsPerBlock > surfaceSemantics.Length
                || destinationOffset + VoxelsPerBlock > boundarySamples.Length)
                return false;

            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded < 0) return false;

            int sourceOffset = encoded * VoxelsPerBlock;
            NativeArray<byte>.Copy(_mixedVoxels, sourceOffset,
                                   materials, destinationOffset, VoxelsPerBlock);
            NativeArray<ushort>.Copy(_mixedSurfaceSemantics, sourceOffset,
                                     surfaceSemantics, destinationOffset, VoxelsPerBlock);
            NativeArray<byte>.Copy(_mixedBoundarySamples, sourceOffset,
                                   boundarySamples, destinationOffset, VoxelsPerBlock);
            return true;
        }

        /// <summary>True when any voxel in the local block is occupied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlockOccupied(int3 localBlock)
        {
            if (!IsCreated || !IsLocalBlock(localBlock)) return false;
            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded == -1) return false;
            if (encoded < 0) return (byte)(-encoded - 1) != VoxelGrid.MaterialEmpty;

            int occupancyOffset = encoded * OccupancyWordsPerBlock;
            ulong aggregate = 0UL;
            for (int i = 0; i < OccupancyWordsPerBlock; i++)
                aggregate |= _mixedOccupancy[occupancyOffset + i];
            return aggregate != 0UL;
        }

        /// <summary>
        /// Samples the local region at a requested mip level. Negative levels read the exact
        /// voxel, level zero aggregates one read block, and stored levels read the region mip
        /// pyramid. Returns false only when the coordinate or requested mip is unavailable.
        /// </summary>
        public bool TrySample(int3 localVoxel, int level, out bool occupied, out byte material)
        {
            occupied = false;
            material = VoxelGrid.MaterialEmpty;
            if (!IsCreated || !IsLocalVoxel(localVoxel)) return false;

            if (level < 0)
            {
                if (!TryReadCell(localVoxel, out VoxelCell cell)) return false;
                occupied = cell.IsSolid;
                material = cell.BaseMaterialId;
                return true;
            }

            int3 localBlock = localVoxel >> BlockEdgeLog2;
            if (level == 0)
            {
                int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
                if (encoded == -1) return true;
                if (encoded < 0)
                {
                    material = (byte)(-encoded - 1);
                    occupied = material != VoxelGrid.MaterialEmpty;
                    return true;
                }

                int occupancyOffset = encoded * OccupancyWordsPerBlock;
                ulong aggregate = 0UL;
                for (int i = 0; i < OccupancyWordsPerBlock; i++)
                    aggregate |= _mixedOccupancy[occupancyOffset + i];
                occupied = aggregate != 0UL;
                material = occupied ? DominantMixedMaterial(encoded) : VoxelGrid.MaterialEmpty;
                return true;
            }

            if (!HasMips || level >= _mipLevelCount) return false;
            int3 cell = localBlock >> level;
            int edge = RegionBlockEdge >> level;
            int index = StoredMipLevelOffset(level)
                      + cell.x + edge * (cell.y + edge * cell.z);
            occupied = _occupancyMips[index] != 0UL;
            material = _materialMips[index];
            return true;
        }

        /// <summary>Whether this local read block was authored as hard structure geometry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsHardSurfaceBlock(int3 localBlock)
        {
            if (!_hardSurfaceWords.IsCreated || !IsLocalBlock(localBlock)) return false;
            int index = BlockIndex(localBlock);
            ulong word = _hardSurfaceWords[index >> 6];
            return (word & (1UL << (index & 63))) != 0UL;
        }

        private byte DominantMixedMaterial(int storageIndex)
        {
            Span<int> counts = stackalloc int[256];
            counts.Clear();
            int sourceOffset = storageIndex * VoxelsPerBlock;
            for (int i = 0; i < VoxelsPerBlock; i++)
                counts[_mixedVoxels[sourceOffset + i]]++;

            byte best = VoxelGrid.MaterialEmpty;
            int bestCount = 0;
            for (int candidate = 1; candidate < 256; candidate++)
            {
                if (counts[candidate] <= bestCount) continue;
                bestCount = counts[candidate];
                best = (byte)candidate;
            }
            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLocalVoxel(int3 localVoxel) =>
            !math.any(localVoxel < int3.zero)
            && !math.any(localVoxel >= new int3(VoxelGrid.RegionVoxelEdge));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLocalBlock(int3 localBlock) =>
            !math.any(localBlock < int3.zero)
            && !math.any(localBlock >= new int3(RegionBlockEdge));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BlockIndex(int3 localBlock) =>
            localBlock.x | (localBlock.y << RegionBlockEdgeLog2)
                         | (localBlock.z << (RegionBlockEdgeLog2 * 2));

        private static int StoredMipLevelOffset(int level)
        {
            int offset = 0;
            for (int current = 1; current < level; current++)
            {
                int edge = RegionBlockEdge >> current;
                offset += edge * edge * edge;
            }
            return offset;
        }
    }
}
