using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using System.Runtime.CompilerServices;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Wires client destruction input to C_AlterationRequest submission.
    ///
    /// When the player holds a destruction tool (pickaxe, explosive) and fires, this
    /// component collects the input parameters — origin in world space, radius/shape,
    /// material index — and encodes them into a C_AlterationRequest message. The request
    /// is sent to the server via the EVENT channel for authoritative adjudication.
    ///
    /// Follows Constitution Principle III: client prediction is presentation only;
    /// the server decides what actually happens. This component never mutates the grid.
    /// </summary>
    public static class DestructionInput
    {
        /// <summary>
        /// Encode a destruction attempt into a C_AlterationRequest and return its wire bytes.
        /// The caller is responsible for sending via the EVENT channel (ChannelSetup).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EncodeDestructionRequest(
            in C_PlayerInput playerInput,
            float3 worldOrigin,
            byte radius,
            byte brushKind,   // 0 = sphere, 1 = cylinder, 2 = cube
            out C_AlterationRequest request)
        {
            request = new C_AlterationRequest
            {
                tick = playerInput.tick,
                playerId = playerInput.playerId,
                sequence = playerInput.sequence,
                eventKind = (byte)AlterationEventKind.Explosion,
                origin = new int3(
                    (int)math.round(worldOrigin.x),
                    (int)math.round(worldOrigin.y),
                    (int)math.round(worldOrigin.z)),
                shapeRadius = radius,
                shapeExtentsYz = brushKind,
                material = VoxelDimensions.MaterialEmpty, // destruction removes material
                seed = GenerateSeed(playerInput.playerId, playerInput.sequence),
            };

            // Encode returns void; the caller owns the destination buffer and the
            // request's own WireSize is the byte count.
            return C_AlterationRequest.WireSize;
        }

        /// <summary>
        /// Create a DestructionRequest for sending. Overload that takes raw parameters.
        /// </summary>
        public static C_AlterationRequest Build(
            uint tick,
            ushort playerId,
            ushort sequence,
            int3 origin,
            byte radius,
            byte kind)
        {
            return new C_AlterationRequest
            {
                tick = tick,
                playerId = playerId,
                sequence = sequence,
                eventKind = (byte)AlterationEventKind.Explosion,
                origin = origin,
                shapeRadius = radius,
                shapeExtentsYz = kind,
                material = VoxelDimensions.MaterialEmpty,
                seed = GenerateSeed(playerId, sequence),
            };
        }

        /// <summary>
        /// Generate a deterministic seed from player and sequence identifiers.
        /// Ensures identical expansion on every client for the same event — the seed
        /// is part of the AlterationEvent wire format and must be reproducible server-side.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GenerateSeed(ushort playerId, ushort sequence)
        {
            // Mix player + sequence into a 32-bit seed using splitmix32-style mixing.
            uint mixed = (uint)playerId ^ ((uint)sequence << 16);
            mixed ^= mixed >> 13;
            mixed *= 0x5bd1e995u;
            mixed ^= mixed >> 15;
            return mixed;
        }

        /// <summary>
        /// Determine whether a destruction request should be sent now (coalescing).
        /// Prevents spamming the server with individual requests — coalesces rapid-fire
        /// destruction into a single request per tick.
        /// </summary>
        public static bool ShouldSendDestruction(uint lastSentTick, uint currentTick)
        {
            // Coalesce: one destruction request per server tick (30 Hz max).
            return currentTick != lastSentTick;
        }
    }

    /// <summary>
    /// Event kinds matching AlterationEvent.cs — kept in sync with the wire format.
    /// </summary>
    public enum AlterationEventKind : byte
    {
        Explosion = 1,
        Brush = 2,
        RawBatch = 3,
    }
}
