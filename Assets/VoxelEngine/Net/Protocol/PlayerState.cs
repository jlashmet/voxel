using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// S_PlayerState — server-to-client delta position update for a remote player.
    ///
    /// Sends compressed position/velocity deltas rather than absolute values to reduce bandwidth.
    /// Only transmitted when the delta exceeds the configured threshold (threshold-based send).
    ///
    /// Wire format (32 bytes):
    ///   Offset  Size  Field
    ///   0       2     playerId (ushort)         — target player ID
    ///   2       4     tick (uint)                — server tick of this update
    ///   6       2     sequence (ushort)          — update ordinal for interpolation
    ///   8       12    positionDelta (float3)     — quantised delta position
    ///   20      12    velocityDelta (float3)     — quantised delta velocity
    /// </summary>
    public struct S_PlayerState : IEquatable<S_PlayerState>
    {
        // -- quantisation constants -----------------------------------------------

        /// <summary>Position delta quantisation: Q16.16 fixed-point in metres.
        /// Range ±32768 m at ~0.5 mm precision — sufficient for inter-player deltas.</summary>
        private const int k_PosQuantBits = 16;

        /// <summary>Velocity delta quantisation: Q12.20 fixed-point in m/s.
        /// Range ±4096 m/s at ~0.001 m/s precision.</summary>
        private const int k_VelQuantBits = 20;

        // -- constants ------------------------------------------------------------

        /// <summary>Minimum delta threshold in metres — below this, no message is sent.</summary>
        public const float k_PositionThreshold = 0.01f; // 1 cm minimum delta

        /// <summary>Wire size in bytes for S_PlayerState (always fixed).</summary>
        public const int WireSize = 32;

        // -- fields ---------------------------------------------------------------

        /// <summary>ID of the player whose state is being updated.</summary>
        public ushort playerId;

        /// <summary>Server tick when this update was authored.</summary>
        public uint tick;

        /// <summary>Update ordinal for interpolation ordering on the client.</summary>
        public ushort sequence;

        /// <summary>Delta position — Q16.16 fixed-point in metres.</summary>
        public int3 positionDeltaInt;

        /// <summary>Delta velocity — Q12.20 fixed-point in m/s.</summary>
        public int3 velocityDeltaInt;

        // -- construction ---------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_PlayerState(ushort playerId, uint tick)
        {
            this.playerId = playerId;
            this.tick = tick;
            this.sequence = 0;
            this.positionDeltaInt = int3.zero;
            this.velocityDeltaInt = int3.zero;
        }

        /// <summary>
        /// Checks whether a delta exceeds the send threshold. Returns true if this update
        /// should be transmitted.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldSend(float3 delta)
        {
            return math.length(delta) >= k_PositionThreshold;
        }

        // -- encoding ------------------------------------------------------------

        /// <summary>Encodes the player state to wire format with the given deltas.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst, float3 positionDelta, float3 velocityDelta)
        {
            ThrowIfTooSmall(dst, WireSize);

            // playerId (2 bytes)
            dst[0] = (byte)(playerId >> 0);
            dst[1] = (byte)(playerId >> 8);

            // tick (4 bytes)
            WriteUint32(dst, 2, tick);

            // sequence (2 bytes)
            dst[6] = (byte)(sequence >> 0);
            dst[7] = (byte)(sequence >> 8);

            // positionDelta — Q16.16 fixed-point (12 bytes as int3)
            int3 packedPos = QuantisePosition(positionDelta);
            WriteInt32(dst, 8, packedPos.x);
            WriteInt32(dst, 12, packedPos.y);
            WriteInt32(dst, 16, packedPos.z);

            // velocityDelta — Q12.20 fixed-point (12 bytes as int3)
            int3 packedVel = QuantiseVelocity(velocityDelta);
            WriteInt32(dst, 20, packedVel.x);
            WriteInt32(dst, 24, packedVel.y);
            WriteInt32(dst, 28, packedVel.z);

            this.positionDeltaInt = packedPos;
            this.velocityDeltaInt = packedVel;
        }

        /// <summary>Decodes the delta values from wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (S_PlayerState msg, float3 positionDelta, float3 velocityDelta) Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize);

            S_PlayerState msg;
            msg.playerId = (ushort)(src[0] | (src[1] << 8));
            msg.tick = ReadUint32(src, 2);
            msg.sequence = (ushort)(src[6] | (src[7] << 8));

            int3 posDelta = new int3(
                ReadInt32(src, 8),
                ReadInt32(src, 12),
                ReadInt32(src, 16));
            int3 velDelta = new int3(
                ReadInt32(src, 20),
                ReadInt32(src, 24),
                ReadInt32(src, 28));

            msg.positionDeltaInt = posDelta;
            msg.velocityDeltaInt = velDelta;

            float3 positionDelta = DequantisePosition(posDelta);
            float3 velocityDelta = DequantiseVelocity(velDelta);

            return (msg, positionDelta, velocityDelta);
        }

        // -- quantisation helpers ------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 QuantisePosition(float3 delta)
        {
            int scale = 1 << k_PosQuantBits;
            return new int3(
                math.clamp((int)(delta.x * scale), int.MinValue, int.MaxValue),
                math.clamp((int)(delta.y * scale), int.MinValue, int.MaxValue),
                math.clamp((int)(delta.z * scale), int.MinValue, int.MaxValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 DequantisePosition(int3 packed)
        {
            float inv = 1f / (1 << k_PosQuantBits);
            return new float3(packed.x * inv, packed.y * inv, packed.z * inv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 QuantiseVelocity(float3 delta)
        {
            int scale = 1 << k_VelQuantBits;
            return new int3(
                math.clamp((int)(delta.x * scale), int.MinValue, int.MaxValue),
                math.clamp((int)(delta.y * scale), int.MinValue, int.MaxValue),
                math.clamp((int)(delta.z * scale), int.MinValue, int.MaxValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 DequantiseVelocity(int3 packed)
        {
            float inv = 1f / (1 << k_VelQuantBits);
            return new float3(packed.x * inv, packed.y * inv, packed.z * inv);
        }

        // -- equality ------------------------------------------------------------

        public bool Equals(S_PlayerState other) =>
            playerId == other.playerId && tick == other.tick && sequence == other.sequence &&
            math.all(positionDeltaInt == other.positionDeltaInt) && math.all(velocityDeltaInt == other.velocityDeltaInt);
        public override bool Equals(object obj) => obj is S_PlayerState o && Equals(o);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = playerId.GetHashCode();
                h = (h * 397) ^ tick.GetHashCode();
                h = (h * 397) ^ sequence.GetHashCode();
                h = (h * 397) ^ positionDeltaInt.GetHashCode();
                h = (h * 397) ^ velocityDeltaInt.GetHashCode();
                return h;
            }
        }
        public static bool operator ==(S_PlayerState a, S_PlayerState b) => a.Equals(b);
        public static bool operator !=(S_PlayerState a, S_PlayerState b) => !a.Equals(b);

        // -- bounds checking ----------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"S_PlayerState: dst too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"S_PlayerState: src too small");
        }

        // -- primitive readers/writers ------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset]     = (byte)(value >> 0);
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] | ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) | ((uint)src[offset + 3] << 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32(Span<byte> dst, int offset, int value)
        {
            uint u = (uint)value;
            dst[offset]     = (byte)(u >> 0);
            dst[offset + 1] = (byte)(u >> 8);
            dst[offset + 2] = (byte)(u >> 16);
            dst[offset + 3] = (byte)(u >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            (int)(uint)(src[offset] | (src[offset + 1] << 8) |
                         (src[offset + 2] << 16) | (src[offset + 3] << 24));
    }
}
