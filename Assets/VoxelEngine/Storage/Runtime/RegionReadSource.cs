using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Storage implementation of the public region-read, semantic-snapshot and focused
    /// world-surface boundaries. It owns no memory: RegionTable/BrickPool native containers are
    /// borrowed and remain owned by the world-storage lifecycle.
    /// </summary>
    public sealed class RegionReadSource : IRegionReadSource, IVoxelSurfaceQuery, IRegionSnapshotSource
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

        /// <summary>
        /// Refreshes borrowed owner handles without allocating a new source object.
        /// </summary>
        public void Refresh(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        public ulong Version => _changes?.CurrentVersion ?? 0UL;

        public bool IsRegionResident(int3 regionCoord) => _table.IsResident(regionCoord);

        public NativeArray<int3> GetResidentRegionCoords(Allocator allocator) =>
            _table.GetResidentCoords(allocator);

        public bool TryAcquireRegionContainingBlock(int3 worldBlockCoord, out RegionReadView view)
        {
            int3 regionCoord = worldBlockCoord >> VoxelDimensions.RegionEdgeLog2;
            return TryAcquireRegion(regionCoord, out view);
        }

        public bool TryAcquireRegion(int3 regionCoord, out RegionReadView view)
        {
            if (!_table.TryGetRegion(regionCoord, out Region region))
            {
                view = default;
                return false;
            }

            // BrickRef is a private one-int encoding. Reinterpretation avoids a 1 MiB region copy;
            // the API factory accepts the backing descriptor but RegionReadView never exposes it to
            // consumers.
            NativeArray<int> encodedRefs = region.BrickRefs.Reinterpret<int>();
            view = RegionReadViewFactory.CreateBorrowed(
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

        public RegionSnapshotCaptureResult CaptureSemanticSnapshot(
            int3 regionCoord,
            int maxBytes,
            out RegionSemanticSnapshot snapshot)
        {
            snapshot = default;
            if (!_table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                return RegionSnapshotCaptureResult.NotResident;

            if (!SemanticRegionSnapshotCodec.TryEncode(in region, in _pool, maxBytes, out byte[] bytes))
                return RegionSnapshotCaptureResult.TooLarge;

            uint semanticHash = SemanticRegionHasher.HashRegion(in region, in _pool);
            snapshot = new RegionSemanticSnapshot(regionCoord, semanticHash, bytes);
            return RegionSnapshotCaptureResult.Ok;
        }

        public bool TryRead(int3 worldVoxel, out VoxelCell cell)
        {
            VoxelAccess.Decompose(worldVoxel, out int3 regionCoord, out _, out _);
            if (!_table.IsResident(regionCoord))
            {
                cell = default;
                return false;
            }

            cell = VoxelAccess.GetCell(ref _table, in _pool, worldVoxel);
            return true;
        }

        public bool TryFindTopSolid(int x, int z, int minY, int maxY,
                                    out int y, out VoxelCell cell)
        {
            return TryFindTop(x, z, minY, maxY, 0, 0, false, out y, out cell);
        }

        public bool TryFindTopSolidExcluding(int x, int z, int minY, int maxY,
                                             byte excludedMaterialA, byte excludedMaterialB,
                                             out int y, out VoxelCell cell)
        {
            return TryFindTop(x, z, minY, maxY,
                              excludedMaterialA, excludedMaterialB, true,
                              out y, out cell);
        }

        private bool TryFindTop(int x, int z, int minY, int maxY,
                                byte excludedMaterialA, byte excludedMaterialB,
                                bool hasExclusions, out int y, out VoxelCell cell)
        {
            if (maxY < minY)
            {
                y = default;
                cell = default;
                return false;
            }

            for (int candidateY = maxY; candidateY >= minY; candidateY--)
            {
                int3 worldVoxel = new int3(x, candidateY, z);
                VoxelAccess.Decompose(worldVoxel, out int3 regionCoord, out _, out _);
                if (!_table.IsResident(regionCoord)) continue;

                VoxelCell candidate = VoxelAccess.GetCell(ref _table, in _pool, worldVoxel);
                if (!candidate.IsSolid) continue;
                if (hasExclusions && (candidate.BaseMaterialId == excludedMaterialA
                                   || candidate.BaseMaterialId == excludedMaterialB)) continue;

                y = candidateY;
                cell = candidate;
                return true;
            }

            y = default;
            cell = default;
            return false;
        }
    }
}
