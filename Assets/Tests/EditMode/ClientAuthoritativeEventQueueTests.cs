using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ClientAuthoritativeEventQueueTests
    {
        [Test]
        public void MissingNeighborRegionDefersBatchWithoutConsumingAuthority()
        {
            var queue = new ClientAuthoritativeEventQueue(new DeterministicAlterationApplier());
            var table = new RegionTable(2, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            try
            {
                table.LoadRegion(new int3(0, 0, 0));

                AlterationEvent evt = Explosion(
                    tick: 10,
                    playerId: 1,
                    sequence: 1,
                    origin: new int3(510, 256, 256));
                byte[] packet = EncodeBatch(new int3(0, 0, 0), evt);

                Assert.That(queue.TryEnqueueEventPacket(packet), Is.True);
                Assert.That(queue.PendingEventCount, Is.EqualTo(1));
                Assert.That(queue.DrainReady(new RegionMutationStore(in table, in pool), new RegionReadSource(in table, in pool), out int appliedBefore), Is.Zero);
                Assert.That(appliedBefore, Is.Zero);
                Assert.That(queue.PendingEventCount, Is.EqualTo(1));

                table.LoadRegion(new int3(1, 0, 0));
                Assert.That(queue.DrainReady(new RegionMutationStore(in table, in pool), new RegionReadSource(in table, in pool), out int appliedAfter), Is.EqualTo(1));
                Assert.That(appliedAfter, Is.EqualTo(1));
                Assert.That(queue.PendingEventCount, Is.Zero);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void LaterReadyBatchCannotLeapfrogDeferredAuthority()
        {
            var queue = new ClientAuthoritativeEventQueue(new DeterministicAlterationApplier());
            var table = new RegionTable(2, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            try
            {
                table.LoadRegion(int3.zero);

                AlterationEvent deferred = Explosion(
                    20,
                    1,
                    1,
                    new int3(510, 256, 256));
                AlterationEvent ready = Explosion(
                    21,
                    1,
                    1,
                    new int3(256, 256, 256));

                Assert.That(queue.TryEnqueueEventPacket(EncodeBatch(int3.zero, deferred)), Is.True);
                Assert.That(queue.TryEnqueueEventPacket(EncodeBatch(int3.zero, ready)), Is.True);
                Assert.That(queue.PendingBatchCount, Is.EqualTo(2));

                Assert.That(queue.DrainReady(new RegionMutationStore(in table, in pool), new RegionReadSource(in table, in pool), out int applied), Is.Zero);
                Assert.That(applied, Is.Zero);
                Assert.That(queue.PendingBatchCount, Is.EqualTo(2));

                table.LoadRegion(new int3(1, 0, 0));
                Assert.That(queue.DrainReady(new RegionMutationStore(in table, in pool), new RegionReadSource(in table, in pool), out applied), Is.EqualTo(2));
                Assert.That(applied, Is.EqualTo(2));
                Assert.That(queue.PendingBatchCount, Is.Zero);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void RejectionNotifiesImmediatelyAndDoesNotEnterWorldQueue()
        {
            var queue = new ClientAuthoritativeEventQueue(new DeterministicAlterationApplier());
            var notifications = new RecordingNotifications();
            var rejection = new S_AlterationRejected(
                tick: 50,
                playerId: 7,
                S_AlterationRejected.Reason.ProtectedZone);
            var packet = new byte[AlterationRejectedPacket.PacketSize];
            Assert.That(AlterationRejectedPacket.TryEncode(packet, in rejection), Is.True);

            Assert.That(queue.TryEnqueueEventPacket(packet, notifications), Is.True);
            Assert.That(queue.PendingEventCount, Is.Zero);
            Assert.That(notifications.Count, Is.EqualTo(1));
            Assert.That(notifications.Last, Is.EqualTo(rejection));
        }

        [Test]
        public void RegressingServerAuthorityIsRejectedBeforeQueueing()
        {
            var queue = new ClientAuthoritativeEventQueue(new DeterministicAlterationApplier());
            AlterationEvent first = Explosion(100, 2, 1, new int3(100, 100, 100));
            AlterationEvent older = Explosion(99, 9, 99, new int3(100, 100, 100));

            Assert.That(queue.TryEnqueueEventPacket(EncodeBatch(int3.zero, first)), Is.True);
            Assert.That(queue.TryEnqueueEventPacket(EncodeBatch(int3.zero, older)), Is.False);
            Assert.That(queue.PendingEventCount, Is.EqualTo(1));
        }

        private static AlterationEvent Explosion(
            uint tick,
            ushort playerId,
            ushort sequence,
            int3 origin) =>
            new AlterationEvent(
                AlterationEvent.KindExplosion,
                tick,
                origin,
                shapeRadius: 1,
                material: 0,
                seed: 123,
                playerId,
                sequence);

        private static byte[] EncodeBatch(int3 encodingRegion, AlterationEvent evt)
        {
            int payloadSize = S_AlterationEventBatch.EncodedSize(1);
            var packet = new byte[ProtocolEnvelope.HeaderSize + payloadSize];
            Assert.That(
                ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_AlterationEventBatch),
                Is.True);

            var events = new[] { evt };
            Assert.That(
                S_AlterationEventBatch.TryEncode(
                    packet.AsSpan(ProtocolEnvelope.HeaderSize),
                    encodingRegion,
                    evt.tick,
                    events,
                    out int written),
                Is.True);
            Assert.That(written, Is.EqualTo(payloadSize));
            return packet;
        }

        private sealed class RecordingNotifications : IClientEventNotificationSink
        {
            public int Count { get; private set; }
            public S_AlterationRejected Last { get; private set; }

            public void OnAlterationRejected(in S_AlterationRejected rejection)
            {
                Count++;
                Last = rejection;
            }
        }
    }
}
