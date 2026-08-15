using System;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>
    /// EPHEMERAL server snapshot bundle. Bundling several players into one sequenced datagram avoids
    /// making normal four-player state depend on ordering between multiple packets on the same UTP
    /// unreliable-sequenced pipeline.
    /// </summary>
    public static class PlayerStateBundlePacket
    {
        public const int CountBytes = 1;
        public const int MaxStates = 6;
        public const int MaxPacketSize = ProtocolEnvelope.HeaderSize + CountBytes + MaxStates * S_PlayerState.WireSize;

        public static int PacketSize(int count) =>
            ProtocolEnvelope.HeaderSize + CountBytes + count * S_PlayerState.WireSize;

        public static bool TryEncode(Span<byte> packet, ReadOnlySpan<S_PlayerState> states, out int bytesWritten)
        {
            bytesWritten = 0;
            if (states.Length < 1 || states.Length > MaxStates)
                return false;

            int required = PacketSize(states.Length);
            if (packet.Length < required ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_PlayerState))
                return false;

            packet[ProtocolEnvelope.HeaderSize] = (byte)states.Length;
            int offset = ProtocolEnvelope.HeaderSize + CountBytes;
            for (int i = 0; i < states.Length; i++)
            {
                states[i].Encode(packet.Slice(offset, S_PlayerState.WireSize));
                offset += S_PlayerState.WireSize;
            }

            bytesWritten = required;
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, Span<S_PlayerState> destination, out int count)
        {
            count = 0;
            if (!ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.S_PlayerState ||
                packet.Length < payloadOffset + CountBytes)
                return false;

            int decodedCount = packet[payloadOffset];
            if (decodedCount < 1 || decodedCount > MaxStates || destination.Length < decodedCount ||
                packet.Length != PacketSize(decodedCount))
                return false;

            int offset = payloadOffset + CountBytes;
            for (int i = 0; i < decodedCount; i++)
            {
                if (!S_PlayerState.TryDecode(packet.Slice(offset, S_PlayerState.WireSize), out destination[i]))
                    return false;
                offset += S_PlayerState.WireSize;
            }

            count = decodedCount;
            return true;
        }
    }
}
