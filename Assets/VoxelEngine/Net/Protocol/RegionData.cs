using System;
using System.Runtime.CompilerServices;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// S_RegionData — server-to-client full region state for late-join or re-download.
    ///
    /// Contains the complete seed + compressed edit overlay so the client can reconstruct
    /// the full region state from scratch. Uses a compact binary format: uniform bricks are
    /// stored as (position, material) runs; mixed bricks use pool indices.
    ///
    /// Wire format (variable):
    ///   Offset      Size         Field
    ///   0           4            seed (uint)             — region deterministic seed
    ///   4           2            compressedLength (ushort)— bytes in overlay payload
    ///   6           2            brickCount (ushort)      — total bricks in region grid
    ///   8           10           mipLevels (byte[10])     — per-level occupancy counts
    ///   18          compressedLen overlayPayload         — uniform runs + mixed pool indices
    /// </summary>
    public struct S_RegionData : IEquatable<S_RegionData>
    {
        /// <summary>Header size in bytes (no overlay payload).</summary>
        public const int HeaderSize = 18;

        /// <summary>World seed for this region's deterministic generation.</summary>
        public uint seed;

        /// <summary>Length of the compressed edit overlay payload.</summary>
        public ushort compressedLength;

        /// <summary>Total number of bricks in the region grid (64³ = 262144).</summary>
        public ushort brickCount;

        /// <summary>Occupancy count per mip level (levels 0..9). Used for progressive loading.</summary>
        public byte mipLevelsCount; // actual valid entries in mipLevelsData (up to 10)

        /// <summary>Mip level occupancy data — stored as a small array on the struct.
        /// On the wire, these are 10 bytes: one count per mip level.</summary>
        public uint mipLevelsHash; // compacted hash of all mip levels for fast comparison

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_RegionData(uint seed)
        {
            this.seed = seed;
            this.compressedLength = 0;
            this.brickCount = 0;
            this.mipLevelsCount = 0;
            this.mipLevelsHash = 0;
        }

        /// <summary>Encodes the region data with mip level counts and overlay payload.</summary>
        /// <param name="dst">Destination buffer (must be HeaderSize + compressedLength bytes).</param>
        /// <param name="mipCounts">Array of mip occupancy counts (up to 10 entries).</param>
        /// <param name="overlayPayload">Compressed brick overlay data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst, ReadOnlySpan<byte> mipCounts, ReadOnlySpan<byte> overlayPayload)
        {
            int totalSize = HeaderSize + overlayPayload.Length;
            ThrowIfTooSmall(dst, totalSize);

            // seed (4 bytes)
            WriteUint32(dst, 0, seed);

            // compressedLength (2 bytes)
            dst[4] = (byte)(overlayPayload.Length >> 0);
            dst[5] = (byte)(overlayPayload.Length >> 8);

            // brickCount (2 bytes)
            dst[6] = (byte)(brickCount >> 0);
            dst[7] = (byte)(brickCount >> 8);

            // mipLevels — store first byte as count, then raw level data
            mipLevelsCount = (byte)mipCounts.Length;
            dst[8] = mipLevelsCount;

            // Copy up to 10 bytes of mip data
            int mipCopyLen = mipLevelsCount < 10 ? mipLevelsCount : 10;
            for (int i = 0; i < mipCopyLen && i < mipCounts.Length; i++)
                dst[9 + i] = mipCounts[i];

            // Pad mip area to aligned boundary if needed
            int mipEnd = 9 + mipCopyLen;
            while (mipEnd % 4 != 0 && mipEnd < HeaderSize)
                dst[mipEnd++] = 0;

            // overlay payload
            if (overlayPayload.Length > 0)
                overlayPayload.CopyTo(dst.Slice(HeaderSize));
        }

        /// <summary>Decodes region data from wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReadOnlySpan is a ref struct and cannot be a tuple element, so both slices come
        // back through out parameters rather than a tuple return.
        public static S_RegionData Decode(
            ReadOnlySpan<byte> src,
            out ReadOnlySpan<byte> mipCounts,
            out ReadOnlySpan<byte> overlayPayload)
        {
            ThrowIfTooSmall(src, HeaderSize);

            S_RegionData msg = default;
            msg.seed = ReadUint32(src, 0);
            msg.compressedLength = (ushort)(src[4] | (src[5] << 8));
            msg.brickCount = (ushort)(src[6] | (src[7] << 8));
            msg.mipLevelsCount = src[8];

            int totalSize = HeaderSize + msg.compressedLength;
            ThrowIfTooSmall(src, totalSize);

            // Extract mip counts
            int mipCountLen = msg.mipLevelsCount < 10 ? msg.mipLevelsCount : 10;
            mipCounts = src.Slice(9, mipCountLen);

            // Overlay payload starts after header
            overlayPayload = src.Slice(HeaderSize, msg.compressedLength);

            return msg;
        }

        public bool Equals(S_RegionData other) =>
            seed == other.seed && compressedLength == other.compressedLength &&
            brickCount == other.brickCount && mipLevelsCount == other.mipLevelsCount;
        public override bool Equals(object obj) => obj is S_RegionData o && Equals(o);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = seed.GetHashCode();
                h = (h * 397) ^ compressedLength;
                h = (h * 397) ^ brickCount;
                h = (h * 397) ^ mipLevelsCount;
                return h;
            }
        }
        public static bool operator ==(S_RegionData a, S_RegionData b) => a.Equals(b);
        public static bool operator !=(S_RegionData a, S_RegionData b) => !a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"S_RegionData: dst too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"S_RegionData: src too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset]     = (byte)(value >> 0);
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] | ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) | ((uint)src[offset + 3] << 24);
    }
}
