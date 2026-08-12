using System;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    public interface IClientEventCommandHandler
    {
        void HandleAlterationRequest(uint connectionId, in C_AlterationRequest request);
    }

    public interface IClientConvergenceCommandHandler
    {
        void HandleRegionHashMismatch(uint connectionId, in C_RegionHashMismatch mismatch);
    }

    /// <summary>
    /// Server-side reliable EVENT dispatcher. Framing/type validation happens here; decoded intent
    /// is handed to bounded queues with transport-owned connection identity supplied separately.
    /// </summary>
    public static class ClientEventPacketReceiver
    {
        public static bool TryDispatch(
            uint connectionId,
            ReadOnlySpan<byte> packet,
            IClientEventCommandHandler eventHandler,
            IClientConvergenceCommandHandler convergenceHandler = null)
        {
            if (eventHandler == null)
                throw new ArgumentNullException(nameof(eventHandler));
            if (!ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out _))
                return false;

            switch (kind)
            {
                case ProtocolMessageKind.C_AlterationRequest:
                    if (!AlterationRequestPacket.TryDecode(packet, out C_AlterationRequest request))
                        return false;
                    eventHandler.HandleAlterationRequest(connectionId, in request);
                    return true;

                case ProtocolMessageKind.C_RegionHashMismatch:
                    if (convergenceHandler == null ||
                        !RegionHashMismatchPacket.TryDecode(packet, out C_RegionHashMismatch mismatch))
                        return false;
                    convergenceHandler.HandleRegionHashMismatch(connectionId, in mismatch);
                    return true;

                default:
                    return false;
            }
        }
    }
}
