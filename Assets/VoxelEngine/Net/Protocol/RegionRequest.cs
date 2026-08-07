using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// C_RegionRequest — client-to-server message requesting a region's state.
    /// Used during initial join and mid-session refinement of loaded regions.
    ///
    /// Wire format (16 bytes):
    ///   Offset  Size  Field
    ///   0       12    regionCoord (int3)     — region key in the world grid
    ///   12      1     haveMipLevel (byte)    — refinement: client already has this mip level
    ///   13      3     padding                  — alignment
    /// </summary>
    public struct C_RegionRequest : IEquatable<C_RegionRequest>
    {
        public const int WireSize = 16;

        /// <summary>Coordinate of the requested region in the world grid.</summary>
        public int3 regionCoord;

        /// <summary>
        /// Refinement field — the mip level the client already has locally.
        /// Server uses this to send only data above this level (progressive loading).
        /// </summary>
        public byte haveMipLevel;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public C_RegionRequest(int3 regionCoord, byte haveMipLevel)
        {
            this.regionCoord = regionCoord;
            this.haveMipLevel = haveMipLevel;
        }

        /// <summary>Encodes the request to wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize);

            dst[0]  = (byte)regionCoord.x;
            dst[1]  = (byte)(regionCoord.x >> 8);
            dst[2]  = (byte)(regionCoord.x >> 16);
            dst[3]  = (byte)(regionCoord.x >> 24);
            dst[4]  = (byte)regionCoord.y;
            dst[5]  = (byte)(regionCoord.y >> 8);
            dst[6]  = (byte)(regionCoord.y >> 16);
            dst[7]  = (byte)(regionCoord.y >> 24);
            dst[8]  = (byte)regionCoord.z;
            dst[9]  = (byte)(regionCoord.z >> 8);
            dst[10] = (byte)(regionCoord.z >> 16);
            dst[11] = (byte)(regionCoord.z >> 24);
            dst[12] = haveMipLevel;
            dst[13] = 0;
            dst[14] = 0;
            dst[15] = 0;
        }

        /// <summary>Decodes from wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static C_RegionRequest Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize);
            return new C_RegionRequest(
                new int3(ReadInt32(src, 0), ReadInt32(src, 4), ReadInt32(src, 8)),
                src[12]);
        }

        public bool Equals(C_RegionRequest other) =>
            math.all(regionCoord == other.regionCoord) && haveMipLevel == other.haveMipLevel;
        public override bool Equals(object obj) => obj is C_RegionRequest o && Equals(o);
        public override int GetHashCode()
        {
            unchecked { return (regionCoord.GetHashCode() * 397) ^ haveMipLevel; }
        }
        public static bool operator ==(C_RegionRequest a, C_RegionRequest b) => a.Equals(b);
        public static bool operator !=(C_RegionRequest a, C_RegionRequest b) => !a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"C_RegionRequest: src too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"C_RegionRequest: dst too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            (int)(uint)(src[offset] | (src[offset + 1] << 8) |
                         (src[offset + 2] << 16) | (src[offset + 3] << 24));
    }

    /// <summary>
    /// S_RegionResponse — server-to-client response to a region request.
    ///
    /// Wire format (5 bytes):
    ///   Offset  Size  Field
    ///   0       1     hasRegion (bool as byte) — 1 if region exists, 0 otherwise
    ///   1       1     mipLevel (byte)           — highest available mip level
    ///   2       3     padding                    — alignment
    /// </summary>
    public struct S_RegionResponse : IEquatable<S_RegionResponse>
    {
        public const int WireSize = 5;

        /// <summary>True if the server has data for the requested region.</summary>
        public bool hasRegion;

        /// <summary>Highest mip level available on the server for this region.</summary>
        public byte mipLevel;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_RegionResponse(bool hasRegion, byte mipLevel)
        {
            this.hasRegion = hasRegion;
            this.mipLevel = mipLevel;
        }

        /// <summary>Encodes the response to wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize);
            dst[0] = hasRegion ? (byte)1 : (byte)0;
            dst[1] = mipLevel;
            dst[2] = 0;
            dst[3] = 0;
            dst[4] = 0;
        }

        /// <summary>Decodes from wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static S_RegionResponse Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize);
            return new S_RegionResponse(src[0] != 0, src[1]);
        }

        public bool Equals(S_RegionResponse other) =>
            hasRegion == other.hasRegion && mipLevel == other.mipLevel;
        public override bool Equals(object obj) => obj is S_RegionResponse o && Equals(o);
        public override int GetHashCode()
        {
            unchecked { return (hasRegion.GetHashCode() * 397) ^ mipLevel; }
        }
        public static bool operator ==(S_RegionResponse a, S_RegionResponse b) => a.Equals(b);
        public static bool operator !=(S_RegionResponse a, S_RegionResponse b) => !a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"S_RegionResponse: dst too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"S_RegionResponse: src too small");
        }
    }
}
