using System;

namespace VoxelEngine.Net.Runtime.Client
{
    /// <summary>Optional handler for the game-level authoritative state packet family.</summary>
    public interface IGameplayStatePacketHandler
    {
        bool HandleGameplayStatePacket(ReadOnlySpan<byte> packet);
    }
}
