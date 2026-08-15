using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>
    /// C_PlayerInput — ephemeral client command sample.
    ///
    /// Payload (16 bytes):
    ///   0   4  clientTick    uint
    ///   4   2  sequence      ushort
    ///   6   1  moveX         sbyte   [-127,127]
    ///   7   1  moveY         sbyte   [-127,127]
    ///   8   2  viewYaw       ushort  full turn
    ///   10  2  viewPitch     short   [-90,+90] degrees
    ///   12  2  actions       ushort  bitfield
    ///   14  1  toolMaterial  byte
    ///   15  1  flags         byte
    ///
    /// There is deliberately no player ID and no claimed world position on the wire. Identity and
    /// position are authoritative server state. The client sends intent only.
    /// </summary>
    public struct C_PlayerInput : IEquatable<C_PlayerInput>
    {
        public const int WireSize = 16;

        [Flags]
        public enum ActionBits : ushort
        {
            None = 0,
            Move = 1 << 0,
            Aim = 1 << 1,
            UseMain = 1 << 2,
            UseAlt = 1 << 3,
            Cancel = 1 << 4,
        }

        /// <summary>Legacy single-action enum retained for source compatibility with scaffold callers.</summary>
        public enum ActionType : byte
        {
            None = 0,
            Move = 1,
            Aim = 2,
            UseMain = 3,
            UseAlt = 4,
            Cancel = 5,
        }

        public uint tick;
        public ushort sequence;
        public sbyte moveX;
        public sbyte moveY;
        public ushort viewYaw;
        public short viewPitch;
        public ushort actions;
        public byte toolMaterial;
        public byte flags;

        /// <summary>
        /// Compatibility-only field for old in-process callers. It is always zero and is never
        /// encoded. Server code must derive player identity from the transport connection.
        /// </summary>
        [Obsolete("Player identity is connection-owned and is not part of C_PlayerInput.")]
        public ushort playerId;

        public C_PlayerInput(
            uint tick,
            ushort sequence,
            float2 movement,
            float3 viewDirection,
            ActionBits actions,
            byte toolMaterial,
            byte flags = 0)
        {
            this.tick = tick;
            this.sequence = sequence;
            moveX = QuantiseAxis(movement.x);
            moveY = QuantiseAxis(movement.y);
            QuantiseView(viewDirection, out viewYaw, out viewPitch);
            this.actions = (ushort)actions;
            this.toolMaterial = toolMaterial;
            this.flags = flags;
            playerId = 0;
        }

        /// <summary>
        /// Compatibility constructor for the original scaffold. The supplied playerId and position
        /// are intentionally not trusted or transmitted. position.xz is interpreted as movement
        /// intent so older tests/callers continue to compile while migrating to the canonical API.
        /// </summary>
        [Obsolete("Use the constructor taking movement, viewDirection and ActionBits.")]
        public C_PlayerInput(
            uint tick,
            ushort playerId,
            ushort sequence,
            float3 position,
            float3 direction,
            ActionType actionType,
            byte toolMaterial)
            : this(
                tick,
                sequence,
                new float2(position.x, position.z),
                direction,
                ToActionBits(actionType),
                toolMaterial,
                0)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            if (dst.Length < WireSize)
                throw new ArgumentException("C_PlayerInput destination is too small.", nameof(dst));

            WriteUint32(dst, 0, tick);
            WriteUint16(dst, 4, sequence);
            dst[6] = unchecked((byte)moveX);
            dst[7] = unchecked((byte)moveY);
            WriteUint16(dst, 8, viewYaw);
            WriteInt16(dst, 10, viewPitch);
            WriteUint16(dst, 12, actions);
            dst[14] = toolMaterial;
            dst[15] = flags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static C_PlayerInput Decode(ReadOnlySpan<byte> src)
        {
            if (src.Length < WireSize)
                throw new ArgumentException("C_PlayerInput source is too small.", nameof(src));

            return new C_PlayerInput
            {
                tick = ReadUint32(src, 0),
                sequence = ReadUint16(src, 4),
                moveX = unchecked((sbyte)src[6]),
                moveY = unchecked((sbyte)src[7]),
                viewYaw = ReadUint16(src, 8),
                viewPitch = ReadInt16(src, 10),
                actions = ReadUint16(src, 12),
                toolMaterial = src[14],
                flags = src[15],
                playerId = 0,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 Movement() => new float2(moveX / 127f, moveY / 127f);

        public float3 ViewDirection()
        {
            float yaw = ((float)viewYaw / ushort.MaxValue) * (2f * math.PI) - math.PI;
            float pitch = ((float)viewPitch / short.MaxValue) * (math.PI * 0.5f);
            float cosPitch = math.cos(pitch);

            return new float3(
                math.sin(yaw) * cosPitch,
                math.sin(pitch),
                math.cos(yaw) * cosPitch);
        }

        [Obsolete("C_PlayerInput no longer carries world position; this returns movement intent in XZ.")]
        public float3 Position()
        {
            float2 movement = Movement();
            return new float3(movement.x, 0f, movement.y);
        }

        [Obsolete("Use ViewDirection().")]
        public float3 Direction() => ViewDirection();

        public ActionBits Actions => (ActionBits)actions;

        public ActionType ActionTypeEnum()
        {
            ActionBits bits = Actions;
            if ((bits & ActionBits.UseMain) != 0) return ActionType.UseMain;
            if ((bits & ActionBits.UseAlt) != 0) return ActionType.UseAlt;
            if ((bits & ActionBits.Cancel) != 0) return ActionType.Cancel;
            if ((bits & ActionBits.Aim) != 0) return ActionType.Aim;
            if ((bits & ActionBits.Move) != 0) return ActionType.Move;
            return ActionType.None;
        }

        public bool Equals(C_PlayerInput other) =>
            tick == other.tick &&
            sequence == other.sequence &&
            moveX == other.moveX &&
            moveY == other.moveY &&
            viewYaw == other.viewYaw &&
            viewPitch == other.viewPitch &&
            actions == other.actions &&
            toolMaterial == other.toolMaterial &&
            flags == other.flags;

        public override bool Equals(object obj) => obj is C_PlayerInput other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)tick;
                hash = (hash * 397) ^ sequence;
                hash = (hash * 397) ^ moveX;
                hash = (hash * 397) ^ moveY;
                hash = (hash * 397) ^ viewYaw;
                hash = (hash * 397) ^ viewPitch;
                hash = (hash * 397) ^ actions;
                hash = (hash * 397) ^ toolMaterial;
                hash = (hash * 397) ^ flags;
                return hash;
            }
        }

        public static bool operator ==(C_PlayerInput left, C_PlayerInput right) => left.Equals(right);
        public static bool operator !=(C_PlayerInput left, C_PlayerInput right) => !left.Equals(right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActionBits ToActionBits(ActionType actionType)
        {
            return actionType switch
            {
                ActionType.Move => ActionBits.Move,
                ActionType.Aim => ActionBits.Aim,
                ActionType.UseMain => ActionBits.UseMain,
                ActionType.UseAlt => ActionBits.UseAlt,
                ActionType.Cancel => ActionBits.Cancel,
                _ => ActionBits.None,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static sbyte QuantiseAxis(float value)
        {
            int quantised = (int)math.round(math.clamp(value, -1f, 1f) * 127f);
            return (sbyte)math.clamp(quantised, -127, 127);
        }

        private static void QuantiseView(float3 direction, out ushort yaw, out short pitch)
        {
            float lengthSq = math.lengthsq(direction);
            float3 normal = lengthSq > 1e-8f ? direction * math.rsqrt(lengthSq) : new float3(0f, 0f, 1f);

            float yawRadians = math.atan2(normal.x, normal.z);
            float yaw01 = (yawRadians + math.PI) / (2f * math.PI);
            yaw = (ushort)math.clamp((int)math.round(yaw01 * ushort.MaxValue), 0, ushort.MaxValue);

            float pitchRadians = math.asin(math.clamp(normal.y, -1f, 1f));
            float pitchNormal = pitchRadians / (math.PI * 0.5f);
            pitch = (short)math.clamp((int)math.round(pitchNormal * short.MaxValue), short.MinValue + 1, short.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt16(Span<byte> dst, int offset, short value) =>
            WriteUint16(dst, offset, unchecked((ushort)value));

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
        private static short ReadInt16(ReadOnlySpan<byte> src, int offset) =>
            unchecked((short)ReadUint16(src, offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUint16(ReadOnlySpan<byte> src, int offset) =>
            (ushort)(src[offset] | (src[offset + 1] << 8));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] |
            ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) |
            ((uint)src[offset + 3] << 24);
    }
}
