using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// C_PlayerInput — client-to-server message for player actions (movement, aiming, tool use).
    ///
    /// Wire format (34 bytes):
    ///   Offset  Size  Field
    ///   0       4     tick (uint)            — server tick timestamp
    ///   4       2     playerId (ushort)      — submitting player
    ///   6       2     sequence (ushort)      — client submission ordinal
    ///   8       12    position quantized (3 × uint — 8-bit fractional precision)
    ///   20      12    direction quantized (sphere-mapped, 6-bit per axis → uint pair)
    ///   32      1     actionType (byte)      — C_ActionType discriminator
    ///   33      1     toolMaterial (byte)    — tool/material index for the action
    ///
    /// Quantisation:
    ///   Position: encoded as fixed-point with 8-bit fractional part (Q8.24 format).
    ///     World space is scaled to [-1024, +1024] meters per axis before quantization.
    ///     Range: ±1023.996 m at ~0.004 m precision — sufficient for voxel-accurate positioning.
    ///   Direction: sphere-mapped to a unit hemisphere (upper) using 6-bit precision
    ///     per axis, packed into two uints. Decodes back to float3 via inverse mapping.
    /// </summary>
    public struct C_PlayerInput : IEquatable<C_PlayerInput>
    {
        // -- constants -------------------------------------------------------------

        /// <summary>Wire size in bytes for C_PlayerInput.</summary>
        public const int WireSize = 34;

        // Position quantisation — Q8.24 fixed-point, scaled world space.
        private const int k_PositionScaleBits = 8;
        private const float k_PositionWorldScale = 1024f; // metres per axis before quantisation

        /// <summary>Action type discriminator for C_PlayerInput.</summary>
        public enum ActionType : byte
        {
            /// <summary>No action — heart-beat / keep-alive message.</summary>
            None     = 0,

            /// <summary>Move to target position (continuous locomotion).</summary>
            Move     = 1,

            /// <summary>Aim direction update (reoriented gaze/weapon lock).</summary>
            Aim      = 2,

            /// <summary>Primary tool use — e.g. excavation or placement.</summary>
            UseMain  = 3,

            /// <summary>Secondary tool use — e.g. alternative mode of the same tool.</summary>
            UseAlt   = 4,

            /// <summary>Cancel ongoing action (release grip / disengage tool).</summary>
            Cancel   = 5,
        }

        // -- fields ---------------------------------------------------------------

        /// <summary>Server tick this input targets. Used for lag-compensation.</summary>
        public uint tick;

        /// <summary>ID of the player submitting this input.</summary>
        public ushort playerId;

        /// <summary>Client-assigned ordinal within their input stream.</summary>
        public ushort sequence;

        /// <summary>Player position — Q8.24 fixed-point, scaled to [-1024,+1024].</summary>
        public uint posX, posY, posZ;

        /// <summary>Sphere-mapped direction component X — 6-bit per-axis encoding.</summary>
        public uint dirX;

        /// <summary>Sphere-mapped direction component Y — 6-bit per-axis encoding.</summary>
        public uint dirY;

        /// <summary>Sphere-mapped direction component Z (derived) — packed for compression.</summary>
        public uint dirZ;

        /// <summary>Action type discriminator from ActionType enum.</summary>
        public byte actionType;

        /// <summary>Tool/material index being used. 0 = unarmed.</summary>
        public byte toolMaterial;

        // -- construction ---------------------------------------------------------

        /// <summary>Construct a C_PlayerInput with all fields explicitly set.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public C_PlayerInput(uint tick, ushort playerId, ushort sequence,
            float3 position, float3 direction, ActionType actionType, byte toolMaterial)
        {
            this.tick = tick;
            this.playerId = playerId;
            this.sequence = sequence;

            // Quantise position to Q8.24 fixed-point with world space scale.
            this.posX = QuantisePosition(position.x);
            this.posY = QuantisePosition(position.y);
            this.posZ = QuantisePosition(position.z);

            // Sphere-map and quantise direction (6-bit per axis).
            dirX = QuantiseDirectionX(direction);
            dirY = QuantiseDirectionY(direction);
            dirZ = QuantiseDirectionZ(direction);

            this.actionType = (byte)actionType;
            this.toolMaterial = toolMaterial;
        }

        // -- encoding ------------------------------------------------------------

        /// <summary>Encodes this message to the wire format defined in the struct docs.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize);

            // tick (4 bytes)
            WriteUint32(dst, 0, tick);

            // playerId (2 bytes)
            dst[4] = (byte)(playerId >> 0);
            dst[5] = (byte)(playerId >> 8);

            // sequence (2 bytes)
            dst[6] = (byte)(sequence >> 0);
            dst[7] = (byte)(sequence >> 8);

            // position quantized — 3 × uint (12 bytes)
            WriteUint32(dst, 8, posX);
            WriteUint32(dst, 12, posY);
            WriteUint32(dst, 16, posZ);

            // direction sphere-mapped — 3 × uint (12 bytes)
            WriteUint32(dst, 20, dirX);
            WriteUint32(dst, 24, dirY);
            WriteUint32(dst, 28, dirZ);

            // actionType + toolMaterial (2 bytes)
            dst[32] = actionType;
            dst[33] = toolMaterial;
        }

        /// <summary>Decodes a C_PlayerInput from the wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static C_PlayerInput Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize);

            uint tick = ReadUint32(src, 0);
            ushort playerId = (ushort)(src[4] | (src[5] << 8));
            ushort sequence = (ushort)(src[6] | (src[7] << 8));

            C_PlayerInput msg;
            msg.tick = tick;
            msg.playerId = playerId;
            msg.sequence = sequence;

            // Decode position from Q8.24 back to float3.
            msg.posX    = ReadUint32(src, 8);
            msg.posY    = ReadUint32(src, 12);
            msg.posZ    = ReadUint32(src, 16);

            // Decode direction from sphere-mapped encoding.
            msg.dirX    = ReadUint32(src, 20);
            msg.dirY    = ReadUint32(src, 24);
            msg.dirZ    = ReadUint32(src, 28);

            msg.actionType   = src[32];
            msg.toolMaterial = src[33];

            return msg;
        }

        /// <summary>Decodes the quantised position back to world-space float3.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 Position()
        {
            float scale = 1f / (1 << k_PositionScaleBits);
            return new float3(
                QuantiseFloat(posX) * k_PositionWorldScale,
                QuantiseFloat(posY) * k_PositionWorldScale,
                QuantiseFloat(posZ) * k_PositionWorldScale);
        }

        /// <summary>Decodes the sphere-mapped direction back to a unit float3.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 Direction()
        {
            float inv = 1f / 63f; // 6-bit → max value 63
            float x = ((float)dirX - 32f) * inv;
            float y = ((float)dirY - 32f) * inv;

            // Z is derived from the hemisphere constraint |dir| = 1, y ≥ 0.
            float xSq = x * x, ySq = y * y;
            float z = (xSq + ySq <= 1f) ? math.sqrt(1f - xSq - ySq) : 0f;

            return math.normalize(new float3(x, y, z));
        }

        /// <summary>Decoded action type.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActionType ActionTypeEnum() => (ActionType)actionType;

        // -- quantisation helpers ------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantisePosition(float v)
        {
            float scaled = math.clamp(v / k_PositionWorldScale, -1f, 1f);
            return (uint)((int)(scaled * (1 << k_PositionScaleBits)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int QuantiseFloat(uint encoded)
        {
            // Convert back to signed integer in [-256, +255] range for Q8.24.
            return (int)encoded - (1 << k_PositionScaleBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantiseDirectionX(float3 dir)
        {
            // Map [-1, +1] → [0, 63] for 6-bit encoding.
            return math.clamp((uint)((dir.x * 0.5f + 0.5f) * 63f), 0u, 63u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantiseDirectionY(float3 dir)
        {
            // Map [0, 1] (upper hemisphere) → [0, 63].
            return math.clamp((uint)(dir.y * 63f), 0u, 63u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantiseDirectionZ(float3 dir)
        {
            // Map [0, 1] (derived from hemisphere constraint) → [0, 63].
            return math.clamp((uint)((dir.z * 0.5f + 0.5f) * 63f), 0u, 63u);
        }

        // -- equality ------------------------------------------------------------

        public bool Equals(C_PlayerInput other) =>
            tick == other.tick && playerId == other.playerId && sequence == other.sequence &&
            posX == other.posX && posY == other.posY && posZ == other.posZ &&
            dirX == other.dirX && dirY == other.dirY && dirZ == other.dirZ &&
            actionType == other.actionType && toolMaterial == other.toolMaterial;

        public override bool Equals(object obj) => obj is C_PlayerInput o && Equals(o);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = tick.GetHashCode();
                h = (h * 397) ^ playerId.GetHashCode();
                h = (h * 397) ^ sequence.GetHashCode();
                h = (h * 397) ^ posX.GetHashCode();
                h = (h * 397) ^ posY.GetHashCode();
                h = (h * 397) ^ posZ.GetHashCode();
                h = (h * 397) ^ dirX.GetHashCode();
                h = (h * 397) ^ dirY.GetHashCode();
                h = (h * 397) ^ dirZ.GetHashCode();
                h = (h * 397) ^ actionType;
                h = (h * 397) ^ toolMaterial;
                return h;
            }
        }

        public static bool operator ==(C_PlayerInput a, C_PlayerInput b) => a.Equals(b);
        public static bool operator !=(C_PlayerInput a, C_PlayerInput b) => !a.Equals(b);

        // -- bounds checking ----------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"C_PlayerInput: dst too small ({dst.Length} < {required})");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"C_PlayerInput: src too small ({src.Length} < {required})");
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
    }
}
