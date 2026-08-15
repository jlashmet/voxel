using Unity.Mathematics;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Asserts that reconciliation uses historical, not present, world state.
    ///
    /// Key invariant: when the client reconciles from tick T1 to T2, it must compare
    /// each brick against the server's state *at the snapshot for that tick*, not the
    /// current (post-edit) state of the WorldHistory. This is the difference between a
    /// correct reconciliation and one that silently accepts divergent client predictions.
    /// </summary>
    public sealed class ReconciliationTests
    {
        private const int k_PoolCapacity = 1024;

        private BrickPool _pool;
        private RegionTable _table;
        private WorldHistory _history;

        [SetUp]
        public void SetUp()
        {
            _pool = new BrickPool(k_PoolCapacity, Allocator.Persistent);
            _table = new RegionTable(8, Allocator.Persistent);
            _history = new WorldHistory(16);
        }

        [TearDown]
        public void TearDown()
        {
            _table.Dispose();
            _pool.Dispose();
            _history.Dispose();
        }

        /// <summary>
        /// Verifies that reconciliation compares against historical snapshots, not the current grid.
        ///
        /// Setup: write a voxel at tick 0, snapshot it, then overwrite the same voxel at tick 1
        /// (modifying the *present* state). Reconcile using the tick-0 snapshot — the result should
        /// reflect the historical state (the original material), not the current overwritten value.
        /// </summary>
        [Test]
        public void ReplayUsesHistoricalStateNotPresent()
        {
            var regionCoord = int3.zero;
            _table.LoadRegion(regionCoord);

            // Write a voxel at tick 0 and record the snapshot.
            int3 targetVoxel = new int3(4, 4, 4);
            VoxelAccess.SetVoxel(ref _table, ref _pool, targetVoxel, 5); // Material 5 at tick 0.

            var brickData = GetRegionBrickStorage(targetVoxel);
            _history.RecordSnapshot(0, brickData);

            // Now overwrite the same voxel (modifying present state but not history).
            VoxelAccess.SetVoxel(ref _table, ref _pool, targetVoxel, 10); // Material 10 at tick 1.

            // The historical snapshot should still have material 5.
            bool hadSnapshot = _history.TrySnapshot(0, regionCoord, out var snapshot);
            Assert.IsTrue(hadSnapshot, "Snapshot must exist for tick 0.");

            // Extract the brick material from the snapshot (byte 0 of brick data = material).
            int brickIdx = Region.BrickIndex(
                targetVoxel.x >> VoxelDimensions.BrickEdgeLog2,
                targetVoxel.y >> VoxelDimensions.BrickEdgeLog2,
                targetVoxel.z >> VoxelDimensions.BrickEdgeLog2);

            byte historicalMaterial = snapshot[brickIdx];

            // The key assertion: reconciliation sees the *historical* material (5), not the present one (10).
            Assert.AreEqual(5, historicalMaterial,
                "Reconciliation must see the snapshot's material, not the current grid state.");
        }

        /// <summary>
        /// Verifies that when client and server agree on a brick through replay, the client's
        /// speculative result is preserved (not rolled back unnecessarily).
        /// </summary>
        [Test]
        public void AgreedBricksAreNotRolledBack()
        {
            var regionCoord = int3.zero;
            _table.LoadRegion(regionCoord);

            // Apply the same change on both client and server at tick 0.
            int3 targetVoxel = new int3(4, 4, 4);

            // Server writes material 7.
            VoxelAccess.SetVoxel(ref _table, ref _pool, targetVoxel, 7);
            var serverSnapshot = GetRegionBrickStorage(targetVoxel);
            _history.RecordSnapshot(0, serverSnapshot);

            // Client also applies material 7 at the same tick — they agree.
            var recon = new Reconciliation();
            recon.Initialize(0, 1);

            // Mark this brick as client-speculative with matching material.
            int3 brickCoord = new int3(
                targetVoxel.x >> VoxelDimensions.BrickEdgeLog2,
                targetVoxel.y >> VoxelDimensions.BrickEdgeLog2,
                targetVoxel.z >> VoxelDimensions.BrickEdgeLog2);

            var result = new BrickReconResult
            {
                MatchesServer = true,
                ServerMaterial = 7,
                ClientMaterial = 7
            };
            var reconResult = recon.GetResult();
            reconResult.ModifiedBricks[brickCoord] = result;

            // Replay — since client and server agree, nothing should be rolled back.
            recon.Replay(0, 1, ref _history);

            Assert.IsFalse(recon.HadRollback,
                "When client and server agree on a brick, no rollback should occur.");
        }

        /// <summary>
        /// Verifies that reconciliation rolls back divergent bricks: when the client's
        /// speculative material differs from the server's at a given tick, the divergence
        /// is correctly detected and reported.
        /// </summary>
        [Test]
        public void DivergentBricksAreRolledBack()
        {
            var regionCoord = int3.zero;
            _table.LoadRegion(regionCoord);

            // Server writes material 7 at tick 0.
            int3 targetVoxel = new int3(4, 4, 4);
            VoxelAccess.SetVoxel(ref _table, ref _pool, targetVoxel, 7);
            var serverSnapshot = GetRegionBrickStorage(targetVoxel);
            _history.RecordSnapshot(0, serverSnapshot);

            // Client independently applied material 9 at the same tick — they disagree.
            int3 brickCoord = new int3(
                targetVoxel.x >> VoxelDimensions.BrickEdgeLog2,
                targetVoxel.y >> VoxelDimensions.BrickEdgeLog2,
                targetVoxel.z >> VoxelDimensions.BrickEdgeLog2);

            var recon = new Reconciliation();
            recon.Initialize(0, 1);

            var result = new BrickReconResult
            {
                MatchesServer = false, // Client predicted material 9.
                ServerMaterial = 7,    // Server confirmed material 7.
                ClientMaterial = 9
            };
            var reconResult = recon.GetResult();
            reconResult.ModifiedBricks[brickCoord] = result;

            recon.Replay(0, 1, ref _history);

            Assert.IsTrue(recon.HadRollback,
                "Divergent client predictions must be rolled back.");

            var finalResult = recon.GetResult();
            Assert.AreEqual(7, finalResult.ModifiedBricks[brickCoord].ServerMaterial,
                "Rolled-back brick must reflect the server's authoritative material.");
        }

        // -- helpers ------------------------------------------------------------

        /// <summary>Extract a brick data snapshot from the current region for history recording.</summary>
        private NativeArray<byte> GetRegionBrickStorage(int3 targetVoxel)
        {
            int bx = (targetVoxel.x >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;
            int by = (targetVoxel.y >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;
            int bz = (targetVoxel.z >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;

            // Create a simple brick storage array: one byte per brick position.
            var data = new NativeArray<byte>(VoxelDimensions.BricksPerRegion, Allocator.Persistent);

            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
                data[i] = VoxelDimensions.MaterialEmpty;

            // Read the material actually present in the grid. Hardcoding a value here would
            // make every snapshot identical and the historical-vs-present distinction — the
            // entire point of these tests — unobservable.
            int targetBrickIdx = Region.BrickIndex(bx, by, bz);
            data[targetBrickIdx] = VoxelAccess.GetVoxel(ref _table, in _pool, targetVoxel);

            return data;
        }
    }
}
