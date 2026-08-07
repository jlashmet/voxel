using System;
using System.Runtime.CompilerServices;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// S_AlterationRejected — server-to-client rejection of a submitted alteration.
    ///
    /// Tells the client why their event was not accepted and allows UI feedback (FR-009).
    ///
    /// Wire format (8 bytes):
    ///   Offset  Size  Field
    ///   0       4     tick (uint)              — server tick of rejection
    ///   4       2     playerId (ushort)         — rejected player
    ///   6       1     reason (byte)             — AlterationRejected.Reason discriminator
    ///   7       1     padding                   — alignment
    /// </summary>
    public struct S_AlterationRejected : IEquatable<S_AlterationRejected>
    {
        public const int WireSize = 8;

        /// <summary>Reason codes for alteration rejection (FR-018 to FR-021, FR-032).</summary>
        public enum Reason : byte
        {
            /// <summary>Player is altering faster than the allowed rate (rate budget exceeded).</summary>
            TooFast = 1,

            /// <summary>Player's total allocation exceeds their per-tick budget.</summary>
            OverBudget = 2,

            /// <summary>Target region density cap would be exceeded by this alteration.</summary>
            OverDensity = 3,

            /// <summary>Alteration origin is not attached to existing structure (FR-attachment).</summary>
            NotAttached = 4,

            /// <summary>Placement intersects an occupied player volume (FR-playerVolume).</summary>
            InPlayerVolume = 5,

            /// <summary>Target is out of the player's reach distance (FR-reach).</summary>
            OutOfReach = 6,

            /// <summary>Target falls within a protected zone (e.g., spawn area).</summary>
            ProtectedZone = 7,

            /// <summary>The target brick/material combination is invalid for this alteration.</summary>
            InvalidTarget = 8,
        }

        // -- fields ---------------------------------------------------------------

        /// <summary>Server tick when this rejection was authored.</summary>
        public uint tick;

        /// <summary>ID of the player whose alteration was rejected.</summary>
        public ushort playerId;

        /// <summary>Reason discriminator from the Reason enum.</summary>
        public byte reason;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_AlterationRejected(uint tick, ushort playerId, Reason reason)
        {
            this.tick = tick;
            this.playerId = playerId;
            this.reason = (byte)reason;
        }

        /// <summary>Decoded rejection reason.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Reason ReasonEnum() => (Reason)reason;

        // -- encoding ------------------------------------------------------------

        /// <summary>Encodes the rejection to wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize);

            WriteUint32(dst, 0, tick);

            dst[4] = (byte)(playerId >> 0);
            dst[5] = (byte)(playerId >> 8);

            dst[6] = reason;
            dst[7] = 0; // padding
        }

        /// <summary>Decodes from wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static S_AlterationRejected Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize);
            return new S_AlterationRejected(
                ReadUint32(src, 0),
                (ushort)(src[4] | (src[5] << 8)),
                (Reason)src[6]);
        }

        // -- equality ------------------------------------------------------------

        public bool Equals(S_AlterationRejected other) =>
            tick == other.tick && playerId == other.playerId && reason == other.reason;
        public override bool Equals(object obj) => obj is S_AlterationRejected o && Equals(o);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = tick.GetHashCode();
                h = (h * 397) ^ playerId.GetHashCode();
                h = (h * 397) ^ reason;
                return h;
            }
        }
        public static bool operator ==(S_AlterationRejected a, S_AlterationRejected b) => a.Equals(b);
        public static bool operator !=(S_AlterationRejected a, S_AlterationRejected b) => !a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"S_AlterationRejected: dst too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"S_AlterationRejected: src too small");
        }

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
