using System;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>Versioned framed C_PlayerInput packet (2-byte envelope + 16-byte payload).</summary>
    public static class PlayerInputPacket
    {
        public const int PacketSize = ProtocolEnvelope.HeaderSize + C_PlayerInput.WireSize; // 18 B

        public static bool TryEncode(Span<byte> packet, in C_PlayerInput input)
        {
            if (packet.Length < PacketSize)
                return false;
            if (!ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.C_PlayerInput))
                return false;

            input.Encode(packet.Slice(ProtocolEnvelope.HeaderSize, C_PlayerInput.WireSize));
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out C_PlayerInput input)
        {
            input = default;
            if (packet.Length != PacketSize)
                return false;
            if (!ProtocolEnvelope.TryReadHeader(packet, out var kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.C_PlayerInput)
            {
                return false;
            }

            input = C_PlayerInput.Decode(packet.Slice(payloadOffset, C_PlayerInput.WireSize));
            return true;
        }
    }
}
