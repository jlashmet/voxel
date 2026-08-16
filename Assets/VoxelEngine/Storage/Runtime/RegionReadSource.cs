using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Current Storage implementation of the public region-read, semantic-snapshot and focused
    /// world-surface boundaries.
    ///
    /// This type moves to Storage.Runtime when the physical storage files leave Core. It owns
    /// no memory: RegionTable/BrickPool native containers are borrowed and remain owned by the
    /// existing world-storage lifecycle.
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
        /// Refreshes borrowed owner handles without allocating a new source object. This is used
        /// by transitional composition/render wiring until Storage.Runtime owns construction.
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

        public bool CopyResidentRegionCoords(ref int cursor, NativeArray<int3> destination,
                                             out int count) =>
            _table.CopyResidentCoords(ref cursor, destination, out count);

        public bool TryAcquireRegionContainingBlock(int3 worldBlockCoord, out RegionReadView view)
        {
            int3 regionCoord = worldBlockCoord >> VoxelDimensions.RegionEdgeLog2;
            return TryAcquireRegion(regionCoord, out view);
        }

        public bool TryPinWorldBlock(int3 worldBlockCoord, out PinnedVoxelReadBlock block)
        {
            int3 regionCoord = worldBlockCoord >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!_table.TryGetRegion(regionCoord, out Region region))
            {
                block = default;
                return false;
            }

            int3 local = worldBlockCoord & VoxelReadGrid.BlocksPerRegionEdgeMask;
            BrickRef brick = region.BrickRefs[Region.BrickIndex(local.x, local.y, local.z)];
            if (brick.IsEmpty)
            {
                block = PinnedVoxelReadBlock.Empty;
                return true;
            }
            if (brick.IsUniform)
            {
                block = PinnedVoxelReadBlock.Uniform(brick.UniformMaterial);
                return true;
            }

            if (!_pool.TryPin(brick.PoolIndex, out BrickPool.PinToken physicalPin))
            {
                block = default;
                return false;
            }
            var apiPin = new VoxelReadPinToken(physicalPin.BrickIndex,
                                               physicalPin.Generation);
            block = new PinnedVoxelReadBlock(
                VoxelReadBlockKind.Mixed,
                VoxelGrid.MaterialEmpty,
                brick.PoolIndex * VoxelReadGrid.VoxelsPerBlock,
                _pool.Voxels,
                _pool.SurfaceSemantics,
                _pool.BoundarySamples,
                in apiPin);
            return true;
        }

        public void ReleasePinnedWorldBlock(in VoxelReadPinToken token)
        {
            if (!token.IsValid) return;
            var physicalPin = new BrickPool.PinToken(token.Slot, token.Generation);
            _pool.Unpin(in physicalPin);
        }

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

        public bool TryPinRegionBlockRefs(int3 regionCoord, out PinnedRegionBlockRefs pinned)
        {
            if (!_table.TryPinRegion(regionCoord, out Region region,
                                     out int slot, out uint generation, out uint revision))
            {
                pinned = default;
                return false;
            }

            var token = new VoxelRegionPinToken(slot, generation, revision);
            pinned = new PinnedRegionBlockRefs(
                regionCoord, region.BrickRefs.Reinterpret<int>(), in token);
            return true;
        }

        public bool IsPinnedRegionCurrent(in VoxelRegionPinToken token) =>
            token.IsValid
            && _table.IsRegionPinCurrent(token.Slot, token.Generation, token.Revision);

        public void ReleasePinnedRegion(in VoxelRegionPinToken token)
        {
            if (!token.IsValid) return;
            _table.UnpinRegion(token.Slot, token.Generation, ref _pool);
        }

        public bool TryCopyBlockSummary(int3 regionCoord,
                                        NativeArray<ulong> occupiedWords,
                                        NativeArray<ulong> fullySolidWords,
                                        out ulong version)
        {
            version = Version;
            int wordCount = VoxelReadGrid.BlockSummaryWordCount;
            if (occupiedWords.Length < wordCount || fullySolidWords.Length < wordCount
                || !_table.TryGetRegion(regionCoord, out Region region)
                || !region.OccupiedBlockWords.IsCreated || !region.FullySolidBlockWords.IsCreated)
                return false;

            NativeArray<ulong>.Copy(region.OccupiedBlockWords, 0, occupiedWords, 0, wordCount);
            NativeArray<ulong>.Copy(region.FullySolidBlockWords, 0, fullySolidWords, 0, wordCount);

            // Authoritative mutation is currently serialized by the world owner, but retain a
            // version check at this API boundary so the snapshot remains correct if Storage later
            // permits concurrent publication. The caller simply retries a rejected copy.
            return Version == version;
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
