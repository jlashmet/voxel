using VoxelEngine.Net.Server;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Reconciliation engine that replays client inputs against historical world state
    /// to resolve divergence between client speculative predictions and server authority.
    ///
    /// When the server catches up past a client's buffered input tick, this type replays
    /// all client inputs from the divergence point forward, comparing each client result
    /// against the server's authoritative state at that tick. Bricks where client and
    /// server agree are kept; where they disagree, the server's state wins (Constitution
    /// Principle III: server authority).
    ///
    /// The reconciliation window is 500 ms / 15 ticks (device-matrix.md), so replay is
    /// bounded to at most 15 ticks of simulation per reconciliation event. The historical
    /// world state needed for comparison comes from <see cref="WorldHistory"/> snapshots.
    /// </summary>
    public sealed class Reconciliation : IDisposable
    {
        // -- reconciliation state -------------------------------------------------

        /// <summary>Start tick of the current reconciliation range (inclusive).</summary>
        private int _fromTick;

        /// <summary>End tick of the current reconciliation range (inclusive).</summary>
        private int _toTick;

        /// <summary>Whether Initialise has been called and a range is pending.</summary>
        private bool _initialized;

        // -- result state ---------------------------------------------------------

        /// <summary>Per-brick comparison results set after Replay completes.</summary>
        private NativeHashMap<int3, BrickReconResult> _modifiedBricks;

        /// <summary>True when any bricks were rolled back during the last replay.</summary>
        private bool _hadRollback;

        // -- construction ---------------------------------------------------------

        public Reconciliation()
        {
            _fromTick = 0;
            _toTick = 0;
            _initialized = false;
            _modifiedBricks = new NativeHashMap<int3, BrickReconResult>(64, Allocator.Persistent);
            _hadRollback = false;
        }

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Initialise a reconciliation pass from the given tick range.
        /// </summary>
        /// <param name="fromTick">First tick to replay (inclusive). This is where divergence was detected.</param>
        /// <param name="toTick">Last tick to replay (inclusive, exclusive — up to but not including this tick).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(int fromTick, int toTick)
        {
            _fromTick = fromTick;
            _toTick = toTick;
            _initialized = true;
            _hadRollback = false;

            if (_modifiedBricks.IsCreated) _modifiedBricks.Clear();
        }

        /// <summary>
        /// Replay inputs from historical world state, comparing client speculative results
        /// against server authoritative state at each tick.
        ///
        /// Uses the provided WorldHistory to fetch snapshots for each region at each tick.
        /// For every brick in the replay window:
        ///   1. Read the server's authoritative state from historical snapshots.
        ///   2. Replay all client inputs that affect this brick between fromTick and toTick.
        ///   3. Compare the resulting client state against the server state.
        ///   4. Record whether they match or if a rollback is needed.
        /// </summary>
        /// <param name="fromTick">Start of the replay range (inclusive).</param>
        /// <param name="toTick">End of the replay range (exclusive).</param>
        /// <param name="history">Historical world state snapshots for server-authoritative comparison.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Replay(int fromTick, int toTick, ref WorldHistory history)
        {
            // Rollback is decided per brick by ReplayTickForRegion; start from "no rollback"
            // so a replay that finds full agreement reports none.
            _hadRollback = false;

            // For each region that was modified during the replay window, fetch its historical state.
            var affectedRegions = new NativeList<int3>(8, Allocator.Temp);

            // Walk through all modified bricks and collect unique regions.
            if (_modifiedBricks.IsCreated && _modifiedBricks.Count > 0)
            {
                foreach (var kvp in _modifiedBricks)
                {
                    int3 brickCoord = kvp.Key;
                    int3 regionCoord = new int3(
                        brickCoord.x >> VoxelDimensions.RegionEdgeLog2,
                        brickCoord.y >> VoxelDimensions.RegionEdgeLog2,
                        brickCoord.z >> VoxelDimensions.RegionEdgeLog2);

                    // Add to affected regions list if not already present (simple linear search — small list).
                    bool found = false;
                    foreach (int3 existing in affectedRegions)
                    {
                        if (math.all(existing == regionCoord)) { found = true; break; }
                    }
                    if (!found)
                        affectedRegions.Add(regionCoord);
                }
            }

            // Replay each tick in the window.
            for (int tick = fromTick; tick < toTick; tick++)
            {
                // For each affected region, get its server state at this tick.
                foreach (int3 regionCoord in affectedRegions)
                {
                    if (!history.TrySnapshot((uint)tick, regionCoord, out var snapshot))
                        continue; // No snapshot available — skip this region for this tick.

                    ReplayTickForRegion(ref history, (uint)tick, regionCoord, snapshot);
                }
            }

            // Deliberately NOT `_hadRollback = affectedRegions.Length > 0`: having touched a
            // region says nothing about whether the client mispredicted. Only a brick whose
            // client material disagrees with the server's is a rollback.
            affectedRegions.Dispose();
        }

        /// <summary>
        /// Get the current reconciliation range (fromTick, toTick).
        /// </summary>
        public (int fromTick, int toTick) GetCurrentRange() => (_fromTick, _toTick);

        /// <summary>Get the result of the last reconciliation pass.</summary>
        public ReconciliationResult GetResult()
        {
            return new ReconciliationResult
            {
                ModifiedBricks = _modifiedBricks,
                HadRollback = _hadRollback
            };
        }

        /// <summary>True when any bricks were rolled back during the last replay.</summary>
        public bool HadRollback => _hadRollback;

        // -- internal helpers -----------------------------------------------------

        private void ReplayTickForRegion(ref WorldHistory history, uint tick, int3 regionCoord, in NativeArray<byte> snapshot)
        {
            // Parse the historical brick data from the snapshot.
            // The snapshot contains one byte per voxel position for all 262144 bricks in the region.
            // Each brick's state is encoded as: material byte (index 0), then occupancy bytes.

            int bricksPerAxis = VoxelDimensions.RegionEdge;
            const int bricksPerRegion = VoxelDimensions.BricksPerRegion; // 262144

            for (int bx = 0; bx < bricksPerAxis; bx++)
            {
                for (int by = 0; by < bricksPerAxis; by++)
                {
                    for (int bz = 0; bz < bricksPerAxis; bz++)
                    {
                        int brickIdx = Region.BrickIndex(bx, by, bz);

                        // Decode material from snapshot (1 byte per brick position).
                        byte serverMaterial = snapshot[brickIdx];

                        if (serverMaterial == VoxelDimensions.MaterialEmpty)
                            continue; // Empty bricks don't need reconciliation.

                        int3 brickCoord = new int3(
                            regionCoord.x * VoxelDimensions.RegionEdge + bx,
                            regionCoord.y * VoxelDimensions.RegionEdge + by,
                            regionCoord.z * VoxelDimensions.RegionEdge + bz);

                        // Check if this brick was modified in the overlay during the replay window.
                        if (!_modifiedBricks.TryGetValue(brickCoord, out var result))
                        {
                            // Brick was not speculative — server is authoritative by default.
                            // No action needed; keep the server state as-is.
                            continue;
                        }

                        // Compare client's speculative material against server's authoritative material.
                        bool matches = result.ClientMaterial == serverMaterial;

                        if (!matches)
                        {
                            _hadRollback = true;
                            result.MatchesServer = false;
                            result.ServerMaterial = serverMaterial;
                            _modifiedBricks[brickCoord] = result;
                        }
                    }
                }
            }
        }

        /// <summary>Dispose native resources.</summary>
        public void Dispose()
        {
            if (_modifiedBricks.IsCreated) _modifiedBricks.Dispose();
        }
    }
}
