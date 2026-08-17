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

        public RegionResidencyStore(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        /// <summary>
        /// Refreshes borrowed native-container handles after the owning world mutates or replaces
        /// its RegionTable/BrickPool structs. This store never owns or disposes those containers.
        /// </summary>
        public void Refresh(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        public StoragePressure Pressure
        {
            get
            {
                long bytesPerMixedBlock = VoxelDimensions.BytesPerMixedBrick;
                int criticalAllocatedBlocks = _pool.Capacity - (_pool.Capacity >> 14);
                return new StoragePressure(
                    _pool.AllocatedCount * bytesPerMixedBlock,
                    _pool.Capacity * bytesPerMixedBlock,
                    criticalAllocatedBlocks * bytesPerMixedBlock,
                    _pool.IsUnderPressure);
            }
        }

        public bool IsRegionResident(int3 regionCoord) => _table.IsResident(regionCoord);

        public void EnsureRegionResident(int3 regionCoord) => _table.LoadRegion(regionCoord);

        public bool EvictRegion(int3 regionCoord)
        {
            if (!_table.IsResident(regionCoord)) return false;
            _table.EvictRegion(regionCoord, ref _pool);
            return true;
        }

        public bool TryGetNextResidentCoord(ref int cursor, out int3 regionCoord) =>
            _table.TryGetNextResidentCoord(ref cursor, out regionCoord);
    }
}
