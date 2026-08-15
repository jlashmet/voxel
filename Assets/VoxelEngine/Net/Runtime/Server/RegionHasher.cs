using System;
using System.Runtime.CompilerServices;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Network-payload hashing helpers only. Authoritative semantic region hashing belongs to
    /// Storage and must be obtained through a Storage.Api capability; allocator-local Region,
    /// BrickRef, and BrickPool representation never enter the networking contract.
    /// </summary>
    public static class RegionHasher
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint HashBytes(ReadOnlySpan<byte> data)
        {
            uint hash = FnvOffsetBasis;
            for (int i = 0; i < data.Length; i++)
                hash = MixByte(hash, data[i]);
            return hash;
        }

        public static uint HashUintSpan(ReadOnlySpan<uint> values)
        {
            uint hash = FnvOffsetBasis;
            for (int i = 0; i < values.Length; i++)
            {
                uint value = values[i];
                hash = MixByte(hash, (byte)value);
                hash = MixByte(hash, (byte)(value >> 8));
                hash = MixByte(hash, (byte)(value >> 16));
                hash = MixByte(hash, (byte)(value >> 24));
            }
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreEqual(uint a, uint b) => a == b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixByte(uint hash, byte value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }
    }
}
