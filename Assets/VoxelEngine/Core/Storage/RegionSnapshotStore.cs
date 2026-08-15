using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// Physical Storage implementation of semantic snapshot capture/replacement.
    /// Networking and other clients see only Storage.Api snapshot capabilities; RegionTable,
    /// BrickPool and the semantic codec remain owned by Storage.
    /// </summary>
    public sealed class RegionSnapshotStore : IRegionSnapshotSource, IRegionSnapshotMutationStore
    {
        private RegionTable _table;
        private BrickPool _pool;

        public RegionSnapshotStore(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        /// <summary>
        /// Refresh borrowed native-container handles after an owning world replaces either struct.
        /// </summary>
        public void Refresh(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        public RegionSnapshotCaptureResult CaptureSemanticSnapshot(
            int3 regionCoord,
            int maxBytes,
            out RegionSemanticSnapshot snapshot)
        {
            snapshot = default;
            if (!_table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                return RegionSnapshotCaptureResult.NotResident;

            if (maxBytes <= 0 ||
                !SemanticRegionSnapshotCodec.TryEncode(in region, in _pool, maxBytes, out byte[] bytes))
                return RegionSnapshotCaptureResult.TooLarge;

            uint semanticHash = SemanticRegionHasher.HashRegion(in region, in _pool);
            snapshot = new RegionSemanticSnapshot(regionCoord, semanticHash, bytes);
            return RegionSnapshotCaptureResult.Ok;
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

            if (!_table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
            {
                if (!createIfMissing)
                    return false;
                _table.LoadRegion(regionCoord);
            }

            if (!SemanticRegionSnapshotCodec.TryApply(
                    ref _table,
                    ref _pool,
                    regionCoord,
                    snapshotBytes))
                return false;

            return _table.TryGetRegion(regionCoord, out Region applied) &&
                   applied.BrickRefs.IsCreated &&
                   SemanticRegionHasher.HashRegion(in applied, in _pool) == expectedSemanticHash;
        }
    }
}
