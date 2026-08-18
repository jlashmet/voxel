using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Streaming.Runtime;
using VoxelEngine.Tiering.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ResidencyEvictionRegressionTests
    {
        [Test]
        public void BoundedScanEvictsHistoricalResidentLeftBehindPlayer()
        {
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(16, Allocator.Persistent);
            var storage = new RegionResidencyStore(in table, in pool);
            try
            {
                int3 historical = int3.zero;
                float3 player = new float3(5000f, 64f, 0f);
                int3 current = ResidencyManager.PositionToRegion(player);
                AddMixedBrickRegion(ref table, ref pool, historical);
                table.LoadRegion(current);
                storage.Refresh(in table, in pool);

                Assert.AreEqual(1, pool.AllocatedCount,
                    "Fixture must own a real mixed brick so the regression proves reclamation, " +
                    "not only logical RegionTable removal.");

                int cursor = 0;
                int unloadBlocks = (int)(ResidencyManager.GetUnloadRadius(DeviceTier.PC) / 0.8f);
                for (int pass = 0; pass < 4 && table.IsResident(historical); pass++)
                    ResidencyManager.EvictFarResidents(
                        player, unloadBlocks, storage, ref cursor, maxRegionsToScan: 8);

                Assert.False(table.IsResident(historical),
                    "A region left behind the player's current unload cube was never considered for eviction.");
                Assert.True(table.IsResident(current),
                    "Bounded historical eviction removed a region inside the unload radius.");
                Assert.AreEqual(0, pool.AllocatedCount,
                    "Evicting an unpinned historical region must return its mixed bricks to the pool. " +
                    "A resident-count-only assertion would miss a physical brick retention leak.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void PinnedRegionEvictionDefersMixedBrickReclamationUntilMetadataPinReleases()
        {
            var table = new RegionTable(4, Allocator.Persistent);
            var pool = new BrickPool(2, Allocator.Persistent);
            var reads = new RegionReadSource(in table, in pool);
            var storage = new RegionResidencyStore(in table, in pool);
            bool pinHeld = false;
            VoxelEngine.Storage.Api.VoxelRegionPinToken pin = default;

            try
            {
                int3 coord = int3.zero;
                AddMixedBrickRegion(ref table, ref pool, coord);
                reads.Refresh(in table, in pool);
                storage.Refresh(in table, in pool);

                Assert.AreEqual(1, table.ResidentCount);
                Assert.AreEqual(1, pool.AllocatedCount);
                Assert.True(reads.TryPinRegionBlockRefs(
                    coord, out VoxelEngine.Storage.Api.PinnedRegionBlockRefs pinned));
                pin = pinned.Pin;
                pinHeld = true;

                Assert.True(storage.EvictRegion(coord));

                Assert.AreEqual(0, table.ResidentCount,
                    "Logical eviction should remove a pinned region from normal residency immediately.");
                Assert.False(table.IsResident(coord));
                Assert.AreEqual(1, pool.AllocatedCount,
                    "A metadata-pinned retired region still owns its mixed bricks. This is the " +
                    "resident-count/mixed-pool divergence that can hide pool pressure after eviction.");

                reads.ReleasePinnedRegion(in pin);
                pinHeld = false;

                Assert.AreEqual(0, pool.AllocatedCount,
                    "The final metadata-pin release must reclaim mixed bricks from the retired region.");
            }
            finally
            {
                if (pinHeld)
                    reads.ReleasePinnedRegion(in pin);
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void PinnedEvictedRegionCanExhaustPoolWithZeroLogicalResidents()
        {
            var table = new RegionTable(2, Allocator.Persistent);
            var pool = new BrickPool(1, Allocator.Persistent);
            var reads = new RegionReadSource(in table, in pool);
            var storage = new RegionResidencyStore(in table, in pool);
            bool pinHeld = false;
            VoxelEngine.Storage.Api.VoxelRegionPinToken pin = default;

            try
            {
                int3 coord = int3.zero;
                AddMixedBrickRegion(ref table, ref pool, coord);
                reads.Refresh(in table, in pool);
                storage.Refresh(in table, in pool);

                Assert.True(reads.TryPinRegionBlockRefs(
                    coord, out VoxelEngine.Storage.Api.PinnedRegionBlockRefs pinned));
                pin = pinned.Pin;
                pinHeld = true;
                Assert.True(storage.EvictRegion(coord));

                Assert.AreEqual(0, table.ResidentCount,
                    "The failure fixture needs zero logical residents after eviction.");
                Assert.AreEqual(pool.Capacity, pool.AllocatedCount,
                    "The retired pinned region should still physically occupy the one-slot pool.");

                InvalidOperationException exhausted = Assert.Throws<InvalidOperationException>(
                    () => pool.Allocate());
                StringAssert.Contains("BrickPool exhausted", exhausted.Message);

                reads.ReleasePinnedRegion(in pin);
                pinHeld = false;
                Assert.AreEqual(0, pool.AllocatedCount,
                    "Releasing the metadata pin should make the retired slot recyclable.");

                int replacement = pool.Allocate();
                Assert.AreEqual(1, pool.AllocatedCount,
                    "The same pool must allocate successfully once deferred reclamation completes.");
                pool.Free(replacement);
                Assert.AreEqual(0, pool.AllocatedCount);
            }
            finally
            {
                if (pinHeld)
                    reads.ReleasePinnedRegion(in pin);
                table.Dispose();
                pool.Dispose();
            }
        }

        private static void AddMixedBrickRegion(ref RegionTable table, ref BrickPool pool, int3 coord)
        {
            Region region = table.LoadRegion(coord);
            int brick = pool.Allocate();
            pool.SetVoxel(brick, 0, 1);
            region.BrickRefs[0] = BrickRef.FromPoolIndex(brick);
            table.CommitRegion(region);
        }
    }
}
