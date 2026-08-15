using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// Current Storage implementation of authoritative semantic snapshot replacement. This type
    /// moves with the physical Storage implementation during the final Core dissolution.
    /// </summary>
    public sealed class RegionSnapshotMutationStore : IRegionSnapshotMutationStore
    {
        private RegionTable _table;
        private BrickPool _pool;

        public RegionSnapshotMutationStore(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        public void Refresh(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        public bool TryApplySemanticSnapshot(
            int3 regionCoord,
            byte[] snapshotBytes,
            uint expectedSemanticHash,
            bool createIfMissing)
        {
            if (snapshotBytes == null ||
                !SemanticRegionSnapshotCodec.TryComputeSemanticHash(
                    regionCoord,
                    snapshotBytes,
                    out uint encodedHash) ||
                encodedHash != expectedSemanticHash)
                return false;

            if (!_table.IsResident(regionCoord))
            {
                if (!createIfMissing)
                    return false;

                Region created = _table.LoadRegion(regionCoord);
                if (!created.BrickRefs.IsCreated)
                    return false;
            }

            if (!SemanticRegionSnapshotCodec.TryApply(
                    ref _table,
                    ref _pool,
                    regionCoord,
                    snapshotBytes) ||
                !_table.TryGetRegion(regionCoord, out Region region))
                return false;

            return SemanticRegionHasher.HashRegion(in region, in _pool) == expectedSemanticHash;
        }
    }
}
