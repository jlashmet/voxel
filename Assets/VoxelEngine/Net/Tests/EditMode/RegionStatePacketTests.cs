using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RegionStatePacketTests
    {
        [Test]
        public void FullRegionRequestIsExactlyEighteenBytesAndRejectsTrailingData()
        {
            var request = new C_RegionRequest(new int3(-3, 7, 11), RegionRequestPacket.FullSemanticState);
            var packet = new byte[RegionRequestPacket.PacketSize];

            Assert.That(RegionRequestPacket.TryEncode(packet, in request), Is.True);
            Assert.That(packet.Length, Is.EqualTo(18));
            Assert.That(RegionRequestPacket.TryDecode(packet, out C_RegionRequest decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(request));

            var trailing = new byte[packet.Length + 1];
            packet.CopyTo(trailing, 0);
            Assert.That(RegionRequestPacket.TryDecode(trailing, out _), Is.False);
        }

        [Test]
        public void RegionStateChunkRoundTripsAndMaximumPacketIsSixteenKiB()
        {
            Assert.That(RegionStateChunkPacket.MaxPacketSize, Is.EqualTo(16 * 1024));
            Assert.That(RegionStateChunkPacket.MaxChunkBytes,
                Is.EqualTo(RegionStateChunkPacket.MaxPacketSize - RegionStateChunkPacket.HeaderSize));

            var chunk = new byte[RegionStateChunkPacket.MaxChunkBytes];
            for (int i = 0; i < chunk.Length; i++) chunk[i] = (byte)(i * 31);
            var packet = new byte[RegionStateChunkPacket.MaxPacketSize];

            Assert.That(RegionStateChunkPacket.TryEncode(
                packet,
                transferId: 77,
                regionCoord: new int3(4, -2, 9),
                snapshotTick: 1234,
                semanticHash: 0xAABBCCDD,
                totalLength: chunk.Length,
                offset: 0,
                chunk: chunk,
                bytesWritten: out int written), Is.True);
            Assert.That(written, Is.EqualTo(packet.Length));

            Assert.That(RegionStateChunkPacket.TryDecode(packet, out var header, out var decodedChunk), Is.True);
            Assert.That(header.TransferId, Is.EqualTo(77));
            Assert.That(header.RegionCoord, Is.EqualTo(new int3(4, -2, 9)));
            Assert.That(header.SnapshotTick, Is.EqualTo(1234));
            Assert.That(header.SemanticHash, Is.EqualTo(0xAABBCCDDu));
            Assert.That(header.IsFinal, Is.True);
            Assert.That(decodedChunk.SequenceEqual(chunk), Is.True);
        }

        [Test]
        public void RegionStateChunkRejectsHostileOversizedSnapshotLength()
        {
            var packet = new byte[RegionStateChunkPacket.HeaderSize + 1];
            Assert.That(ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_RegionData), Is.True);

            packet[2] = 1; // transferId=1
            uint hostileLength = (uint)RegionStateChunkPacket.MaxSnapshotBytes + 1u;
            packet[26] = (byte)hostileLength;
            packet[27] = (byte)(hostileLength >> 8);
            packet[28] = (byte)(hostileLength >> 16);
            packet[29] = (byte)(hostileLength >> 24);
            packet[34] = 1; // chunkLength=1

            Assert.That(RegionStateChunkPacket.TryDecode(packet, out _, out _), Is.False);
        }

        [Test]
        public void RegionStateFenceIsExactlyTwentyTwoBytesAndRoundTrips()
        {
            var fence = new S_RegionStateFence(19, new int3(1, 2, -3), 8080);
            var packet = new byte[RegionStateFencePacket.PacketSize];

            Assert.That(RegionStateFencePacket.TryEncode(packet, in fence), Is.True);
            Assert.That(packet.Length, Is.EqualTo(22));
            Assert.That(RegionStateFencePacket.TryDecode(packet, out S_RegionStateFence decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(fence));
        }
    }
}
