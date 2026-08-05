using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Core.Occupancy;

namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// Voxel-level read and write across the three storage tiers.
    ///
    /// A lookup is two indirections: hash the region coordinate, index the brick
    /// pointer grid, index the brick. Both are cache-friendly and neither divides —
    /// coordinate decomposition is shifts and masks throughout, because this runs in
    /// the innermost loop of collision and edit expansion.
    ///
    /// Everything here is integer (Constitution Principle I).
    /// </summary>
    public static class VoxelAccess
    {
        /// <summary>
        /// Decomposes a world voxel coordinate into region / brick-within-region /
        /// voxel-within-brick. Arithmetic shift, so negative coordinates floor
        /// correctly — the world extends in both directions from the origin and
        /// truncation toward zero would produce a seam there.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decompose(int3 worldVoxel, out int3 regionCoord,
                                     out int3 brickInRegion, out int3 voxelInBrick)
        {
            regionCoord = new int3(
                worldVoxel.x >> VoxelDimensions.RegionVoxelEdgeLog2,
                worldVoxel.y >> VoxelDimensions.RegionVoxelEdgeLog2,
                worldVoxel.z >> VoxelDimensions.RegionVoxelEdgeLog2);

            brickInRegion = new int3(
                (worldVoxel.x >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask,
                (worldVoxel.y >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask,
                (worldVoxel.z >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask);

            voxelInBrick = new int3(
                worldVoxel.x & VoxelDimensions.BrickEdgeMask,
                worldVoxel.y & VoxelDimensions.BrickEdgeMask,
                worldVoxel.z & VoxelDimensions.BrickEdgeMask);
        }

        /// <summary>
        /// Reads a voxel. Non-resident regions read as empty rather than throwing:
        /// callers routinely probe outside the loaded set (raycasts leaving the
        /// residency radius, support propagation at a region border) and treating that
        /// as an error would push the check into every call site.
        /// </summary>
        public static byte GetVoxel(ref RegionTable table, in BrickPool pool, int3 worldVoxel)
        {
            Decompose(worldVoxel, out var regionCoord, out var brickInRegion, out var voxelInBrick);

            if (!table.TryGetRegion(regionCoord, out var region))
                return VoxelDimensions.MaterialEmpty;

            var brick = region.GetBrick(brickInRegion.x, brickInRegion.y, brickInRegion.z);

            if (brick.IsUniform)
                return brick.UniformMaterial;

            return pool.GetVoxel(brick.PoolIndex,
                                 OccupancyMask.VoxelIndex(voxelInBrick.x, voxelInBrick.y, voxelInBrick.z));
        }

        /// <summary>
        /// Writes a voxel, maintaining the allocation invariants in both directions.
        ///
        /// Two transitions matter, and both are load-bearing:
        ///
        ///   Uniform -> Mixed: the first differing write materialises a pool slot and
        ///   fills it with the previous uniform material. This is why editing pristine
        ///   terrain costs an allocation and editing already-edited terrain does not.
        ///
        ///   Mixed -> Uniform: a write that leaves the brick single-material collapses
        ///   it back to a uniform reference and returns the slot. Skipping this does
        ///   not break anything visibly; memory simply climbs across a long session
        ///   until the pool is full of bricks that hold no surface. That is the slow
        ///   leak this design is most susceptible to, and it is why the collapse lives
        ///   inside the write rather than in a periodic sweep that can be forgotten.
        ///
        /// Returns true when the world actually changed, so callers can skip mip
        /// rebuild, replication, and structural re-evaluation for no-op writes.
        /// </summary>
        public static bool SetVoxel(ref RegionTable table, ref BrickPool pool,
                                    int3 worldVoxel, byte material)
        {
            Decompose(worldVoxel, out var regionCoord, out var brickInRegion, out var voxelInBrick);

            var region = table.LoadRegion(regionCoord);
            var brickIdx = Region.BrickIndex(brickInRegion.x, brickInRegion.y, brickInRegion.z);
            var brick = region.BrickRefs[brickIdx];
            var voxelIdx = OccupancyMask.VoxelIndex(voxelInBrick.x, voxelInBrick.y, voxelInBrick.z);

            if (brick.IsUniform)
            {
                // Writing the material the brick already is everywhere: nothing changes,
                // and critically we must not allocate a slot to represent it.
                if (brick.UniformMaterial == material)
                    return false;

                var newIndex = pool.Allocate();
                pool.FillBrick(newIndex, brick.UniformMaterial);
                pool.SetVoxel(newIndex, voxelIdx, material);

                region.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(newIndex);
                region.Dirty = true;
                table.CommitRegion(region);
                return true;
            }

            var poolIndex = brick.PoolIndex;
            if (pool.GetVoxel(poolIndex, voxelIdx) == material)
                return false;

            pool.SetVoxel(poolIndex, voxelIdx, material);

            // Collapse check. Cheap relative to the write itself, and the only thing
            // standing between this engine and unbounded pool growth.
            if (pool.TryGetUniformMaterial(poolIndex, out var uniform))
            {
                pool.Free(poolIndex);
                region.BrickRefs[brickIdx] = BrickRef.Uniform(uniform);
            }

            region.Dirty = true;
            table.CommitRegion(region);
            return true;
        }

        /// <summary>
        /// True when the voxel is solid. Reads occupancy rather than material, which is
        /// the same information but the form collision and structural passes want.
        /// </summary>
        public static bool IsSolid(ref RegionTable table, in BrickPool pool, int3 worldVoxel) =>
            GetVoxel(ref table, in pool, worldVoxel) != VoxelDimensions.MaterialEmpty;
    }
}
