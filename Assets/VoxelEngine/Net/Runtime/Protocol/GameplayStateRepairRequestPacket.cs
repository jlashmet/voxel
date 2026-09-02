using System;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>Transport-owned request to re-send current authoritative gameplay state.</summary>
    public readonly struct C_GameplayStateRepairRequest
    {
        public enum RepairReason : byte
        {
            GapDetected = 1,
            IncompatibleProjection = 2
        }

        public C_GameplayStateRepairRequest(ulong knownRevision, RepairReason reason)
        {
            if (reason != RepairReason.GapDetected && reason != RepairReason.IncompatibleProjection)
                throw new ArgumentOutOfRangeException(nameof(reason));
            KnownRevision = knownRevision;
            Reason = reason;
        }

        public ulong KnownRevision { get; }
        public RepairReason Reason { get; }
    }

    public static class GameplayStateRepairRequestPacket
    {
        public const int PacketSize = ProtocolEnvelope.HeaderSize + sizeof(byte) + sizeof(ulong);

        public static bool TryEncode(Span<byte> destination, in C_GameplayStateRepairRequest request)
        {
            if (destination.Length < PacketSize ||
                !ProtocolEnvelope.TryWriteHeader(destination, ProtocolMessageKind.C_GameplayStateRepairRequest))
                return false;

            destination[ProtocolEnvelope.HeaderSize] = (byte)request.Reason;
            WriteU64(destination, ProtocolEnvelope.HeaderSize + sizeof(byte), request.KnownRevision);
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out C_GameplayStateRepairRequest request)
        {
            request = default;
            if (packet.Length != PacketSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.C_GameplayStateRepairRequest)
                return false;

            var reason = (C_GameplayStateRepairRequest.RepairReason)packet[payloadOffset];
            if (reason != C_GameplayStateRepairRequest.RepairReason.GapDetected &&
                reason != C_GameplayStateRepairRequest.RepairReason.IncompatibleProjection)
                return false;

            request = new C_GameplayStateRepairRequest(ReadU64(packet, payloadOffset + sizeof(byte)), reason);
            return true;
        }

        private static void WriteU64(Span<byte> destination, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
                destination[offset + i] = (byte)(value >> (i * 8));
        }

        private static ulong ReadU64(ReadOnlySpan<byte> source, int offset)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++)
                value |= (ulong)source[offset + i] << (i * 8);
            return value;
        }
    }
}
