using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// C_AlterationRequest — client-to-server request for a semantic world alteration.
    /// Payload is exactly 32 bytes; framed packet is 34 bytes.
    /// There is no player ID: identity is authoritative connection state.
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
        /// Compatibility constructor for the old radius/extents call shape. playerId is ignored.
        /// Brush callers are canonicalized to an axis-aligned cube with byte-sized full dimensions.
        /// New code should use the canonical constructor plus BrushShapeCodec directly.
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
                    if (ex >= 1 && ex <= byte.MaxValue && ey >= 1 && ez >= 1)
                    {
                        shapeKind = BrushShapeCodec.PackCube((byte)ex, (byte)ey, (byte)ez);
                        shapeData = 0;
                    }
                    else
                    {
                        // Deliberately invalid canonical brush; server validation fails closed.
                        shapeKind = 0;
                        shapeData = 0;
                    }
                    break;
                }

                default:
                    shapeKind = shapeRadius;
                    shapeData = shapeExtentsYz;
                    break;
            }
        }

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
            tick == other.tick && math.all(origin == other.origin) &&
            eventKind == other.eventKind && material == other.material &&
            shapeKind == other.shapeKind && shapeData == other.shapeData &&
            seed == other.seed && sequence == other.sequence;

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
