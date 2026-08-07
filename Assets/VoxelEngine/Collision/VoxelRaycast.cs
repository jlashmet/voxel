using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Collision
{
    /// <summary>
    /// Raycast results from a <see cref="VoxelRaycast"/> query.
    ///
    /// Carries the position of the first solid brick hit, the face normal at which the ray
    /// entered that brick (for resolving against the opposite side), and the pool index of
    /// the hit brick for further queries into <see cref="BrickPool"/>.
    /// </summary>
    public struct HitInfo
    {
        /// <summary>Brick coordinate in world space where the ray first encountered a solid voxel.</summary>
        public int3 Position;

        /// <summary>
        /// Face normal at the entry point into the hit brick. Points outward from the brick face,
        /// so it can be used directly as a collision response normal (pushing the ray origin
        /// in the opposite direction).
        /// </summary>
        public float3 Normal;

        /// <summary>Pool index of the hit brick (only valid when IsHit is true).</summary>
        public int BrickIndex;

        /// <summary>True when a solid brick was found along the ray.</summary>
        public bool IsHit => BrickIndex >= 0;

        /// <summary>Distance from ray origin to the hit point, in voxel units.</summary>
        public float Distance { get; set; }
    }

    /// <summary>
    /// Burst-accelerated raycast over the shared DDA traversal from <see cref="DdaTraversal"/>.
    ///
    /// Returns the first solid brick along a ray from origin in direction. Both collision and
    /// rendering use this same code path (Constitution Principle II: Single source of truth).
    /// The raymarcher simply uses the same DDA but reads through a different code path for
    /// visual output — the traversal logic is identical.
    /// </summary>
    [BurstCompile]
    public static class VoxelRaycast
    {
        /// <summary>
        /// Cast a ray through the world and return the first solid brick it intersects.
        ///
        /// Uses integer DDA stepping from <see cref="DdaTraversal"/> for cache-friendly traversal,
        /// checking each visited brick's occupancy via the <see cref="BrickPool"/> to find the
        /// first non-empty voxel. Non-resident regions are treated as empty — only resident data
        /// is queried.
        /// </summary>
        /// <param name="table">Region table providing residency checks for each visited brick.</param>
        /// <param name="pool">Brick pool containing mixed-brick voxel and occupancy data.</param>
        /// <param name="origin">Ray origin in world voxel coordinates (not necessarily integer).</param>
        /// <param name="direction">Ray direction (will be normalised internally). Must not be zero.</param>
        /// <param name="hit">Output hit information. Valid only if the method returns true.</param>
        /// <returns>True when a solid brick was found; false when the ray exits the loaded world without intersection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Raycast(in RegionTable table, in BrickPool pool, float3 origin, float3 direction, out HitInfo hit)
        {
            hit = default;
            hit.BrickIndex = -1;

            if (math.lengthsq(direction) < 1e-6f)
                return false; // Degenerate ray: zero direction.

            // Float appears only here, converting a camera-space ray into the integer
            // endpoints the traversal walks. Past this point everything is integer, which is
            // what lets the render path reach the identical brick sequence (C-004).
            int3 startBrick = new int3(
                (int)math.floor(origin.x),
                (int)math.floor(origin.y),
                (int)math.floor(origin.z));

            float3 normalisedDir = math.normalize(direction);
            const float maxDistance = 10000f; // Max world extent from device-matrix.md.

            int3 endBrick = new int3(
                (int)math.round(origin.x + normalisedDir.x * maxDistance),
                (int)math.round(origin.y + normalisedDir.y * maxDistance),
                (int)math.round(origin.z + normalisedDir.z * maxDistance));

            // The shared traversal — not a private copy. Constitution Principle II: one DDA,
            // two callers. A second implementation here is exactly how visual and collision
            // drift apart.
            var cursor = DdaTraversal.Cursor.Between(startBrick, endBrick);

            while (cursor.MoveNext())
            {
                int3 current = cursor.Current;

                if (!IsBrickSolid(in table, in pool, current, out int poolIndex))
                    continue;

                hit.Position = current;
                hit.Normal = new float3(
                    cursor.EntryNormal.x, cursor.EntryNormal.y, cursor.EntryNormal.z);
                hit.BrickIndex = poolIndex;
                hit.Distance = math.length((float3)(current - startBrick));
                return true;
            }

            return false;
        }

        /// <summary>
        /// True when the brick at <paramref name="brickCoord"/> contains any solid voxel.
        ///
        /// Non-resident regions read as empty rather than throwing, which is what lets a ray
        /// cross an unloaded region without the caller special-casing residency.
        /// </summary>
        /// <param name="poolIndex">Pool slot of the hit brick, or -1 when it is uniform.</param>
        private static bool IsBrickSolid(
            in RegionTable table, in BrickPool pool, int3 brickCoord, out int poolIndex)
        {
            poolIndex = -1;

            var regionCoord = new int3(
                brickCoord.x >> VoxelDimensions.RegionEdgeLog2,
                brickCoord.y >> VoxelDimensions.RegionEdgeLog2,
                brickCoord.z >> VoxelDimensions.RegionEdgeLog2);

            if (!table.TryGetRegion(regionCoord, out var region))
                return false;

            int bx = brickCoord.x & VoxelDimensions.RegionEdgeMask;
            int by = brickCoord.y & VoxelDimensions.RegionEdgeMask;
            int bz = brickCoord.z & VoxelDimensions.RegionEdgeMask;

            var brickRef = region.BrickRefs[Region.BrickIndex(bx, by, bz)];

            if (brickRef.IsUniform)
                return brickRef.UniformMaterial != VoxelDimensions.MaterialEmpty;

            // Mixed: solid when any occupancy bit is set.
            int occOffset = pool.OccupancyOffset(brickRef.PoolIndex);
            var occArray = pool.Occupancy;

            ulong acc = 0UL;
            for (int w = 0; w < VoxelDimensions.OccupancyWordsPerBrick; w++)
                acc |= occArray[occOffset + w];

            if (acc == 0UL) return false;

            poolIndex = brickRef.PoolIndex;
            return true;
        }
    }
}
