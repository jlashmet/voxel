using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Storage.Runtime.Occupancy
{
    /// <summary>
    /// Reads occupancy and material at a chosen resolution, from the finest source available.
    ///
    /// This is the seam between the LOD rings and the voxel data. A ring extracting at
    /// <c>SourceStep</c> voxels per sample asks for the mip level whose cells match that
    /// stride, and the sampler answers from full-resolution bricks when they are resident or
    /// from the region's pyramid when they are not. Rings never decide where data comes from,
    /// and the pyramid is never consulted for detail it does not have.
    ///
    /// <para><b>Stride to level.</b> A level-0 mip cell is one brick — <see cref="VoxelDimensions.BrickEdge"/>
    /// voxels on a side. So a stride of 8 voxels maps to level 0, 16 to level 1, and each
    /// doubling adds a level. Strides finer than a brick have no mip representation at all and
    /// must read voxels directly; <see cref="LevelForStride"/> returns a negative level to say
    /// so, which is the signal that a ring requires resident bricks.</para>
    /// </summary>
    public static class VoxelMipSampler
    {
        /// <summary>
        /// Mip level whose cells span <paramref name="sourceStep"/> voxels, or -1 when the
        /// stride is finer than one brick and only raw voxels can answer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LevelForStride(int sourceStep)
        {
            if (sourceStep < VoxelDimensions.BrickEdge) return -1;
            int level = 0;
            for (int span = VoxelDimensions.BrickEdge; span < sourceStep; span <<= 1) level++;
            return level;
        }

        /// <summary>Voxels spanned by one cell at a mip level.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int VoxelsPerCell(int level) => VoxelDimensions.BrickEdge << level;

        /// <summary>
        /// Samples the region containing <paramref name="worldVoxel"/> at <paramref name="level"/>.
        /// Returns false when the region is not resident, or when the requested level is finer
        /// than the pyramid this region holds.
        /// </summary>
        public static bool TrySample(ref RegionTable table, in BrickPool pool,
                                     int3 worldVoxel, int level,
                                     out bool occupied, out byte material)
        {
            occupied = false;
            material = VoxelDimensions.MaterialEmpty;

            int3 regionCoord = new(
                FloorDiv(worldVoxel.x, VoxelDimensions.RegionVoxelEdge),
                FloorDiv(worldVoxel.y, VoxelDimensions.RegionVoxelEdge),
                FloorDiv(worldVoxel.z, VoxelDimensions.RegionVoxelEdge));
            if (!table.TryGetRegion(regionCoord, out Region region)) return false;

            int3 localVoxel = worldVoxel - regionCoord * VoxelDimensions.RegionVoxelEdge;
            int3 brick = localVoxel >> VoxelDimensions.BrickEdgeLog2;

            if (level < 0)
            {
                // Finer than one brick: read the voxel itself. This is what a transition face
                // needs, because it samples at half its ring's stride to meet the finer
                // neighbour, and half of a brick-sized stride is sub-brick.
                BrickRef brick2 = region.BrickRefs[
                    Region.BrickIndex(brick.x, brick.y, brick.z)];
                if (brick2.IsEmpty) return true;
                if (!brick2.IsMixed)
                {
                    occupied = true;
                    material = brick2.UniformMaterial;
                    return true;
                }
                int3 inner = localVoxel & VoxelDimensions.BrickEdgeMask;
                int voxelIndex = inner.x | (inner.y << 3) | (inner.z << 6);
                material = pool.Voxels[pool.VoxelOffset(brick2.PoolIndex) + voxelIndex];
                occupied = material != VoxelDimensions.MaterialEmpty;
                return true;
            }

            if (level == 0)
            {
                // Level 0 is derived from the bricks themselves rather than stored.
                int brickIndex = Region.BrickIndex(brick.x, brick.y, brick.z);
                MipBuilder.ReadLevel0(in pool, in region, brickIndex,
                                      out ulong bits, out material);
                occupied = bits != 0UL;
                return true;
            }

            if (!region.HasMips || level >= region.MipLevelCount) return false;

            int3 cell = brick >> level;
            int index = RegionMipLayout.Index(level, cell.x, cell.y, cell.z);
            occupied = region.OccupancyMips[index] != 0UL;
            material = region.MaterialMips[index];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            if ((value % divisor != 0) && ((value < 0) != (divisor < 0))) quotient--;
            return quotient;
        }
    }
}
