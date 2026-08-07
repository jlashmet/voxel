using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Collision
{
    /// <summary>
    /// Read-only view of a speculative overlay, supplied by the caller.
    ///
    /// Collision deliberately does not reference VoxelEngine.Net: the layering runs
    /// Core -> Collision -> Net, and the server needs to sweep too. The client's
    /// SpeculativeOverlay lives in Net.Client and adapts itself to this interface, so
    /// the dependency points inward rather than creating a cycle.
    ///
    /// Implementations must be structs — the sweep methods are generic over T with a
    /// struct constraint so the call devirtualises and stays Burst-compatible with no
    /// boxing in the collision inner loop.
    /// </summary>
    public interface IOverlayQuery
    {
        /// <summary>True when the overlay holds a pending voxel at this brick coordinate.</summary>
        bool TryGetPendingMaterial(int3 brickCoord, out byte material);
    }

    /// <summary>
    /// Collision result from a swept AABB query against the voxel grid.
    ///
    /// Carries which axes were blocked and the contact normals for each blocked axis,
    /// so the caller can resolve movement along individual axes (slide along walls,
    /// stand on floors, hit ceilings).
    /// </summary>
    public struct CollisionResult
    {
        /// <summary>True when movement along the X axis was blocked by a solid brick.</summary>
        public bool BlockedX;

        /// <summary>True when movement along the Y axis was blocked by a solid brick.</summary>
        public bool BlockedY;

        /// <summary>True when movement along the Z axis was blocked by a solid brick.</summary>
        public bool BlockedZ;

        /// <summary>Contact normal for the X-axis block. Points outward from the blocking face.</summary>
        public float3 NormalX;

        /// <summary>Contact normal for the Y-axis block. Points outward from the blocking face.</summary>
        public float3 NormalY;

        /// <summary>Contact normal for the Z-axis block. Points outward from the blocking face.</summary>
        public float3 NormalZ;

        /// <summary>Total number of axes blocked (0 = free movement, 1-3 = partial or full block).</summary>
        public int BlockedCount =>
            (BlockedX ? 1 : 0) + (BlockedY ? 1 : 0) + (BlockedZ ? 1 : 0);

        /// <summary>True when movement is completely blocked on all axes.</summary>
        public bool IsFullyBlocked => BlockedX && BlockedY && BlockedZ;
    }

    /// <summary>
    /// Swept AABB collision against the voxel grid, checking all bricks an AABB would
    /// sweep through during a displacement.
    ///
    /// Used for character movement resolution: tests each axis independently (X, then Y,
    /// then Z) so that wall-sliding works naturally — if X is blocked but Y is free, the
    /// character slides along the wall rather than being fully stopped.
    /// </summary>
    [BurstCompile]
    public static class SweptAabb
    {
        /// <summary>
        /// Sweep an AABB through the voxel grid by a displacement vector, returning contact normals
        /// and blocked axes.
        ///
        /// The sweep resolves axis-by-axis in the order X, Y, Z (standard approach from Quake's
        /// collision model): each axis is tested independently against the current position, so
        /// earlier blocks affect later tests. This allows sliding along surfaces rather than full stops.
        /// </summary>
        /// <param name="table">Region table for residency checks during the sweep.</param>
        /// <param name="pool">Brick pool with voxel and occupancy data for mixed bricks.</param>
        /// <param name="min">AABB minimum corner in voxel coordinates (body's lower bounds).</param>
        /// <param name="max">AABB maximum corner in voxel coordinates (body's upper bounds).</param>
        /// <param name="delta">Displacement vector in voxel coordinates (position change this frame).</param>
        /// <returns>Collision result with blocked axes and contact normals. Blocked means the AABB
        /// would intersect a solid brick if delta were applied fully.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CollisionResult Sweep(in RegionTable table, in BrickPool pool, float3 min, float3 max, float3 delta)
        {
            var result = new CollisionResult();
            float3 currentMin = min;
            float3 currentMax = max;
            float3 residualDelta = delta;

            // --- X axis sweep -------------------------------------------------------
            if (math.abs(delta.x) > 1e-6f)
            {
                CollisionResult xRes = SweepAxis(table, pool, currentMin, currentMax, delta.x);
                result.BlockedX = xRes.BlockedX;
                result.NormalX = xRes.NormalX;

                if (!xRes.BlockedX)
                    residualDelta.x = delta.x;
            }

            // --- Y axis sweep -------------------------------------------------------
            if (math.abs(residualDelta.y) > 1e-6f)
            {
                CollisionResult yRes = SweepAxis(table, pool, currentMin, currentMax, residualDelta.y);
                result.BlockedY = yRes.BlockedY;
                result.NormalY = yRes.NormalY;

                if (!yRes.BlockedY)
                    residualDelta.y = delta.y;
            }

            // --- Z axis sweep -------------------------------------------------------
            if (math.abs(residualDelta.z) > 1e-6f)
            {
                CollisionResult zRes = SweepAxis(table, pool, currentMin, currentMax, residualDelta.z);
                result.BlockedZ = zRes.BlockedZ;
                result.NormalZ = zRes.NormalZ;

                if (!zRes.BlockedZ)
                    residualDelta.z = delta.z;
            }

            return result;
        }

        /// <summary>
        /// Swept AABB collision against both the real grid and the speculative overlay,
        /// checking for collisions in whichever side is authoritative at each coordinate.
        ///
        /// C-003 compliance: this method NEVER blends between real and overlay — it checks
        /// the overlay first (since pending voxels are provisional but must be respected for
        /// collision to prevent clipping through visible builds), then falls back to the real
        /// grid only for coordinates not in the overlay. This is "one side only, never a blend"
        /// per Constitution Principle I: if the overlay says solid, it's solid; if not in the
        /// overlay, check the grid. There is no intermediate state.
        /// </summary>
        /// <param name="table">Region table for real-grid lookups (coordinates not in overlay).</param>
        /// <param name="pool">Brick pool for voxel-level occupancy data.</param>
        /// <param name="overlay">The speculative overlay to check alongside the real grid.</param>
        /// <param name="min">AABB minimum corner in voxel coordinates.</param>
        /// <param name="max">AABB maximum corner in voxel coordinates.</param>
        /// <param name="delta">Displacement vector in voxel coordinates.</param>
        /// <returns>Collision result. Overlay voxels take priority; grid voxels are fallback.</returns>
        public static CollisionResult SweepAgainstOverlay<TOverlay>(
            ref RegionTable table,
            in BrickPool pool,
            in TOverlay overlay,
            float3 min,
            float3 max,
            float3 delta)
            where TOverlay : struct, IOverlayQuery
        {
            var result = new CollisionResult();
            float3 currentMin = min;
            float3 currentMax = max;
            float3 residualDelta = delta;

            // --- X axis sweep -------------------------------------------------------
            if (math.abs(delta.x) > 1e-6f)
            {
                CollisionResult xRes = SweepAxisAgainstOverlay(table, pool, overlay, currentMin, currentMax, delta.x);
                result.BlockedX = xRes.BlockedX;
                result.NormalX = xRes.NormalX;

                if (!xRes.BlockedX)
                    residualDelta.x = delta.x;
            }

            // --- Y axis sweep -------------------------------------------------------
            if (math.abs(residualDelta.y) > 1e-6f)
            {
                CollisionResult yRes = SweepAxisAgainstOverlay(table, pool, overlay, currentMin, currentMax, residualDelta.y);
                result.BlockedY = yRes.BlockedY;
                result.NormalY = yRes.NormalY;

                if (!yRes.BlockedY)
                    residualDelta.y = delta.y;
            }

            // --- Z axis sweep -------------------------------------------------------
            if (math.abs(residualDelta.z) > 1e-6f)
            {
                CollisionResult zRes = SweepAxisAgainstOverlay(table, pool, overlay, currentMin, currentMax, residualDelta.z);
                result.BlockedZ = zRes.BlockedZ;
                result.NormalZ = zRes.NormalZ;

                if (!zRes.BlockedZ)
                    residualDelta.z = delta.z;
            }

            return result;
        }

        // -- internal helpers -----------------------------------------------------

        /// <summary>
        /// Sweep an AABB along a single axis, checking overlay first then grid for collision.
        /// One side only — never a blend (C-003).
        /// </summary>
        private static CollisionResult SweepAxisAgainstOverlay<TOverlay>(
            in RegionTable table, in BrickPool pool,
            in TOverlay overlay,
            float3 min, float3 max, float delta)
            where TOverlay : struct, IOverlayQuery
        {
            var res = new CollisionResult();

            if (delta == 0f) return res;

            // Compute the bounding box of the swept volume.
            int minX = math.min((int)math.floor(min.x), (int)math.floor(max.x + delta));
            int maxX = math.max((int)math.floor(min.x - 1e-6f), (int)math.floor(max.x - 1e-6f + delta));
            int minY = math.min((int)math.floor(min.y), (int)math.floor(max.y + delta));
            int maxY = math.max((int)math.floor(min.y - 1e-6f), (int)math.floor(max.y - 1e-6f + delta));
            int minZ = math.min((int)math.floor(min.z), (int)math.floor(max.z + delta));
            int maxZ = math.max((int)math.floor(min.z - 1e-6f), (int)math.floor(max.z - 1e-6f + delta));

            if (minX > maxX) minX = maxX;
            if (minY > maxY) minY = maxY;
            if (minZ > maxZ) minZ = maxZ;

            float sign = math.sign(delta);

            for (int bx = minX; bx <= maxX; bx++)
            {
                for (int by = minY; by <= maxY; by++)
                {
                    for (int bz = minZ; bz <= maxZ; bz++)
                    {
                        int3 brickCoord = new int3(bx, by, bz);

                        // C-003: check overlay FIRST — if the overlay has a voxel here, it's solid.
                        // We never blend with the grid at this coordinate.
                        if (IsOverlaySolidAtBrick(brickCoord, in overlay))
                        {
                            res.BlockedX = true;
                            res.BlockedY = true;
                            res.BlockedZ = true;
                            res.NormalX = -new float3(sign, 0, 0);
                            res.NormalY = -new float3(0, sign, 0);
                            res.NormalZ = -new float3(0, 0, sign);
                            return res;
                        }

                        // Overlay has no entry at this brick — check the real grid as fallback.
                        if (IsSolidAtBrick(table, pool, brickCoord))
                        {
                            res.BlockedX = true;
                            res.BlockedY = true;
                            res.BlockedZ = true;
                            res.NormalX = -new float3(sign, 0, 0);
                            res.NormalY = -new float3(0, sign, 0);
                            res.NormalZ = -new float3(0, 0, sign);
                            return res;
                        }
                    }
                }
            }

            return res;
        }

        /// <summary>True when the speculative overlay contains a solid voxel at this brick coordinate.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOverlaySolidAtBrick<TOverlay>(int3 brickCoord, in TOverlay overlay)
            where TOverlay : struct, IOverlayQuery
        {
            // Overlay voxels are always treated as solid (provisionally) — we never distinguish
            // material types for collision against the speculative layer.
            return overlay.TryGetPendingMaterial(brickCoord, out var mat) && mat != VoxelDimensions.MaterialEmpty;
        }

        /// <summary>
        /// Sweep an AABB along a single axis against the voxel grid. Returns whether blocked
        /// and the contact normal pointing out of the blocking face.
        /// </summary>
        private static CollisionResult SweepAxis(in RegionTable table, in BrickPool pool, float3 min, float3 max, float delta)
        {
            var res = new CollisionResult();

            if (delta == 0f) return res;

            // Compute the bounding box of the swept volume along each axis.
            int minX = math.min((int)math.floor(min.x), (int)math.floor(max.x + delta));
            int maxX = math.max((int)math.floor(min.x - 1e-6f), (int)math.floor(max.x - 1e-6f + delta));
            int minY = math.min((int)math.floor(min.y), (int)math.floor(max.y + delta));
            int maxY = math.max((int)math.floor(min.y - 1e-6f), (int)math.floor(max.y - 1e-6f + delta));
            int minZ = math.min((int)math.floor(min.z), (int)math.floor(max.z + delta));
            int maxZ = math.max((int)math.floor(min.z - 1e-6f), (int)math.floor(max.z - 1e-6f + delta));

            // Clamp ranges for zero-thickness slabs.
            if (minX > maxX) minX = maxX;
            if (minY > maxY) minY = maxY;
            if (minZ > maxZ) minZ = maxZ;

            float sign = math.sign(delta);

            // Iterate over every brick in the swept bounding box and check for solidity.
            for (int bx = minX; bx <= maxX; bx++)
            {
                for (int by = minY; by <= maxY; by++)
                {
                    for (int bz = minZ; bz <= maxZ; bz++)
                    {
                        if (IsSolidAtBrick(table, pool, new int3(bx, by, bz)))
                        {
                            res.BlockedX = true;
                            res.BlockedY = true;
                            res.BlockedZ = true;
                            res.NormalX = -new float3(sign, 0, 0);
                            res.NormalY = -new float3(0, sign, 0);
                            res.NormalZ = -new float3(0, 0, sign);
                            return res;
                        }
                    }
                }
            }

            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSolidAtBrick(in RegionTable table, in BrickPool pool, int3 brickCoord)
        {
            // Non-resident regions read as empty.
            if (!table.TryGetRegion(
                new int3(brickCoord.x >> VoxelDimensions.RegionEdgeLog2,
                         brickCoord.y >> VoxelDimensions.RegionEdgeLog2,
                         brickCoord.z >> VoxelDimensions.RegionEdgeLog2), out var region))
                return false;

            int bx = (brickCoord.x >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;
            int by = (brickCoord.y >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;
            int bz = (brickCoord.z >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;

            int brickIdx = Region.BrickIndex(bx, by, bz);
            var brickRef = region.BrickRefs[brickIdx];

            if (brickRef.IsUniform)
                return brickRef.UniformMaterial != VoxelDimensions.MaterialEmpty;

            if (!brickRef.IsMixed)
                return false;

            int occOffset = pool.OccupancyOffset(brickRef.PoolIndex);
            var occArray = pool.Occupancy;
            ulong acc = 0UL;
            for (int w = 0; w < VoxelDimensions.OccupancyWordsPerBrick; w++)
                acc |= occArray[occOffset + w];

            return acc != 0UL;
        }
    }
}
