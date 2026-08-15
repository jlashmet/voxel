using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DeterministicAlterationApplierTests
    {
        [Test]
        public void PartialExplosionClearsInsideSphereAndPreservesOutsideVoxel()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(16, Allocator.TempJob);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);
                int brickIndex = Region.BrickIndex(1, 1, 1);
                region.BrickRefs[brickIndex] = BrickRef.Uniform(3);
                table.CommitRegion(region);

                var evt = new AlterationEvent(
                    AlterationEvent.KindExplosion,
                    tick: 1,
                    origin: new int3(8, 8, 8),
                    shapeRadius: 1,
                    material: 0,
                    seed: 123,
                    playerId: 1,
                    sequence: 1);

                Assert.That(DeterministicAlterationApplier.TryApply(
                    storage,
                    in evt,
                    out var affected), Is.True);

                try
                {
                    Assert.That(affected.Length, Is.EqualTo(1));
                    Assert.That(VoxelAccess.GetVoxel(ref table, in pool, new int3(8, 8, 8)),
                        Is.EqualTo(VoxelDimensions.MaterialEmpty));
                    Assert.That(VoxelAccess.GetVoxel(ref table, in pool, new int3(15, 15, 15)),
                        Is.EqualTo((byte)3));

                    Assert.That(table.TryGetRegion(int3.zero, out Region after), Is.True);
                    Assert.That(after.BrickRefs[brickIndex].IsMixed, Is.True);
                }
                finally
                {
                    if (affected.IsCreated) affected.Dispose();
                }
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void FullyCoveredUniformBrickCollapsesDirectlyToEmptyWithoutPoolAllocation()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(16, Allocator.TempJob);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);
                int brickIndex = Region.BrickIndex(1, 1, 1);
                region.BrickRefs[brickIndex] = BrickRef.Uniform(4);
                table.CommitRegion(region);

                var evt = new AlterationEvent(
                    AlterationEvent.KindExplosion,
                    2,
                    new int3(12, 12, 12),
                    1,
                    0,
                    99,
                    1,
                    1);

                Assert.That(DeterministicAlterationApplier.TryApply(
                    storage,
                    in evt,
                    out var affected), Is.True);

                try
                {
                    Assert.That(table.TryGetRegion(int3.zero, out Region after), Is.True);
                    Assert.That(after.BrickRefs[brickIndex].IsEmpty, Is.True);
                    Assert.That(pool.AllocatedCount, Is.Zero);
                }
                finally
                {
                    if (affected.IsCreated) affected.Dispose();
                }
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void MissingCrossRegionResidencyFailsBeforeMutatingLoadedRegion()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(16, Allocator.TempJob);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);
                int brickIndex = Region.BrickIndex(63, 0, 0);
                region.BrickRefs[brickIndex] = BrickRef.Uniform(5);
                table.CommitRegion(region);

                var evt = new AlterationEvent(
                    AlterationEvent.KindExplosion,
                    3,
                    new int3(510, 4, 4),
                    1,
                    0,
                    77,
                    1,
                    1);

                Assert.That(DeterministicAlterationApplier.TryApply(
                    storage,
                    in evt,
                    out var affected), Is.False);

                try
                {
                    Assert.That(affected.Length, Is.Zero);
                    Assert.That(table.TryGetRegion(int3.zero, out Region after), Is.True);
                    Assert.That(after.BrickRefs[brickIndex].UniformMaterial, Is.EqualTo((byte)5));
                    Assert.That(pool.AllocatedCount, Is.Zero);
                }
                finally
                {
                    if (affected.IsCreated) affected.Dispose();
                }
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
