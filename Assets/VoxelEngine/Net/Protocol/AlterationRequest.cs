using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// C_AlterationRequest — client-to-server request for a semantic world alteration.
    ///
    /// Payload (32 bytes, no alignment padding):
    ///   Offset  Size  Field
    ///   0       4     clientTick (uint)
    ///   4       12    origin (int3)
    ///   16      1     eventKind (byte)
    ///   17      1     material (byte)
    ///   18      4     shapeKind (uint)
    ///   22      4     shapeData (uint)
    ///   26      4     requestedSeed (uint)
    ///   30      2     clientSequence (ushort)
    ///
    /// With ProtocolEnvelope the complete packet is 34 bytes.
    ///
    /// There is deliberately NO playerId on the wire. Player identity is authoritative connection
    /// state and must never be accepted from an untrusted client payload. The full 8-byte shape
    /// union matches AlterationEvent so request -> authoritative event conversion is lossless.
    /// </summary>
    public struct C_AlterationRequest : IEquatable<C_AlterationRequest>
    {
        public const int WireSize = 32;

        public uint tick;
        public int3 origin;
        public byte eventKind;
        public byte material;
        public uint shapeKind;
        public uint shapeData;
        public uint seed;
        public ushort sequence;

        /// <summary>Canonical constructor matching the actual wire fields.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public C_AlterationRequest(
            uint tick,
            int3 origin,
            byte eventKind,
            byte material,
            uint shapeKind,
            uint shapeData,
            uint seed,
            ushort sequence)
        {
            this.tick = tick;
            this.origin = origin;
            this.eventKind = eventKind;
            this.material = material;
            this.shapeKind = shapeKind;
            this.shapeData = shapeData;
            this.seed = seed;
            this.sequence = sequence;
        }

        /// <summary>
        /// Compatibility constructor for existing callers from the pre-envelope scaffold.
        /// The playerId argument is intentionally ignored: identity no longer belongs to the client
        /// wire message. New code should use the canonical constructor above.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public C_AlterationRequest(
            uint tick,
            int3 origin,
            byte eventKind,
            ushort shapeRadius,
            ushort shapeExtentsYz,
            byte material,
            uint seed,
            ushort playerId,
            ushort sequence)
        {
            this.tick = tick;
            this.origin = origin;
            this.eventKind = eventKind;
            this.material = material;
            this.seed = seed;
            this.sequence = sequence;

            switch (eventKind)
            {
                case AlterationEvent.KindExplosion:
                    shapeKind = eventKind;
                    shapeData = shapeRadius;
                    break;

                case AlterationEvent.KindBrush:
                {
                    uint ex = shapeRadius;
                    uint ey = (uint)((shapeExtentsYz >> 8) & 0xFF);
                    uint ez = (uint)(shapeExtentsYz & 0xFF);
                    shapeKind = ex | (ey << 16);
                    shapeData = ez;
                    break;
                }

                default:
                    // Preserve the two legacy 16-bit shape words without inventing semantics.
                    shapeKind = shapeRadius;
                    shapeData = shapeExtentsYz;
                    break;
            }
        }

        /// <summary>Encode only the 32-byte message payload.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize, nameof(dst));

            WriteUint32(dst, 0, tick);
            WriteInt32(dst, 4, origin.x);
            WriteInt32(dst, 8, origin.y);
            WriteInt32(dst, 12, origin.z);
            dst[16] = eventKind;
            dst[17] = material;
            WriteUint32(dst, 18, shapeKind);
            WriteUint32(dst, 22, shapeData);
            WriteUint32(dst, 26, seed);
            WriteUint16(dst, 30, sequence);
        }

        /// <summary>Decode only the 32-byte message payload.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static C_AlterationRequest Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize, nameof(src));

            return new C_AlterationRequest(
                ReadUint32(src, 0),
                new int3(ReadInt32(src, 4), ReadInt32(src, 8), ReadInt32(src, 12)),
                src[16],
                src[17],
                ReadUint32(src, 18),
                ReadUint32(src, 22),
                ReadUint32(src, 26),
                ReadUint16(src, 30));
        }

        /// <summary>
        /// Materialize the server-owned semantic event after authentication/validation has selected
        /// authoritative identity, tick, sequence, and seed. No shape re-packing is required.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AlterationEvent ToAuthoritativeEvent(
            uint authoritativeTick,
            ushort authoritativePlayerId,
            ushort authoritativeSequence,
            uint authoritativeSeed)
        {
            return new AlterationEvent
            {
                kind = eventKind,
                tick = authoritativeTick,
                origin = origin,
                shapeKind = shapeKind,
                shapeData = shapeData,
                material = material,
                seed = authoritativeSeed,
                playerId = authoritativePlayerId,
                sequence = authoritativeSequence,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 OriginAsInt3() => origin;

        public bool Equals(C_AlterationRequest other) =>
            tick == other.tick &&
            math.all(origin == other.origin) &&
            eventKind == other.eventKind &&
            material == other.material &&
            shapeKind == other.shapeKind &&
            shapeData == other.shapeData &&
            seed == other.seed &&
            sequence == other.sequence;

        public override bool Equals(object obj) => obj is C_AlterationRequest o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                var h = tick.GetHashCode();
                h = (h * 397) ^ origin.GetHashCode();
                h = (h * 397) ^ eventKind;
                h = (h * 397) ^ material;
                h = (h * 397) ^ shapeKind.GetHashCode();
                h = (h * 397) ^ shapeData.GetHashCode();
                h = (h * 397) ^ seed.GetHashCode();
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
