using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
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

            // Snapshot decode replaces physical refs in bulk and intentionally bypasses the
            // per-block mutation API. Rebuild the compact local occupancy summaries once here so
            // asynchronous consumers never inherit stale metadata from the previous region image.
            RebuildBlockSummaries(ref region);
            _table.CommitRegion(in region);

            return SemanticRegionHasher.HashRegion(in region, in _pool) == expectedSemanticHash;
        }

        private void RebuildBlockSummaries(ref Region region)
        {
            for (int blockIndex = 0; blockIndex < VoxelReadGrid.BlocksPerRegion; blockIndex++)
            {
                BrickRef block = region.BrickRefs[blockIndex];
                if (block.IsUniform)
                {
                    bool solid = block.UniformMaterial != VoxelGrid.MaterialEmpty;
                    region.SetBlockOccupancySummary(blockIndex, solid, solid);
                    continue;
                }

                int occupancyOffset = _pool.OccupancyOffset(block.PoolIndex);
                bool occupied = false;
                bool fullySolid = true;
                for (int i = 0; i < VoxelReadGrid.OccupancyWordsPerBlock; i++)
                {
                    ulong word = _pool.Occupancy[occupancyOffset + i];
                    occupied |= word != 0UL;
                    fullySolid &= word == ulong.MaxValue;
                }
                region.SetBlockOccupancySummary(blockIndex, occupied, fullySolid);
            }
        }
    }
}
