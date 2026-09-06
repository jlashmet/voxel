using System;
using VoxelEngine.Net.Runtime.Transport;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>
    /// Direction-specific, non-fragmented EVENT framing for Sessions-owned admission messages.
    /// Net validates framing only: accepted delivery is not authenticated membership or readiness.
    /// Session ids, applicant credentials and admission decisions belong to the upper-layer codec.
    /// </summary>
    public static class SessionAdmissionPacket
    {
        public const int HeaderSize = ProtocolEnvelope.HeaderSize + sizeof(ushort);
        public const int MaxPacketBytes = ChannelSetup.k_MaxEventPacketBytes;
        public const int MaxPayloadBytes = MaxPacketBytes - HeaderSize;

        public static bool TryEncodeRequest(Span<byte> destination, ReadOnlySpan<byte> payload, out int bytesWritten) =>
            TryEncode(destination, payload, ProtocolMessageKind.C_SessionAdmission, out bytesWritten);

        public static bool TryEncodeReply(Span<byte> destination, ReadOnlySpan<byte> payload, out int bytesWritten) =>
            TryEncode(destination, payload, ProtocolMessageKind.S_SessionAdmission, out bytesWritten);

        public static bool TryDecodeRequest(ReadOnlySpan<byte> packet, out ReadOnlySpan<byte> payload) =>
            TryDecode(packet, ProtocolMessageKind.C_SessionAdmission, out payload);

        public static bool TryDecodeReply(ReadOnlySpan<byte> packet, out ReadOnlySpan<byte> payload) =>
            TryDecode(packet, ProtocolMessageKind.S_SessionAdmission, out payload);

        private static bool TryEncode(Span<byte> destination, ReadOnlySpan<byte> payload,
            ProtocolMessageKind kind, out int bytesWritten)
        {
            bytesWritten = 0;
            if (payload.Length < 1 || payload.Length > MaxPayloadBytes ||
                destination.Length < HeaderSize + payload.Length)
                return false;

            // Copy before framing so overlapping caller-owned spans are safe as well.
            payload.CopyTo(destination.Slice(HeaderSize));
            ProtocolEnvelope.TryWriteHeader(destination, kind);
            destination[ProtocolEnvelope.HeaderSize] = (byte)payload.Length;
            destination[ProtocolEnvelope.HeaderSize + 1] = (byte)(payload.Length >> 8);
            bytesWritten = HeaderSize + payload.Length;
            return true;
        }

        private static bool TryDecode(ReadOnlySpan<byte> packet, ProtocolMessageKind expectedKind,
            out ReadOnlySpan<byte> payload)
        {
            payload = default;
            if (packet.Length < HeaderSize + 1 || packet.Length > MaxPacketBytes ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int offset) ||
                kind != expectedKind)
                return false;

            int length = packet[offset] | (packet[offset + 1] << 8);
            if (length < 1 || length > MaxPayloadBytes || packet.Length != HeaderSize + length)
                return false;

            payload = packet.Slice(HeaderSize, length);
            return true;
        }
    }
}
