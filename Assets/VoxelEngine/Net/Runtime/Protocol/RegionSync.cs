using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// S_RegionHash — server-to-client semantic drift fingerprint at an authoritative server tick.
    ///
    /// Payload (20 bytes): regionCoord int3 (12), serverTick uint (4), semanticHash uint (4).
    /// The client compares only after authoritative events through serverTick are applied.
    /// </summary>
    public struct S_RegionHash : IEquatable<S_RegionHash>
    {
        public const int WireSize = 20;

        public int3 regionCoord;
        public uint serverTick;
        public uint mipHash; // historical field name; value is the canonical semantic region hash.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_RegionHash(int3 regionCoord, uint serverTick, uint semanticHash)
        {
            this.regionCoord = regionCoord;
            this.serverTick = serverTick;
            mipHash = semanticHash;
        }

        [Obsolete("Use the tick-scoped constructor.")]
        public S_RegionHash(int3 regionCoord, uint semanticHash)
            : this(regionCoord, 0, semanticHash)
        {
        }

        public void Encode(Span<byte> dst)
        {
            if (dst.Length < WireSize)
                throw new ArgumentException("S_RegionHash destination is too small.", nameof(dst));

            WriteInt32(dst, 0, regionCoord.x);
            WriteInt32(dst, 4, regionCoord.y);
            WriteInt32(dst, 8, regionCoord.z);
            WriteUint32(dst, 12, serverTick);
            WriteUint32(dst, 16, mipHash);
        }

        public static S_RegionHash Decode(ReadOnlySpan<byte> src)
        {
            if (src.Length < WireSize)
                throw new ArgumentException("S_RegionHash source is too small.", nameof(src));

            return new S_RegionHash(
                new int3(ReadInt32(src, 0), ReadInt32(src, 4), ReadInt32(src, 8)),
                ReadUint32(src, 12),
                ReadUint32(src, 16));
        }

        public bool Equals(S_RegionHash other) =>
            math.all(regionCoord == other.regionCoord) &&
            serverTick == other.serverTick &&
            mipHash == other.mipHash;

        public override bool Equals(object obj) => obj is S_RegionHash other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = regionCoord.GetHashCode();
                hash = (hash * 397) ^ (int)serverTick;
                hash = (hash * 397) ^ (int)mipHash;
                return hash;
            }
        }

        public static bool operator ==(S_RegionHash a, S_RegionHash b) => a.Equals(b);
        public static bool operator !=(S_RegionHash a, S_RegionHash b) => !a.Equals(b);

        private static void WriteInt32(Span<byte> dst, int offset, int value) =>
            WriteUint32(dst, offset, unchecked((uint)value));

        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] |
            ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) |
            ((uint)src[offset + 3] << 24);

        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            unchecked((int)ReadUint32(src, offset));
    }

    /// <summary>
    /// Legacy repair envelope retained for source compatibility while convergence migrates to the
    /// semantic snapshot codec. Raw BrickPool indices MUST NOT be placed in dataBytes.
    /// </summary>
    public struct S_RegionRepair : IEquatable<S_RegionRepair>
    {
        public const int HeaderSize = 20;
        public int3 regionCoord;
        public uint repairStartTick;
        public int dataLength;

        public S_RegionRepair(int3 regionCoord, uint repairStartTick)
        {
            this.regionCoord = regionCoord;
            this.repairStartTick = repairStartTick;
            dataLength = 0;
        }

        public void Encode(Span<byte> dst, ReadOnlySpan<byte> dataBytes)
        {
            int totalSize = HeaderSize + dataBytes.Length;
            if (dst.Length < totalSize)
                throw new ArgumentException("S_RegionRepair destination is too small.", nameof(dst));

            WriteInt32(dst, 0, regionCoord.x);
            WriteInt32(dst, 4, regionCoord.y);
            WriteInt32(dst, 8, regionCoord.z);
            WriteUint32(dst, 12, repairStartTick);
            WriteUint32(dst, 16, (uint)dataBytes.Length);
            dataBytes.CopyTo(dst.Slice(HeaderSize));
        }

        public static S_RegionRepair Decode(ReadOnlySpan<byte> src, out ReadOnlySpan<byte> dataBytes)
        {
            if (src.Length < HeaderSize)
                throw new ArgumentException("S_RegionRepair source is too small.", nameof(src));

            S_RegionRepair msg = new S_RegionRepair(
                new int3(ReadInt32(src, 0), ReadInt32(src, 4), ReadInt32(src, 8)),
                ReadUint32(src, 12));
            msg.dataLength = checked((int)ReadUint32(src, 16));

            if (msg.dataLength < 0 || src.Length < HeaderSize + msg.dataLength)
                throw new ArgumentException("S_RegionRepair payload is truncated.", nameof(src));

            dataBytes = src.Slice(HeaderSize, msg.dataLength);
            return msg;
        }

        public bool Equals(S_RegionRepair other) =>
            math.all(regionCoord == other.regionCoord) &&
            repairStartTick == other.repairStartTick &&
            dataLength == other.dataLength;

        public override bool Equals(object obj) => obj is S_RegionRepair other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = regionCoord.GetHashCode();
                hash = (hash * 397) ^ (int)repairStartTick;
                hash = (hash * 397) ^ dataLength;
                return hash;
            }
        }

        public static bool operator ==(S_RegionRepair a, S_RegionRepair b) => a.Equals(b);
        public static bool operator !=(S_RegionRepair a, S_RegionRepair b) => !a.Equals(b);

        private static void WriteInt32(Span<byte> dst, int offset, int value) =>
            WriteUint32(dst, offset, unchecked((uint)value));

        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] |
            ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) |
            ((uint)src[offset + 3] << 24);

        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            unchecked((int)ReadUint32(src, offset));
    }
}
