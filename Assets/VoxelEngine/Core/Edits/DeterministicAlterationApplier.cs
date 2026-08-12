using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// Shared authoritative/client application of semantic alteration events.
    /// Explosion writes are batched per brick and use integer-only geometry.
    /// </summary>
    public static class DeterministicAlterationApplier
    {
        public static bool Supports(in AlterationEvent evt) => evt.kind == AlterationEvent.KindExplosion;

        /// <summary>
        /// True only when every region whose voxels may be touched by the event is resident.
        /// Peers must not partially apply an event based on their current streaming set.
        /// </summary>
        public static bool HasRequiredResidency(ref RegionTable table, in AlterationEvent evt)
        {
            if (!Supports(in evt))
                return false;

            int radiusVoxels = evt.Radius() * VoxelDimensions.BrickEdge;
            if (radiusVoxels <= 0)
                return false;

            int3 minVoxel = evt.origin - new int3(radiusVoxels);
            int3 maxVoxel = evt.origin + new int3(radiusVoxels);
            int3 minRegion = minVoxel >> VoxelDimensions.RegionVoxelEdgeLog2;
            int3 maxRegion = maxVoxel >> VoxelDimensions.RegionVoxelEdgeLog2;

            for (int rz = minRegion.z; rz <= maxRegion.z; rz++)
            for (int ry = minRegion.y; ry <= maxRegion.y; ry++)
            for (int rx = minRegion.x; rx <= maxRegion.x; rx++)
                if (!table.IsResident(new int3(rx, ry, rz)))
                    return false;

            return true;
        }

        public static bool TryApply(
            ref RegionTable table,
            ref BrickPool pool,
            in AlterationEvent evt,
            out NativeList<int3> affectedBricks)
        {
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                    return ApplyExplosion(ref table, ref pool, in evt, out affectedBricks);
                default:
                    affectedBricks = new NativeList<int3>(0, Allocator.Temp);
                    return false;
            }
        }

        private static bool ApplyExplosion(
            ref RegionTable table,
            ref BrickPool pool,
            in AlterationEvent evt,
            out NativeList<int3> affectedBricks)
        {
            affectedBricks = new NativeList<int3>(64, Allocator.Temp);
            int radiusVoxels = evt.Radius() * VoxelDimensions.BrickEdge;
            if (radiusVoxels <= 0 || !HasRequiredResidency(ref table, in evt))
                return false;

            long radiusSq = (long)radiusVoxels * radiusVoxels;
            int3 minVoxel = evt.origin - new int3(radiusVoxels);
            int3 maxVoxel = evt.origin + new int3(radiusVoxels);
            int3 minBrick = minVoxel >> VoxelDimensions.BrickEdgeLog2;
            int3 maxBrick = maxVoxel >> VoxelDimensions.BrickEdgeLog2;
            bool anyChanged = false;

            for (int bz = minBrick.z; bz <= maxBrick.z; bz++)
            {
                for (int by = minBrick.y; by <= maxBrick.y; by++)
                {
                    for (int bx = minBrick.x; bx <= maxBrick.x; bx++)
                    {
                        int3 worldBrick = new int3(bx, by, bz);
                        int3 brickMinVoxel = worldBrick << VoxelDimensions.BrickEdgeLog2;
                        int3 brickMaxVoxel = brickMinVoxel + new int3(VoxelDimensions.BrickEdge - 1);

                        if (DistanceSqToAabb(evt.origin, brickMinVoxel, brickMaxVoxel) > radiusSq)
                            continue;

                        int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
                        if (!table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                            return false;

                        int localX = bx & VoxelDimensions.RegionEdgeMask;
                        int localY = by & VoxelDimensions.RegionEdgeMask;
                        int localZ = bz & VoxelDimensions.RegionEdgeMask;
                        int brickIndex = Region.BrickIndex(localX, localY, localZ);
                        BrickRef brickRef = region.BrickRefs[brickIndex];

                        if (brickRef.IsUniform && brickRef.UniformMaterial == VoxelDimensions.MaterialEmpty)
                            continue;

                        bool changed;
                        if (FarthestCornerDistanceSq(evt.origin, brickMinVoxel, brickMaxVoxel) <= radiusSq)
                        {
                            changed = ClearWholeBrick(ref region, brickIndex, brickRef, ref pool);
                        }
                        else
                        {
                            changed = ClearPartialBrick(
                                evt.origin,
                                radiusSq,
                                brickMinVoxel,
                                ref region,
                                brickIndex,
                                brickRef,
                                ref pool);
                        }

                        if (!changed)
                            continue;

                        region.Dirty = true;
                        table.CommitRegion(region);
                        affectedBricks.Add(worldBrick);
                        anyChanged = true;
                    }
                }
            }

            return anyChanged;
        }

        private static bool ClearWholeBrick(
            ref Region region,
            int brickIndex,
            BrickRef brickRef,
            ref BrickPool pool)
        {
            if (brickRef.IsUniform)
            {
                if (brickRef.UniformMaterial == VoxelDimensions.MaterialEmpty)
                    return false;
            }
            else
            {
                pool.Free(brickRef.PoolIndex);
            }

            region.BrickRefs[brickIndex] = BrickRef.Empty;
            return true;
        }

        private static bool ClearPartialBrick(
            int3 center,
            long radiusSq,
            int3 brickMinVoxel,
            ref Region region,
            int brickIndex,
            BrickRef brickRef,
            ref BrickPool pool)
        {
            int poolIndex;
            if (brickRef.IsUniform)
            {
                if (brickRef.UniformMaterial == VoxelDimensions.MaterialEmpty)
                    return false;

                poolIndex = pool.Allocate();
                pool.FillBrick(poolIndex, brickRef.UniformMaterial);
                region.BrickRefs[brickIndex] = BrickRef.FromPoolIndex(poolIndex);
            }
            else
            {
                poolIndex = brickRef.PoolIndex;
            }

            bool changed = false;
            for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
            {
                long dz = (long)brickMinVoxel.z + z - center.z;
                long dzSq = dz * dz;
                for (int y = 0; y < VoxelDimensions.BrickEdge; y++)
                {
                    long dy = (long)brickMinVoxel.y + y - center.y;
                    long yzSq = dy * dy + dzSq;
                    if (yzSq > radiusSq)
                        continue;

                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        long dx = (long)brickMinVoxel.x + x - center.x;
                        if (dx * dx + yzSq > radiusSq)
                            continue;

                        int voxelIndex = OccupancyMask.VoxelIndex(x, y, z);
                        if (pool.GetVoxel(poolIndex, voxelIndex) == VoxelDimensions.MaterialEmpty)
                            continue;

                        pool.SetVoxel(poolIndex, voxelIndex, VoxelDimensions.MaterialEmpty);
                        changed = true;
                    }
                }
            }

            if (!changed)
            {
                if (brickRef.IsUniform)
                {
                    pool.Free(poolIndex);
                    region.BrickRefs[brickIndex] = brickRef;
                }
                return false;
            }

            if (pool.TryGetUniformMaterial(poolIndex, out byte uniform))
            {
                pool.Free(poolIndex);
                region.BrickRefs[brickIndex] = BrickRef.Uniform(uniform);
            }

            return true;
        }

        private static long DistanceSqToAabb(int3 point, int3 min, int3 max)
        {
            long dx = AxisDistance(point.x, min.x, max.x);
            long dy = AxisDistance(point.y, min.y, max.y);
            long dz = AxisDistance(point.z, min.z, max.z);
            return dx * dx + dy * dy + dz * dz;
        }

        private static long FarthestCornerDistanceSq(int3 point, int3 min, int3 max)
        {
            long dx = Max(Abs((long)point.x - min.x), Abs((long)max.x - point.x));
            long dy = Max(Abs((long)point.y - min.y), Abs((long)max.y - point.y));
            long dz = Max(Abs((long)point.z - min.z), Abs((long)max.z - point.z));
            return dx * dx + dy * dy + dz * dz;
        }

        private static long AxisDistance(int value, int min, int max)
        {
            if (value < min) return (long)min - value;
            if (value > max) return (long)value - max;
            return 0;
        }

        private static long Abs(long value) => value < 0 ? -value : value;
        private static long Max(long a, long b) => a > b ? a : b;
    }
}
