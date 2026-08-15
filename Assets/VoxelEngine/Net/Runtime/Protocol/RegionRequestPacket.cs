using System;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>
    /// Framed C_RegionRequest. The live full-state path currently uses haveMipLevel=0xFF to mean
    /// "send one complete semantic region snapshot"; lower mip values remain reserved for later
    /// progressive refinement.
    /// </summary>
    public static class RegionRequestPacket
    {
        public const byte FullSemanticState = 0xFF;
        public const int PacketSize = ProtocolEnvelope.HeaderSize + C_RegionRequest.WireSize; // 18 B

        public static bool TryEncode(Span<byte> packet, in C_RegionRequest request)
        {
            if (packet.Length != PacketSize ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.C_RegionRequest))
                return false;

            request.Encode(packet.Slice(ProtocolEnvelope.HeaderSize, C_RegionRequest.WireSize));
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out C_RegionRequest request)
        {
            request = default;
            if (packet.Length != PacketSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.C_RegionRequest)
                return false;

            request = C_RegionRequest.Decode(packet.Slice(payloadOffset, C_RegionRequest.WireSize));
            return true;
        }
    }
}
