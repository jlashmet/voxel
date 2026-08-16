using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Generation/revision token for one physically pinned region slot. Generation protects slot
    /// reuse; Revision detects any authoritative commit that raced an optimistic metadata job.
    /// </summary>
    public readonly struct VoxelRegionPinToken
    {
        internal readonly int Slot;
        public readonly uint Generation;
        public readonly uint Revision;
        public bool IsValid => Slot >= 0 && Generation != 0 && Revision != 0;

        internal VoxelRegionPinToken(int slot, uint generation, uint revision)
        {
            Slot = slot;
            Generation = generation;
            Revision = revision;
        }
    }

    /// <summary>
    /// Physically stable region block-reference storage for optimistic Burst metadata traversal.
    /// The encoded refs may change in place while the lease is pinned; consumers must therefore
    /// accept job output only when <c>IsPinnedRegionCurrent</c> still validates the token revision.
    /// Eviction is logical immediately but physical array disposal is deferred until release.
    /// </summary>
    public readonly struct PinnedRegionBlockRefs
    {
        public readonly int3 RegionCoord;
        public readonly NativeArray<int> EncodedBlockRefs;
        public readonly VoxelRegionPinToken Pin;

        public bool IsCreated => Pin.IsValid && EncodedBlockRefs.IsCreated;

        internal PinnedRegionBlockRefs(int3 regionCoord, NativeArray<int> encodedBlockRefs,
                                       in VoxelRegionPinToken pin)
        {
            RegionCoord = regionCoord;
            EncodedBlockRefs = encodedBlockRefs;
            Pin = pin;
        }
    }

    /// <summary>
    /// Stable decoder for Storage's compact block-reference representation. Rendering may consume
    /// encoded refs only through this helper; physical BrickRef remains a Storage.Runtime type.
    /// </summary>
    public static class VoxelReadBlockRefEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VoxelReadBlockKind Kind(int encoded) => encoded >= 0
            ? VoxelReadBlockKind.Mixed
            : encoded == -1 ? VoxelReadBlockKind.Empty : VoxelReadBlockKind.Uniform;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte UniformMaterial(int encoded) => (byte)(-encoded - 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MixedPayloadOffset(int encoded) => encoded * VoxelReadGrid.VoxelsPerBlock;
    }
}
