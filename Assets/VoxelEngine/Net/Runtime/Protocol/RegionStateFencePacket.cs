using System;
using Unity.Mathematics;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>
    /// Reliable EVENT fence for a BULK full-region snapshot. Because this packet is queued after all
    /// EVENT authority included by SnapshotTick, the client can safely suppress duplicate effects on
    /// the replaced region until it reaches this fence, then resume ordinary global EVENT order.
    /// </summary>
    public readonly struct S_RegionStateFence : IEquatable<S_RegionStateFence>
    {
        public const int WireSize = 20;

        public readonly uint transferId;
        public readonly int3 regionCoord;
        public readonly uint snapshotTick;

        public S_RegionStateFence(uint transferId, int3 regionCoord, uint snapshotTick)
        {
            this.transferId = transferId;
            this.regionCoord = regionCoord;
            this.snapshotTick = snapshotTick;
        }

        public bool Equals(S_RegionStateFence other) =>
            transferId == other.transferId && regionCoord.Equals(other.regionCoord) && snapshotTick == other.snapshotTick;
        public override bool Equals(object obj) => obj is S_RegionStateFence other && Equals(other);
        public override int GetHashCode() => unchecked(((int)transferId * 397 ^ regionCoord.GetHashCode()) * 397 ^ (int)snapshotTick);
    }

    public static class RegionStateFencePacket
    {
        public const int PacketSize = ProtocolEnvelope.HeaderSize + S_RegionStateFence.WireSize; // 22 B

        public static bool TryEncode(Span<byte> packet, in S_RegionStateFence fence)
        {
            if (packet.Length != PacketSize || fence.transferId == 0 ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_RegionStateFence))
                return false;

            WriteUint32(packet, 2, fence.transferId);
            WriteInt32(packet, 6, fence.regionCoord.x);
            WriteInt32(packet, 10, fence.regionCoord.y);
            WriteInt32(packet, 14, fence.regionCoord.z);
            WriteUint32(packet, 18, fence.snapshotTick);
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out S_RegionStateFence fence)
        {
            fence = default;
            if (packet.Length != PacketSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out _) ||
                kind != ProtocolMessageKind.S_RegionStateFence)
                return false;

            uint transferId = ReadUint32(packet, 2);
            if (transferId == 0)
                return false;

            fence = new S_RegionStateFence(
                transferId,
                new int3(ReadInt32(packet, 6), ReadInt32(packet, 10), ReadInt32(packet, 14)),
                ReadUint32(packet, 18));
            return true;
        }

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
}
