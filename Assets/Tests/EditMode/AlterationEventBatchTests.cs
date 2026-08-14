using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class AlterationEventBatchTests
    {
        private static AlterationEvent Explosion(uint tick, int3 origin, ushort sequence) =>
            new AlterationEvent(
                AlterationEvent.KindExplosion,
                tick,
                origin,
                12,
                0,
                0x12345678u + sequence,
                7,
                sequence);

        [Test]
        [Category("Bandwidth")]
        public void RoundTripPreservesEverySemanticField()
        {
            const uint tick = 9001u;
            var region = new int3(3, -1, 8);
            int3 regionOrigin = region << VoxelDimensions.RegionVoxelEdgeLog2;

            var explosion = Explosion(tick, regionOrigin + new int3(10, 20, 30), 1);

            var brush = new AlterationEvent(
                AlterationEvent.KindBrush,
                tick,
                regionOrigin + new int3(511, -8, 256),
                1,
                19,
                0xCAFEBABEu,
                8,
                2);
            brush.shapeKind = 4u | (5u << 16);
            brush.shapeData = 6u | (0xABCDu << 16);

            var raw = new AlterationEvent(
                AlterationEvent.KindRawBatch,
                tick,
                regionOrigin + new int3(-200, 100, 700),
                1,
                3,
                0xDEADBEEFu,
                9,
                3);
            raw.shapeKind = 0x11223344u;
            raw.shapeData = 0x55667788u;

            var events = new[] { explosion, brush, raw };
            var wire = new byte[S_AlterationEventBatch.EncodedSize(events.Length)];

            Assert.IsTrue(S_AlterationEventBatch.TryEncode(wire, region, tick, events, out int bytesWritten));
            Assert.AreEqual(wire.Length, bytesWritten);
            Assert.IsTrue(S_AlterationEventBatch.TryDecodeHeader(wire, out var batch));
            Assert.AreEqual(region, batch.regionCoord);
            Assert.AreEqual(tick, batch.tick);
            Assert.AreEqual((ushort)events.Length, batch.count);

            for (int i = 0; i < events.Length; i++)
            {
                Assert.IsTrue(S_AlterationEventBatch.TryDecodeEvent(wire, in batch, i, out var decoded));
                Assert.AreEqual(events[i], decoded, $"Event {i} changed during compact batch round-trip.");
            }
        }

        [Test]
        [Category("Bandwidth")]
        public void TenEventBurstUsesAboutHalfTheCurrentBroadcastBytes()
        {
            const int count = 10;
            int currentBytes = count * (S_AlterationEvent.HeaderSize + AlterationEvent.WireSize());
            int batchedBytes = S_AlterationEventBatch.EncodedSize(count);

            Assert.AreEqual(520, currentBytes);
            Assert.AreEqual(258, batchedBytes);
            Assert.Less(batchedBytes, currentBytes * 0.55f,
                $"Expected structural batching to save about half the payload: old={currentBytes}, batch={batchedBytes}.");
        }

        [Test]
        public void RejectsMixedTicksInsteadOfEncodingLossily()
        {
            var region = int3.zero;
            var events = new[]
            {
                Explosion(100u, new int3(10, 10, 10), 1),
                Explosion(101u, new int3(20, 20, 20), 2),
            };
            var wire = new byte[S_AlterationEventBatch.EncodedSize(events.Length)];

            Assert.IsFalse(S_AlterationEventBatch.TryEncode(wire, region, 100u, events, out int bytesWritten));
            Assert.AreEqual(0, bytesWritten);
        }

        [Test]
        public void RejectsOriginThatCannotFitSignedRegionRelativeCoordinate()
        {
            var events = new[]
            {
                Explosion(100u, new int3(40000, 0, 0), 1),
            };
            var wire = new byte[S_AlterationEventBatch.EncodedSize(events.Length)];

            Assert.IsFalse(S_AlterationEventBatch.TryEncode(wire, int3.zero, 100u, events, out int bytesWritten));
            Assert.AreEqual(0, bytesWritten);
        }

        [Test]
        public void MaximumBatchStaysBelowTwelveHundredBytes()
        {
            Assert.AreEqual(1170, S_AlterationEventBatch.MaxWireSize);
            Assert.Less(S_AlterationEventBatch.MaxWireSize, 1200);
        }

        [Test]
        public void DecodeRejectsTruncatedPacket()
        {
            var events = new[]
            {
                Explosion(100u, new int3(1, 2, 3), 1),
                Explosion(100u, new int3(4, 5, 6), 2),
            };
            var full = new byte[S_AlterationEventBatch.EncodedSize(events.Length)];
            Assert.IsTrue(S_AlterationEventBatch.TryEncode(full, int3.zero, 100u, events, out _));

            var truncated = new byte[full.Length - 1];
            for (int i = 0; i < truncated.Length; i++)
                truncated[i] = full[i];

            Assert.IsFalse(S_AlterationEventBatch.TryDecodeHeader(truncated, out _));
        }
    }
}
