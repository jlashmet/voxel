using System;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
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
            ref RegionTable table,
            ref BrickPool pool,
            out bool anyChanged) =>
            TryApply(packet, ref table, ref pool, out anyChanged, null);

        public static bool TryApply(
            ReadOnlySpan<byte> packet,
            ref RegionTable table,
            ref BrickPool pool,
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
                        ref table,
                        ref pool,
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
