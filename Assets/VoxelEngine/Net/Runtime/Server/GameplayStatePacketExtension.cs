using System;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Optional game-level state emitter invoked from the existing authoritative server tick.
    /// Implementations own their semantic capture/encoding; networking owns connection routing.
    /// </summary>
    public interface IAuthoritativeGameplayStateEmitter
    {
        void Emit(uint serverTick, ServerPlayerRegistry players, IGameplayStatePacketSink sink);
    }

    public interface IGameplayStatePacketSink
    {
        bool SendGameplayStatePacket(uint connectionId, ReadOnlySpan<byte> packet);
    }
}
