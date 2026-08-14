using System;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// Reliable EVENT notification that exact-checkpoint repair cannot be supplied and the client
    /// must obtain a full authoritative region state before resuming the paused authority queue.
    /// </summary>
    public struct S_RegionResyncRequired : IEquatable<S_RegionResyncRequired>
    {
        public const int WireSize = 17;

        public enum Reason : byte
        {
            CheckpointExpired = 1,
            SnapshotUnavailable = 2,
            ServerStateUnavailable = 3,
        }

        public int3 regionCoord;
        public uint failedHashTick;
        public Reason reason;

        public S_RegionResyncRequired(int3 regionCoord, uint failedHashTick, Reason reason)
        {
            this.regionCoord = regionCoord;
            this.failedHashTick = failedHashTick;
            this.reason = reason;
        }

        public void Encode(Span<byte> dst)
        {
            if (dst.Length < WireSize)
                throw new ArgumentException("S_RegionResyncRequired destination is too small.", nameof(dst));

            WriteInt32(dst, 0, regionCoord.x);
            WriteInt32(dst, 4, regionCoord.y);
            WriteInt32(dst, 8, regionCoord.z);
            WriteUint32(dst, 12, failedHashTick);
            dst[16] = (byte)reason;
        }

        public static S_RegionResyncRequired Decode(ReadOnlySpan<byte> src)
        {
            if (src.Length < WireSize)
                throw new ArgumentException("S_RegionResyncRequired source is too small.", nameof(src));

            return new S_RegionResyncRequired(
                new int3(ReadInt32(src, 0), ReadInt32(src, 4), ReadInt32(src, 8)),
                ReadUint32(src, 12),
                (Reason)src[16]);
        }

        public bool Equals(S_RegionResyncRequired other) =>
            regionCoord.Equals(other.regionCoord) && failedHashTick == other.failedHashTick && reason == other.reason;
        public override bool Equals(object obj) => obj is S_RegionResyncRequired other && Equals(other);
        public override int GetHashCode() => unchecked((regionCoord.GetHashCode() * 397 ^ (int)failedHashTick) * 397 ^ (byte)reason);

        private static void WriteInt32(Span<byte> dst, int offset, int value) => WriteUint32(dst, offset, unchecked((uint)value));
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }
        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] | ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) | ((uint)src[offset + 3] << 24);
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) => unchecked((int)ReadUint32(src, offset));
    }

    public static class RegionResyncRequiredPacket
    {
        public const int PacketSize = ProtocolEnvelope.HeaderSize + S_RegionResyncRequired.WireSize; // 19 B

        public static bool TryEncode(Span<byte> packet, in S_RegionResyncRequired message)
        {
            if (packet.Length < PacketSize ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_RegionResyncRequired))
                return false;

            message.Encode(packet.Slice(ProtocolEnvelope.HeaderSize, S_RegionResyncRequired.WireSize));
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out S_RegionResyncRequired message)
        {
            message = default;
            if (packet.Length != PacketSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.S_RegionResyncRequired)
                return false;

            message = S_RegionResyncRequired.Decode(packet.Slice(payloadOffset, S_RegionResyncRequired.WireSize));
            return true;
        }
    }
}
