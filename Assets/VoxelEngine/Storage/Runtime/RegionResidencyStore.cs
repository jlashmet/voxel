using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Current physical Storage implementation of the region-residency contract.
    /// Moves to Storage.Runtime when RegionTable/BrickPool leave Core.
    /// </summary>
    public sealed class RegionResidencyStore : IRegionResidencyStore
    {
        private RegionTable _table;
        private BrickPool _pool;
        private int _ensureLoads;
        private int _regionsEvicted;
        private long _mixedBricksReclaimed;
        private int _evictionsWithoutImmediateReclaim;
        private int _lastPressureBucket = -1;
        private int _nextResidentLogThreshold = 16;

        public RegionResidencyStore(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
            while (_nextResidentLogThreshold <= _table.ResidentCount)
                _nextResidentLogThreshold += 16;
        }

        /// <summary>
        /// Refreshes borrowed native-container handles after the owning world mutates or replaces
        /// its RegionTable/BrickPool structs. This store never owns or disposes those containers.
        /// </summary>
        public void Refresh(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;

            // Terrain generation currently loads through RegionTable directly, while other callers
            // use EnsureRegionResident. Observe the authoritative table itself so diagnostics do not
            // accidentally under-count the dominant load path. Report each 16-region high-water
            // crossing with the mixed-pool occupancy at that moment.
            int resident = _table.ResidentCount;
            if (resident >= _nextResidentLogThreshold)
            {
                Debug.Log(
                    $"[VoxelResidency] residentRegions={resident:N0} " +
                    $"mixed={_pool.AllocatedCount:N0}/{_pool.Capacity:N0} " +
                    $"evicted={_regionsEvicted:N0} reclaimedMixed={_mixedBricksReclaimed:N0}");
                while (_nextResidentLogThreshold <= resident)
                    _nextResidentLogThreshold += 16;
            }
        }

        public StoragePressure Pressure
        {
            get
            {
                long bytesPerMixedBlock = VoxelDimensions.BytesPerMixedBrick;
                int allocatedBlocks = _pool.AllocatedCount;
                int capacityBlocks = _pool.Capacity;
                int criticalAllocatedBlocks = capacityBlocks - (capacityBlocks >> 14);

                // Diagnostics are intentionally bucketed rather than emitted per allocation. The
                // showcase can allocate millions of mixed bricks, so logging each mutation would
                // perturb the very streaming/eviction behaviour this instrumentation measures.
                int pressureBucket = capacityBlocks > 0
                    ? (int)((long)allocatedBlocks * 10L / capacityBlocks)
                    : 0;
                if (pressureBucket != _lastPressureBucket)
                {
                    _lastPressureBucket = pressureBucket;
                    Debug.Log(
                        $"[VoxelResidency] pressure={pressureBucket * 10}% " +
                        $"mixed={allocatedBlocks:N0}/{capacityBlocks:N0} " +
                        $"residentRegions={_table.ResidentCount:N0} " +
                        $"ensureLoads={_ensureLoads:N0} evicted={_regionsEvicted:N0} " +
                        $"reclaimedMixed={_mixedBricksReclaimed:N0} " +
                        $"zeroImmediateReclaim={_evictionsWithoutImmediateReclaim:N0}");
                }

                return new StoragePressure(
                    allocatedBlocks * bytesPerMixedBlock,
                    capacityBlocks * bytesPerMixedBlock,
                    criticalAllocatedBlocks * bytesPerMixedBlock,
                    _pool.IsUnderPressure);
            }
        }

        public bool IsRegionResident(int3 regionCoord) => _table.IsResident(regionCoord);

        public void EnsureRegionResident(int3 regionCoord)
        {
            bool alreadyResident = _table.IsResident(regionCoord);
            _table.LoadRegion(regionCoord);
            if (alreadyResident) return;

            _ensureLoads++;
            Refresh(in _table, in _pool);
        }

        public bool EvictRegion(int3 regionCoord)
        {
            if (!_table.IsResident(regionCoord)) return false;

            int residentBefore = _table.ResidentCount;
            int mixedBefore = _pool.AllocatedCount;
            _table.EvictRegion(regionCoord, ref _pool);
            int mixedAfter = _pool.AllocatedCount;
            int reclaimed = math.max(0, mixedBefore - mixedAfter);

            _regionsEvicted++;
            _mixedBricksReclaimed += reclaimed;
            if (reclaimed == 0) _evictionsWithoutImmediateReclaim++;

            // Every 16 evictions gives enough temporal resolution to see whether traversal is
            // actually returning memory, without creating a log line for every region. Once the
            // pool is above 90%, log every eviction so the final approach to exhaustion is visible.
            bool highPressure = _pool.Capacity > 0
                && (long)mixedAfter * 10L >= (long)_pool.Capacity * 9L;
            if ((_regionsEvicted & 15) == 0 || highPressure)
            {
                Debug.Log(
                    $"[VoxelResidency] evict#{_regionsEvicted:N0} rc={regionCoord} " +
                    $"resident={residentBefore:N0}->{_table.ResidentCount:N0} " +
                    $"mixed={mixedBefore:N0}->{mixedAfter:N0} reclaimed={reclaimed:N0} " +
                    $"totalReclaimed={_mixedBricksReclaimed:N0} " +
                    $"zeroImmediateReclaim={_evictionsWithoutImmediateReclaim:N0}");
            }

            return true;
        }

        public bool TryGetNextResidentCoord(ref int cursor, out int3 regionCoord) =>
            _table.TryGetNextResidentCoord(ref cursor, out regionCoord);
    }
}
