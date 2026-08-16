using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Guards the allocation argument the whole engine rests on: pool slots are spent
    /// on *surfaces*, never on volume. If these fail, a kilometre-scale world no longer
    /// fits a capped memory budget and SC-005 is unreachable.
    /// </summary>
    public sealed class StorageAllocationTests
    {
        private const int PoolCapacity = 4096;

        private BrickPool _pool;
        private RegionTable _table;

        [SetUp]
        public void SetUp()
        {
            _pool = new BrickPool(PoolCapacity, Allocator.Persistent);
            _table = new RegionTable(16, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            _table.Dispose();
            _pool.Dispose();
        }

        [Test]
        public void EmptyWorldAllocatesNothing()
        {
            _table.LoadRegion(int3.zero);
            Assert.AreEqual(0, _pool.AllocatedCount,
                "A resident but untouched region must hold zero pool slots.");
        }

        [Test]
        public void ReadingUnloadedRegionReturnsEmptyAndAllocatesNothing()
        {
            var material = VoxelAccess.GetVoxel(ref _table, in _pool, new int3(9999, 9999, 9999));

            Assert.AreEqual(VoxelDimensions.MaterialEmpty, material);
            Assert.AreEqual(0, _pool.AllocatedCount,
                "Probing outside the resident set must not materialise storage — " +
                "raycasts and support propagation do this constantly.");
        }

        [Test]
        public void FirstEditToUniformBrickAllocatesExactlyOneSlot()
        {
            Assert.IsTrue(VoxelAccess.SetVoxel(ref _table, ref _pool, new int3(4, 4, 4), 1));
            Assert.AreEqual(1, _pool.AllocatedCount);
        }

        [Test]
        public void WritingSameMaterialIsNoOpAndAllocatesNothing()
        {
            // The brick is uniformly empty; writing empty must not allocate a slot to
            // represent "still empty".
            Assert.IsFalse(VoxelAccess.SetVoxel(ref _table, ref _pool, new int3(4, 4, 4),
                                                VoxelDimensions.MaterialEmpty));
            Assert.AreEqual(0, _pool.AllocatedCount);
        }

        [Test]
        public void BrickBecomingUniformAgainReturnsItsSlot()
        {
            var p = new int3(4, 4, 4);

            VoxelAccess.SetVoxel(ref _table, ref _pool, p, 1);
            Assert.AreEqual(1, _pool.AllocatedCount, "Edit should have materialised a brick.");

            // Undo it. The brick is uniformly empty once more and must collapse.
            VoxelAccess.SetVoxel(ref _table, ref _pool, p, VoxelDimensions.MaterialEmpty);

            Assert.AreEqual(0, _pool.AllocatedCount,
                "Collapse-to-uniform is the invariant preventing unbounded pool growth. " +
                "Without it nothing breaks visibly — memory just climbs across a long " +
                "session until the pool holds bricks containing no surface.");
        }

        [Test]
        public void FillingBrickCompletelyCollapsesToUniform()
        {
            for (var z = 0; z < VoxelDimensions.BrickEdge; z++)
            for (var y = 0; y < VoxelDimensions.BrickEdge; y++)
            for (var x = 0; x < VoxelDimensions.BrickEdge; x++)
                VoxelAccess.SetVoxel(ref _table, ref _pool, new int3(x, y, z), 3);

            Assert.AreEqual(0, _pool.AllocatedCount,
                "A brick filled entirely with one material holds no surface and must " +
                "cost nothing — this is why solid rock underground is free at any volume.");

            Assert.AreEqual(3, VoxelAccess.GetVoxel(ref _table, in _pool, new int3(3, 3, 3)),
                "Collapsing to uniform must preserve the material.");
        }

        [Test]
        public void SlotsAreRecycledRatherThanLeaked()
        {
            var p = new int3(4, 4, 4);

            for (var i = 0; i < 200; i++)
            {
                VoxelAccess.SetVoxel(ref _table, ref _pool, p, 1);
                VoxelAccess.SetVoxel(ref _table, ref _pool, p, VoxelDimensions.MaterialEmpty);
            }

            Assert.AreEqual(0, _pool.AllocatedCount);
            Assert.Less(_pool.AllocatedCount, PoolCapacity,
                "Repeated build/destroy on one voxel must not consume the pool.");
        }

        [Test]
        public void PartiallyEditedBrickStaysMixed()
        {
            VoxelAccess.SetVoxel(ref _table, ref _pool, new int3(0, 0, 0), 1);
            VoxelAccess.SetVoxel(ref _table, ref _pool, new int3(1, 0, 0), 2);

            Assert.AreEqual(1, _pool.AllocatedCount,
                "Two differing materials in one brick is a surface; it must stay mixed.");
        }

        [Test]
        public void EvictingRegionReturnsAllItsSlots()
        {
            for (var i = 0; i < 32; i++)
                VoxelAccess.SetVoxel(ref _table, ref _pool,
                                     new int3(i * VoxelDimensions.BrickEdge, 0, 0), 1);

            Assert.Greater(_pool.AllocatedCount, 0);

            _table.EvictRegion(int3.zero, ref _pool);

            Assert.AreEqual(0, _pool.AllocatedCount,
                "Eviction must return every mixed brick the region held.");
            Assert.AreEqual(0, _table.ResidentCount);
        }

        [Test]
        public void RoundTripPreservesMaterialAcrossRegionBoundaries()
        {
            // Straddle a region boundary: region 0 and region 1 on x.
            var a = new int3(511, 0, 0);
            var b = new int3(512, 0, 0);

            VoxelAccess.SetVoxel(ref _table, ref _pool, a, 7);
            VoxelAccess.SetVoxel(ref _table, ref _pool, b, 9);

            Assert.AreEqual(7, VoxelAccess.GetVoxel(ref _table, in _pool, a));
            Assert.AreEqual(9, VoxelAccess.GetVoxel(ref _table, in _pool, b));
            Assert.AreEqual(2, _table.ResidentCount, "Should have materialised two regions.");
        }

        [Test]
        public void NegativeCoordinatesFloorCorrectly()
        {
            // Truncation toward zero would collapse -1 and 0 into the same region and
            // produce a visible seam at the origin.
            var p = new int3(-1, -1, -1);

            VoxelAccess.SetVoxel(ref _table, ref _pool, p, 5);

            Assert.AreEqual(5, VoxelAccess.GetVoxel(ref _table, in _pool, p));
            Assert.AreEqual(0, VoxelAccess.GetVoxel(ref _table, in _pool, int3.zero),
                "Voxel at the origin must be unaffected by a write at (-1,-1,-1).");

            VoxelAccess.Decompose(p, out var regionCoord, out _, out var voxelInBrick);
            Assert.AreEqual(new int3(-1, -1, -1), regionCoord);
            Assert.AreEqual(new int3(7, 7, 7), voxelInBrick);
        }


        [Test]
        public void PinnedBrickUsesCopyOnWriteAndDefersRecycling()
        {
            int original = _pool.Allocate();
            _pool.FillBrick(original, 3);
            BrickPool.PinToken pin = _pool.Pin(original);

            int writable = _pool.EnsureWritable(original);
            Assert.AreNotEqual(original, writable,
                "A pinned version must never be mutated in place.");
            Assert.AreEqual(2, _pool.AllocatedCount,
                "The retired reader version stays allocated until its final pin releases.");
            Assert.AreEqual(3, _pool.GetVoxel(original, 0));
            Assert.AreEqual(3, _pool.GetVoxel(writable, 0));

            _pool.SetVoxel(writable, 0, 7);
            Assert.AreEqual(3, _pool.GetVoxel(original, 0),
                "Reader-visible payload changed after COW publication.");
            Assert.AreEqual(7, _pool.GetVoxel(writable, 0));

            _pool.Unpin(in pin);
            Assert.AreEqual(1, _pool.AllocatedCount,
                "Retired storage must become recyclable when its final reader exits.");
            _pool.Free(writable);
            Assert.AreEqual(0, _pool.AllocatedCount);
        }

        [Test]
        public void BrickPinGenerationRejectsAbaReuse()
        {
            int slot = _pool.Allocate();
            BrickPool.PinToken oldPin = _pool.Pin(slot);
            uint oldGeneration = oldPin.Generation;
            _pool.Unpin(in oldPin);
            _pool.Free(slot);

            int reused = _pool.Allocate();
            Assert.AreEqual(slot, reused, "Free-list reuse is expected for this ABA guard test.");
            BrickPool.PinToken newPin = _pool.Pin(reused);
            Assert.AreNotEqual(oldGeneration, newPin.Generation);
            _pool.Unpin(in newPin);
            _pool.Free(reused);
        }


        [Test]
        public void VoxelAccessPublishesCowCloneBeforeEditingPinnedBrick()
        {
            int3 voxel = new int3(1, 2, 3);
            Assert.True(VoxelAccess.SetVoxel(ref _table, ref _pool, voxel, 5));
            VoxelAccess.Decompose(voxel, out int3 regionCoord,
                                  out int3 brickInRegion, out int3 voxelInBrick);
            Assert.True(_table.TryGetRegion(regionCoord, out Region before));
            int blockIndex = Region.BrickIndex(brickInRegion.x, brickInRegion.y, brickInRegion.z);
            int oldSlot = before.BrickRefs[blockIndex].PoolIndex;
            int voxelIndex = VoxelEngine.Storage.Runtime.Occupancy.OccupancyMask.VoxelIndex(
                voxelInBrick.x, voxelInBrick.y, voxelInBrick.z);
            BrickPool.PinToken pin = _pool.Pin(oldSlot);

            Assert.True(VoxelAccess.SetVoxel(ref _table, ref _pool, voxel, 7));
            Assert.True(_table.TryGetRegion(regionCoord, out Region after));
            int newSlot = after.BrickRefs[blockIndex].PoolIndex;
            Assert.AreNotEqual(oldSlot, newSlot);
            Assert.AreEqual(5, _pool.GetVoxel(oldSlot, voxelIndex),
                "Pinned reader version was mutated in place.");
            Assert.AreEqual(7, _pool.GetVoxel(newSlot, voxelIndex));

            _pool.Unpin(in pin);
        }


        [Test]
        public void BorrowedWriterBlocksPinsAndDefersRetiredSlotReuse()
        {
            int slot = _pool.Allocate();
            _pool.FillBrick(slot, 4);
            _pool.BeginWrite(slot);
            Assert.False(_pool.TryPin(slot, out _));

            _pool.Free(slot);
            Assert.AreEqual(1, _pool.AllocatedCount,
                "A retired slot with an active writer must not be recycled.");
            _pool.EndWrite(slot);
            Assert.AreEqual(0, _pool.AllocatedCount);
        }

        [Test]
        public void BrickRefEncodingRoundTrips()
        {
            Assert.IsTrue(BrickRef.Empty.IsEmpty);
            Assert.IsTrue(BrickRef.Empty.IsUniform);
            Assert.IsFalse(BrickRef.Empty.IsMixed);
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, BrickRef.Empty.UniformMaterial);

            for (var m = 0; m < 256; m++)
            {
                var r = BrickRef.Uniform((byte)m);
                Assert.IsTrue(r.IsUniform);
                Assert.AreEqual((byte)m, r.UniformMaterial, $"Uniform material {m} round-trip.");
            }

            var mixed = BrickRef.FromPoolIndex(12345);
            Assert.IsTrue(mixed.IsMixed);
            Assert.AreEqual(12345, mixed.PoolIndex);
        }
    }
}
