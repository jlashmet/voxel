using Unity.Mathematics;
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

            // Keep the high-water bookkeeping deterministic and engine-independent. Presentation-layer
            // diagnostics may observe the storage contract, but deterministic storage must not depend on
            // UnityEngine logging just to report progress.
            int resident = _table.ResidentCount;
            while (_nextResidentLogThreshold <= resident)
                _nextResidentLogThreshold += 16;
        }

        public StoragePressure Pressure
        {
            get
            {
                long bytesPerMixedBlock = VoxelDimensions.BytesPerMixedBrick;
                int allocatedBlocks = _pool.AllocatedCount;
                int capacityBlocks = _pool.Capacity;
                int criticalAllocatedBlocks = capacityBlocks - (capacityBlocks >> 14);

                // Retain deterministic pressure-bucket bookkeeping without coupling simulation to a
                // presentation/logger implementation.
                _lastPressureBucket = capacityBlocks > 0
                    ? (int)((long)allocatedBlocks * 10L / capacityBlocks)
                    : 0;

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

            int mixedBefore = _pool.AllocatedCount;
            _table.EvictRegion(regionCoord, ref _pool);
            int mixedAfter = _pool.AllocatedCount;
            int reclaimed = math.max(0, mixedBefore - mixedAfter);

            _regionsEvicted++;
            _mixedBricksReclaimed += reclaimed;
            if (reclaimed == 0) _evictionsWithoutImmediateReclaim++;

            return true;
        }

        public bool TryGetNextResidentCoord(ref int cursor, out int3 regionCoord) =>
            _table.TryGetNextResidentCoord(ref cursor, out regionCoord);
    }
}
