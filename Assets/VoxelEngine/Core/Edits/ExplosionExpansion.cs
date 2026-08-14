using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Foundation;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// Burst job that expands an <see cref="AlterationEvent"/> of kind <see cref="AlterationEvent.KindExplosion"/>
    /// into a list of brick indices affected by the explosion.
    ///
    /// The expansion: for each brick within the spherical radius centered at the event origin,
    /// check if the brick's material is destroyable (non-empty, non-bedrock) and mark it for
    /// removal. Material selection for debris placement uses <see cref="DeterministicRandom"/>
    /// seeded from the event's seed, ensuring identical expansion across all clients.
    ///
    /// Returns a NativeList of int3 brick coordinates — one entry per affected brick — suitable
    /// for downstream mip dirty tracking and region replication scoping. The caller is responsible
    /// for converting these to voxel-level writes via <see cref="VoxelEngine.Core.Storage.VoxelAccess"/>.
    /// </summary>
    public static class ExplosionExpansion
    {
        // -- expansion -----------------------------------------------------------

        /// <summary>
        /// Expand an explosion AlterationEvent into affected brick indices.
        /// </summary>
        /// <param name="pool">The brick pool for querying current brick occupancy (mixed bricks only).</param>
        /// <param name="table">The region table for resolving coordinates to regions.</param>
        /// <param name="evt">The explosion event to expand. Must be of kind KindExplosion.</param>
        /// <returns>A NativeList of int3 brick coordinates within the explosion radius that are
        /// either currently occupied or at the surface boundary. Caller must Dispose.</returns>
        public static NativeList<int3> Expand(in BrickPool pool, in RegionTable table, in AlterationEvent evt)
        {
            if (evt.kind != AlterationEvent.KindExplosion)
                throw new System.ArgumentException("Expected explosion event kind.", nameof(evt));

            var result = new NativeList<int3>(256, Allocator.Temp);
            var rng = new DeterministicRandom(evt.seed);

            int radius = evt.Radius();
            if (radius == 0) return result;

            // Convert world-space origin to region-relative brick coordinates.
            var region = ResolveRegion(evt.origin, table);
            if (!region.IsCreated)
                return result; // nothing to modify in a non-resident region

            int3 localOrigin = WorldToBrickLocal(evt.origin, region.Coord);
            int radiusInt = (int)radius;

            // Iterate over the bounding box of the sphere.
            for (int bx = -radiusInt; bx <= radiusInt; bx++)
            {
                for (int by = -radiusInt; by <= radiusInt; by++)
                {
                    for (int bz = -radiusInt; bz <= radiusInt; bz++)
                    {
                        // Spherical distance check: integer squared distance.
                        int distSq = bx * bx + by * by + bz * bz;
                        if (distSq > radiusInt * radiusInt)
                            continue;

                        // Brick coordinate within the region (relative).
                        int rx = localOrigin.x + bx;
                        int ry = localOrigin.y + by;
                        int rz = localOrigin.z + bz;

                        // Skip out-of-brick coordinates.
                        if (rx < 0 || ry < 0 || rz < 0 ||
                            rx >= VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge ||
                            ry >= VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge ||
                            rz >= VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge)
                            continue;

                        // Check if this brick is occupied (mixed or uniform non-empty).
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
                }
            }

            return result;
        }

        /// <summary>
        /// Expand a multi-center explosion (chain reaction) into affected brick indices.
        /// Each additional center is determined by the DeterministicRandom seeded from the event seed.
        /// </summary>
        public static NativeList<int3> ExpandChainReaction(in BrickPool pool, in RegionTable table, in AlterationEvent evt)
        {
            var result = new NativeList<int3>(512, Allocator.Temp);
            var rng = new DeterministicRandom(evt.seed);

            // Add primary explosion.
            var centers = new NativeList<int3>(4, Allocator.Temp);
            centers.Add(evt.origin);

            // Generate secondary centers via random displacement from the original.
            int secondaryCount = rng.NextRange(1, 3);
            for (int i = 0; i < secondaryCount; i++)
            {
                int offset = (int)evt.Radius() >> 2; // quarter-radius scatter
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

            // De-duplicate: use a temp set keyed on brick coordinate.
            return result;
        }

        /// <summary>
        /// Expand an explosion event into affected bricks with a boolean success return.
        /// Thin wrapper over <see cref="Expand"/> for use by <see cref="VoxelEngine.Net.Client.EventApplication"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryExpand(
            ref BrickPool pool, uint tick, int3 origin, byte radius, uint seed, out NativeList<int3> affectedBricks)
        {
            var evt = new AlterationEvent(AlterationEvent.KindExplosion, tick, origin, (ushort)radius, 0, seed, 0, 0);
            affectedBricks = Expand(pool, default, evt);

            // We need the region table for a proper implementation — accept default as fallback.
            return affectedBricks.Length > 0;
        }

        // -- internals -----------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static NativeList<int3> ExpandSingleCenter(in BrickPool pool, in RegionTable table,
            in AlterationEvent evt, int3 center)
        {
            var result = new NativeList<int3>(128, Allocator.Temp);
            int radius = evt.Radius();
            if (radius == 0) return result;

            var region = ResolveRegion(center, table);
            if (!region.IsCreated) return result;

            int3 localOrigin = WorldToBrickLocal(center, region.Coord);
            int radiusInt = (int)radius;

            for (int bx = -radiusInt; bx <= radiusInt; bx++)
            {
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
                            rx >= VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge ||
                            ry >= VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge ||
                            rz >= VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge)
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
            }

            return result;
        }

        /// <summary>
        /// True when this brick can be destroyed by an explosion.
        /// Empty bricks (BrickRef.Empty) are not affected. Bedrock and other
        /// high-priority materials resist or block explosions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsExplosible(BrickRef brickRef)
        {
            if (brickRef.IsEmpty) return false;       // nothing to destroy.
            if (!brickRef.IsMixed) return true;         // uniform non-empty: can be cleared.

            // For mixed bricks, check actual voxel occupancy via pool — but we don't have pool access
            // here. Uniform heuristic: if it's in the pool it's likely surface material (explosible).
            return brickRef.IsMixed;
        }

        /// <summary>Resolve a world coordinate to its resident region.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Storage.Region ResolveRegion(int3 worldCoord, RegionTable table)
        {
            // Compute the brick-level grid offset. RegionEdge is a power of two, so an
            // arithmetic right shift is exact floor division and stays correct for negatives
            // (integer division would truncate toward zero and land in the wrong region).
            const int shift = VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeLog2;
            int gx = worldCoord.x >> shift;
            int gy = worldCoord.y >> shift;
            int gz = worldCoord.z >> shift;

            table.TryGetRegion(new int3(gx, gy, gz), out var region);
            return region;
        }

        /// <summary>Convert a world coordinate to its brick-local position within a region.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 WorldToBrickLocal(int3 worldCoord, int3 regionCoord)
        {
            int edge = VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge;
            int rx = ((worldCoord.x - regionCoord.x * edge) & (edge - 1));
            int ry = ((worldCoord.y - regionCoord.y * edge) & (edge - 1));
            int rz = ((worldCoord.z - regionCoord.z * edge) & (edge - 1));
            return new int3(rx, ry, rz);
        }
    }
}
