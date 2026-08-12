using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Client;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RegionStateCatchupTests
    {
        [Test]
        public void CurrentStateSnapshotSuppressesPreFenceEventOnlyInReplacedRegion()
        {
            var table = new RegionTable(2, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            try
            {
                int3 replacedRegion = int3.zero;
                int3 neighbourRegion = new int3(1, 0, 0);
                table.LoadRegion(replacedRegion);
                table.LoadRegion(neighbourRegion);

                int3 replacedVoxel = new int3(511, 256, 256);
                int3 neighbourVoxel = new int3(512, 256, 256);
                Assert.That(VoxelAccess.SetVoxel(ref table, ref pool, replacedVoxel, 7), Is.True);
                Assert.That(VoxelAccess.SetVoxel(ref table, ref pool, neighbourVoxel, 7), Is.True);

                var evt = new AlterationEvent(
                    AlterationEvent.KindExplosion,
                    tick: 5,
                    origin: replacedVoxel,
                    shapeRadius: 1,
                    material: 0,
                    seed: 123,
                    playerId: 4,
                    sequence: 1);

                var queue = new ClientAuthoritativeEventQueue();
                byte[] batchPacket = FrameBatch(replacedRegion, 5, evt);
                Assert.That(queue.TryEnqueueEventPacket(batchPacket), Is.True);

                var fence = new S_RegionStateFence(91, replacedRegion, 5);
                var fencePacket = new byte[RegionStateFencePacket.PacketSize];
                Assert.That(RegionStateFencePacket.TryEncode(fencePacket, in fence), Is.True);
                Assert.That(queue.TryEnqueueEventPacket(fencePacket), Is.True);

                Assert.That(queue.BeginFullRegionSnapshotWait(replacedRegion), Is.True);
                Assert.That(queue.CompleteFullRegionSnapshot(91, replacedRegion, 5), Is.True);
                Assert.That(queue.SnapshotCatchupActive, Is.True);

                Assert.That(queue.DrainReady(ref table, ref pool, out int appliedEvents), Is.EqualTo(1));
                Assert.That(appliedEvents, Is.EqualTo(1));

                Assert.That(VoxelAccess.GetVoxel(ref table, in pool, replacedVoxel), Is.EqualTo(7),
                    "Pre-fence event must not be re-applied inside the region represented by the current snapshot.");
                Assert.That(VoxelAccess.GetVoxel(ref table, in pool, neighbourVoxel),
                    Is.EqualTo(VoxelDimensions.MaterialEmpty),
                    "The same pre-fence event must still catch up neighbouring regions not replaced by the snapshot.");
                Assert.That(queue.SnapshotCatchupActive, Is.False);
                Assert.That(queue.PendingFenceCount, Is.Zero);
                Assert.That(queue.PendingEventCount, Is.Zero);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private static byte[] FrameBatch(int3 encodingRegion, uint tick, AlterationEvent evt)
        {
            int payloadLength = S_AlterationEventBatch.EncodedSize(1);
            var packet = new byte[ProtocolEnvelope.HeaderSize + payloadLength];
            Assert.That(ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_AlterationEventBatch), Is.True);
            var events = new[] { evt };
            Assert.That(S_AlterationEventBatch.TryEncode(
                packet.AsSpan(ProtocolEnvelope.HeaderSize),
                encodingRegion,
                tick,
                events,
                out int written), Is.True);
            Assert.That(written, Is.EqualTo(payloadLength));
            return packet;
        }
    }
}
