using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Physical implementation of block-granular authoritative mutation. Public mutation leases
    /// contain only logical block slices; region/block/pool rollback state stays private here.
    /// </summary>
    public sealed class RegionMutationStore : IRegionMutationStore
    {
        private readonly Dictionary<ulong, PendingMutation> _pendingMutations = new();
        private RegionTable _table;
        private BrickPool _pool;
        private ulong _nextLeaseToken = 1UL;

        private readonly struct PendingMutation
        {
            public readonly int3 RegionCoord;
            public readonly int BlockIndex;
            public readonly int OriginalEncodedRef;
            public readonly int PoolIndex;
            public readonly bool MaterializedUniform;
            public readonly bool MetadataChanged;

            public PendingMutation(
                int3 regionCoord,
                int blockIndex,
                int originalEncodedRef,
                int poolIndex,
                bool materializedUniform,
                bool metadataChanged)
            {
                RegionCoord = regionCoord;
                BlockIndex = blockIndex;
                OriginalEncodedRef = originalEncodedRef;
                PoolIndex = poolIndex;
                MaterializedUniform = materializedUniform;
                MetadataChanged = metadataChanged;
            }
        }

        public RegionMutationStore(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        /// <summary>
        /// Refreshes borrowed native-container handles after an owning world replaces its table or
        /// pool structs. Allocator state itself is shared by BrickPool copies.
        /// </summary>
        public void Refresh(in RegionTable table, in BrickPool pool)
        {
            _table = table;
            _pool = pool;
        }

        public bool IsRegionResident(int3 regionCoord) => _table.IsResident(regionCoord);

        public bool SetWholeBlock(int3 worldBlock, byte material, bool markHardSurface)
        {
            DecomposeBlock(worldBlock, out int3 regionCoord, out int blockIndex);
            if (!_table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                return false;

            bool changed = markHardSurface && region.MarkHardSurfaceBrick(blockIndex);
            BrickRef current = region.BrickRefs[blockIndex];

            if (!(current.IsUniform && current.UniformMaterial == material))
            {
                if (current.IsMixed)
                    _pool.Free(current.PoolIndex);
                region.BrickRefs[blockIndex] = BrickRef.Uniform(material);
                changed = true;
            }

            if (!changed)
                return false;

            region.Dirty = true;
            _table.CommitRegion(in region);
            return true;
        }

        public bool SetWholeCellBlock(int3 worldBlock, in VoxelCell cell, bool markHardSurface)
        {
            DecomposeBlock(worldBlock, out int3 regionCoord, out int blockIndex);
            Region region = _table.LoadRegion(regionCoord);
            if (!region.BrickRefs.IsCreated)
                return false;

            bool changed = markHardSurface && region.MarkHardSurfaceBrick(blockIndex);
            BrickRef current = region.BrickRefs[blockIndex];
            bool hasAuthoredPayload = (cell.IsSolid && cell.Surface.PackedStorage != 0)
                                   || cell.Boundary.Packed != 0;

            if (!hasAuthoredPayload)
            {
                if (!(current.IsUniform && current.UniformMaterial == cell.BaseMaterialId))
                {
                    if (current.IsMixed)
                        _pool.Free(current.PoolIndex);
                    region.BrickRefs[blockIndex] = BrickRef.Uniform(cell.BaseMaterialId);
                    changed = true;
                }
            }
            else
            {
                int poolIndex;
                if (current.IsMixed)
                {
                    poolIndex = current.PoolIndex;
                }
                else
                {
                    poolIndex = _pool.Allocate();
                    region.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                }

                _pool.FillBrick(poolIndex, in cell);
                changed = true;
            }

            if (!changed)
                return false;

            region.Dirty = true;
            _table.CommitRegion(in region);
            return true;
        }

        public bool TryBeginPartialBlock(
            int3 worldBlock,
            byte targetMaterial,
            bool markHardSurface,
            out VoxelBlockMutation mutation)
        {
            DecomposeBlock(worldBlock, out int3 regionCoord, out int blockIndex);
            if (!_table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
            {
                mutation = default;
                return false;
            }

            bool metadataChanged = markHardSurface && region.MarkHardSurfaceBrick(blockIndex);
            BrickRef original = region.BrickRefs[blockIndex];

            if (original.IsUniform && original.UniformMaterial == targetMaterial)
            {
                ulong leaseToken = RegisterPending(new PendingMutation(
                    regionCoord,
                    blockIndex,
                    original.Value,
                    -1,
                    false,
                    metadataChanged));
                mutation = VoxelBlockMutation.MetadataOnly(leaseToken, metadataChanged);
                return true;
            }

            mutation = MaterializeBlock(
                in region, regionCoord, blockIndex, in original, metadataChanged);
            return true;
        }

        public bool TryBeginCellBlock(
            int3 worldBlock,
            bool markHardSurface,
            out VoxelBlockMutation mutation)
        {
            DecomposeBlock(worldBlock, out int3 regionCoord, out int blockIndex);
            // Full-cell mutation is the authoring/generation path. The legacy authoritative cell
            // write made a region resident on the first authored voxel, so preserve that behavior
            // here while material-oriented gameplay edits remain resident-only above.
            Region region = _table.LoadRegion(regionCoord);
            if (!region.BrickRefs.IsCreated)
            {
                mutation = default;
                return false;
            }

            bool metadataChanged = markHardSurface && region.MarkHardSurfaceBrick(blockIndex);
            BrickRef original = region.BrickRefs[blockIndex];
            mutation = MaterializeBlock(
                in region, regionCoord, blockIndex, in original, metadataChanged);
            return true;
        }

        public bool CompletePartialBlock(ref VoxelBlockMutation mutation, bool payloadChanged)
        {
            ulong leaseToken = mutation.LeaseToken;
            if (leaseToken == 0UL || !_pendingMutations.TryGetValue(leaseToken, out PendingMutation pending))
            {
                mutation = default;
                return false;
            }
            _pendingMutations.Remove(leaseToken);

            if (!_table.TryGetRegion(pending.RegionCoord, out Region region) || !region.BrickRefs.IsCreated)
            {
                mutation = default;
                return false;
            }

            bool changed = pending.MetadataChanged || payloadChanged;

            if (!payloadChanged)
            {
                if (pending.MaterializedUniform)
                {
                    _pool.Free(pending.PoolIndex);
                    region.BrickRefs[pending.BlockIndex] =
                        BrickRef.Uniform(DecodeUniformMaterial(pending.OriginalEncodedRef));
                }
            }
            else if (mutation.IsCreated &&
                     _pool.TryGetUniformMaterial(pending.PoolIndex, out byte uniform))
            {
                _pool.Free(pending.PoolIndex);
                region.BrickRefs[pending.BlockIndex] = BrickRef.Uniform(uniform);
            }

            if (changed)
            {
                region.Dirty = true;
                _table.CommitRegion(in region);
            }

            mutation = default;
            return changed;
        }

        private VoxelBlockMutation MaterializeBlock(
            in Region region,
            int3 regionCoord,
            int blockIndex,
            in BrickRef original,
            bool metadataChanged)
        {
            int poolIndex;
            bool materializedUniform = false;
            if (original.IsUniform)
            {
                poolIndex = _pool.Allocate();
                _pool.FillBrick(poolIndex, original.UniformMaterial);
                Region writable = region;
                writable.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                materializedUniform = true;
            }
            else
            {
                poolIndex = original.PoolIndex;
            }

            ulong leaseToken = RegisterPending(new PendingMutation(
                regionCoord,
                blockIndex,
                original.Value,
                poolIndex,
                materializedUniform,
                metadataChanged));

            return new VoxelBlockMutation(
                _pool.Voxels.GetSubArray(
                    _pool.VoxelOffset(poolIndex), VoxelReadGrid.VoxelsPerBlock),
                _pool.SurfaceSemantics.GetSubArray(
                    _pool.VoxelOffset(poolIndex), VoxelReadGrid.VoxelsPerBlock),
                _pool.BoundarySamples.GetSubArray(
                    _pool.VoxelOffset(poolIndex), VoxelReadGrid.VoxelsPerBlock),
                _pool.Occupancy.GetSubArray(
                    _pool.OccupancyOffset(poolIndex), VoxelReadGrid.OccupancyWordsPerBlock),
                leaseToken,
                metadataChanged);
        }

        private ulong RegisterPending(in PendingMutation pending)
        {
            ulong leaseToken = _nextLeaseToken++;
            if (leaseToken == 0UL)
                leaseToken = _nextLeaseToken++;
            _pendingMutations.Add(leaseToken, pending);
            return leaseToken;
        }

        private static byte DecodeUniformMaterial(int encoded)
        {
            return (byte)(-encoded - 1);
        }

        private static void DecomposeBlock(int3 worldBlock, out int3 regionCoord, out int blockIndex)
        {
            regionCoord = worldBlock >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int localX = worldBlock.x & VoxelReadGrid.BlocksPerRegionEdgeMask;
            int localY = worldBlock.y & VoxelReadGrid.BlocksPerRegionEdgeMask;
            int localZ = worldBlock.z & VoxelReadGrid.BlocksPerRegionEdgeMask;
            blockIndex = Region.BrickIndex(localX, localY, localZ);
        }
    }
}
