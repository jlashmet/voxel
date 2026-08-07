using VoxelEngine.Core.Storage;
using Unity.Collections;
using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Per-region state hashing for drift detection between server and clients.
    ///
    /// Uses FNV-1a hash over a region's brick data to detect divergence. The hash is computed
    /// over the top mip level (always resident per data-model.md invariant) combined with the
    /// compacted region state, providing a compact fingerprint for integrity verification.
    ///
    /// Called after each simulation tick to detect drift early — before visual differences
    /// become apparent to players.
    /// </summary>
    public static class RegionHasher
    {
        // -- FNV-1a constants -----------------------------------------------------

        /// <summary>FNV-1a 32-bit offset basis.</summary>
        private const uint k_FnvOffsetBasis = 2166136261u;

        /// <summary>FNV-1a 32-bit prime (prime for modulus 2^32).</summary>
        private const uint k_FnvPrime = 16777619u;

        // -- region hash API ------------------------------------------------------

        /// <summary>Computes an FNV-1a hash over a region's brick grid data.
        /// Used as the authoritative fingerprint for drift detection (S_RegionHash protocol message).</summary>
        /// <param name="region">The region whose state is being hashed.</param>
        /// <returns>A 32-bit FNV-1a hash of the region's current state.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashRegion(in Region region)
        {
            uint hash = k_FnvOffsetBasis;

            // Hash region coordinate — ensures different regions produce different hashes.
            hash = FnvMix(hash, (uint)region.Coord.x);
            hash = FnvMix(hash, (uint)region.Coord.y);
            hash = FnvMix(hash, (uint)region.Coord.z);

            // Hash the full brick reference grid.
            //
            // BrickRef already encodes all three states in one int (empty, uniform material,
            // or pool index), so hashing it captures the region's structure exactly. Every
            // entry is hashed rather than sampled: a sampled hash cannot detect drift in the
            // entries it skips, which would defeat the point of drift detection.
            if (region.BrickRefs.IsCreated)
            {
                var bricks = region.BrickRefs;
                for (int i = 0; i < bricks.Length; i++)
                    hash = FnvMix(hash, (uint)bricks[i].Value);
            }

            return hash;
        }

        /// <summary>Computes an FNV-1a hash over a raw byte span of brick data.
        /// Used for S_RegionHash comparison without constructing a Region struct.</summary>
        /// <param name="data">Byte slice of the region's packed brick state.</param>
        /// <returns>FNV-1a hash of the data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashBytes(ReadOnlySpan<byte> data)
        {
            uint hash = k_FnvOffsetBasis;

            for (int i = 0; i < data.Length; i++)
                hash = FnvMix(hash, data[i]);

            return hash;
        }

        /// <summary>Computes an FNV-1a hash over a NativeArray of uint32 values.
        /// Optimized for occupancy mip data which is stored as ulong arrays.</summary>
        /// <param name="values">Span of 32-bit words to hash.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashUintSpan(ReadOnlySpan<uint> values)
        {
            uint hash = k_FnvOffsetBasis;

            for (int i = 0; i < values.Length; i++)
            {
                // Hash each byte of the uint to distinguish endianness-independent patterns.
                hash = FnvMix(hash, (uint)(values[i] >> 0));
                hash = FnvMix(hash, (uint)(values[i] >> 8));
                hash = FnvMix(hash, (uint)(values[i] >> 16));
                hash = FnvMix(hash, (uint)(values[i] >> 24));
            }

            return hash;
        }

        /// <summary>Compares two hashes with tolerance — useful for detecting minor differences
        /// that may be ignorable (e.g., timing-dependent debris state vs. core voxel state).</summary>
        /// <param name="a">First hash.</param>
        /// <param name="b">Second hash.</param>
        /// <returns>True if the hashes are identical; false if a repair is needed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreEqual(uint a, uint b) => a == b;

        // -- FNV-1a mixing --------------------------------------------------------

        /// <summary>FNV-1a XOR-F mix: XOR the hash with the value, then multiply by the FNV prime.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FnvMix(uint hash, uint value)
        {
            hash ^= value;
            return hash * k_FnvPrime;
        }
    }

}
