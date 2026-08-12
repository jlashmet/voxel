using System;
using Unity.Mathematics;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// One reliable REPAIR chunk from a semantic region checkpoint snapshot.
    /// Header is 32 bytes including ProtocolEnvelope; chunk payload is at most 992 bytes when the
    /// configured REPAIR packet ceiling is 1024 bytes.
    /// </summary>
    public static class RegionRepairChunkPacket
    {
        public const int HeaderSize = 32;
        public const int MaxPacketSize = 1024;
        public const int MaxChunkBytes = MaxPacketSize - HeaderSize;

        public static bool TryEncode(
            Span<byte> packet,
            int3 regionCoord,
            uint snapshotTick,
            uint semanticHash,
            int totalLength,
            int offset,
            ReadOnlySpan<byte> chunk,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (totalLength <= 0 || offset < 0 || chunk.Length <= 0 || chunk.Length > MaxChunkBytes ||
                offset > totalLength || offset + chunk.Length > totalLength ||
                packet.Length < HeaderSize + chunk.Length ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_RegionRepair))
                return false;

            WriteInt32(packet, 2, regionCoord.x);
            WriteInt32(packet, 6, regionCoord.y);
            WriteInt32(packet, 10, regionCoord.z);
            WriteUint32(packet, 14, snapshotTick);
            WriteUint32(packet, 18, semanticHash);
            WriteUint32(packet, 22, checked((uint)totalLength));
            WriteUint32(packet, 26, checked((uint)offset));
            WriteUint16(packet, 30, checked((ushort)chunk.Length));
            chunk.CopyTo(packet.Slice(HeaderSize, chunk.Length));
            bytesWritten = HeaderSize + chunk.Length;
            return true;
        }

        public static bool TryDecode(
            ReadOnlySpan<byte> packet,
            out Header header,
            out ReadOnlySpan<byte> chunk)
        {
            header = default;
            chunk = default;
            if (packet.Length < HeaderSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out _) ||
                kind != ProtocolMessageKind.S_RegionRepair)
                return false;

            int totalLength = checked((int)ReadUint32(packet, 22));
            int offset = checked((int)ReadUint32(packet, 26));
            int chunkLength = ReadUint16(packet, 30);
            if (totalLength <= 0 || offset < 0 || chunkLength <= 0 || chunkLength > MaxChunkBytes ||
                offset > totalLength || offset + chunkLength > totalLength ||
                packet.Length != HeaderSize + chunkLength)
                return false;

            header = new Header(
                new int3(ReadInt32(packet, 2), ReadInt32(packet, 6), ReadInt32(packet, 10)),
                ReadUint32(packet, 14),
                ReadUint32(packet, 18),
                totalLength,
                offset,
                chunkLength);
            chunk = packet.Slice(HeaderSize, chunkLength);
            return true;
        }

        public readonly struct Header
        {
            public readonly int3 RegionCoord;
            public readonly uint SnapshotTick;
            public readonly uint SemanticHash;
            public readonly int TotalLength;
            public readonly int Offset;
            public readonly int ChunkLength;

            public Header(int3 regionCoord, uint snapshotTick, uint semanticHash, int totalLength, int offset, int chunkLength)
            {
                RegionCoord = regionCoord;
                SnapshotTick = snapshotTick;
                SemanticHash = semanticHash;
                TotalLength = totalLength;
                Offset = offset;
                ChunkLength = chunkLength;
            }

            public bool IsFinal => Offset + ChunkLength == TotalLength;
        }

        private static void WriteInt32(Span<byte> dst, int offset, int value) => WriteUint32(dst, offset, unchecked((uint)value));
        private static void WriteUint16(Span<byte> dst, int offset, ushort value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
        }
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }
        private static ushort ReadUint16(ReadOnlySpan<byte> src, int offset) =>
            (ushort)(src[offset] | (src[offset + 1] << 8));
        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] | ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) | ((uint)src[offset + 3] << 24);
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) => unchecked((int)ReadUint32(src, offset));
    }
}
