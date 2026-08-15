using System.Runtime.CompilerServices;
using Unity.Collections;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Borrowed mutable payload for one logical 8^3 block.
    ///
    /// Physical allocation identity stays entirely inside Storage.Runtime. The public hot path
    /// exposes only logical cell/material channels plus an opaque lease token that the issuing
    /// <see cref="IRegionMutationStore"/> consumes when the mutation is completed.
    /// </summary>
    public struct VoxelBlockMutation
    {
        private NativeArray<byte> _materials;
        private NativeArray<ushort> _surfaceSemantics;
        private NativeArray<byte> _boundarySamples;
        private NativeArray<ulong> _occupancy;
        private ulong _leaseToken;
        private bool _metadataChanged;

        public bool IsCreated => _materials.IsCreated;
        public bool MetadataChanged => _metadataChanged;

        /// <summary>
        /// Opaque issuer-owned lease identity. Callers must not interpret or persist this value;
        /// it exists only so the issuing mutation store can match completion to private rollback
        /// state without exposing region, block or pool representation through Storage.Api.
        /// </summary>
        public ulong LeaseToken => _leaseToken;

        /// <summary>
        /// Provider construction boundary for a borrowed logical block. The native arrays must be
        /// block-sized slices and remain owned by the issuing Storage implementation.
        /// </summary>
        public VoxelBlockMutation(
            NativeArray<byte> materials,
            NativeArray<ushort> surfaceSemantics,
            NativeArray<byte> boundarySamples,
            NativeArray<ulong> occupancy,
            ulong leaseToken,
            bool metadataChanged)
        {
            _materials = materials;
            _surfaceSemantics = surfaceSemantics;
            _boundarySamples = boundarySamples;
            _occupancy = occupancy;
            _leaseToken = leaseToken;
            _metadataChanged = metadataChanged;
        }

        /// <summary>
        /// Creates a valid completion lease with no materialised payload. Storage uses this when
        /// the requested material is already uniform but semantic metadata may still have changed.
        /// </summary>
        public static VoxelBlockMutation MetadataOnly(ulong leaseToken, bool metadataChanged) =>
            new VoxelBlockMutation(default, default, default, default, leaseToken, metadataChanged);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetMaterial(int voxelIndex)
        {
            if (!IsCreated || (uint)voxelIndex >= VoxelReadGrid.VoxelsPerBlock)
                return VoxelGrid.MaterialEmpty;
            return _materials[voxelIndex];
        }

        /// <summary>Reads the complete logical cell stored at one voxel in this block.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VoxelCell GetCell(int voxelIndex)
        {
            if (!IsCreated || (uint)voxelIndex >= VoxelReadGrid.VoxelsPerBlock)
                return default;

            byte material = _materials[voxelIndex];
            return new VoxelCell
            {
                BaseMaterialId = material,
                Surface = material == VoxelGrid.MaterialEmpty
                    ? default
                    : VoxelSurfaceSemantics.FromStorage(_surfaceSemantics[voxelIndex]),
                // Authored boundary samples may legitimately survive on the empty side of a
                // surface, so boundary state is independent from occupancy/material.
                Boundary = new VoxelBoundarySample { Packed = _boundarySamples[voxelIndex] }
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

            if (_materials[voxelIndex] == material)
                return false;

            _materials[voxelIndex] = material;
            if (material == VoxelGrid.MaterialEmpty)
            {
                _surfaceSemantics[voxelIndex] = 0;
                _boundarySamples[voxelIndex] = 0;
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

            bool solid = cell.BaseMaterialId != VoxelGrid.MaterialEmpty;
            ushort surface = solid ? cell.Surface.PackedStorage : (ushort)0;
            byte boundary = cell.Boundary.Packed;

            if (_materials[voxelIndex] == cell.BaseMaterialId
                && _surfaceSemantics[voxelIndex] == surface
                && _boundarySamples[voxelIndex] == boundary)
                return false;

            _materials[voxelIndex] = cell.BaseMaterialId;
            _surfaceSemantics[voxelIndex] = surface;
            _boundarySamples[voxelIndex] = boundary;
            SetOccupancy(voxelIndex, solid);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetOccupancy(int voxelIndex, bool occupied)
        {
            int wordIndex = voxelIndex >> 6;
            ulong mask = 1UL << (voxelIndex & 63);
            ulong word = _occupancy[wordIndex];
            _occupancy[wordIndex] = occupied ? word | mask : word & ~mask;
        }
    }
}
