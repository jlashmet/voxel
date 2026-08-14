using System;
using System.Runtime.CompilerServices;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Compatibility facade for region hashing. Cross-peer hashes delegate to the shared Core
    /// semantic implementation so server/client allocation history cannot affect convergence.
    /// </summary>
    public static class RegionHasher
    {
        public static uint HashRegion(in Region region, in BrickPool pool) =>
            SemanticRegionHasher.HashRegion(in region, in pool);

        [Obsolete("Use HashRegion(in Region, in BrickPool) for cross-peer drift detection.")]
        public static uint HashRegion(in Region region)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            hash = MixInt(hash, region.Coord.x, prime);
            hash = MixInt(hash, region.Coord.y, prime);
            hash = MixInt(hash, region.Coord.z, prime);
            if (region.BrickRefs.IsCreated)
                for (int i = 0; i < region.BrickRefs.Length; i++)
                    hash = MixInt(hash, region.BrickRefs[i].Value, prime);
            return hash;
        }

        public static uint HashBytes(ReadOnlySpan<byte> data) => SemanticRegionHasher.HashBytes(data);

        public static uint HashUintSpan(ReadOnlySpan<uint> values)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            for (int i = 0; i < values.Length; i++)
            {
                uint value = values[i];
                hash = MixByte(hash, (byte)value, prime);
                hash = MixByte(hash, (byte)(value >> 8), prime);
                hash = MixByte(hash, (byte)(value >> 16), prime);
                hash = MixByte(hash, (byte)(value >> 24), prime);
            }
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreEqual(uint a, uint b) => a == b;

        private static uint MixInt(uint hash, int value, uint prime)
        {
            uint v = unchecked((uint)value);
            hash = MixByte(hash, (byte)v, prime);
            hash = MixByte(hash, (byte)(v >> 8), prime);
            hash = MixByte(hash, (byte)(v >> 16), prime);
            return MixByte(hash, (byte)(v >> 24), prime);
        }

        private static uint MixByte(uint hash, byte value, uint prime)
        {
            hash ^= value;
            return hash * prime;
        }
    }
}
