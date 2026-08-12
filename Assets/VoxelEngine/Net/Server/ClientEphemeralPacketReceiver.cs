using System;
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

    public static class ClientEphemeralPacketReceiver
    {
        public static bool TryDispatch(
            uint connectionId,
            ReadOnlySpan<byte> packet,
            IClientInputCommandHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (!PlayerInputPacket.TryDecode(packet, out var input))
                return false;

            handler.HandlePlayerInput(connectionId, in input);
            return true;
        }
    }
}
