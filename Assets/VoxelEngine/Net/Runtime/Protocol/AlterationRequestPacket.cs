using System;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// Complete framed C_AlterationRequest packet codec.
    /// Fixed-size packets are required to be exact length so malformed/trailing data fails closed.
    /// </summary>
    public static class AlterationRequestPacket
    {
        public const int PacketSize = ProtocolEnvelope.HeaderSize + C_AlterationRequest.WireSize; // 34 B

        public static bool TryEncode(Span<byte> packet, in C_AlterationRequest request)
        {
            if (packet.Length != PacketSize)
                return false;
            if (!ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.C_AlterationRequest))
                return false;

            request.Encode(packet.Slice(ProtocolEnvelope.HeaderSize, C_AlterationRequest.WireSize));
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out C_AlterationRequest request)
        {
            request = default;
            if (packet.Length != PacketSize)
                return false;
            if (!ProtocolEnvelope.TryReadHeader(packet, out var kind, out int payloadOffset))
                return false;
            if (kind != ProtocolMessageKind.C_AlterationRequest)
                return false;

            request = C_AlterationRequest.Decode(
                packet.Slice(payloadOffset, C_AlterationRequest.WireSize));
            return true;
        }
    }
}
