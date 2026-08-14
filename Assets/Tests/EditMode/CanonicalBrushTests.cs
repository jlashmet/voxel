using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Client;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CanonicalBrushTests
    {
        [Test]
        public void ShapePackingKeepsThreeExtentsAndDiscriminatorIndependent()
        {
            uint packed = BrushShapeCodec.PackCube(64, 63, 62);

            Assert.That(BrushShapeCodec.ShapeType(packed), Is.EqualTo(BrushShapeCodec.ShapeCube));
            Assert.That(BrushShapeCodec.ExtentsBricks(packed), Is.EqualTo(new int3(64, 63, 62)));
            Assert.That(BrushShapeCodec.Validate(packed, 0), Is.True);
            Assert.That(BrushShapeCodec.Validate(packed, BrushShapeCodec.FlagHardSurface), Is.True);
            Assert.That(BrushShapeCodec.Validate(packed, 1u << 7), Is.False);

            uint unsupportedShape = (packed & 0x00FFFFFFu) | (2u << 24);
            Assert.That(BrushShapeCodec.Validate(unsupportedShape, 0), Is.False);
        }

        [Test]
        public void OneBrickCubeWritesExactEightCubedVolumeWithoutMixedAllocation()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            try
            {
                table.LoadRegion(int3.zero);
                var evt = AlterationEvent.CreateCubeBrush(
                    tick: 1,
                    origin: new int3(4, 4, 4),
                    extentXBricks: 1,
                    extentYBricks: 1,
                    extentZBricks: 1,
                    material: 6,
                    seed: 99,
                    playerId: 1,
                    sequence: 1);

                Assert.That(DeterministicAlterationApplier.TryApply(
                    ref table,
                    ref pool,
                    in evt,
                    out NativeList<int3> affected), Is.True);
                try
                {
                    Assert.That(affected.Length, Is.EqualTo(1));
                    Assert.That(affected[0], Is.EqualTo(int3.zero));
                }
                finally
                {
                    affected.Dispose();
                }

                Assert.That(pool.AllocatedCount, Is.Zero,
                    "A full-brick uniform placement should stay in BrickRef and never allocate mixed storage.");
                Assert.That(VoxelAccess.GetVoxel(ref table, in pool, int3.zero), Is.EqualTo((byte)6));
                Assert.That(VoxelAccess.GetVoxel(ref table, in pool, new int3(7, 7, 7)), Is.EqualTo((byte)6));
                Assert.That(VoxelAccess.GetVoxel(ref table, in pool, new int3(8, 4, 4)),
                    Is.EqualTo(VoxelDimensions.MaterialEmpty));
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void OffGridOneBrickCubeSpansTwoWorldBricksAndBudgetCountsBoth()
        {
            var evt = AlterationEvent.CreateCubeBrush(
                tick: 1,
                origin: new int3(8, 4, 4),
                extentXBricks: 1,
                extentYBricks: 1,
                extentZBricks: 1,
                material: 3,
                seed: 1,
                playerId: 1,
                sequence: 1);

            BrushShapeCodec.GetCubeVoxelBounds(evt.origin, evt.BrushExtents(), out int3 min, out int3 max);
            Assert.That(min, Is.EqualTo(new int3(4, 0, 0)));
            Assert.That(max, Is.EqualTo(new int3(11, 7, 7)));
            Assert.That(AuthoritativeAlterationValidator.EstimateAffectedBricks(in evt), Is.EqualTo(2));
        }

        [Test]
        public void HardSurfaceFlagCanCreateSemanticChangeWithoutMaterialAllocation()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(4, Allocator.TempJob);
            try
            {
                table.LoadRegion(int3.zero);
                var materialBrush = AlterationEvent.CreateCubeBrush(
                    1, new int3(4, 4, 4), 1, 1, 1, 5, 1, 1, 1, false);
                Assert.That(ApplyAndDispose(ref table, ref pool, in materialBrush), Is.True);
                Assert.That(pool.AllocatedCount, Is.Zero);

                var hardBrush = AlterationEvent.CreateCubeBrush(
                    2, new int3(4, 4, 4), 1, 1, 1, 5, 2, 1, 2, true);
                Assert.That(ApplyAndDispose(ref table, ref pool, in hardBrush), Is.True,
                    "Marking authored hard geometry is authoritative even when material bytes already match.");
                Assert.That(pool.AllocatedCount, Is.Zero);

                Assert.That(table.TryGetRegion(int3.zero, out Region region), Is.True);
                Assert.That(region.IsHardSurfaceBrick(Region.BrickIndex(0, 0, 0)), Is.True);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void CrossRegionBrushFailsBeforeMutatingWhenNeighbourIsNotResident()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            try
            {
                table.LoadRegion(int3.zero);
                int3 sentinel = new int3(508, 4, 4);
                Assert.That(VoxelAccess.SetVoxel(ref table, ref pool, sentinel, 9), Is.True);

                var evt = AlterationEvent.CreateCubeBrush(
                    1,
                    new int3(VoxelDimensions.RegionVoxelEdge, 4, 4),
                    1, 1, 1,
                    3,
                    1,
                    1,
                    1);

                Assert.That(DeterministicAlterationApplier.HasRequiredResidency(ref table, in evt), Is.False);
                Assert.That(ApplyAndDispose(ref table, ref pool, in evt), Is.False);
                Assert.That(VoxelAccess.GetVoxel(ref table, in pool, sentinel), Is.EqualTo((byte)9),
                    "Residency preflight must prevent any partial write in the loaded half.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void CompactBatchRoundTripAppliesSameBrushStateOnClient()
        {
            var serverTable = new RegionTable(1, Allocator.TempJob);
            var clientTable = new RegionTable(1, Allocator.TempJob);
            var serverPool = new BrickPool(16, Allocator.TempJob);
            var clientPool = new BrickPool(16, Allocator.TempJob);
            try
            {
                serverTable.LoadRegion(int3.zero);
                clientTable.LoadRegion(int3.zero);

                var evt = AlterationEvent.CreateCubeBrush(
                    11,
                    new int3(8, 8, 8),
                    2, 1, 1,
                    4,
                    12345,
                    2,
                    1,
                    hardSurface: true);

                Assert.That(ApplyAndDispose(ref serverTable, ref serverPool, in evt), Is.True);

                int payloadSize = S_AlterationEventBatch.EncodedSize(1);
                var packet = new byte[ProtocolEnvelope.HeaderSize + payloadSize];
                Assert.That(ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_AlterationEventBatch), Is.True);
                var events = new[] { evt };
                Assert.That(S_AlterationEventBatch.TryEncode(
                    packet.AsSpan(ProtocolEnvelope.HeaderSize),
                    int3.zero,
                    evt.tick,
                    events,
                    out int written), Is.True);
                Assert.That(written, Is.EqualTo(payloadSize));

                var queue = new ClientAuthoritativeEventQueue();
                Assert.That(queue.TryEnqueueEventPacket(packet), Is.True);
                Assert.That(queue.DrainReady(ref clientTable, ref clientPool, out int appliedEvents), Is.EqualTo(1));
                Assert.That(appliedEvents, Is.EqualTo(1));

                Assert.That(serverTable.TryGetRegion(int3.zero, out Region serverRegion), Is.True);
                Assert.That(clientTable.TryGetRegion(int3.zero, out Region clientRegion), Is.True);
                Assert.That(
                    SemanticRegionHasher.HashRegion(in clientRegion, in clientPool),
                    Is.EqualTo(SemanticRegionHasher.HashRegion(in serverRegion, in serverPool)));
            }
            finally
            {
                serverTable.Dispose();
                clientTable.Dispose();
                serverPool.Dispose();
                clientPool.Dispose();
            }
        }

        private static bool ApplyAndDispose(
            ref RegionTable table,
            ref BrickPool pool,
            in AlterationEvent evt)
        {
            bool result = DeterministicAlterationApplier.TryApply(
                ref table,
                ref pool,
                in evt,
                out NativeList<int3> affected);
            if (affected.IsCreated) affected.Dispose();
            return result;
        }
    }
}
