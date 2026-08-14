using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Foundation;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Edits
{
    /// <summary>Deterministic integer-only expansion of explosion edits into affected bricks.</summary>
    public static class ExplosionExpansion
    {
        public static NativeList<int3> Expand(in BrickPool pool, in RegionTable table, in AlterationEvent evt)
        {
            if (evt.kind != AlterationEvent.KindExplosion)
                throw new System.ArgumentException("Expected explosion event kind.", nameof(evt));

            var result = new NativeList<int3>(256, Allocator.Temp);
            var rng = new DeterministicRandom(evt.seed);

            int radius = evt.Radius();
            if (radius == 0) return result;

            var region = ResolveRegion(evt.origin, table);
            if (!region.IsCreated)
                return result;

            int3 localOrigin = WorldToBrickLocal(evt.origin, region.Coord);
            int radiusInt = radius;

            for (int bx = -radiusInt; bx <= radiusInt; bx++)
            for (int by = -radiusInt; by <= radiusInt; by++)
            for (int bz = -radiusInt; bz <= radiusInt; bz++)
            {
                int distSq = bx * bx + by * by + bz * bz;
                if (distSq > radiusInt * radiusInt)
                    continue;

                int rx = localOrigin.x + bx;
                int ry = localOrigin.y + by;
                int rz = localOrigin.z + bz;
                if (rx < 0 || ry < 0 || rz < 0 ||
                    rx >= VoxelDimensions.RegionEdge ||
                    ry >= VoxelDimensions.RegionEdge ||
                    rz >= VoxelDimensions.RegionEdge)
                    continue;

                int brickIdx = Region.BrickIndex(rx, ry, rz);
                var brickRef = region.BrickRefs[brickIdx];
                if (IsExplosible(brickRef))
                {
                    result.Add(new int3(
                        evt.origin.x + bx,
                        evt.origin.y + by,
                        evt.origin.z + bz));
                }
            }

            return result;
        }

        public static NativeList<int3> ExpandChainReaction(
            in BrickPool pool,
            in RegionTable table,
            in AlterationEvent evt)
        {
            var result = new NativeList<int3>(512, Allocator.Temp);
            var rng = new DeterministicRandom(evt.seed);
            var centers = new NativeList<int3>(4, Allocator.Temp);
            centers.Add(evt.origin);

            int secondaryCount = rng.NextRange(1, 3);
            for (int i = 0; i < secondaryCount; i++)
            {
                int offset = evt.Radius() >> 2;
                centers.Add(new int3(
                    evt.origin.x + rng.NextRange(-offset, offset),
                    evt.origin.y + rng.NextRange(-offset, offset),
                    evt.origin.z + rng.NextRange(-offset, offset)));
            }

            foreach (var center in centers)
            {
                var expanded = ExpandSingleCenter(pool, table, evt, center);
                for (int i = 0; i < expanded.Length; i++)
                    result.Add(expanded[i]);
            }

            centers.Dispose();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryExpand(
            ref BrickPool pool,
            uint tick,
            int3 origin,
            byte radius,
            uint seed,
            out NativeList<int3> affectedBricks)
        {
            var evt = new AlterationEvent(
                AlterationEvent.KindExplosion, tick, origin, radius, 0, seed, 0, 0);
            affectedBricks = Expand(pool, default, evt);
            return affectedBricks.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static NativeList<int3> ExpandSingleCenter(
            in BrickPool pool,
            in RegionTable table,
            in AlterationEvent evt,
            int3 center)
        {
            var result = new NativeList<int3>(128, Allocator.Temp);
            int radius = evt.Radius();
            if (radius == 0) return result;

            var region = ResolveRegion(center, table);
            if (!region.IsCreated) return result;

            int3 localOrigin = WorldToBrickLocal(center, region.Coord);
            int radiusInt = radius;

            for (int bx = -radiusInt; bx <= radiusInt; bx++)
            for (int by = -radiusInt; by <= radiusInt; by++)
            {
                int innerZ = radiusInt * radiusInt - bx * bx - by * by;
                if (innerZ < 0) continue;
                int maxBz = IntMath.Isqrt(innerZ);

                for (int bz = -maxBz; bz <= maxBz; bz++)
                {
                    int rx = localOrigin.x + bx;
                    int ry = localOrigin.y + by;
                    int rz = localOrigin.z + bz;
                    if (rx < 0 || ry < 0 || rz < 0 ||
                        rx >= VoxelDimensions.RegionEdge ||
                        ry >= VoxelDimensions.RegionEdge ||
                        rz >= VoxelDimensions.RegionEdge)
                        continue;

                    int brickIdx = Region.BrickIndex(rx, ry, rz);
                    var brickRef = region.BrickRefs[brickIdx];
                    if (IsExplosible(brickRef))
                    {
                        result.Add(new int3(
                            center.x + bx,
                            center.y + by,
                            center.z + bz));
                    }
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsExplosible(BrickRef brickRef)
        {
            if (brickRef.IsEmpty) return false;
            if (!brickRef.IsMixed) return true;
            return brickRef.IsMixed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Region ResolveRegion(int3 worldCoord, RegionTable table)
        {
            const int shift = VoxelDimensions.RegionEdgeLog2;
            int gx = worldCoord.x >> shift;
            int gy = worldCoord.y >> shift;
            int gz = worldCoord.z >> shift;

            table.TryGetRegion(new int3(gx, gy, gz), out var region);
            return region;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 WorldToBrickLocal(int3 worldCoord, int3 regionCoord)
        {
            int edge = VoxelDimensions.RegionEdge;
            int rx = (worldCoord.x - regionCoord.x * edge) & (edge - 1);
            int ry = (worldCoord.y - regionCoord.y * edge) & (edge - 1);
            int rz = (worldCoord.z - regionCoord.z * edge) & (edge - 1);
            return new int3(rx, ry, rz);
        }
    }
}
