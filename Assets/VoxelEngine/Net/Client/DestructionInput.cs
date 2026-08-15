using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Compatibility helper that turns presentation-side destruction input into the canonical
    /// connection-authenticated C_AlterationRequest. It never mutates voxel state locally.
    /// </summary>
    public static class DestructionInput
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EncodeDestructionRequest(
            in C_PlayerInput playerInput,
            float3 worldOrigin,
            byte radius,
            byte brushKind,
            out C_AlterationRequest request)
        {
            _ = brushKind; // Explosion requests are spherical; shape type is authoritative protocol data.
            request = new C_AlterationRequest(
                playerInput.tick,
                new int3(
                    (int)math.round(worldOrigin.x),
                    (int)math.round(worldOrigin.y),
                    (int)math.round(worldOrigin.z)),
                AlterationEvent.KindExplosion,
                VoxelGrid.MaterialEmpty,
                AlterationEvent.KindExplosion,
                radius,
                GenerateSeed(0, playerInput.sequence),
                playerInput.sequence);
            return C_AlterationRequest.WireSize;
        }

        /// <summary>
        /// Legacy call shape retained for old in-process callers. playerId is used only to retain
        /// deterministic compatibility seeding; it is not placed in the request and is never trusted
        /// as identity by the server.
        /// </summary>
        public static C_AlterationRequest Build(
            uint tick,
            ushort playerId,
            ushort sequence,
            int3 origin,
            byte radius,
            byte kind)
        {
            _ = kind;
            return new C_AlterationRequest(
                tick,
                origin,
                AlterationEvent.KindExplosion,
                VoxelGrid.MaterialEmpty,
                AlterationEvent.KindExplosion,
                radius,
                GenerateSeed(playerId, sequence),
                sequence);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GenerateSeed(ushort playerId, ushort sequence)
        {
            uint mixed = (uint)playerId ^ ((uint)sequence << 16);
            mixed ^= mixed >> 13;
            mixed *= 0x5bd1e995u;
            mixed ^= mixed >> 15;
            return mixed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldSendDestruction(uint lastSentTick, uint currentTick) =>
            currentTick != lastSentTick;
    }
}
