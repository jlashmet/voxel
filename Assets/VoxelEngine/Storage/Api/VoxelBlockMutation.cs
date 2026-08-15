using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Borrowed mutable payload for one logical 8^3 block.
    ///
    /// Physical allocation identity stays internal to Storage. The public hot path exposes only
    /// material reads/writes and the semantic metadata-change bit needed by edit orchestration.
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

            int wordIndex = _occupancyOffset + (voxelIndex >> 6);
            ulong mask = 1UL << (voxelIndex & 63);
            ulong word = _occupancy[wordIndex];
            _occupancy[wordIndex] = material == VoxelGrid.MaterialEmpty
                ? word & ~mask
                : word | mask;
            return true;
        }
    }
}
