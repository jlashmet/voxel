using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Acceptance tests for scenario 6: alterations persist across loads and session lifecycle.
    ///
    /// Tests:
    ///   - Alter a region, leave it, re-enter → changes are still there (persistence).
    ///   - Session end → all changes discarded per FR-031.
    /// </summary>
    public sealed class PersistenceTests
    {
        private const int k_PoolCapacity = 4096;

        private BrickPool _pool;
        private RegionTable _table;
        private NativeHashMap<int3, uint> _lastCompactionTick;

        [SetUp]
        public void SetUp()
        {
            _pool = new BrickPool(k_PoolCapacity, Allocator.Persistent);
            _table = new RegionTable(16, Allocator.Persistent);
            _lastCompactionTick = new NativeHashMap<int3, uint>(8, Allocator.Persistent);

            // Initialize session with pristine terrain.
            SessionLifecycle.Create(42u, ref _table, ref _pool);
        }

        [TearDown]
        public void TearDown()
        {
            _lastCompactionTick.Dispose();
            _table.Dispose();
            _pool.Dispose();
        }

        /// <summary>
        /// Test: alter a region, leave it, re-enter — changes are still there.
        ///
        /// Scenario:
        ///   1. Load region (0, 0, 0) — pristine state (all empty/uniform).
        ///   2. Apply an alteration (fill with material 7).
        ///   3. "Leave" the region by evicting it (simulates player walking away and region going Cold).
        ///   4. "Re-enter" the region — load it again from the table.
        ///   5. The alteration should still be present because the server retains truth via RegionTable.
        /// </summary>
        [Test]
        public void AlterThenEvictThenReenter_ChangesPersist()
        {
            int3 regionCoord = new int3(1, 0, 1);

            // Step 1: Load and record pristine state for comparison.
            Region pristine = _table.LoadRegion(regionCoord);
            var pristineBricks = CopyRegionBricks(pristine);
            _table.CommitRegion(pristine);

            // Step 2: Alter the region — fill a surface layer with material 7.
            int alteredMaterial = 7;
            for (int x = 0; x < VoxelDimensions.RegionEdge; x++)
            {
                for (int z = 0; z < VoxelDimensions.RegionEdge; z++)
                {
                    // Fill brick at Y=4 with material 7 (a horizontal surface).
                    int yBrick = 4 >> VoxelDimensions.BrickEdgeLog2;
                    int brickIdx = Region.BrickIndex(x, yBrick, z);

                    if (!pristine.BrickRefs[brickIdx].IsMixed)
                    {
                        // Allocate a mixed brick.
                        int poolIdx = _pool.Allocate();
                        _pool.FillBrick(poolIdx, (byte)alteredMaterial);
                        pristine.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(poolIdx);
                    }
                }
            }
            _table.CommitRegion(pristine);

            // Verify: the alteration is present.
            Region altered = _table.LoadRegion(regionCoord);
            Assert.IsTrue(HasMaterialInRegion(altered, (byte)alteredMaterial),
                "Altered region must contain the new material.");

            // Step 3: Evict — simulate leaving the region.
            _table.EvictRegion(regionCoord, ref _pool);

            // Step 4: Re-enter.
            Region reentered = _table.LoadRegion(regionCoord);

            // Step 5: Verify changes persist after eviction/re-entry.
            Assert.IsTrue(HasMaterialInRegion(reentered, (byte)alteredMaterial),
                "Changes must persist after eviction and re-entry per FR-031's implicit invariant: " +
                "alterations survive as long as the session is active.");
        }

        /// <summary>
        /// Test: session end — all changes discarded per FR-031.
        ///
        /// Scenario:
        ///   1. Alter multiple regions extensively.
        ///   2. Call SignalEnd() to mark session ending.
        ///   3. Call DiscardAllAlterations() to complete cleanup.
        ///   4. All altered regions must return to pristine terrain state.
        /// </summary>
        [Test]
        public void SessionEnd_AllChangesDiscarded_PerFR031()
        {
            int3 regionCoord = new int3(2, 0, 3);

            // Step 1: Alter the region extensively — multiple brick layers.
            //
            // Layers span k_LayerExtent rather than the full 64-brick region edge: four full
            // layers would be 16,384 bricks against a 4,096-slot pool. What FR-031 asserts is
            // that *all* alterations are discarded, which any non-trivial count demonstrates.
            const int k_LayerExtent = 30;

            Region r = _table.LoadRegion(regionCoord);
            for (int layer = 0; layer < 4; layer++)
            {
                int yBrick = layer;
                for (int x = 0; x < k_LayerExtent; x++)
                {
                    for (int z = 0; z < k_LayerExtent; z++)
                    {
                        int brickIdx = Region.BrickIndex(x, yBrick, z);
                        if (!r.BrickRefs[brickIdx].IsMixed)
                        {
                            int poolIdx = _pool.Allocate();
                            byte mat = (byte)(layer + 1); // Different material per layer.
                            _pool.FillBrick(poolIdx, mat);
                            r.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(poolIdx);
                        }
                    }
                }
            }
            _table.CommitRegion(r);

            // Verify: the region has been altered.
            Assert.IsTrue(HasMixedBricks(_table, regionCoord), "Region must contain mixed bricks after alteration.");
            int poolAllocatedBefore = _pool.AllocatedCount;
            Assert.Greater(poolAllocatedBefore, 0, "Pool must have allocated bricks before session end.");

            // Step 2: Signal session ending.
            SessionLifecycle.SignalEnd(1000f);
            Assert.AreEqual(SessionLifecycle.State.Ending, SessionLifecycle.CurrentState);

            // Step 3: Discard all alterations (completes the session end).
            SessionLifecycle.DiscardAllAlterations(ref _table, ref _pool);

            // Verify: session is now ended.
            Assert.AreEqual(SessionLifecycle.State.Ended, SessionLifecycle.CurrentState,
                "Session must be in Ended state after DiscardAllAlterations.");

            // Step 4: Load the region and verify it returned to pristine.
            Region postSession = _table.LoadRegion(regionCoord);

            Assert.IsFalse(HasMixedBricks(_table, regionCoord),
                "FR-031: no mixed bricks should remain after session end — all modifications discarded.");

            // Verify pool was cleared (all allocated bricks returned).
            Assert.AreEqual(0, _pool.AllocatedCount,
                "Pool must have zero allocated bricks after session end — memory is clean.");
        }

        /// <summary>
        /// Test: compaction preserves altered state for late-join players.
        ///
        /// Scenario:
        ///   1. Alter a region extensively.
        ///   2. Advance the compaction tick past the hot retention window.
        ///   3. Create a compaction snapshot.
        ///   4. Evict and re-enter — apply the snapshot.
        ///   5. The altered state must be faithfully restored.
        /// </summary>
        [Test]
        public void CompactionAndReapply_PreservesAlteredState()
        {
            int3 regionCoord = new int3(0, 0, 0);

            // Step 1: Alter a significant volume of the region.
            Region r = _table.LoadRegion(regionCoord);
            for (int x = 0; x < VoxelDimensions.RegionEdge; x++)
            {
                for (int y = 0; y < VoxelDimensions.RegionEdge; y++)
                {
                    for (int z = 0; z < VoxelDimensions.RegionEdge; z++)
                    {
                        // Fill a corner cube with material 9.
                        if (x < 8 && y < 8 && z < 8)
                        {
                            int brickIdx = Region.BrickIndex(x, y, z);
                            if (!r.BrickRefs[brickIdx].IsMixed)
                            {
                                int poolIdx = _pool.Allocate();
                                _pool.FillBrick(poolIdx, (byte)9);
                                r.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(poolIdx);
                            }
                        }
                    }
                }
            }
            BrickRef styledBrick = r.BrickRefs[Region.BrickIndex(0, 0, 0)];
            var styledCell = new VoxelCell
            {
                BaseMaterialId = 9,
                Surface = new VoxelSurfaceSemantics
                {
                    StyleId = SurfaceStyles.Rounded,
                    CoatingId = Coatings.Moss
                },
                Boundary = VoxelBoundarySample.FromSignedQ4(7)
            };
            _pool.SetCell(styledBrick.PoolIndex, 0, in styledCell);
            _table.CommitRegion(r);

            // Verify pre-compaction state.
            int mixedBricksBefore = CountMixedBricks(_table, regionCoord);
            Assert.Greater(mixedBricksBefore, 0, "Region must have mixed bricks before compaction.");

            // Step 2: Create a compaction snapshot (simulating events beyond hot retention).
            NativeArray<byte> snapshot = LogCompaction.CreateSnapshot(
                regionCoord, default, ref _pool, ref _table);

            // The snapshot may be empty if no bricks differ from terrain (which is empty/uniform here),
            // but we filled a corner with material 9 which should produce mixed entries.
            Assert.IsTrue(snapshot.IsCreated && snapshot.Length > 0,
                "Snapshot must contain data for the altered region.");

            // Step 3: Evict and clear — simulate server compaction purging the event log.
            _table.EvictRegion(regionCoord, ref _pool);

            // Step 4: Apply the snapshot to restore state.
            // Convert NativeArray<byte> to byte[] for ReadOnlySpan<>.
            byte[] snapBuffer = new byte[snapshot.Length];
            for (int i = 0; i < snapshot.Length; i++) snapBuffer[i] = snapshot[i];

            ReadOnlySpan<byte> snapSpan = new ReadOnlySpan<byte>(snapBuffer);
            LogCompaction.ApplySnapshot(ref _pool, ref _table, regionCoord, snapSpan);

            // Step 5: Verify — the material 9 corner must still be present.
            Region restored = _table.LoadRegion(regionCoord);
            Assert.IsTrue(HasMaterialInRegion(restored, (byte)9),
                "Compacted brick state must be faithfully restored.");
            VoxelCell restoredCell = VoxelAccess.GetCell(ref _table, in _pool, int3.zero);
            Assert.AreEqual(9, restoredCell.BaseMaterialId);
            Assert.AreEqual(SurfaceStyles.Rounded, restoredCell.Surface.StyleId);
            Assert.AreEqual(Coatings.Moss, restoredCell.Surface.CoatingId);
            Assert.AreEqual(7, restoredCell.Boundary.SignedQ4,
                "compaction must preserve authored geometry constraints");
        }

        /// <summary>Test helper: check if a region contains any bricks of the given material.</summary>
        private bool HasMaterialInRegion(Region region, byte material)
        {
            if (!region.IsCreated) return false;

            for (int i = 0; i < VoxelDimensions.BricksPerRegion && i < region.BrickRefs.Length; i++)
            {
                BrickRef br = region.BrickRefs[i];
                if (!br.IsMixed) continue;

                int poolIdx = br.PoolIndex;
                if (poolIdx >= 0 && poolIdx < _pool.Capacity)
                {
                    // Check voxels directly.
                    int vo = _pool.VoxelOffset(poolIdx);
                    for (int v = 0; v < VoxelDimensions.VoxelsPerBrick; v++)
                    {
                        if (_pool.Voxels[vo + v] == material) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Test helper: check if a resident region has any mixed bricks.</summary>
        private bool HasMixedBricks(RegionTable table, int3 coord)
        {
            if (!table.TryGetRegion(coord, out Region r)) return false;
            return CountMixedBricks(r) > 0;
        }

        /// <summary>Test helper: count mixed bricks in a region.</summary>
        private static int CountMixedBricks(RegionTable table, int3 coord)
        {
            if (!table.TryGetRegion(coord, out var r)) return 0;
            return CountMixedBricks(r);
        }

        private static int CountMixedBricks(Region r)
        {
            int count = 0;
            for (int i = 0; i < VoxelDimensions.BricksPerRegion && i < r.BrickRefs.Length; i++)
            {
                if (r.BrickRefs[i].IsMixed) count++;
            }
            return count;
        }

        /// <summary>Copy region brick references for comparison.</summary>
        private NativeArray<byte> CopyRegionBricks(Region region)
        {
            var data = new NativeArray<byte>(region.BrickRefs.Length, Allocator.Persistent);
            for (int i = 0; i < region.BrickRefs.Length; i++)
                data[i] = (byte)(region.BrickRefs[i].IsMixed ? 1 : 0);
            return data;
        }

    }
}
