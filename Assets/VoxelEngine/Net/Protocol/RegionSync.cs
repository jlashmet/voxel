using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// S_RegionHash — server-to-client drift-detection hash for a region.
    ///
    /// Sends a checksum of the region's top mip level so clients can detect state divergence
    /// and request repair. Used continuously during gameplay, not just on join.
    ///
    /// Wire format (17 bytes):
    ///   Offset  Size  Field
    ///   0       12    regionCoord (int3)     — region key
    ///   12      4     mipHash (uint)          — FNV-1a hash of top mip level
    ///   16      1     padding                 — alignment
    /// </summary>
    public struct S_RegionHash : IEquatable<S_RegionHash>
    {
        public const int WireSize = 17;

        /// <summary>Coordinate of the region whose hash is being sent.</summary>
        public int3 regionCoord;

        /// <summary>FNV-1a hash over the top mip level's brick data.</summary>
        public uint mipHash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_RegionHash(int3 regionCoord, uint mipHash)
        {
            this.regionCoord = regionCoord;
            this.mipHash = mipHash;
        }

        /// <summary>Encodes the hash message to wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst)
        {
            ThrowIfTooSmall(dst, WireSize);

            // regionCoord (12 bytes)
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

            // mipHash (4 bytes, little-endian)
            WriteUint32(dst, 12, mipHash);

            // padding
            dst[16] = 0;
        }

        /// <summary>Decodes from wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static S_RegionHash Decode(ReadOnlySpan<byte> src)
        {
            ThrowIfTooSmall(src, WireSize);
            return new S_RegionHash(
                new int3(ReadInt32(src, 0), ReadInt32(src, 4), ReadInt32(src, 8)),
                ReadUint32(src, 12));
        }

        public bool Equals(S_RegionHash other) =>
            math.all(regionCoord == other.regionCoord) && mipHash == other.mipHash;
        public override bool Equals(object obj) => obj is S_RegionHash o && Equals(o);
        public override int GetHashCode()
        {
            unchecked { return (regionCoord.GetHashCode() * 397) ^ (int)mipHash; }
        }
        public static bool operator ==(S_RegionHash a, S_RegionHash b) => a.Equals(b);
        public static bool operator !=(S_RegionHash a, S_RegionHash b) => !a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"S_RegionHash: dst too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"S_RegionHash: src too small");
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            (int)(uint)(src[offset] | (src[offset + 1] << 8) |
                         (src[offset + 2] << 16) | (src[offset + 3] << 24));
    }

    /// <summary>
    /// S_RegionRepair — server-to-client repair payload for a drifted region.
    ///
    /// Sent when a hash mismatch is detected (S_RegionHash). Contains the starting tick
    /// for replay and raw brick data to bring the client up to date.
    ///
    /// Wire format (variable):
    ///   Offset      Size         Field
    ///   0           12           regionCoord (int3)     — target region
    ///   12          4            repairStartTick (uint)  — tick from which to replay edits
    ///   16          4            dataLength (uint)        — bytes in data payload
    ///   20          dataLength   dataBytes                — raw brick/brick-pool indices
    /// </summary>
    public struct S_RegionRepair : IEquatable<S_RegionRepair>
    {
        /// <summary>Header size in bytes (no payload).</summary>
        public const int HeaderSize = 20;

        /// <summary>Coordinate of the region to repair.</summary>
        public int3 regionCoord;

        /// <summary>Server tick from which the repair data begins. Edits before this tick
        /// are already baked into the client's local snapshot.</summary>
        public uint repairStartTick;

        /// <summary>Length of the dataBytes payload.</summary>
        public int dataLength;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_RegionRepair(int3 regionCoord, uint repairStartTick)
        {
            this.regionCoord = regionCoord;
            this.repairStartTick = repairStartTick;
            this.dataLength = 0;
        }

        /// <summary>Encodes the repair message with the given data payload.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> dst, ReadOnlySpan<byte> dataBytes)
        {
            int totalSize = HeaderSize + dataBytes.Length;
            ThrowIfTooSmall(dst, totalSize);

            // regionCoord (12 bytes)
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

            // repairStartTick (4 bytes)
            WriteUint32(dst, 12, repairStartTick);

            // dataLength (4 bytes)
            WriteUint32(dst, 16, (uint)dataBytes.Length);

            // dataBytes payload
            if (dataBytes.Length > 0)
                dataBytes.CopyTo(dst.Slice(HeaderSize));
        }

        /// <summary>Decodes from wire format.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReadOnlySpan is a ref struct and cannot be a tuple element, so the payload slice
        // comes back through an out parameter rather than a tuple return.
        public static S_RegionRepair Decode(ReadOnlySpan<byte> src, out ReadOnlySpan<byte> dataBytes)
        {
            ThrowIfTooSmall(src, HeaderSize);

            S_RegionRepair msg;
            msg.regionCoord = new int3(
                ReadInt32(src, 0), ReadInt32(src, 4), ReadInt32(src, 8));
            msg.repairStartTick = ReadUint32(src, 12);
            msg.dataLength = (int)ReadUint32(src, 16);

            int totalSize = HeaderSize + msg.dataLength;
            ThrowIfTooSmall(src, totalSize);

            dataBytes = src.Slice(HeaderSize, msg.dataLength);
            return msg;
        }

        public bool Equals(S_RegionRepair other) =>
            math.all(regionCoord == other.regionCoord) && repairStartTick == other.repairStartTick &&
            dataLength == other.dataLength;
        public override bool Equals(object obj) => obj is S_RegionRepair o && Equals(o);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = regionCoord.GetHashCode();
                h = (h * 397) ^ repairStartTick.GetHashCode();
                h = (h * 397) ^ dataLength;
                return h;
            }
        }
        public static bool operator ==(S_RegionRepair a, S_RegionRepair b) => a.Equals(b);
        public static bool operator !=(S_RegionRepair a, S_RegionRepair b) => !a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(Span<byte> dst, int required)
        {
            if (dst.Length < required) UnityEngine.Debug.LogError($"S_RegionRepair: dst too small");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfTooSmall(ReadOnlySpan<byte> src, int required)
        {
            if (src.Length < required) UnityEngine.Debug.LogError($"S_RegionRepair: src too small");
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            (int)(uint)(src[offset] | (src[offset + 1] << 8) |
                         (src[offset + 2] << 16) | (src[offset + 3] << 24));
    }
}
