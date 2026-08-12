using System;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>Stable message-kind registry for the custom UTP protocol.</summary>
    public enum ProtocolMessageKind : byte
    {
        None = 0,

        // Client -> server.
        C_PlayerInput = 1,
        C_AlterationRequest = 2,
        C_RegionRequest = 3,
        C_PlayerInputBundle = 4,
        C_RegionHashMismatch = 5,

        // Server -> client.
        S_AlterationEvent = 32,
        S_AlterationEventBatch = 33,
        S_AlterationRejected = 34,
        S_RegionHash = 35,
        S_RegionRepair = 36,
        S_RegionData = 37,
        S_PlayerState = 38,
        S_RegionResyncRequired = 39,
    }

    public static class ProtocolEnvelope
    {
        public const byte CurrentVersion = 1;
        public const int HeaderSize = 2;

        public static bool TryWriteHeader(Span<byte> destination, ProtocolMessageKind kind)
        {
            if (destination.Length < HeaderSize || kind == ProtocolMessageKind.None)
                return false;

            destination[0] = CurrentVersion;
            destination[1] = (byte)kind;
            return true;
        }

        public static bool TryReadHeader(
            ReadOnlySpan<byte> packet,
            out ProtocolMessageKind kind,
            out int payloadOffset)
        {
            kind = ProtocolMessageKind.None;
            payloadOffset = 0;

            if (packet.Length < HeaderSize || packet[0] != CurrentVersion)
                return false;

            ProtocolMessageKind decoded = (ProtocolMessageKind)packet[1];
            if (!IsKnown(decoded))
                return false;

            kind = decoded;
            payloadOffset = HeaderSize;
            return true;
        }

        public static bool IsKnown(ProtocolMessageKind kind)
        {
            switch (kind)
            {
                case ProtocolMessageKind.C_PlayerInput:
                case ProtocolMessageKind.C_AlterationRequest:
                case ProtocolMessageKind.C_RegionRequest:
                case ProtocolMessageKind.C_PlayerInputBundle:
                case ProtocolMessageKind.C_RegionHashMismatch:
                case ProtocolMessageKind.S_AlterationEvent:
                case ProtocolMessageKind.S_AlterationEventBatch:
                case ProtocolMessageKind.S_AlterationRejected:
                case ProtocolMessageKind.S_RegionHash:
                case ProtocolMessageKind.S_RegionRepair:
                case ProtocolMessageKind.S_RegionData:
                case ProtocolMessageKind.S_PlayerState:
                case ProtocolMessageKind.S_RegionResyncRequired:
                    return true;
                default:
                    return false;
            }
        }
    }
}
