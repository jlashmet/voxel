using System;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Authoritative handler for decoded client commands arriving on the EVENT-side receive path.
    /// The connection ID is supplied separately from the payload so callers can map it to the
    /// authenticated player and must not trust a client-authored playerId field.
    /// </summary>
    public interface IClientEventCommandHandler
    {
        void HandleAlterationRequest(uint connectionId, in C_AlterationRequest request);
    }

    /// <summary>
    /// Server-side framed packet dispatcher. It performs framing/type validation only; gameplay
    /// validation, connection->player identity, rate limits, reach checks, and authoritative seed
    /// substitution belong to the handler behind this boundary.
    /// </summary>
    public static class ClientEventPacketReceiver
    {
        public static bool TryDispatch(
            uint connectionId,
            ReadOnlySpan<byte> packet,
            IClientEventCommandHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (!ProtocolEnvelope.TryReadHeader(packet, out var kind, out _))
                return false;

            switch (kind)
            {
                case ProtocolMessageKind.C_AlterationRequest:
                    if (!AlterationRequestPacket.TryDecode(packet, out var request))
                        return false;

                    handler.HandleAlterationRequest(connectionId, in request);
                    return true;

                // Player input and region requests remain on their existing codecs until the
                // concrete host decides the final ephemeral/BULK receive-channel split.
                default:
                    return false;
            }
        }
    }
}
