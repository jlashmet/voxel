using System;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>Versioned EVENT packet wrapper for S_AlterationRejected.</summary>
    public static class AlterationRejectedPacket
    {
        public const int PacketSize = ProtocolEnvelope.HeaderSize + S_AlterationRejected.WireSize;

        public static bool TryEncode(Span<byte> packet, in S_AlterationRejected rejection)
        {
            if (packet.Length < PacketSize ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_AlterationRejected))
            {
                return false;
            }

            rejection.Encode(packet.Slice(ProtocolEnvelope.HeaderSize, S_AlterationRejected.WireSize));
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out S_AlterationRejected rejection)
        {
            rejection = default;
            if (packet.Length != PacketSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.S_AlterationRejected)
            {
                return false;
            }

            rejection = S_AlterationRejected.Decode(packet.Slice(payloadOffset, S_AlterationRejected.WireSize));
            return true;
        }
    }
}
