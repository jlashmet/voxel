using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Legacy fixed-clock scaffold retained for source compatibility.
    ///
    /// Networking, authentication, command queues, semantic hashes and repair now live in
    /// AuthoritativeServerSession. This type deliberately performs no packet handling or world
    /// authority; the previous scaffold trusted C_PlayerInput.playerId, retained Allocator.Temp
    /// arrays across frames, and ran a second incompatible convergence path.
    ///
    /// New server code should own its gameplay clock and call
    /// AuthoritativeServerSession.ProcessAuthoritativeTick once per authoritative tick.
    /// </summary>
    [Obsolete("Use AuthoritativeServerSession for authoritative networking and convergence.")]
    public struct ServerTickLoop
    {
        public const uint k_TickRateHz = 30;
        public const float k_TickDurationMs = 1000f / k_TickRateHz;
        public const uint k_RollbackWindowTicks = 15;
        public const uint k_HotRetentionTicks = 60;

        private uint _currentTick;
        private float _simulatedTimeMs;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(Allocator allocator)
        {
            _currentTick = 0;
            _simulatedTimeMs = 0f;
        }

        /// <summary>
        /// Compatibility-only clock advancement. brickStorage/regions are intentionally untouched;
        /// authoritative simulation belongs to the caller that invokes AuthoritativeServerSession.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float deltaTime, NativeArray<byte> brickStorage, ref RegionTable regions)
        {
            if (deltaTime <= 0f)
                return;

            _simulatedTimeMs += deltaTime * 1000f;
            while (_simulatedTimeMs >= k_TickDurationMs)
            {
                _simulatedTimeMs -= k_TickDurationMs;
                _currentTick++;
            }
        }

        /// <summary>
        /// Retained only so old callers compile. Input submitted here is intentionally ignored;
        /// live input must enter UtpServerHost -> ServerCommandInbox with connection-owned identity.
        /// </summary>
        [Obsolete("Submit input through UtpClientHost/ServerCommandInbox instead.")]
        public void SubmitInput(C_PlayerInput input)
        {
        }

        public void Dispose()
        {
        }

        public uint CurrentTick => _currentTick;
    }
}
