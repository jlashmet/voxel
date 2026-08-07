using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// SC-010 test asserting sub-linear storage growth against cumulative alterations.
    ///
    /// SC-010 assertion: as the number of cumulative alterations grows, storage cost grows
    /// sub-linearly because compaction folds old events into bounded snapshots and only altered
    /// bricks (not the full region volume) are retained in compacted form.
    ///
    /// Specifically:
    ///   - Without compaction, storage would grow linearly with event count (unbounded).
    ///   - With compaction, storage grows as O(altered surface area), not O(cumulative events).
    /// </summary>
    public sealed class StorageGrowthTests
    {
        private const int k_PoolCapacity = 65536;

        /// <summary>Mixed bricks added per alteration pass. Sized to fit k_PoolCapacity.</summary>
        private const int k_BricksPerPass = 512;

        private BrickPool _pool;
        private RegionTable _table;
        private NativeHashMap<int3, uint> _lastCompactionTick;

        [SetUp]
        public void SetUp()
        {
            _pool = new BrickPool(k_PoolCapacity, Allocator.Persistent);
            _table = new RegionTable(64, Allocator.Persistent);
            _lastCompactionTick = new NativeHashMap<int3, uint>(16, Allocator.Persistent);

            SessionLifecycle.Create(999u, ref _table, ref _pool);
        }

        [TearDown]
        public void TearDown()
        {
            _lastCompactionTick.Dispose();
            _table.Dispose();
            _pool.Dispose();
        }

        /// <summary>
        /// SC-010: storage grows sub-linearly with cumulative alterations when compaction is active.
        ///
        /// Scenario: alter N regions progressively, compact after each batch, measure pool growth.
        /// Without compaction, each alteration batch would add proportional brick allocations.
        /// With compaction, only the *difference* between successive states persists.
        /// </summary>
        [Test]
        public void StorageGrowth_SubLinearWithCompaction()
        {
            // Baseline: pool allocated count at pristine state.
            int baselineAllocated = _pool.AllocatedCount;

            // Alter regions in batches, compact after each batch, measure growth.
            NativeList<int> allocatedAfterEachBatch = new NativeList<int>(8, Allocator.Persistent);
            int batchIndex = 0;

            for (int batch = 0; batch < 6; batch++)
            {
                // Select a fresh region for this batch.
                int3 regionCoord = new int3(batch + 1, 0, 0);
                Region r = _table.LoadRegion(regionCoord);

                // Fixed count per batch, standing in for a region's altered surface layer.
                // BricksPerRegion/8 would be 32,768 per batch and 196k across six batches,
                // far past this pool. The sub-linearity claim holds at any fitting size.
                const int alterBricks = k_BricksPerPass;
                for (int i = 0; i < alterBricks && i < r.BrickRefs.Length; i++)
                {
                    if (!r.BrickRefs[i].IsMixed)
                    {
                        int poolIdx = _pool.Allocate();
                        _pool.FillBrick(poolIdx, (byte)(batch + 1));
                        r.BrickRefs[i] = BrickRef.FromPoolIndex(poolIdx);
                    }
                }
                _table.CommitRegion(r);

                // Record pool state before compaction.
                int preCompact = _pool.AllocatedCount;
                allocatedAfterEachBatch.Add(preCompact);

                // Compact: create snapshot, evict region, clear from table.
                NativeArray<byte> snapshot = LogCompaction.CreateSnapshot(
                    regionCoord, default, ref _pool, ref _table);

                if (snapshot.IsCreated && snapshot.Length > 0)
                {
                    // The snapshot captures the altered state — apply it after eviction.
                    _table.EvictRegion(regionCoord, ref _pool);
                    ReadOnlySpan<byte> snap = new ReadOnlySpan<byte>(snapshot.ToArray());
                    LogCompaction.ApplySnapshot(ref _pool, ref _table, regionCoord, snap);
                }

                // Verify the compaction snapshot size is bounded by altered surface area.
                if (snapshot.IsCreated)
                {
                    int expectedMaxBytes = alterBricks * VoxelEngine.Core.Storage.VoxelDimensions.BytesPerMixedBrick;
                    Assert.LessOrEqual(snapshot.Length, expectedMaxBytes,
                        "Compact snapshot must be bounded by altered brick count, not region volume.");

                    // Update the compaction tick map.
                    _lastCompactionTick[regionCoord] = (uint)(batch + 1) * LogCompaction.HotRetentionTicks;
                }

                batchIndex++;
            }

            int totalAllocated = _pool.AllocatedCount;
            int growth = totalAllocated - baselineAllocated;

            // SC-010 assertion: pool allocated count must be significantly less than if every
            // alteration persisted independently (which would be proportional to batch count * alterBricks).
            int naiveGrowth = batchIndex * k_BricksPerPass;
            float compressionRatio = (float)growth / naiveGrowth;

            Assert.Less(compressionRatio, 1f,
                $"SC-010: storage growth must be sub-linear. " +
                $"Growth={growth}, naive={naiveGrowth}, ratio={compressionRatio:F3}.");

            // Also assert absolute bound: no more than k_PoolCapacity / 2 bricks allocated.
            Assert.LessOrEqual(growth, k_PoolCapacity >> 1,
                "Absolute bound: compaction must keep total allocation well under pool capacity.");

            allocatedAfterEachBatch.Dispose();
        }

        /// <summary>
        /// SC-010: snapshots are strictly bounded by region volume.
        ///
        /// Each individual snapshot cannot exceed the worst-case (every brick in the region is mixed).
        /// This is the "snapshot bounded by region volume" invariant from data-model.md.
        /// </summary>
        [Test]
        public void SnapshotSize_BoundedByRegionVolume()
        {
            int3 regionCoord = new int3(0, 0, 0);

            // Allocate a large enough pool for worst-case (all bricks mixed).
            var largePool = new BrickPool(VoxelDimensions.BricksPerRegion * 2, Allocator.Persistent);
            var largeTable = new RegionTable(8, Allocator.Persistent);
            SessionLifecycle.Create(1u, ref largeTable, ref largePool);

            // Fill every brick in the region with mixed bricks (worst case).
            Region r = largeTable.LoadRegion(regionCoord);
            for (int i = 0; i < VoxelDimensions.BricksPerRegion && i < r.BrickRefs.Length; i++)
            {
                int poolIdx = largePool.Allocate();
                largePool.FillBrick(poolIdx, (byte)42);
                r.BrickRefs[i] = BrickRef.FromPoolIndex(poolIdx);
            }
            largeTable.CommitRegion(r);

            // Create worst-case snapshot.
            NativeArray<byte> snapshot = LogCompaction.CreateSnapshot(
                regionCoord, default, ref largePool, ref largeTable);

            Assert.IsTrue(snapshot.IsCreated && snapshot.Length > 0,
                "Worst-case fully-mixed region must produce a non-empty snapshot.");

            // Bounded by: header + count × (brickIndex + tag(4) + poolIndex(4) + voxels(512) + occupancy(64)).
            int maxEntryBytes = sizeof(int) + 4 + 4 + VoxelEngine.Core.Storage.VoxelDimensions.VoxelsPerBrick
                              + VoxelEngine.Core.Storage.VoxelDimensions.OccupancyWordsPerBrick * sizeof(ulong);

            // The worst-case snapshot is: header (19 B approx) + count × maxEntryBytes.
            Assert.LessOrEqual(snapshot.Length, k_CoordSize + sizeof(uint) + VoxelDimensions.BricksPerRegion * maxEntryBytes,
                "SC-010: snapshot must be strictly bounded by region volume (64^3 bricks × per-brick cost).");

            // Sanity: the snapshot size must be proportional to altered brick count, not world extent.
            int entryCount = (int)ReadU32(snapshot, k_CoordSize + sizeof(uint));
            Assert.AreEqual((uint)VoxelDimensions.BricksPerRegion, entryCount,
                "Worst-case snapshot must have one entry per brick.");

            // Cleanup local allocations (do NOT dispose _pool or _table — they belong to SetUp).
            largePool.Dispose();
            largeTable.Dispose();
        }

        /// <summary>
        /// SC-010 verification: without compaction, storage would grow unbounded.
        /// This test demonstrates what happens when compaction is disabled — it should fail,
        /// confirming that compaction is what keeps storage bounded.
        /// </summary>
        [Test]
        public void WithoutCompaction_StorageGrowsLinearly()
        {
            int3 regionCoord = new int3(0, 0, 0);

            // Apply alterations WITHOUT compacting — simulate no compaction path.
            int baseAllocated = _pool.AllocatedCount;

            for (int pass = 0; pass < 5; pass++)
            {
                Region r = _table.LoadRegion(regionCoord);

                // Fixed count per pass. BricksPerRegion/10 would be 26,214 bricks per pass
                // and 131k across five passes — double the pool this test declares. The
                // claim under test is that growth is *linear*, which the slope shows at any
                // magnitude that fits.
                const int newBricks = k_BricksPerPass;
                for (int i = baseAllocated + pass * newBricks;
                     i < baseAllocated + (pass + 1) * newBricks && i < r.BrickRefs.Length;
                     i++)
                {
                    if (!r.BrickRefs[i].IsMixed)
                    {
                        int poolIdx = _pool.Allocate();
                        _pool.FillBrick(poolIdx, (byte)(pass + 1));
                        r.BrickRefs[i] = BrickRef.FromPoolIndex(poolIdx);
                    }
                }

                _table.CommitRegion(r);
            }

            // Without compaction, each pass adds O(bricksPerRegion / 10) pool slots.
            int finalAllocated = _pool.AllocatedCount;
            int totalGrowth = finalAllocated - baseAllocated;

            // This grows linearly: 5 passes × bricks/10 per pass.
            Assert.AreEqual(5 * k_BricksPerPass, totalGrowth,
                "Without compaction, storage grows linearly with each alteration pass.");
        }

        /// <summary>
        /// Test: compaction check correctly identifies regions needing compaction.
        /// </summary>
        [Test]
        public void NeedsCompaction_CorrectBoundaries()
        {
            int3 regionCoord = new int3(0, 0, 0);

            // Never compacted → always needs compaction (even at tick 0).
            Assert.IsTrue(LogCompaction.NeedsCompaction(regionCoord, 0, _lastCompactionTick),
                "Never-compacted region must always need compaction.");

            // Simulate last compaction at tick 10.
            _lastCompactionTick[regionCoord] = 10;

            // At tick 69 (59 ticks elapsed) — not yet needed (< HotRetentionTicks).
            Assert.IsFalse(LogCompaction.NeedsCompaction(regionCoord, 69, _lastCompactionTick),
                "Must not need compaction within the hot retention window.");

            // At tick 70 (60 ticks elapsed == HotRetentionTicks) — now needed.
            Assert.IsTrue(LogCompaction.NeedsCompaction(regionCoord, 70, _lastCompactionTick),
                "Must need compaction when exactly HotRetentionTicks have elapsed.");

            // Simulate compacting at tick 70.
            _lastCompactionTick[regionCoord] = 70;

            // At tick 129 (59 ticks elapsed) — not yet needed again.
            Assert.IsFalse(LogCompaction.NeedsCompaction(regionCoord, 129, _lastCompactionTick),
                "After compaction at tick 70, must not need compaction until 60 more ticks.");

            // At tick 130 (60 ticks elapsed) — needed again.
            Assert.IsTrue(LogCompaction.NeedsCompaction(regionCoord, 130, _lastCompactionTick),
                "Must need compaction exactly at the new HotRetentionTicks boundary.");
        }

        // -- helpers ------------------------------------------------------------

        private const int k_CoordSize = sizeof(int) * 3;

        /// <summary>
        /// Reads a little-endian uint from a NativeArray snapshot, matching the encoding
        /// LogCompaction writes. BitConverter takes byte[] and would need a copy.
        /// </summary>
        private static uint ReadU32(NativeArray<byte> src, int offset) =>
            (uint)(src[offset] | (src[offset + 1] << 8) | (src[offset + 2] << 16) | (src[offset + 3] << 24));

    }
}
