using System;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Transport;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Optional capability on the composition-supplied IClientEventCommandHandler. The connection id
    /// is transport-owned, never decoded from the untrusted payload. Implementations validate their
    /// Sessions schema and copy into a bounded admission queue; they must not retain the borrowed span
    /// or mutate gameplay from a transport callback. True means queued, not authenticated/admitted.
    /// </summary>
    public interface IClientSessionAdmissionHandler
    {
        bool TryEnqueueSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload);
    }

    /// <summary>
    /// Sessions-supplied policy consumer invoked by AuthoritativeServerSession at a fixed-tick boundary,
    /// never from the transport callback. Sender identity remains transport-owned. The payload is
    /// borrowed for this call only; acceptance, credentials and durable membership remain Sessions-owned.
    /// </summary>
    public interface IAuthoritativeSessionAdmissionConsumer
    {
        void HandleSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload);
    }

    public static class SessionAdmissionTransport
    {
        /// <summary>Send a Sessions-owned reply on the existing server runtime's reliable EVENT path.</summary>
        public static bool TrySendSessionAdmissionReply(this ServerNetworkRuntime server,
            uint connectionId, ReadOnlySpan<byte> payload)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (connectionId == 0) return false;
            Span<byte> packet = stackalloc byte[SessionAdmissionPacket.MaxPacketBytes];
            return SessionAdmissionPacket.TryEncodeReply(packet, payload, out int written) &&
                server.TrySend(connectionId, UtpChannel.Event, packet.Slice(0, written));
        }
    }
}
