using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    public enum VoxelReadBlockKind : byte
    {
        Empty = 0,
        Uniform = 1,
        Mixed = 2,
    }

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
    /// Borrowed, zero-copy read access to one resident voxel region. Backing native memory is
    /// Storage-owned; callers never dispose it and reacquire after source version changes/unload.
    /// </summary>
    public readonly struct RegionReadView
    {
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
        public bool HasMips => _mipLevelCount > 0;
        public int MipLevelCount => _mipLevelCount;
        public int BlockEdgeCount => VoxelReadGrid.BlocksPerRegionEdge;

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
            // The mip pyramid is optional storage, but a view is a job struct: Unity's job safety
            // system rejects a schedule when any NativeArray field is unconstructed, even one no
            // code path reads. An absent pyramid therefore aliases containers this view already
            // holds and always has, and MipLevelCount drops to zero so nothing can read them as
            // mip data — every pyramid read is gated on HasMips or MipLevelCount below.
            bool hasMips = occupancyMips.IsCreated && materialMips.IsCreated && mipLevelCount > 0;
            _occupancyMips = hasMips ? occupancyMips : hardSurfaceWords;
            _materialMips = hasMips ? materialMips : mixedVoxels;
            _mipLevelCount = hasMips ? mipLevelCount : 0;
            _mixedVoxels = mixedVoxels;
            _mixedSurfaceSemantics = mixedSurfaceSemantics;
            _mixedBoundarySamples = mixedBoundarySamples;
            _mixedOccupancy = mixedOccupancy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsWorldBlock(int3 worldBlockCoord) =>
            math.all((worldBlockCoord >> VoxelReadGrid.BlocksPerRegionEdgeLog2) == RegionCoord);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetWorldBlock(int3 worldBlockCoord, out VoxelReadBlock block)
        {
            if (!ContainsWorldBlock(worldBlockCoord))
            {
                block = default;
                return false;
            }
            return TryGetBlock(WorldToLocalBlock(worldBlockCoord), out block);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWorldBlockOccupied(int3 worldBlockCoord) =>
            ContainsWorldBlock(worldBlockCoord) && IsBlockOccupied(WorldToLocalBlock(worldBlockCoord));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWorldBlockFullySolid(int3 worldBlockCoord) =>
            ContainsWorldBlock(worldBlockCoord) && IsBlockFullySolid(WorldToLocalBlock(worldBlockCoord));

        public bool TryCopyWorldBlock(
            int3 worldBlockCoord,
            NativeArray<byte> materials,
            NativeArray<ushort> surfaceSemantics,
            NativeArray<byte> boundarySamples,
            int destinationOffset)
        {
            if (!ContainsWorldBlock(worldBlockCoord)) return false;
            return TryCopyBlock(WorldToLocalBlock(worldBlockCoord), materials,
                                surfaceSemantics, boundarySamples, destinationOffset);
        }

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
                block = new VoxelReadBlock(VoxelReadBlockKind.Uniform, (byte)(-encoded - 1));
                return true;
            }
            block = new VoxelReadBlock(VoxelReadBlockKind.Mixed, VoxelGrid.MaterialEmpty);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCell(int3 localVoxel, out VoxelCell cell)
        {
            if (!IsCreated || !IsLocalVoxel(localVoxel))
            {
                cell = default;
                return false;
            }

            int3 localBlock = localVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded == -1)
            {
                cell = default;
                return true;
            }
            if (encoded < 0)
            {
                cell = new VoxelCell { BaseMaterialId = (byte)(-encoded - 1) };
                return true;
            }

            int3 inner = localVoxel & VoxelReadGrid.BlockEdgeMask;
            int voxelIndex = inner.x
                           | (inner.y << VoxelReadGrid.BlockEdgeLog2)
                           | (inner.z << (VoxelReadGrid.BlockEdgeLog2 * 2));
            int offset = encoded * VoxelReadGrid.VoxelsPerBlock + voxelIndex;
            byte material = _mixedVoxels[offset];
            cell = new VoxelCell
            {
                BaseMaterialId = material,
                Surface = material == VoxelGrid.MaterialEmpty
                    ? default
                    : VoxelSurfaceSemantics.FromStorage(_mixedSurfaceSemantics[offset]),
                Boundary = new VoxelBoundarySample { Packed = _mixedBoundarySamples[offset] },
            };
            return true;
        }

        public bool TryCopyBlock(
            int3 localBlock,
            NativeArray<byte> materials,
            NativeArray<ushort> surfaceSemantics,
            NativeArray<byte> boundarySamples,
            int destinationOffset)
        {
            if (!IsCreated || !IsLocalBlock(localBlock) || destinationOffset < 0
                || destinationOffset + VoxelReadGrid.VoxelsPerBlock > materials.Length
                || destinationOffset + VoxelReadGrid.VoxelsPerBlock > surfaceSemantics.Length
                || destinationOffset + VoxelReadGrid.VoxelsPerBlock > boundarySamples.Length)
                return false;

            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded == -1)
            {
                FillBlock(materials, destinationOffset, VoxelGrid.MaterialEmpty);
                FillBlock(surfaceSemantics, destinationOffset, 0);
                FillBlock(boundarySamples, destinationOffset, 0);
                return true;
            }
            if (encoded < 0)
            {
                FillBlock(materials, destinationOffset, (byte)(-encoded - 1));
                FillBlock(surfaceSemantics, destinationOffset, 0);
                FillBlock(boundarySamples, destinationOffset, 0);
                return true;
            }

            int sourceOffset = encoded * VoxelReadGrid.VoxelsPerBlock;
            NativeArray<byte>.Copy(_mixedVoxels, sourceOffset, materials, destinationOffset,
                                   VoxelReadGrid.VoxelsPerBlock);
            NativeArray<ushort>.Copy(_mixedSurfaceSemantics, sourceOffset,
                                     surfaceSemantics, destinationOffset,
                                     VoxelReadGrid.VoxelsPerBlock);
            NativeArray<byte>.Copy(_mixedBoundarySamples, sourceOffset,
                                   boundarySamples, destinationOffset,
                                   VoxelReadGrid.VoxelsPerBlock);
            return true;
        }

        public bool TryCopyMixedBlock(
            int3 localBlock,
            NativeArray<byte> materials,
            NativeArray<ushort> surfaceSemantics,
            NativeArray<byte> boundarySamples,
            int destinationOffset)
        {
            if (!IsCreated || !IsLocalBlock(localBlock) || destinationOffset < 0
                || destinationOffset + VoxelReadGrid.VoxelsPerBlock > materials.Length
                || destinationOffset + VoxelReadGrid.VoxelsPerBlock > surfaceSemantics.Length
                || destinationOffset + VoxelReadGrid.VoxelsPerBlock > boundarySamples.Length)
                return false;

            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded < 0) return false;
            int sourceOffset = encoded * VoxelReadGrid.VoxelsPerBlock;
            NativeArray<byte>.Copy(_mixedVoxels, sourceOffset, materials, destinationOffset,
                                   VoxelReadGrid.VoxelsPerBlock);
            NativeArray<ushort>.Copy(_mixedSurfaceSemantics, sourceOffset,
                                     surfaceSemantics, destinationOffset,
                                     VoxelReadGrid.VoxelsPerBlock);
            NativeArray<byte>.Copy(_mixedBoundarySamples, sourceOffset,
                                   boundarySamples, destinationOffset,
                                   VoxelReadGrid.VoxelsPerBlock);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlockOccupied(int3 localBlock)
        {
            if (!IsCreated || !IsLocalBlock(localBlock)) return false;
            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded == -1) return false;
            if (encoded < 0) return (byte)(-encoded - 1) != VoxelGrid.MaterialEmpty;

            int occupancyOffset = encoded * VoxelReadGrid.OccupancyWordsPerBlock;
            ulong aggregate = 0UL;
            for (int i = 0; i < VoxelReadGrid.OccupancyWordsPerBlock; i++)
                aggregate |= _mixedOccupancy[occupancyOffset + i];
            return aggregate != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlockFullySolid(int3 localBlock)
        {
            if (!IsCreated || !IsLocalBlock(localBlock)) return false;
            int encoded = _encodedBlockRefs[BlockIndex(localBlock)];
            if (encoded == -1) return false;
            if (encoded < 0) return (byte)(-encoded - 1) != VoxelGrid.MaterialEmpty;

            int occupancyOffset = encoded * VoxelReadGrid.OccupancyWordsPerBlock;
            for (int i = 0; i < VoxelReadGrid.OccupancyWordsPerBlock; i++)
                if (_mixedOccupancy[occupancyOffset + i] != ulong.MaxValue) return false;
            return true;
        }

        public bool TrySample(int3 localVoxel, int level, out bool occupied, out byte material)
        {
            occupied = false;
            material = VoxelGrid.MaterialEmpty;
            if (!IsCreated || !IsLocalVoxel(localVoxel)) return false;

            if (level < 0)
            {
                if (!TryReadCell(localVoxel, out VoxelCell voxelCell)) return false;
                occupied = voxelCell.IsSolid;
                material = voxelCell.BaseMaterialId;
                return true;
            }

            int3 localBlock = localVoxel >> VoxelReadGrid.BlockEdgeLog2;
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
                int occupancyOffset = encoded * VoxelReadGrid.OccupancyWordsPerBlock;
                ulong aggregate = 0UL;
                for (int i = 0; i < VoxelReadGrid.OccupancyWordsPerBlock; i++)
                    aggregate |= _mixedOccupancy[occupancyOffset + i];
                occupied = aggregate != 0UL;
                material = occupied ? DominantMixedMaterial(encoded) : VoxelGrid.MaterialEmpty;
                return true;
            }

            if (!HasMips || level >= _mipLevelCount) return false;
            int3 mipCell = localBlock >> level;
            int edge = VoxelReadGrid.BlocksPerRegionEdge >> level;
            int index = StoredMipLevelOffset(level)
                      + mipCell.x + edge * (mipCell.y + edge * mipCell.z);
            occupied = _occupancyMips[index] != 0UL;
            material = _materialMips[index];
            return true;
        }

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
            int sourceOffset = storageIndex * VoxelReadGrid.VoxelsPerBlock;
            for (int i = 0; i < VoxelReadGrid.VoxelsPerBlock; i++)
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
        private static int3 WorldToLocalBlock(int3 worldBlockCoord) =>
            worldBlockCoord - (worldBlockCoord >> VoxelReadGrid.BlocksPerRegionEdgeLog2
                               << VoxelReadGrid.BlocksPerRegionEdgeLog2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLocalVoxel(int3 localVoxel) =>
            !math.any(localVoxel < int3.zero)
            && !math.any(localVoxel >= new int3(VoxelGrid.RegionVoxelEdge));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLocalBlock(int3 localBlock) =>
            !math.any(localBlock < int3.zero)
            && !math.any(localBlock >= new int3(VoxelReadGrid.BlocksPerRegionEdge));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BlockIndex(int3 localBlock) =>
            localBlock.x
            | (localBlock.y << VoxelReadGrid.BlocksPerRegionEdgeLog2)
            | (localBlock.z << (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2));

        private static int StoredMipLevelOffset(int level)
        {
            int offset = 0;
            for (int current = 1; current < level; current++)
            {
                int edge = VoxelReadGrid.BlocksPerRegionEdge >> current;
                offset += edge * edge * edge;
            }
            return offset;
        }

        private static void FillBlock(NativeArray<byte> destination, int offset, byte value)
        {
            for (int i = 0; i < VoxelReadGrid.VoxelsPerBlock; i++) destination[offset + i] = value;
        }

        private static void FillBlock(NativeArray<ushort> destination, int offset, ushort value)
        {
            for (int i = 0; i < VoxelReadGrid.VoxelsPerBlock; i++) destination[offset + i] = value;
        }
    }
}
