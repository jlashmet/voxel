using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Core.Storage
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
                mutation = VoxelBlockMutation.MetadataOnly(
                    regionCoord, blockIndex, original.Value, metadataChanged);
                return true;
            }

            int poolIndex;
            bool materializedUniform = false;
            if (original.IsUniform)
            {
                poolIndex = _pool.Allocate();
                _pool.FillBrick(poolIndex, original.UniformMaterial);
                region.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                materializedUniform = true;
            }
            else
            {
                poolIndex = original.PoolIndex;
            }

            mutation = new VoxelBlockMutation(
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
            return true;
        }

        public bool CompletePartialBlock(ref VoxelBlockMutation mutation, bool materialChanged)
        {
            if (!_table.TryGetRegion(mutation.RegionCoord, out Region region) || !region.BrickRefs.IsCreated)
            {
                mutation = default;
                return false;
            }

            bool changed = mutation.MetadataChangedInternal || materialChanged;

            if (!materialChanged)
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

            if (changed)
            {
                region.Dirty = true;
                _table.CommitRegion(in region);
            }

            mutation = default;
            return changed;
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
