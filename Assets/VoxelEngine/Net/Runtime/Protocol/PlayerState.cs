using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>
    /// Absolute server-authored player kinematic snapshot.
    ///
    /// Payload (40 bytes):
    ///   0..1   playerId             ushort
    ///   2..5   serverTick           uint
    ///   6..7   stateSequence        ushort
    ///   8..9   ackInputSequence     ushort
    ///   10..11 flags                ushort
    ///   12..23 position             int3, Q19.13 voxels
    ///   24..35 velocity             int3, Q12.20 voxels/second
    ///   36..37 viewYaw              ushort, full turn
    ///   38..39 reserved             ushort (zero in v1)
    ///
    /// Position and velocity are absolute, never deltas from another network packet. This makes
    /// every snapshot independently useful after packet loss and gives the owning client an exact
    /// rewind point for prediction reconciliation.
    /// </summary>
    public struct S_PlayerState : IEquatable<S_PlayerState>
    {
        private const int PositionFractionBits = 13;
        private const int VelocityFractionBits = 20;

        public const int WireSize = 40;

        // Retained only for old scaffold callers. Live scheduling is cadence based rather than
        // threshold based because absolute snapshots are deliberately independent.
        public const float k_PositionThreshold = 0.01f;

        [Flags]
        public enum StateFlags : ushort
        {
            None = 0,
            HasInputAck = 1 << 0,
            Grounded = 1 << 1,
            Teleport = 1 << 2,
            Respawn = 1 << 3,
        }

        public ushort playerId;
        public uint tick;
        public ushort sequence;
        public ushort ackInputSequence;
        public ushort flags;
        public int3 positionInt;
        public int3 velocityInt;
        public ushort viewYaw;
        public ushort reserved;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_PlayerState(ushort playerId, uint tick)
        {
            this.playerId = playerId;
            this.tick = tick;
            sequence = 0;
            ackInputSequence = 0;
            flags = 0;
            positionInt = int3.zero;
            velocityInt = int3.zero;
            viewYaw = 0;
            reserved = 0;
        }

        public static S_PlayerState Create(
            ushort playerId,
            uint serverTick,
            ushort stateSequence,
            float3 positionVoxels,
            float3 velocityVoxelsPerSecond,
            ushort viewYaw,
            StateFlags stateFlags,
            bool hasInputAck,
            ushort ackInputSequence)
        {
            if (playerId == 0 || serverTick == 0)
                throw new ArgumentOutOfRangeException(playerId == 0 ? nameof(playerId) : nameof(serverTick));

            return new S_PlayerState
            {
                playerId = playerId,
                tick = serverTick,
                sequence = stateSequence,
                ackInputSequence = hasInputAck ? ackInputSequence : (ushort)0,
                flags = (ushort)(hasInputAck ? stateFlags | StateFlags.HasInputAck : stateFlags & ~StateFlags.HasInputAck),
                positionInt = QuantisePosition(positionVoxels),
                velocityInt = QuantiseVelocity(velocityVoxelsPerSecond),
                viewYaw = viewYaw,
                reserved = 0,
            };
        }

        public bool HasInputAck => ((StateFlags)flags & StateFlags.HasInputAck) != 0;
        public StateFlags Flags => (StateFlags)flags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 PositionVoxels() => Dequantise(positionInt, PositionFractionBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 VelocityVoxelsPerSecond() => Dequantise(velocityInt, VelocityFractionBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ViewYawRadians() => ((float)viewYaw / ushort.MaxValue) * (2f * math.PI) - math.PI;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize, "dst too small");
            WriteUint16(dst, 0, playerId);
            WriteUint32(dst, 2, tick);
            WriteUint16(dst, 6, sequence);
            WriteUint16(dst, 8, ackInputSequence);
            WriteUint16(dst, 10, flags);
            WriteInt32(dst, 12, positionInt.x);
            WriteInt32(dst, 16, positionInt.y);
            WriteInt32(dst, 20, positionInt.z);
            WriteInt32(dst, 24, velocityInt.x);
            WriteInt32(dst, 28, velocityInt.y);
            WriteInt32(dst, 32, velocityInt.z);
            WriteUint16(dst, 36, viewYaw);
            WriteUint16(dst, 38, 0);
        }

        /// <summary>Compatibility overload. The arguments now represent absolute state, not deltas.</summary>
        [Obsolete("Player state is now an absolute snapshot. Populate fields or use S_PlayerState.Create().")]
        public void Encode(Span<byte> dst, float3 positionVoxels, float3 velocityVoxelsPerSecond)
        {
            positionInt = QuantisePosition(positionVoxels);
            velocityInt = QuantiseVelocity(velocityVoxelsPerSecond);
            Encode(dst);
        }

        public static bool TryDecode(ReadOnlySpan<byte> src, out S_PlayerState state)
        {
            state = default;
            if (src.Length < WireSize)
                return false;

            state.playerId = ReadUint16(src, 0);
            state.tick = ReadUint32(src, 2);
            state.sequence = ReadUint16(src, 6);
            state.ackInputSequence = ReadUint16(src, 8);
            state.flags = ReadUint16(src, 10);
            state.positionInt = new int3(ReadInt32(src, 12), ReadInt32(src, 16), ReadInt32(src, 20));
            state.velocityInt = new int3(ReadInt32(src, 24), ReadInt32(src, 28), ReadInt32(src, 32));
            state.viewYaw = ReadUint16(src, 36);
            state.reserved = ReadUint16(src, 38);

            return state.playerId != 0 &&
                   state.tick != 0 &&
                   state.reserved == 0 &&
                   (((StateFlags)state.flags) & ~(StateFlags.HasInputAck | StateFlags.Grounded | StateFlags.Teleport | StateFlags.Respawn)) == 0;
        }

        /// <summary>
        /// Compatibility decode surface for old in-process tests. Returned vectors are now absolute
        /// position/velocity even though the tuple element names are retained by source callers.
        /// </summary>
        [Obsolete("Use TryDecode and PositionVoxels()/VelocityVoxelsPerSecond().")]
        public static (S_PlayerState msg, float3 positionDelta, float3 velocityDelta) Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize, "src too small");
            if (!TryDecode(src, out S_PlayerState state))
                throw new ArgumentException("Invalid S_PlayerState payload.", nameof(src));
            return (state, state.PositionVoxels(), state.VelocityVoxelsPerSecond());
        }

        [Obsolete("Live player-state replication is cadence based, not delta-threshold based.")]
        public static bool ShouldSend(float3 delta) => math.length(delta) >= k_PositionThreshold;

        public bool Equals(S_PlayerState other) =>
            playerId == other.playerId && tick == other.tick && sequence == other.sequence &&
            ackInputSequence == other.ackInputSequence && flags == other.flags &&
            math.all(positionInt == other.positionInt) && math.all(velocityInt == other.velocityInt) &&
            viewYaw == other.viewYaw && reserved == other.reserved;

        public override bool Equals(object obj) => obj is S_PlayerState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = playerId;
                h = (h * 397) ^ tick.GetHashCode();
                h = (h * 397) ^ sequence;
                h = (h * 397) ^ ackInputSequence;
                h = (h * 397) ^ flags;
                h = (h * 397) ^ positionInt.GetHashCode();
                h = (h * 397) ^ velocityInt.GetHashCode();
                h = (h * 397) ^ viewYaw;
                return h;
            }
        }

        public static bool operator ==(S_PlayerState a, S_PlayerState b) => a.Equals(b);
        public static bool operator !=(S_PlayerState a, S_PlayerState b) => !a.Equals(b);

        private static int3 QuantisePosition(float3 value) => Quantise(value, PositionFractionBits);
        private static int3 QuantiseVelocity(float3 value) => Quantise(value, VelocityFractionBits);

        private static int3 Quantise(float3 value, int fractionBits)
        {
            double scale = 1L << fractionBits;
            return new int3(
                QuantiseComponent(value.x, scale),
                QuantiseComponent(value.y, scale),
                QuantiseComponent(value.z, scale));
        }

        private static int QuantiseComponent(float value, double scale)
        {
            double scaled = Math.Round((double)value * scale, MidpointRounding.AwayFromZero);
            if (scaled > int.MaxValue) return int.MaxValue;
            if (scaled < int.MinValue) return int.MinValue;
            return (int)scaled;
        }

        private static float3 Dequantise(int3 value, int fractionBits)
        {
            float inv = 1f / (1 << fractionBits);
            return new float3(value.x * inv, value.y * inv, value.z * inv);
        }

        private static void ThrowIfTooSmall(Span<byte> span, int required, string message)
        {
            if (span.Length >= required) return;
            UnityEngine.Debug.LogError($"S_PlayerState: {message}");
            throw new ArgumentException($"S_PlayerState requires {required} bytes.", nameof(span));
        }

        private static void ThrowIfTooSmall(ReadOnlySpan<byte> span, int required, string message)
        {
            if (span.Length >= required) return;
            UnityEngine.Debug.LogError($"S_PlayerState: {message}");
            throw new ArgumentException($"S_PlayerState requires {required} bytes.", nameof(span));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint16(Span<byte> dst, int offset, ushort value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32(Span<byte> dst, int offset, int value) => WriteUint32(dst, offset, unchecked((uint)value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUint16(ReadOnlySpan<byte> src, int offset) =>
            (ushort)(src[offset] | (src[offset + 1] << 8));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] |
            ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) |
            ((uint)src[offset + 3] << 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) => unchecked((int)ReadUint32(src, offset));
    }
}
