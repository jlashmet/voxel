using System;
using Unity.Mathematics;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>Reliable EVENT wrapper for a tick-scoped server region hash.</summary>
    public static class RegionHashPacket
    {
        public const int PacketSize = ProtocolEnvelope.HeaderSize + S_RegionHash.WireSize; // 22 B

        public static bool TryEncode(Span<byte> packet, in S_RegionHash hash)
        {
            if (packet.Length < PacketSize ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_RegionHash))
                return false;

            hash.Encode(packet.Slice(ProtocolEnvelope.HeaderSize, S_RegionHash.WireSize));
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out S_RegionHash hash)
        {
            hash = default;
            if (packet.Length != PacketSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.S_RegionHash)
                return false;

            hash = S_RegionHash.Decode(packet.Slice(payloadOffset, S_RegionHash.WireSize));
            return true;
        }
    }

    /// <summary>
    /// Client -> server report that the client compared a semantic region hash at exactly hashTick
    /// and got clientHash instead of the advertised serverHash. Identity comes from the connection.
    /// </summary>
    public struct C_RegionHashMismatch : IEquatable<C_RegionHashMismatch>
    {
        public const int WireSize = 24;

        public int3 regionCoord;
        public uint hashTick;
        public uint clientHash;
        public uint serverHash;

        public C_RegionHashMismatch(int3 regionCoord, uint hashTick, uint clientHash, uint serverHash)
        {
            this.regionCoord = regionCoord;
            this.hashTick = hashTick;
            this.clientHash = clientHash;
            this.serverHash = serverHash;
        }

        public void Encode(Span<byte> dst)
        {
            if (dst.Length < WireSize)
                throw new ArgumentException("C_RegionHashMismatch destination is too small.", nameof(dst));

            WriteInt32(dst, 0, regionCoord.x);
            WriteInt32(dst, 4, regionCoord.y);
            WriteInt32(dst, 8, regionCoord.z);
            WriteUint32(dst, 12, hashTick);
            WriteUint32(dst, 16, clientHash);
            WriteUint32(dst, 20, serverHash);
        }

        public static C_RegionHashMismatch Decode(ReadOnlySpan<byte> src)
        {
            if (src.Length < WireSize)
                throw new ArgumentException("C_RegionHashMismatch source is too small.", nameof(src));

            return new C_RegionHashMismatch(
                new int3(ReadInt32(src, 0), ReadInt32(src, 4), ReadInt32(src, 8)),
                ReadUint32(src, 12),
                ReadUint32(src, 16),
                ReadUint32(src, 20));
        }

        public bool Equals(C_RegionHashMismatch other) =>
            math.all(regionCoord == other.regionCoord) &&
            hashTick == other.hashTick && clientHash == other.clientHash && serverHash == other.serverHash;

        public override bool Equals(object obj) => obj is C_RegionHashMismatch other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = regionCoord.GetHashCode();
                hash = (hash * 397) ^ (int)hashTick;
                hash = (hash * 397) ^ (int)clientHash;
                hash = (hash * 397) ^ (int)serverHash;
                return hash;
            }
        }

        private static void WriteInt32(Span<byte> dst, int offset, int value) =>
            WriteUint32(dst, offset, unchecked((uint)value));
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }
        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] | ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) | ((uint)src[offset + 3] << 24);
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) => unchecked((int)ReadUint32(src, offset));
    }

    public static class RegionHashMismatchPacket
    {
        public const int PacketSize = ProtocolEnvelope.HeaderSize + C_RegionHashMismatch.WireSize; // 26 B

        public static bool TryEncode(Span<byte> packet, in C_RegionHashMismatch mismatch)
        {
            if (packet.Length < PacketSize ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.C_RegionHashMismatch))
                return false;

            mismatch.Encode(packet.Slice(ProtocolEnvelope.HeaderSize, C_RegionHashMismatch.WireSize));
            return true;
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out C_RegionHashMismatch mismatch)
        {
            mismatch = default;
            if (packet.Length != PacketSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.C_RegionHashMismatch)
                return false;

            mismatch = C_RegionHashMismatch.Decode(packet.Slice(payloadOffset, C_RegionHashMismatch.WireSize));
            return true;
        }
    }
}
