using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StorageRegionResidencyStoreTests
    {
        [Test]
        public void EnsureAndEvictOwnRegionMemoryWithoutExposingPoolSlots()
        {
            var table = new RegionTable(2, Allocator.Persistent);
            var pool = new BrickPool(4, Allocator.Persistent);
            try
            {
                var store = new RegionResidencyStore(in table, in pool);
                int3 coord = new int3(3, -1, 2);

                Assert.IsFalse(store.IsRegionResident(coord));
                store.EnsureRegionResident(coord);
                Assert.IsTrue(store.IsRegionResident(coord));

                Region region = table.LoadRegion(coord);
                int slot = pool.Allocate();
                pool.SetVoxel(slot, 0, 7);
                region.SetBrick(1, 2, 3, BrickRef.FromPoolIndex(slot));
                table.CommitRegion(in region);
                store.Refresh(in table, in pool);

                StoragePressure before = store.Pressure;
                Assert.AreEqual(VoxelDimensions.BytesPerMixedBrick, before.UsedBytes);
                Assert.AreEqual((long)pool.Capacity * VoxelDimensions.BytesPerMixedBrick,
                                before.CapacityBytes);

                Assert.IsTrue(store.EvictRegion(coord));
                Assert.IsFalse(store.IsRegionResident(coord));
                Assert.AreEqual(0, pool.AllocatedCount,
                    "Eviction must return mixed region storage to the owning pool.");
                Assert.IsFalse(store.EvictRegion(coord),
                    "Evicting an already-absent region should report no mutation.");
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void RefreshTracksOwnerAllocationsAndPressureInBytes()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(16, Allocator.Persistent);
            try
            {
                var store = new RegionResidencyStore(in table, in pool);
                Assert.AreEqual(0, store.Pressure.UsedBytes);

                pool.Allocate();
                pool.Allocate();
                store.Refresh(in table, in pool);

                StoragePressure pressure = store.Pressure;
                Assert.AreEqual(2L * VoxelDimensions.BytesPerMixedBrick, pressure.UsedBytes);
                Assert.AreEqual(16L * VoxelDimensions.BytesPerMixedBrick, pressure.CapacityBytes);
                Assert.AreEqual(pool.IsUnderPressure, pressure.IsUnderPressure);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }
    }
}
