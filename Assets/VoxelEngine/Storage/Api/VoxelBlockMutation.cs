using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Borrowed mutable payload for one logical 8^3 block.
    ///
    /// Physical allocation identity stays internal to Storage. The public hot path exposes logical
    /// cell/material reads and writes plus the semantic metadata-change bit needed by mutation
    /// orchestration.
    /// </summary>
    public struct VoxelBlockMutation
    {
        private NativeArray<byte> _materials;
        private NativeArray<ushort> _surfaceSemantics;
        private NativeArray<byte> _boundarySamples;
        private NativeArray<ulong> _occupancy;
        private int _voxelOffset;
        private int _occupancyOffset;

        internal int3 RegionCoord;
        internal int BlockIndex;
        internal int OriginalEncodedRef;
        internal int PoolIndex;
        internal bool MaterializedUniform;
        internal bool MetadataChangedInternal;

        public bool IsCreated => _materials.IsCreated && PoolIndex >= 0;
        public bool MetadataChanged => MetadataChangedInternal;

        internal VoxelBlockMutation(
            NativeArray<byte> materials,
            NativeArray<ushort> surfaceSemantics,
            NativeArray<byte> boundarySamples,
            NativeArray<ulong> occupancy,
            int voxelOffset,
            int occupancyOffset,
            int3 regionCoord,
            int blockIndex,
            int originalEncodedRef,
            int poolIndex,
            bool materializedUniform,
            bool metadataChanged)
        {
            _materials = materials;
            _surfaceSemantics = surfaceSemantics;
            _boundarySamples = boundarySamples;
            _occupancy = occupancy;
            _voxelOffset = voxelOffset;
            _occupancyOffset = occupancyOffset;
            RegionCoord = regionCoord;
            BlockIndex = blockIndex;
            OriginalEncodedRef = originalEncodedRef;
            PoolIndex = poolIndex;
            MaterializedUniform = materializedUniform;
            MetadataChangedInternal = metadataChanged;
        }

        internal static VoxelBlockMutation MetadataOnly(
            int3 regionCoord,
            int blockIndex,
            int originalEncodedRef,
            bool metadataChanged) => new VoxelBlockMutation(
                default, default, default, default,
                0, 0,
                regionCoord, blockIndex, originalEncodedRef,
                -1, false, metadataChanged);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetMaterial(int voxelIndex)
        {
            if (!IsCreated || (uint)voxelIndex >= VoxelReadGrid.VoxelsPerBlock)
                return VoxelGrid.MaterialEmpty;
            return _materials[_voxelOffset + voxelIndex];
        }

        /// <summary>Reads the complete logical cell stored at one voxel in this block.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VoxelCell GetCell(int voxelIndex)
        {
            if (!IsCreated || (uint)voxelIndex >= VoxelReadGrid.VoxelsPerBlock)
                return default;

            int offset = _voxelOffset + voxelIndex;
            byte material = _materials[offset];
            return new VoxelCell
            {
                BaseMaterialId = material,
                Surface = material == VoxelGrid.MaterialEmpty
                    ? default
                    : VoxelSurfaceSemantics.FromStorage(_surfaceSemantics[offset]),
                // Authored boundary samples may legitimately survive on the empty side of a
                // surface, so boundary state is independent from occupancy/material.
                Boundary = new VoxelBoundarySample { Packed = _boundarySamples[offset] }
            };
        }

        /// <summary>
        /// Writes one material byte and maintains the same occupancy/surface semantics as physical
        /// Storage: destruction clears authored surface/boundary payload; occupied material-only
        /// writes preserve it. Returns false when the material is already identical.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetMaterial(int voxelIndex, byte material)
        {
            if (!IsCreated || (uint)voxelIndex >= VoxelReadGrid.VoxelsPerBlock)
                return false;

            int offset = _voxelOffset + voxelIndex;
            if (_materials[offset] == material)
                return false;

            _materials[offset] = material;
            if (material == VoxelGrid.MaterialEmpty)
            {
                _surfaceSemantics[offset] = 0;
                _boundarySamples[offset] = 0;
            }

            SetOccupancy(voxelIndex, material != VoxelGrid.MaterialEmpty);
            return true;
        }

        /// <summary>
        /// Writes the complete logical cell. Empty cells discard surface presentation state but
        /// preserve authored boundary distance, matching the authoritative Storage cell contract.
        /// Returns false when the normalized logical cell is unchanged.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetCell(int voxelIndex, in VoxelCell cell)
        {
            if (!IsCreated || (uint)voxelIndex >= VoxelReadGrid.VoxelsPerBlock)
                return false;

            int offset = _voxelOffset + voxelIndex;
            bool solid = cell.BaseMaterialId != VoxelGrid.MaterialEmpty;
            ushort surface = solid ? cell.Surface.PackedStorage : (ushort)0;
            byte boundary = cell.Boundary.Packed;

            if (_materials[offset] == cell.BaseMaterialId
                && _surfaceSemantics[offset] == surface
                && _boundarySamples[offset] == boundary)
                return false;

            _materials[offset] = cell.BaseMaterialId;
            _surfaceSemantics[offset] = surface;
            _boundarySamples[offset] = boundary;
            SetOccupancy(voxelIndex, solid);
            return true;
        }

        /// <summary>
        /// Copies one already-normalized Storage mixed-block payload into this mutation using
        /// contiguous native copies, then rebuilds the eight occupancy words from the material
        /// bytes. This is intended for trusted Storage-to-Storage transfer paths such as async
        /// authoring publication; it avoids 512 logical-cell conversions and setter calls while
        /// preserving the exact authored surface and boundary payload.
        /// </summary>
        public bool CopyStoragePayload(
            NativeArray<byte> materials,
            NativeArray<ushort> surfaceSemantics,
            NativeArray<byte> boundarySamples,
            int sourceOffset)
        {
            int count = VoxelReadGrid.VoxelsPerBlock;
            if (!IsCreated
                || sourceOffset < 0
                || !materials.IsCreated
                || !surfaceSemantics.IsCreated
                || !boundarySamples.IsCreated
                || sourceOffset > materials.Length - count
                || sourceOffset > surfaceSemantics.Length - count
                || sourceOffset > boundarySamples.Length - count)
                return false;

            NativeArray<byte>.Copy(materials, sourceOffset, _materials, _voxelOffset, count);
            NativeArray<ushort>.Copy(
                surfaceSemantics, sourceOffset, _surfaceSemantics, _voxelOffset, count);
            NativeArray<byte>.Copy(
                boundarySamples, sourceOffset, _boundarySamples, _voxelOffset, count);

            for (int word = 0; word < VoxelReadGrid.OccupancyWordsPerBlock; word++)
            {
                ulong occupied = 0UL;
                int firstVoxel = sourceOffset + (word << 6);
                for (int bit = 0; bit < 64; bit++)
                {
                    if (materials[firstVoxel + bit] != VoxelGrid.MaterialEmpty)
                        occupied |= 1UL << bit;
                }
                _occupancy[_occupancyOffset + word] = occupied;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetOccupancy(int voxelIndex, bool occupied)
        {
            int wordIndex = _occupancyOffset + (voxelIndex >> 6);
            ulong mask = 1UL << (voxelIndex & 63);
            ulong word = _occupancy[wordIndex];
            _occupancy[wordIndex] = occupied ? word | mask : word & ~mask;
        }
    }
}
