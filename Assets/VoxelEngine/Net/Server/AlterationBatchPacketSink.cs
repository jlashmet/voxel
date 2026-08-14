using System;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Lowest transport-independent boundary for the reliable EVENT channel.
    /// A future UTP host adapter owns NetworkDriver/NetworkConnection handles and implements this
    /// interface; replication code never needs to know about those lifetimes.
    /// </summary>
    public interface IEventPacketSender
    {
        void SendEventPacket(uint connectionId, ReadOnlySpan<byte> packet);
    }

    /// <summary>
    /// Encodes ReplicationRouter output into a versioned S_AlterationEventBatch packet.
    /// The maximum packet is 1172 bytes including the protocol envelope, keeping live world
    /// mutations below the conservative ~1200-byte non-fragmented target.
    /// </summary>
    public sealed class AlterationBatchPacketSink : IAlterationReplicationSink
    {
        public const int MaxPacketBytes = ProtocolEnvelope.HeaderSize + S_AlterationEventBatch.MaxWireSize;

        private readonly IEventPacketSender _sender;

        public AlterationBatchPacketSink(IEventPacketSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public void SendBatch(
            uint connectionId,
            int3 encodingRegion,
            uint tick,
            ReadOnlySpan<AlterationEvent> events)
        {
            if (events.Length <= 0 || events.Length > S_AlterationEventBatch.MaxEventsPerBatch)
                throw new ArgumentOutOfRangeException(nameof(events));

            int payloadSize = S_AlterationEventBatch.EncodedSize(events.Length);
            int packetSize = ProtocolEnvelope.HeaderSize + payloadSize;
            Span<byte> packet = stackalloc byte[packetSize];

            if (!ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_AlterationEventBatch))
                throw new InvalidOperationException("Failed to encode protocol envelope.");

            Span<byte> payload = packet.Slice(ProtocolEnvelope.HeaderSize);
            if (!S_AlterationEventBatch.TryEncode(payload, encodingRegion, tick, events, out int bytesWritten) ||
                bytesWritten != payloadSize)
            {
                // The router uses the event's containing region as encodingRegion, so a failure is
                // an invariant violation rather than something to silently truncate or partially send.
                throw new InvalidOperationException("Authoritative alteration batch could not be encoded losslessly.");
            }

            _sender.SendEventPacket(connectionId, packet);
        }
    }
}
