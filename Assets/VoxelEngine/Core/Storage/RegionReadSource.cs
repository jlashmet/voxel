using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// Current Storage implementation of the public region-read boundary.
    ///
    /// This type moves to Storage.Runtime when the physical storage files leave Core. It owns
    /// no memory: RegionTable/BrickPool native containers are borrowed and remain owned by the
    /// existing world-storage lifecycle.
    /// </summary>
    public sealed class RegionReadSource : IRegionReadSource
    {
        private RegionTable _table;
        private BrickPool _pool;
        private readonly VoxelChangeJournal _changes;

        public RegionReadSource(in RegionTable table, in BrickPool pool,
                                VoxelChangeJournal changes = null)
        {
            _table = table;
            _pool = pool;
            _changes = changes;
        }

        public ulong Version => _changes?.CurrentVersion ?? 0UL;

        public bool IsRegionResident(int3 regionCoord) => _table.IsResident(regionCoord);

        public bool TryAcquireRegion(int3 regionCoord, out RegionReadView view)
        {
            if (!_table.TryGetRegion(regionCoord, out Region region))
            {
                view = default;
                return false;
            }

            // BrickRef is a private one-int encoding. Reinterpretation avoids a 1 MiB region
            // copy while keeping the encoding inaccessible to consumers of RegionReadView.
            NativeArray<int> encodedRefs = region.BrickRefs.Reinterpret<int>();
            view = new RegionReadView(
                region.Coord,
                Version,
                encodedRefs,
                region.HardSurfaceWords,
                region.OccupancyMips,
                region.MaterialMips,
                region.MipLevelCount,
                _pool.Voxels,
                _pool.SurfaceSemantics,
                _pool.BoundarySamples,
                _pool.Occupancy);
            return true;
        }
    }
}
