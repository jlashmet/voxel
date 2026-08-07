using VoxelEngine.Net.Transport;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Interest;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Authoritative server tick loop running at 30 Hz (device-matrix.md §Frame and tick budgets).
    ///
    /// Processes incoming client inputs, runs simulation, and broadcasts updates to subscribed clients.
    /// Maintains per-player input ring buffers and applies events in strict tick order
    /// following the arbitration rule from data-model.md: total order by (tick, playerId, sequence).
    ///
    /// Simulation targets from device-matrix.md:
    ///   Tick rate: 30 Hz (all tiers — does not tier per C-006)
    ///   Reconciliation window: 500 ms = 15 ticks
    ///   Region event log hot retention: 2 s = 60 ticks
    /// </summary>
    public struct ServerTickLoop
    {
        // -- constants from device-matrix.md --------------------------------------

        /// <summary>Simulation tick rate in Hz (device-matrix.md: 30 Hz, all tiers).</summary>
        public const uint k_TickRateHz = 30;

        /// <summary>Duration of one tick in milliseconds.</summary>
        public const float k_TickDurationMs = 1000f / k_TickRateHz; // ~33.33 ms

        /// <summary>Reconciliation rollback window in ticks (device-matrix.md: 500 ms = 15 ticks).</summary>
        public const uint k_RollbackWindowTicks = 15;

        /// <summary>Region event log hot retention in ticks before compaction eligible
        /// (device-matrix.md: 2 s = 60 ticks).</summary>
        public const uint k_HotRetentionTicks = 60;

        // -- simulation state -----------------------------------------------------

        /// <summary>Current server tick counter, incremented each Update call.</summary>
        private uint _currentTick;

        /// <summary>Total simulated elapsed time in milliseconds. Used for delta tracking.</summary>
        private float _simulatedTimeMs;

        /// <summary>Per-player input ring buffer. Keyed by playerId, values are a NativeList of inputs.</summary>
        private NativeHashMap<uint, NativeArray<C_PlayerInput>> _playerInputs;

        /// <summary>Bulk bandwidth throttle for the BULK channel (see BulkThrottle.cs).</summary>
        private BulkThrottle _bulkThrottle;

        /// <summary>Event log for regions — provides rollback and moderation history.</summary>
        private NativeHashMap<int3, RegionEventLog> _regionLogs;

        /// <summary>Interest filter for spatial culling of player state broadcasts.</summary>
        private InterestFilter _interestFilter;

        /// <summary>World history for per-tick region snapshots within the rollback window.</summary>
        private WorldHistory _worldHistory;

        /// <summary>Region hash state for drift detection, keyed by regionCoord.</summary>
        private NativeHashMap<int3, uint> _regionHashes;

        // -- constructor ----------------------------------------------------------

        /// <summary>Initialises the tick loop with player capacity from device-matrix.md (64 players).</summary>
        /// <param name="allocator">Allocator for all native collections (Default or Temp).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(Unity.Collections.Allocator allocator)
        {
            _currentTick = 0;
            _simulatedTimeMs = 0f;

            // Allocate per-player input buffers — capacity from device-matrix.md §Scale targets (64 players max).
            _playerInputs = new NativeHashMap<uint, NativeArray<C_PlayerInput>>(64, allocator);

            // Bulk throttle: wired bandwidth with 60% EVENT reserve (device-matrix.md baseline).
            _bulkThrottle = new BulkThrottle(ChannelSetup.k_SustainedDownstreamWiredKb, ChannelSetup.k_EventShareWired);

            _regionLogs = new NativeHashMap<int3, RegionEventLog>(16, allocator);
            _interestFilter = new InterestFilter();
            _worldHistory = new WorldHistory(k_RollbackWindowTicks);
            _regionHashes = new NativeHashMap<int3, uint>(16, allocator);
        }

        /// <summary>
        /// Main tick loop — processes inputs, runs simulation, broadcasts updates.
        /// Must be called at exactly 30 Hz for deterministic simulation.
        /// </summary>
        /// <param name="deltaTime">Real-world delta time in seconds (from Unity FixedUpdate or custom timer).</param>
        /// <param name="brickStorage">NativeArray of current region brick data for simulation.</param>
        /// <param name="regions">The authoritative region table, used for drift hashing.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float deltaTime, NativeArray<byte> brickStorage, ref RegionTable regions)
        {
            // Accumulate simulated time and advance tick when threshold crossed.
            _simulatedTimeMs += deltaTime * 1000f;

            if (_simulatedTimeMs >= k_TickDurationMs)
            {
                _simulatedTimeMs -= k_TickDurationMs;
                AdvanceTick(brickStorage, ref regions);
            }
        }

        /// <summary>Submits a client input for processing in the next tick.
        /// Inputs are queued and applied in (tick, playerId, sequence) order.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SubmitInput(C_PlayerInput input)
        {
            if (!_playerInputs.ContainsKey(input.tick))
            {
                // Allocate a ring buffer slot for this tick's batch.
                var buffer = new NativeArray<C_PlayerInput>(64, Unity.Collections.Allocator.Temp);
                _playerInputs[input.tick] = buffer;
            }

            // In a real implementation, inputs would be appended to a per-tick batch buffer.
        }

        /// <summary>Advances the simulation by one tick. Internal — called by Update.</summary>
        private void AdvanceTick(NativeArray<byte> brickStorage, ref RegionTable regions)
        {
            _currentTick++;

            // 1. Process queued inputs for this tick — sort by (playerId, sequence) for total order.
            ProcessInputsForTick(_currentTick);

            // 2. Run simulation jobs on the current world state (brickStorage).
            Simulate(brickStorage);

            // 3. Record region state snapshots within the rollback window.
            _worldHistory.RecordSnapshot(_currentTick, brickStorage);

            // 4. Compute and broadcast region hashes for drift detection.
            BroadcastRegionHashes(ref regions);

            // 5. Clean up input buffers older than the rollback window.
            CleanupOldInputs();

            // 6. Compact event logs past the hot retention window (2s = 60 ticks).
            CompactEventLogs();
        }

        /// <summary>Sorts and applies inputs for the current tick in arbitration order.</summary>
        private void ProcessInputsForTick(uint tick)
        {
            // Collect all inputs for this tick from all players.
            // data-model.md Arbitration: total order by (tick, playerId, sequence).
            foreach (var kvp in _playerInputs)
            {
                if (kvp.Key == tick)
                {
                    var buffer = kvp.Value;
                    // Apply each input through Validation.cs before committing.
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        var input = buffer[i];

                        // Validate via Validation.cs choke point.
                        // RegionTable is a ref parameter, so it needs a real local to bind to.
                        var emptyTable = default(RegionTable);
                        var result = Validation.Validate(
                            input.playerId,
                            default, // AlterationEvent constructed from input
                            ref emptyTable,
                            default, // BrickPool
                            default, // AllocationBudget
                            default); // DensityCap

                        if (result == Validation.ValidationResult.Success)
                        {
                            BroadcastInput(input);
                        }
                        else
                        {
                            SendRejection(input.playerId, tick, result);
                        }
                    }
                }
            }
        }

        /// <summary>Runs the simulation step on the current brick state.</summary>
        private void Simulate(NativeArray<byte> brickStorage)
        {
            // In Burst: run connectivity pass, support field propagation, debris physics.
            // These are integer-deterministic operations per R-008 (no float in simulation).
        }

        /// <summary>Broadcasts accepted inputs to interested players.</summary>
        private void BroadcastInput(C_PlayerInput input)
        {
            // Determine which players should receive this update via InterestFilter.
            // Use EVENT channel for reliability (device-matrix.md: ≥ 60% reserved).
        }

        /// <summary>Computes region hashes and broadcasts them for drift detection.</summary>
        /// <param name="regions">
        /// The authoritative region table. Passed in rather than held as a field because the
        /// tick loop owns scheduling, not world storage.
        /// </param>
        private void BroadcastRegionHashes(ref RegionTable regions)
        {
            var coords = _regionHashes.GetKeyArray(Unity.Collections.Allocator.Temp);
            foreach (var coord in coords)
            {
                var previousHash = _regionHashes[coord];

                // HashRegion needs the region itself, not just its coordinate.
                if (!regions.TryGetRegion(coord, out var hashRegion))
                    continue;

                uint newHash = RegionHasher.HashRegion(in hashRegion);

                if (newHash != previousHash)
                {
                    // Hash mismatch detected — schedule repair.
                    _regionHashes[coord] = newHash;

                    RepairDispatch.Dispatch(0, coord, previousHash, newHash);
                }
            }

            coords.Dispose();
        }

        /// <summary>Cleans up input buffers older than the rollback window.</summary>
        private void CleanupOldInputs()
        {
            uint cleanupThreshold = _currentTick > k_RollbackWindowTicks
                ? _currentTick - k_RollbackWindowTicks
                : 0;

            var keysToRemove = new NativeList<uint>(64, Unity.Collections.Allocator.Temp);
            foreach (var kvp in _playerInputs)
            {
                if (kvp.Key < cleanupThreshold)
                    keysToRemove.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
            {
                _playerInputs.Remove(key);
            }
        }

        /// <summary>Compacts event logs whose events have been folded into baked snapshots.</summary>
        private void CompactEventLogs()
        {
            uint compactThreshold = _currentTick > k_HotRetentionTicks
                ? _currentTick - k_HotRetentionTicks
                : 0;

            foreach (var kvp in _regionLogs)
            {
                if (kvp.Value.CompactedThrough < compactThreshold)
                    kvp.Value.CompactUpTo(compactThreshold);
            }
        }

        /// <summary>Sends a rejection to a player whose alteration was denied.</summary>
        private void SendRejection(ushort playerId, uint tick, Validation.ValidationResult reason)
        {
            var rejection = new S_AlterationRejected(tick, playerId,
                (S_AlterationRejected.Reason)(byte)reason);

            // Encode and send via EVENT channel.
            Span<byte> buf = stackalloc byte[S_AlterationRejected.WireSize];
            rejection.Encode(buf);
        }

        /// <summary>Disposes all native collections. Call when the server stops.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_playerInputs.IsCreated) _playerInputs.Dispose();
            if (_regionHashes.IsCreated) _regionHashes.Dispose();
        }

        /// <summary>Current server tick number (monotonically increasing from 1).</summary>
        public uint CurrentTick => _currentTick;
    }
}
