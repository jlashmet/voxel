using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// C_AlterationRequest — client-to-server message submitting a voxel alteration.
    ///
    /// Message payload (32 bytes):
    ///   Offset  Size  Field
    ///   0       4     tick (uint)
    ///   4       12    origin (int3)
    ///   16      1     eventKind (byte)
    ///   17      1     reserved
    ///   18      2     shapeRadius (ushort)
    ///   20      2     shapeExtentsYz (ushort)
    ///   22      1     material (byte)
    ///   23      1     reserved
    ///   24      4     seed (uint)
    ///   28      2     playerId (ushort)
    ///   30      2     sequence (ushort)
    ///
    /// With the 2-byte ProtocolEnvelope the complete custom packet is 34 bytes, matching the
    /// indicative size in contracts/wire-protocol.md without carrying meaningless tail padding.
    /// The server may replace the requested seed and assigns authoritative ordering on acceptance.
    /// </summary>
    public struct C_AlterationRequest : IEquatable<C_AlterationRequest>
    {
        /// <summary>Message-specific payload size, excluding ProtocolEnvelope.</summary>
        public const int WireSize = 32;

        public uint tick;
        public int3 origin;
        public byte eventKind;
        public ushort shapeRadius;
        public ushort shapeExtentsYz;
        public byte material;
        public uint seed;
        public ushort playerId;
        public ushort sequence;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public C_AlterationRequest(uint tick, int3 origin, byte eventKind, ushort shapeRadius,
            ushort shapeExtentsYz, byte material, uint seed, ushort playerId, ushort sequence)
        {
            this.tick = tick;
            this.origin = origin;
            this.eventKind = eventKind;
            this.shapeRadius = shapeRadius;
            this.shapeExtentsYz = shapeExtentsYz;
            this.material = material;
            this.seed = seed;
            this.playerId = playerId;
            this.sequence = sequence;
        }

        /// <summary>Encode only the message payload. Packet framing is ProtocolEnvelope's job.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize, nameof(dst));

            WriteUint32(dst, 0, tick);
            WriteInt32(dst, 4, origin.x);
            WriteInt32(dst, 8, origin.y);
            WriteInt32(dst, 12, origin.z);

            dst[16] = eventKind;
            dst[17] = 0;
            WriteUint16(dst, 18, shapeRadius);
            WriteUint16(dst, 20, shapeExtentsYz);
            dst[22] = material;
            dst[23] = 0;
            WriteUint32(dst, 24, seed);
            WriteUint16(dst, 28, playerId);
            WriteUint16(dst, 30, sequence);
        }

        /// <summary>Decode only the message payload.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static C_AlterationRequest Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize, nameof(src));

            return new C_AlterationRequest(
                ReadUint32(src, 0),
                new int3(ReadInt32(src, 4), ReadInt32(src, 8), ReadInt32(src, 12)),
                src[16],
                ReadUint16(src, 18),
                ReadUint16(src, 20),
                src[22],
                ReadUint32(src, 24),
                ReadUint16(src, 28),
                ReadUint16(src, 30));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 OriginAsInt3() => origin;

        public bool Equals(C_AlterationRequest other) =>
            tick == other.tick && math.all(origin == other.origin) && eventKind == other.eventKind &&
            shapeRadius == other.shapeRadius && shapeExtentsYz == other.shapeExtentsYz &&
            material == other.material && seed == other.seed &&
            playerId == other.playerId && sequence == other.sequence;

        public override bool Equals(object obj) => obj is C_AlterationRequest o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                var h = tick.GetHashCode();
                h = (h * 397) ^ origin.GetHashCode();
                h = (h * 397) ^ eventKind;
                h = (h * 397) ^ shapeRadius;
                h = (h * 397) ^ shapeExtentsYz;
                h = (h * 397) ^ material;
                h = (h * 397) ^ seed.GetHashCode();
                h = (h * 397) ^ playerId.GetHashCode();
                h = (h * 397) ^ sequence.GetHashCode();
                return h;
            }
        }

        public static bool operator ==(C_AlterationRequest a, C_AlterationRequest b) => a.Equals(b);
        public static bool operator !=(C_AlterationRequest a, C_AlterationRequest b) => !a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> span, int required, string paramName)
        {
            if (span.Length < required)
                throw new ArgumentException($"C_AlterationRequest requires {required} bytes; got {span.Length}.", paramName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> span, int required, string paramName)
        {
            if (span.Length < required)
                throw new ArgumentException($"C_AlterationRequest requires {required} bytes; got {span.Length}.", paramName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint16(Span<byte> dst, int offset, ushort value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32(Span<byte> dst, int offset, int value) =>
            WriteUint32(dst, offset, unchecked((uint)value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

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
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            unchecked((int)ReadUint32(src, offset));
    }
}
