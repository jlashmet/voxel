using System;
using System.Collections.Generic;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Authoritative consumer for loss-tolerant client input. Connection identity is supplied by
    /// the server transport and never read from the packet.
    /// </summary>
    public interface IClientInputCommandHandler
    {
        void HandlePlayerInput(uint connectionId, in C_PlayerInput input);
    }

    /// <summary>
    /// Stateful EPHEMERAL packet receiver. Redundant input bundles intentionally repeat recent
    /// samples, so sequence deduplication is scoped to the transport connection. Valid duplicate
    /// packets are accepted but do not invoke the gameplay handler twice.
    /// </summary>
    public sealed class ClientEphemeralPacketReceiver
    {
        private readonly Dictionary<uint, ushort> _lastSequenceByConnection =
            new Dictionary<uint, ushort>();

        public bool TryDispatch(
            uint connectionId,
            ReadOnlySpan<byte> packet,
            IClientInputCommandHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (!ProtocolEnvelope.TryReadHeader(packet, out var kind, out _))
                return false;

            switch (kind)
            {
                case ProtocolMessageKind.C_PlayerInput:
                    if (!PlayerInputPacket.TryDecode(packet, out var input))
                        return false;

                    DispatchIfNew(connectionId, in input, handler);
                    return true;

                case ProtocolMessageKind.C_PlayerInputBundle:
                    return TryDispatchBundle(connectionId, packet, handler);

                default:
                    return false;
            }
        }

        public void RemoveConnection(uint connectionId)
        {
            _lastSequenceByConnection.Remove(connectionId);
        }

        private bool TryDispatchBundle(
            uint connectionId,
            ReadOnlySpan<byte> packet,
            IClientInputCommandHandler handler)
        {
            if (!PlayerInputBundlePacket.TryDecodeHeader(packet, out int count))
                return false;

            Span<C_PlayerInput> decoded = stackalloc C_PlayerInput[PlayerInputBundlePacket.MaxSamples];
            for (int i = 0; i < count; i++)
            {
                if (!PlayerInputBundlePacket.TryDecodeSample(packet, count, i, out decoded[i]))
                    return false;

                // Bundles are required to be oldest -> newest. Validate the entire bundle before
                // dispatching anything so malformed input cannot cause partial command delivery.
                if (i > 0 && !IsNewer(decoded[i].sequence, decoded[i - 1].sequence))
                    return false;
            }

            for (int i = 0; i < count; i++)
                DispatchIfNew(connectionId, in decoded[i], handler);

            return true;
        }

        private void DispatchIfNew(
            uint connectionId,
            in C_PlayerInput input,
            IClientInputCommandHandler handler)
        {
            if (_lastSequenceByConnection.TryGetValue(connectionId, out ushort lastSequence) &&
                !IsNewer(input.sequence, lastSequence))
            {
                return;
            }

            _lastSequenceByConnection[connectionId] = input.sequence;
            handler.HandlePlayerInput(connectionId, in input);
        }

        /// <summary>RFC-style 16-bit serial comparison; valid as long as the gap is < 32768.</summary>
        private static bool IsNewer(ushort candidate, ushort baseline)
        {
            if (candidate == baseline)
                return false;

            return unchecked((ushort)(candidate - baseline)) < 0x8000;
        }
    }
}
