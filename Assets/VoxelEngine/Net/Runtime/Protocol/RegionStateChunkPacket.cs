using System;
using Unity.Mathematics;

namespace VoxelEngine.Net.Runtime.Protocol
{
    /// <summary>
    /// One reliable fragmented BULK packet carrying a slice of a semantic full-region snapshot.
    /// Pool indices never cross the wire. The 36-byte header identifies one transfer and the exact
    /// authoritative tick/hash represented by the snapshot.
    /// </summary>
    public static class RegionStateChunkPacket
    {
        public const int HeaderSize = 36;
        public const int MaxPacketSize = 16 * 1024;
        public const int MaxChunkBytes = MaxPacketSize - HeaderSize;
        public const int MaxSnapshotBytes = 16 * 1024 * 1024;

        public static bool TryEncode(
            Span<byte> packet,
            uint transferId,
            int3 regionCoord,
            uint snapshotTick,
            uint semanticHash,
            int totalLength,
            int offset,
            ReadOnlySpan<byte> chunk,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (transferId == 0 || totalLength <= 0 || totalLength > MaxSnapshotBytes ||
                offset < 0 || chunk.Length <= 0 || chunk.Length > MaxChunkBytes ||
                offset > totalLength || chunk.Length > totalLength - offset ||
                packet.Length < HeaderSize + chunk.Length ||
                !ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_RegionData))
                return false;

            WriteUint32(packet, 2, transferId);
            WriteInt32(packet, 6, regionCoord.x);
            WriteInt32(packet, 10, regionCoord.y);
            WriteInt32(packet, 14, regionCoord.z);
            WriteUint32(packet, 18, snapshotTick);
            WriteUint32(packet, 22, semanticHash);
            WriteUint32(packet, 26, (uint)totalLength);
            WriteUint32(packet, 30, (uint)offset);
            WriteUint16(packet, 34, (ushort)chunk.Length);
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
            if (packet.Length < HeaderSize || packet.Length > MaxPacketSize ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out _) ||
                kind != ProtocolMessageKind.S_RegionData)
                return false;

            uint transferId = ReadUint32(packet, 2);
            uint totalRaw = ReadUint32(packet, 26);
            uint offsetRaw = ReadUint32(packet, 30);
            int chunkLength = ReadUint16(packet, 34);

            if (transferId == 0 || totalRaw == 0 || totalRaw > MaxSnapshotBytes ||
                offsetRaw > totalRaw || totalRaw > int.MaxValue || offsetRaw > int.MaxValue ||
                chunkLength <= 0 || chunkLength > MaxChunkBytes)
                return false;

            int totalLength = (int)totalRaw;
            int offset = (int)offsetRaw;
            if (chunkLength > totalLength - offset || packet.Length != HeaderSize + chunkLength)
                return false;

            header = new Header(
                transferId,
                new int3(ReadInt32(packet, 6), ReadInt32(packet, 10), ReadInt32(packet, 14)),
                ReadUint32(packet, 18),
                ReadUint32(packet, 22),
                totalLength,
                offset,
                chunkLength);
            chunk = packet.Slice(HeaderSize, chunkLength);
            return true;
        }

        public readonly struct Header
        {
            public readonly uint TransferId;
            public readonly int3 RegionCoord;
            public readonly uint SnapshotTick;
            public readonly uint SemanticHash;
            public readonly int TotalLength;
            public readonly int Offset;
            public readonly int ChunkLength;

            public Header(
                uint transferId,
                int3 regionCoord,
                uint snapshotTick,
                uint semanticHash,
                int totalLength,
                int offset,
                int chunkLength)
            {
                TransferId = transferId;
                RegionCoord = regionCoord;
                SnapshotTick = snapshotTick;
                SemanticHash = semanticHash;
                TotalLength = totalLength;
                Offset = offset;
                ChunkLength = chunkLength;
            }

            public bool IsFinal => Offset + ChunkLength == TotalLength;
        }

        private static void WriteInt32(Span<byte> dst, int offset, int value) =>
            WriteUint32(dst, offset, unchecked((uint)value));

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

        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            unchecked((int)ReadUint32(src, offset));
    }
}
