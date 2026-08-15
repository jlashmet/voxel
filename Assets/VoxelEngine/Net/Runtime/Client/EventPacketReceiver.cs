using System;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Net.Runtime.Client
{
    public interface IClientEventNotificationSink
    {
        void OnAlterationRejected(in S_AlterationRejected rejection);
    }

    /// <summary>
    /// Client-side dispatch boundary for packets received on the reliable EVENT pipeline.
    /// Unknown versions/kinds fail closed. World mutations and semantic notifications share the
    /// framing boundary but remain separate consumers.
    /// </summary>
    public static class EventPacketReceiver
    {
        public static bool TryApply(
            ReadOnlySpan<byte> packet,
            IAlterationApplier applier,
            IRegionMutationStore storage,
            out bool anyChanged) =>
            TryApply(packet, applier, storage, out anyChanged, null);

        public static bool TryApply(
            ReadOnlySpan<byte> packet,
            IAlterationApplier applier,
            IRegionMutationStore storage,
            out bool anyChanged,
            IClientEventNotificationSink notifications)
        {
            anyChanged = false;
            if (!ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset))
                return false;

            switch (kind)
            {
                case ProtocolMessageKind.S_AlterationEventBatch:
                    return AlterationBatchReceiver.TryApply(
                        packet.Slice(payloadOffset),
                        applier,
                        storage,
                        out anyChanged);

                case ProtocolMessageKind.S_AlterationRejected:
                    if (!AlterationRejectedPacket.TryDecode(packet, out S_AlterationRejected rejection))
                        return false;

                    notifications?.OnAlterationRejected(in rejection);
                    return true;

                default:
                    return false;
            }
        }
    }
}
