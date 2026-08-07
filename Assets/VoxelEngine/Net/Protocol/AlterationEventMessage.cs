using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// S_AlterationEvent — server-to-client broadcast of an accepted alteration event.
    ///
    /// Contains the full encoded AlterationEvent as raw bytes so that the client can apply
    /// the identical deterministic expansion. The regionCoord tells the client which region
    /// to apply the event to without needing a separate lookup.
    ///
    /// Wire format (variable, minimum 37 bytes):
    ///   Offset  Size       Field
    ///   0       4          tick (uint)             — server tick of the event
    ///   4       12         regionCoord (int3)      — target region key
    ///   16      4          payloadLength (uint)     — length of eventBytes in bytes
    ///   20      payloadLen eventBytes (NativeSlice<byte>) — encoded AlterationEvent
    ///
    /// Total wire size = 20 + payloadLength, typically 20 + 32 = 52 bytes.
    /// </summary>
    public struct S_AlterationEvent : IEquatable<S_AlterationEvent>
    {
        /// <summary>Minimum wire size (header without payload). AlterationEvent.WireSize() = 32.</summary>
        public const int HeaderSize = 20;

        // -- fields ---------------------------------------------------------------

        /// <summary>Server tick when this event was authored.</summary>
        public uint tick;

        /// <summary>Coordinate of the target region in the world grid.</summary>
        public int3 regionCoord;

        /// <summary>Encoded AlterationEvent payload bytes (NativeSlice<byte> on the wire).</summary>
        public int payloadLength;

        // -- construction ---------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_AlterationEvent(uint tick, int3 regionCoord)
        {
            this.tick = tick;
            this.regionCoord = regionCoord;
            this.payloadLength = 0;
        }

        /// <summary>Encodes this message to the wire format with the given event bytes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst, ReadOnlySpan<byte> eventBytes)
        {
            int totalSize = HeaderSize + eventBytes.Length;
            ThrowIfTooSmall(dst, totalSize);

            // tick (4 bytes)
            WriteUint32(dst, 0, tick);

            // regionCoord (12 bytes)
            dst[4]  = (byte)regionCoord.x;
            dst[5]  = (byte)(regionCoord.x >> 8);
            dst[6]  = (byte)(regionCoord.x >> 16);
            dst[7]  = (byte)(regionCoord.x >> 24);
            dst[8]  = (byte)regionCoord.y;
            dst[9]  = (byte)(regionCoord.y >> 8);
            dst[10] = (byte)(regionCoord.y >> 16);
            dst[11] = (byte)(regionCoord.y >> 24);
            dst[12] = (byte)regionCoord.z;
            dst[13] = (byte)(regionCoord.z >> 8);
            dst[14] = (byte)(regionCoord.z >> 16);
            dst[15] = (byte)(regionCoord.z >> 24);

            // payloadLength (4 bytes)
            WriteUint32(dst, 16, (uint)eventBytes.Length);

            // eventBytes — copied as a raw span (NativeSlice<byte> on transport layer).
            if (eventBytes.Length > 0)
                eventBytes.CopyTo(dst.Slice(HeaderSize));
        }

        /// <summary>Decodes from wire format and returns the event bytes span.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReadOnlySpan is a ref struct and cannot be a tuple element, so the payload slice
        // comes back through an out parameter rather than a tuple return.
        public static S_AlterationEvent Decode(ReadOnlySpan<byte> src, out ReadOnlySpan<byte> eventBytes)
        {
            ThrowIfTooSmall(src, HeaderSize);

            S_AlterationEvent msg;
            msg.tick = ReadUint32(src, 0);

            msg.regionCoord = new int3(
                ReadInt32(src, 4),
                ReadInt32(src, 8),
                ReadInt32(src, 12));

            msg.payloadLength = (int)ReadUint32(src, 16);

            int totalSize = HeaderSize + msg.payloadLength;
            ThrowIfTooSmall(src, totalSize);

            eventBytes = src.Slice(HeaderSize, msg.payloadLength);
            return msg;
        }

        // -- equality ------------------------------------------------------------

        public bool Equals(S_AlterationEvent other) =>
            tick == other.tick && math.all(regionCoord == other.regionCoord) && payloadLength == other.payloadLength;
        public override bool Equals(object obj) => obj is S_AlterationEvent o && Equals(o);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = tick.GetHashCode();
                h = (h * 397) ^ regionCoord.GetHashCode();
                h = (h * 397) ^ payloadLength;
                return h;
            }
        }
        public static bool operator ==(S_AlterationEvent a, S_AlterationEvent b) => a.Equals(b);
        public static bool operator !=(S_AlterationEvent a, S_AlterationEvent b) => !a.Equals(b);

        // -- bounds checking ----------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"S_AlterationEvent: dst too small ({dst.Length} < {required})");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"S_AlterationEvent: src too small ({src.Length} < {required})");
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
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            (int)(uint)(src[offset] | (src[offset + 1] << 8) |
                         (src[offset + 2] << 16) | (src[offset + 3] << 24));
    }
}
