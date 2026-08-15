using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SemanticRegionRepairTests
    {
        [Test]
        public void SemanticSnapshotRestoresMixedMaterialsAndHardSurfaceWithoutPoolIdentity()
        {
            var sourceTable = new RegionTable(1, Allocator.TempJob);
            var targetTable = new RegionTable(1, Allocator.TempJob);
            var sourcePool = new BrickPool(8, Allocator.TempJob);
            var targetPool = new BrickPool(8, Allocator.TempJob);
            try
            {
                Region source = sourceTable.LoadRegion(int3.zero);
                int sourcePoolIndex = sourcePool.Allocate();
                sourcePool.FillBrick(sourcePoolIndex, 2);
                sourcePool.SetVoxel(sourcePoolIndex, 17, 7);
                int brickIndex = Region.BrickIndex(3, 4, 5);
                source.BrickRefs[brickIndex] = BrickRef.FromPoolIndex(sourcePoolIndex);
                source.MarkHardSurfaceBrick(brickIndex);
                sourceTable.CommitRegion(source);

                Region target = targetTable.LoadRegion(int3.zero);
                int throwaway = targetPool.Allocate();
                targetPool.FillBrick(throwaway, 9);
                int targetPoolIndex = targetPool.Allocate();
                targetPool.FillBrick(targetPoolIndex, 4);
                target.BrickRefs[brickIndex] = BrickRef.FromPoolIndex(targetPoolIndex);
                targetTable.CommitRegion(target);
                Assert.That(sourcePoolIndex, Is.Not.EqualTo(targetPoolIndex));

                Assert.That(SemanticRegionSnapshotCodec.TryEncode(
                    in source,
                    in sourcePool,
                    SemanticRegionSnapshotCodec.DefaultMaxSnapshotBytes,
                    out byte[] snapshot), Is.True);
                Assert.That(snapshot.Length, Is.LessThan(SemanticRegionSnapshotCodec.DefaultMaxSnapshotBytes));

                uint sourceHash = SemanticRegionHasher.HashRegion(in source, in sourcePool);
                Assert.That(SemanticRegionSnapshotCodec.TryComputeSemanticHash(
                    int3.zero,
                    snapshot,
                    out uint encodedHash), Is.True);
                Assert.That(encodedHash, Is.EqualTo(sourceHash),
                    "Encoded semantic state must hash identically before any target storage is mutated.");

                Assert.That(SemanticRegionSnapshotCodec.TryApply(
                    ref targetTable,
                    ref targetPool,
                    int3.zero,
                    snapshot), Is.True);

                Assert.That(targetTable.TryGetRegion(int3.zero, out Region repaired), Is.True);
                Assert.That(repaired.IsHardSurfaceBrick(brickIndex), Is.True);
                Assert.That(
                    SemanticRegionHasher.HashRegion(in repaired, in targetPool),
                    Is.EqualTo(sourceHash));
                Assert.That(VoxelAccess.GetVoxel(
                    ref targetTable,
                    in targetPool,
                    new int3(3 * 8 + 1, 4 * 8 + 2, 5 * 8)), Is.EqualTo((byte)7),
                    "The deliberately modified mixed voxel must survive semantic snapshot repair.");
                Assert.That(VoxelAccess.GetVoxel(
                    ref targetTable,
                    in targetPool,
                    new int3(3 * 8, 4 * 8, 5 * 8)), Is.EqualTo((byte)2),
                    "Unmodified voxels in the same mixed brick must retain the fill material.");
            }
            finally
            {
                sourceTable.Dispose();
                targetTable.Dispose();
                sourcePool.Dispose();
                targetPool.Dispose();
            }
        }

        [Test]
        public void MalformedSnapshotDoesNotPartiallyMutateRegion()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(4, Allocator.TempJob);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                int brickIndex = Region.BrickIndex(1, 1, 1);
                region.BrickRefs[brickIndex] = BrickRef.Uniform(6);
                table.CommitRegion(region);
                uint before = SemanticRegionHasher.HashRegion(in region, in pool);

                byte[] malformed = { 0, 1, 0, 0, 0 };
                Assert.That(SemanticRegionSnapshotCodec.TryComputeSemanticHash(
                    int3.zero,
                    malformed,
                    out _), Is.False);
                Assert.That(SemanticRegionSnapshotCodec.TryApply(
                    ref table,
                    ref pool,
                    int3.zero,
                    malformed), Is.False);

                Assert.That(table.TryGetRegion(int3.zero, out Region after), Is.True);
                Assert.That(SemanticRegionHasher.HashRegion(in after, in pool), Is.EqualTo(before));
                Assert.That(after.BrickRefs[brickIndex].UniformMaterial, Is.EqualTo((byte)6));
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void RepairChunkNeverExceedsConfiguredRepairPacketCeiling()
        {
            Assert.That(RegionRepairChunkPacket.MaxPacketSize, Is.EqualTo(1024));
            Assert.That(RegionRepairChunkPacket.MaxChunkBytes, Is.EqualTo(992));

            var chunk = new byte[RegionRepairChunkPacket.MaxChunkBytes];
            for (int i = 0; i < chunk.Length; i++) chunk[i] = (byte)i;
            var packet = new byte[RegionRepairChunkPacket.MaxPacketSize];

            Assert.That(RegionRepairChunkPacket.TryEncode(
                packet,
                new int3(-2, 3, 4),
                snapshotTick: 99,
                semanticHash: 0x12345678,
                totalLength: 2000,
                offset: 0,
                chunk,
                out int written), Is.True);
            Assert.That(written, Is.EqualTo(1024));

            Assert.That(RegionRepairChunkPacket.TryDecode(
                packet,
                out var header,
                out var decodedChunk), Is.True);
            Assert.That(header.RegionCoord, Is.EqualTo(new int3(-2, 3, 4)));
            Assert.That(header.SnapshotTick, Is.EqualTo(99));
            Assert.That(header.SemanticHash, Is.EqualTo(0x12345678u));
            Assert.That(header.TotalLength, Is.EqualTo(2000));
            Assert.That(header.Offset, Is.Zero);
            Assert.That(decodedChunk.Length, Is.EqualTo(992));
        }

        [Test]
        public void SnapshotEncodingFailsCleanlyWhenCheckpointCapIsTooSmall()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(4, Allocator.TempJob);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                Assert.That(SemanticRegionSnapshotCodec.TryEncode(
                    in region,
                    in pool,
                    maxBytes: 10,
                    out _), Is.False);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
