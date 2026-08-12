using System;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Client-side dispatch boundary for packets received on the reliable EVENT pipeline.
    /// Unknown versions/kinds fail closed. Message-specific decoders receive only their payload,
    /// keeping UTP framing concerns out of deterministic world application code.
    /// </summary>
    public static class EventPacketReceiver
    {
        public static bool TryApply(
            ReadOnlySpan<byte> packet,
            ref RegionTable table,
            ref BrickPool pool,
            out bool anyChanged)
        {
            anyChanged = false;
            if (!ProtocolEnvelope.TryReadHeader(packet, out var kind, out int payloadOffset))
                return false;

            ReadOnlySpan<byte> payload = packet.Slice(payloadOffset);
            switch (kind)
            {
                case ProtocolMessageKind.S_AlterationEventBatch:
                    return AlterationBatchReceiver.TryApply(payload, ref table, ref pool, out anyChanged);

                // Other EVENT-channel messages will be added as their receive/application paths
                // become concrete. Failing closed here is preferable to interpreting bytes with
                // the wrong codec.
                default:
                    return false;
            }
        }
    }
}
