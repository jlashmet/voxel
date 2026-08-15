using System;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// Redundant EPHEMERAL input datagram. Carries 1-3 command samples ordered oldest -> newest.
    /// The newest packet supersedes older datagrams, while repeated prior samples let the server
    /// recover action edges lost with one or two isolated packets without reliable retransmission.
    /// </summary>
    public static class PlayerInputBundlePacket
    {
        public const int MaxSamples = 3;
        public const int BundleHeaderSize = ProtocolEnvelope.HeaderSize + 1; // version + kind + count
        public const int MaxPacketSize = BundleHeaderSize + C_PlayerInput.WireSize * MaxSamples; // 51 B

        public static int EncodedSize(int sampleCount) =>
            BundleHeaderSize + C_PlayerInput.WireSize * sampleCount;

        public static bool TryEncode(Span<byte> packet, ReadOnlySpan<C_PlayerInput> samples, out int bytesWritten)
        {
            bytesWritten = 0;
            if (samples.Length <= 0 || samples.Length > MaxSamples)
                return false;

            int required = EncodedSize(samples.Length);
            if (packet.Length < required ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.C_PlayerInputBundle))
            {
                return false;
            }

            packet[ProtocolEnvelope.HeaderSize] = (byte)samples.Length;
            for (int i = 0; i < samples.Length; i++)
            {
                int offset = BundleHeaderSize + i * C_PlayerInput.WireSize;
                samples[i].Encode(packet.Slice(offset, C_PlayerInput.WireSize));
            }

            bytesWritten = required;
            return true;
        }

        public static bool TryDecodeHeader(ReadOnlySpan<byte> packet, out int sampleCount)
        {
            sampleCount = 0;
            if (!ProtocolEnvelope.TryReadHeader(packet, out var kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.C_PlayerInputBundle ||
                packet.Length < BundleHeaderSize)
            {
                return false;
            }

            int count = packet[payloadOffset];
            if (count <= 0 || count > MaxSamples || packet.Length != EncodedSize(count))
                return false;

            sampleCount = count;
            return true;
        }

        public static bool TryDecodeSample(
            ReadOnlySpan<byte> packet,
            int sampleCount,
            int index,
            out C_PlayerInput input)
        {
            input = default;
            if (sampleCount <= 0 || sampleCount > MaxSamples || index < 0 || index >= sampleCount)
                return false;
            if (packet.Length != EncodedSize(sampleCount))
                return false;

            int offset = BundleHeaderSize + index * C_PlayerInput.WireSize;
            input = C_PlayerInput.Decode(packet.Slice(offset, C_PlayerInput.WireSize));
            return true;
        }
    }
}
