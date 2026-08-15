using System;
using System.Runtime.CompilerServices;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>
    /// LEGACY SOURCE-COMPATIBILITY CODEC ONLY.
    ///
    /// The live protocol message kind S_RegionData (37) is encoded by RegionStateChunkPacket and
    /// carries semantic snapshot bytes. This historical struct predates the semantic state contract;
    /// in particular its old overlay design mentioned allocator-local pool indices and a ushort
    /// brickCount that cannot represent all 262,144 bricks in a region. Neither representation is
    /// valid for live networking. Keep this struct only until old protocol round-trip callers migrate.
    /// </summary>
    public struct S_RegionData : IEquatable<S_RegionData>
    {
        public const int HeaderSize = 18;

        public uint seed;
        public ushort compressedLength;
        public ushort brickCount;
        public byte mipLevelsCount;
        public uint mipLevelsHash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_RegionData(uint seed)
        {
            this.seed = seed;
            this.compressedLength = 0;
            this.brickCount = 0;
            this.mipLevelsCount = 0;
            this.mipLevelsHash = 0;
        }

        /// <summary>Legacy encoder retained only for source-compatible tests/callers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst, ReadOnlySpan<byte> mipCounts, ReadOnlySpan<byte> overlayPayload)
        {
            int totalSize = HeaderSize + overlayPayload.Length;
            ThrowIfTooSmall(dst, totalSize);

            WriteUint32(dst, 0, seed);
            dst[4] = (byte)(overlayPayload.Length >> 0);
            dst[5] = (byte)(overlayPayload.Length >> 8);
            dst[6] = (byte)(brickCount >> 0);
            dst[7] = (byte)(brickCount >> 8);

            mipLevelsCount = (byte)mipCounts.Length;
            dst[8] = mipLevelsCount;

            int mipCopyLen = mipLevelsCount < 10 ? mipLevelsCount : 10;
            for (int i = 0; i < mipCopyLen && i < mipCounts.Length; i++)
                dst[9 + i] = mipCounts[i];

            int mipEnd = 9 + mipCopyLen;
            while (mipEnd % 4 != 0 && mipEnd < HeaderSize)
                dst[mipEnd++] = 0;

            if (overlayPayload.Length > 0)
                overlayPayload.CopyTo(dst.Slice(HeaderSize));
        }

        /// <summary>Legacy decoder retained only for source-compatible tests/callers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            int mipCountLen = msg.mipLevelsCount < 10 ? msg.mipLevelsCount : 10;
            mipCounts = src.Slice(9, mipCountLen);
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
            if (dst.Length < required) UnityEngine.Debug.LogError("S_RegionData: dst too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError("S_RegionData: src too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)(value >> 0);
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
