using System;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Optional game-level state emitter invoked from the existing authoritative server tick.
    /// Implementations own semantic capture/encoding and repair policy; networking owns routing.
    /// </summary>
    public interface IAuthoritativeGameplayStateEmitter : IClientGameplayStateRepairHandler
    {
        void Emit(uint serverTick, ServerPlayerRegistry players, IGameplayStatePacketSink sink);
    }

    public interface IGameplayStatePacketSink
    {
        bool SendGameplayStatePacket(uint connectionId, ReadOnlySpan<byte> packet);
    }
}
