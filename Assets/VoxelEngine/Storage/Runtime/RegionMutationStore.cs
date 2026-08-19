using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Current physical implementation of block-granular authoritative mutation.
    /// Moves to Storage.Runtime with RegionTable/BrickPool during the Core split.
    /// </summary>
    public sealed class RegionMutationStore : IRegionMutationStore
    {
        private RegionTable _table;
        private BrickPool _pool;

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
                RefreshBlockSummary(ref region, blockIndex);
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
                    poolIndex = _pool.EnsureWritable(current.PoolIndex);
                    if (poolIndex != current.PoolIndex)
                        region.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
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

            RefreshBlockSummary(ref region, blockIndex);
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
                mutation = VoxelBlockMutation.MetadataOnly(
                    regionCoord, blockIndex, original.Value, metadataChanged);
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
            if (mutation.IsCreated)
                _pool.EndWrite(mutation.PoolIndex);

            if (!_table.TryGetRegion(mutation.RegionCoord, out Region region) || !region.BrickRefs.IsCreated)
            {
                mutation = default;
                return false;
            }

            bool changed = mutation.MetadataChangedInternal || payloadChanged;

            if (!payloadChanged)
            {
                if (mutation.MaterializedUniform)
                {
                    _pool.Free(mutation.PoolIndex);
                    region.BrickRefs[mutation.BlockIndex] =
                        BrickRef.Uniform(DecodeUniformMaterial(mutation.OriginalEncodedRef));
                }
            }
            else if (mutation.IsCreated &&
                     _pool.TryGetUniformMaterial(mutation.PoolIndex, out byte uniform))
            {
                _pool.Free(mutation.PoolIndex);
                region.BrickRefs[mutation.BlockIndex] = BrickRef.Uniform(uniform);
            }

            if (payloadChanged)
                RefreshBlockSummary(ref region, mutation.BlockIndex);

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
            bool publishedPhysicalRef = false;
            Region writable = region;
            if (original.IsUniform)
            {
                poolIndex = _pool.Allocate();
                _pool.FillBrick(poolIndex, original.UniformMaterial);
                writable.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                publishedPhysicalRef = true;
                materializedUniform = true;
            }
            else
            {
                poolIndex = _pool.EnsureWritable(original.PoolIndex);
                if (poolIndex != original.PoolIndex)
                {
                    // The NativeArray backing BrickRefs is shared by Region copies. Publish the
                    // COW version immediately and advance RegionTable's content revision before a
                    // long-lived borrowed writer can overlap an optimistic renderer metadata job.
                    writable.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                    publishedPhysicalRef = true;
                }
            }

            if (publishedPhysicalRef)
                _table.CommitRegion(in writable);

            _pool.BeginWrite(poolIndex);
            return new VoxelBlockMutation(
                _pool.Voxels,
                _pool.SurfaceSemantics,
                _pool.BoundarySamples,
                _pool.Occupancy,
                _pool.VoxelOffset(poolIndex),
                _pool.OccupancyOffset(poolIndex),
                regionCoord,
                blockIndex,
                original.Value,
                poolIndex,
                materializedUniform,
                metadataChanged);
        }

        /// <summary>
        /// Rebuilds a whole region's occupancy summary from its brick contents.
        ///
        /// The per-block refresh below runs as a side effect of mutating through this store. A
        /// bulk writer that fills a region and commits it wholesale — terrain generation does
        /// exactly that — never touches it, leaving the summary at its initial all-empty state.
        /// Surface discovery reads only the summary, so such a region reads as air and is never
        /// meshed no matter how much solid voxel data it holds.
        /// </summary>
        public void RefreshRegionSummary(ref Region region)
        {
            for (int blockIndex = 0; blockIndex < VoxelDimensions.BricksPerRegion; blockIndex++)
                RefreshBlockSummary(ref region, blockIndex);
        }

        private void RefreshBlockSummary(ref Region region, int blockIndex)
        {
            BrickRef block = region.BrickRefs[blockIndex];
            if (block.IsUniform)
            {
                bool solid = block.UniformMaterial != VoxelGrid.MaterialEmpty;
                region.SetBlockOccupancySummary(blockIndex, solid, solid);
                return;
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
