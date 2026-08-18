using Unity.Collections;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Opaque release token for one pinned physical mixed-brick version. Consumers may retain the
    /// token but cannot address Storage slots through it; Storage validates the generation when
    /// the lease is released so a stale token can never affect a recycled slot.
    /// </summary>
    public readonly struct VoxelReadPinToken
    {
        internal readonly int Slot;
        public readonly uint Generation;
        public bool IsValid => Slot >= 0 && Generation != 0;

        internal VoxelReadPinToken(int slot, uint generation)
        {
            Slot = slot;
            Generation = generation;
        }
    }

    /// <summary>
    /// Stable read-only view of one logical 8^3 block. Empty/uniform blocks require no physical
    /// lease. Mixed blocks pin one COW BrickPool version and expose Storage-owned native payload
    /// arrays plus the immutable voxel offset used by Burst jobs. Consumers must never dispose or
    /// write the arrays and must release a valid <see cref="Pin"/> through the source that created
    /// it after every dependent job has finished.
    /// </summary>
    public readonly struct PinnedVoxelReadBlock
    {
        public readonly VoxelReadBlockKind Kind;
        public readonly byte UniformMaterial;
        public readonly int MixedOffset;
        public readonly NativeArray<byte> MixedVoxels;
        public readonly NativeArray<ushort> MixedSurfaceSemantics;
        public readonly NativeArray<byte> MixedBoundarySamples;
        public readonly VoxelReadPinToken Pin;

        public bool HasPinnedPayload => Pin.IsValid;

        internal PinnedVoxelReadBlock(VoxelReadBlockKind kind, byte uniformMaterial,
                                      int mixedOffset,
                                      NativeArray<byte> mixedVoxels,
                                      NativeArray<ushort> mixedSurfaceSemantics,
                                      NativeArray<byte> mixedBoundarySamples,
                                      in VoxelReadPinToken pin)
        {
            Kind = kind;
            UniformMaterial = uniformMaterial;
            MixedOffset = mixedOffset;
            MixedVoxels = mixedVoxels;
            MixedSurfaceSemantics = mixedSurfaceSemantics;
            MixedBoundarySamples = mixedBoundarySamples;
            Pin = pin;
        }

        internal static PinnedVoxelReadBlock Empty => new(
            VoxelReadBlockKind.Empty, VoxelGrid.MaterialEmpty, 0,
            default, default, default, default);

        internal static PinnedVoxelReadBlock Uniform(byte material) => new(
            VoxelReadBlockKind.Uniform, material, 0,
            default, default, default, default);
    }
}
