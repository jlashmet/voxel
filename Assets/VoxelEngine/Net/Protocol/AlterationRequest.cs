using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// C_AlterationRequest — client-to-server message submitting a voxel alteration.
    ///
    /// Wire format (32 bytes, matching AlterationEvent data-model.md layout):
    ///   Offset  Size  Field
    ///   0       4     tick (uint)          — server tick for ordering
    ///   4       12    origin (int3)        — voxel coordinate of effect center
    ///   16      1     eventKind (byte)     — AlterationEvent.Kind* discriminator
    ///   17      1     padding              — alignment to uint boundary
    ///   18      4     shapeInfo (uint pair encoded in two fields below)
    ///                -- shapeRadius (ushort) at offset 18
    ///                -- shapeDataPadding (ushort) at offset 20
    ///   20      4     materialSeed field (material byte + seed uint packed)
    ///   24      1     material (byte)       — placement material index
    ///   25      3     padding                — alignment
    ///   26      4     seed (uint)           — deterministic expansion seed
    ///   30      2     playerId (ushort)     — submitting player ID
    ///   32      2     sequence (ushort)     — client ordinal for arbitration
    ///
    /// Note: This message is exactly 34 bytes on the wire; padding ensures alignment.
    /// The server assigns the authoritative sequence number before broadcast.
    ///
    /// See data-model.md §AlterationEvent for field semantics.
    /// </summary>
    public struct C_AlterationRequest : IEquatable<C_AlterationRequest>
    {
        // -- constants -------------------------------------------------------------

        /// <summary>Wire size in bytes for C_AlterationRequest.</summary>
        public const int WireSize = 34;

        // -- fields ---------------------------------------------------------------

        /// <summary>Server tick this alteration targets. Used for ordering events.</summary>
        public uint tick;

        /// <summary>Voxel coordinate of the effect origin/center.</summary>
        public int3 origin;

        /// <summary>Kind of alteration: 1=Explosion, 2=Brush, 3=RawBatch (AlterationEvent.Kind*).</summary>
        public byte eventKind;

        /// <summary>Shape-specific data. For Explosion: radius in bricks.
        /// For Brush: packed extents.x (ushort) at bits 0-15.</summary>
        public ushort shapeRadius;

        /// <summary>Additional shape data. For Brush: packed extents.yz at bits 0-31.</summary>
        public ushort shapeExtentsYz;

        /// <summary>Placement material index into the session's material palette.</summary>
        public byte material;

        /// <summary>Deterministic PRNG seed for voxel expansion.</summary>
        public uint seed;

        /// <summary>ID of the player submitting this alteration (FR-020 attribution).</summary>
        public ushort playerId;

        /// <summary>Client-assigned ordinal within their submission stream.
        /// The server assigns the authoritative sequence before broadcast.</summary>
        public ushort sequence;

        // -- construction ---------------------------------------------------------

        /// <summary>Construct a C_AlterationRequest with all fields explicitly set.</summary>
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

        // -- encoding ------------------------------------------------------------

        /// <summary>Encodes this message to the wire format defined in the struct docs.</summary>
        /// <param name="dst">Destination buffer — must be at least WireSize bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize);

            // tick (4 bytes, little-endian)
            dst[0]  = (byte)(tick >> 0);
            dst[1]  = (byte)(tick >> 8);
            dst[2]  = (byte)(tick >> 16);
            dst[3]  = (byte)(tick >> 24);

            // origin int3 (12 bytes, little-endian)
            dst[4]  = (byte)origin.x;
            dst[5]  = (byte)(origin.x >> 8);
            dst[6]  = (byte)(origin.x >> 16);
            dst[7]  = (byte)(origin.x >> 24);
            dst[8]  = (byte)origin.y;
            dst[9]  = (byte)(origin.y >> 8);
            dst[10] = (byte)(origin.y >> 16);
            dst[11] = (byte)(origin.y >> 24);
            dst[12] = (byte)origin.z;
            dst[13] = (byte)(origin.z >> 8);
            dst[14] = (byte)(origin.z >> 16);
            dst[15] = (byte)(origin.z >> 24);

            // eventKind + padding (2 bytes → aligned to uint boundary)
            dst[16] = eventKind;
            dst[17] = 0;

            // shapeRadius (2 bytes)
            dst[18] = (byte)(shapeRadius >> 0);
            dst[19] = (byte)(shapeRadius >> 8);

            // shapeExtentsYz (2 bytes)
            dst[20] = (byte)(shapeExtentsYz >> 0);
            dst[21] = (byte)(shapeExtentsYz >> 8);

            // material (1 byte) + padding to align seed
            dst[22] = material;
            dst[23] = 0;

            // seed (4 bytes, little-endian)
            dst[24] = (byte)(seed >> 0);
            dst[25] = (byte)(seed >> 8);
            dst[26] = (byte)(seed >> 16);
            dst[27] = (byte)(seed >> 24);

            // playerId (2 bytes)
            dst[28] = (byte)(playerId >> 0);
            dst[29] = (byte)(playerId >> 8);

            // sequence (2 bytes)
            dst[30] = (byte)(sequence >> 0);
            dst[31] = (byte)(sequence >> 8);
        }

        /// <summary>Decodes a C_AlterationRequest from the wire format.</summary>
        /// <param name="src">Source buffer — must be at least WireSize bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static C_AlterationRequest Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize);

            uint tick = ReadUint32(src, 0);
            int3 origin = new int3(
                ReadInt32(src, 4),
                ReadInt32(src, 8),
                ReadInt32(src, 12));
            byte eventKind = src[16];
            ushort shapeRadius = (ushort)(src[18] | (src[19] << 8));
            ushort shapeExtentsYz = (ushort)(src[20] | (src[21] << 8));
            byte material = src[22];
            uint seed = ReadUint32(src, 24);
            ushort playerId = (ushort)(src[28] | (src[29] << 8));
            ushort sequence = (ushort)(src[30] | (src[31] << 8));

            return new C_AlterationRequest(tick, origin, eventKind, shapeRadius,
                shapeExtentsYz, material, seed, playerId, sequence);
        }

        // -- helpers -------------------------------------------------------------

        /// <summary>Converts this client message into an AlterationEvent for server processing.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Unity.Mathematics.int3 OriginAsInt3() => origin;

        // -- equality ------------------------------------------------------------

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

        // -- bounds checking ----------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"C_AlterationRequest: dst too small ({dst.Length} < {required})");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"C_AlterationRequest: src too small ({src.Length} < {required})");
        }

        // -- primitive readers/writers ------------------------------------------

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
