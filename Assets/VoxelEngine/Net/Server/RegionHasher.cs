using System;
using System.Runtime.CompilerServices;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Canonical semantic region hashing for drift detection.
    ///
    /// Pool indices are allocator-local implementation details and MUST NOT participate in a
    /// cross-peer hash. Uniform bricks hash their material; mixed bricks hash all 512 material
    /// bytes in voxel order. Two peers with identical world material therefore produce the same
    /// hash even if their BrickPool allocation histories differ.
    /// </summary>
    public static class RegionHasher
    {
        private const uint k_FnvOffsetBasis = 2166136261u;
        private const uint k_FnvPrime = 16777619u;

        public static uint HashRegion(in Region region, in BrickPool pool)
        {
            uint hash = k_FnvOffsetBasis;
            hash = MixInt(hash, region.Coord.x);
            hash = MixInt(hash, region.Coord.y);
            hash = MixInt(hash, region.Coord.z);

            if (!region.BrickRefs.IsCreated)
                return hash;

            for (int i = 0; i < region.BrickRefs.Length; i++)
            {
                BrickRef brick = region.BrickRefs[i];
                if (brick.IsMixed)
                {
                    hash = FnvMixByte(hash, 2); // mixed discriminator
                    for (int voxel = 0; voxel < VoxelDimensions.VoxelsPerBrick; voxel++)
                        hash = FnvMixByte(hash, pool.GetVoxel(brick.PoolIndex, voxel));
                }
                else
                {
                    hash = FnvMixByte(hash, 1); // uniform discriminator
                    hash = FnvMixByte(hash, brick.UniformMaterial);
                }
            }

            return hash;
        }

        /// <summary>
        /// Legacy structural hash retained only for local diagnostics. It includes pool indices and
        /// therefore must never be compared across server/client peers.
        /// </summary>
        [Obsolete("Use HashRegion(in Region, in BrickPool) for cross-peer drift detection.")]
        public static uint HashRegion(in Region region)
        {
            uint hash = k_FnvOffsetBasis;
            hash = MixInt(hash, region.Coord.x);
            hash = MixInt(hash, region.Coord.y);
            hash = MixInt(hash, region.Coord.z);

            if (region.BrickRefs.IsCreated)
                for (int i = 0; i < region.BrickRefs.Length; i++)
                    hash = MixInt(hash, region.BrickRefs[i].Value);

            return hash;
        }

        public static uint HashBytes(ReadOnlySpan<byte> data)
        {
            uint hash = k_FnvOffsetBasis;
            for (int i = 0; i < data.Length; i++)
                hash = FnvMixByte(hash, data[i]);
            return hash;
        }

        public static uint HashUintSpan(ReadOnlySpan<uint> values)
        {
            uint hash = k_FnvOffsetBasis;
            for (int i = 0; i < values.Length; i++)
            {
                uint value = values[i];
                hash = FnvMixByte(hash, (byte)value);
                hash = FnvMixByte(hash, (byte)(value >> 8));
                hash = FnvMixByte(hash, (byte)(value >> 16));
                hash = FnvMixByte(hash, (byte)(value >> 24));
            }
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreEqual(uint a, uint b) => a == b;

        private static uint MixInt(uint hash, int value)
        {
            uint v = unchecked((uint)value);
            hash = FnvMixByte(hash, (byte)v);
            hash = FnvMixByte(hash, (byte)(v >> 8));
            hash = FnvMixByte(hash, (byte)(v >> 16));
            hash = FnvMixByte(hash, (byte)(v >> 24));
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FnvMixByte(uint hash, byte value)
        {
            hash ^= value;
            return hash * k_FnvPrime;
        }
    }
}
