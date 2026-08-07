using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Collision
{
    /// <summary>
    /// Export utilities for converting local brick regions to physics-compatible hull representations.
    ///
    /// Used by the debris and vehicle systems to bridge voxel data into Unity's physics engine.
    /// A hull represents a connected mass of solid voxels as a convex mesh, suitable for
    /// creating a ConvexHull for rigid body simulation.
    /// </summary>
    public static class HullExport
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Maximum vertices per exported hull — Unity's convex hull limit.</summary>
        private const int k_MaxHullVertices = 256;

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Export all solid bricks from a region as a NativeArray of float3 vertices forming
        /// a bounding-box convex hull suitable for Unity physics.
        ///
        /// Only solid (non-empty) bricks contribute to the hull envelope: uniform bricks count
        /// if their material is non-zero, and mixed bricks are always treated as solid since
        /// they occupy a pool slot.
        /// </summary>
        /// <param name="region">The region to export from. Must be resident.</param>
        /// <param name="allocator">Allocator for the returned NativeArray.</param>
        /// <returns>A NativeArray of float3 vertices forming a convex hull (8 bounding-box corners),
        /// or an empty array if the region contains no solid bricks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<float3> ExportHulls(in Region region, Allocator allocator)
        {
            var bounds = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            var extents  = new float3(float.MinValue, float.MinValue, float.MinValue);
            bool found = false;

            int bricksPerAxis = VoxelDimensions.RegionEdge;
            for (int bx = 0; bx < bricksPerAxis && !found; bx++)
            {
                for (int by = 0; by < bricksPerAxis && !found; by++)
                {
                    for (int bz = 0; bz < bricksPerAxis && !found; bz++)
                    {
                        int brickIdx = Region.BrickIndex(bx, by, bz);
                        var brickRef = region.BrickRefs[brickIdx];

                        if (!IsBrickSolid(brickRef))
                            continue;

                        found = true;
                        float3 center = new float3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                        bounds = math.min(bounds, center);
                        extents  = math.max(extents,  center);
                    }
                }
            }

            // Continue collecting full bounds even after finding the first solid brick.
            for (int bx = 0; bx < bricksPerAxis; bx++)
            {
                for (int by = 0; by < bricksPerAxis; by++)
                {
                    for (int bz = 0; bz < bricksPerAxis; bz++)
                    {
                        int brickIdx = Region.BrickIndex(bx, by, bz);
                        var brickRef = region.BrickRefs[brickIdx];

                        if (!IsBrickSolid(brickRef))
                            continue;

                        float3 center = new float3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                        bounds = math.min(bounds, center);
                        extents  = math.max(extents,  center);
                    }
                }
            }

            if (!found)
                return new NativeArray<float3>(0, allocator);

            // Return the 8 corners of the bounding box as a simple convex hull.
            var hull = new NativeArray<float3>(8, allocator, NativeArrayOptions.ClearMemory);
            hull[0] = new float3(bounds.x, bounds.y, bounds.z);
            hull[1] = new float3(extents.x, bounds.y, bounds.z);
            hull[2] = new float3(bounds.x, extents.y, bounds.z);
            hull[3] = new float3(extents.x, extents.y, bounds.z);
            hull[4] = new float3(bounds.x, bounds.y, extents.z);
            hull[5] = new float3(extents.x, bounds.y, extents.z);
            hull[6] = new float3(bounds.x, extents.y, extents.z);
            hull[7] = new float3(extents.x, extents.y, extents.z);

            return hull;
        }

        /// <summary>
        /// Export a bounding-box volume of solid bricks as hull vertices.
        /// Used by debris systems to batch nearby solid bricks into a single physics body.
        /// </summary>
        /// <param name="table">Region table for brick access.</param>
        /// <param name="pool">Brick pool for voxel data access in mixed bricks.</param>
        /// <param name="min">Minimum brick coordinate in world space.</param>
        /// <param name="max">Maximum brick coordinate in world space.</param>
        /// <param name="allocator">Allocator for the returned array.</param>
        /// <returns>Convex hull vertices or empty if no solid bricks found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<float3> ExportHulls(in RegionTable table, in BrickPool pool, int3 min, int3 max, Allocator allocator)
        {
            var bounds = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            var extents  = new float3(float.MinValue, float.MinValue, float.MinValue);
            bool found = false;

            for (int bx = min.x; bx <= max.x; bx++)
            {
                for (int by = min.y; by <= max.y; by++)
                {
                    for (int bz = min.z; bz <= max.z; bz++)
                    {
                        int3 brickCoord = new int3(bx, by, bz);
                        if (!IsSolidAtBrick(table, pool, brickCoord))
                            continue;

                        float3 center = new float3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                        bounds = math.min(bounds, center);
                        extents  = math.max(extents,  center);
                    }
                }
            }

            if (bounds.x > extents.x) // No solid bricks found.
                return new NativeArray<float3>(0, allocator);

            var hull = new NativeArray<float3>(8, allocator, NativeArrayOptions.ClearMemory);
            hull[0] = new float3(bounds.x, bounds.y, bounds.z);
            hull[1] = new float3(extents.x, bounds.y, bounds.z);
            hull[2] = new float3(bounds.x, extents.y, bounds.z);
            hull[3] = new float3(extents.x, extents.y, bounds.z);
            hull[4] = new float3(bounds.x, bounds.y, extents.z);
            hull[5] = new float3(extents.x, bounds.y, extents.z);
            hull[6] = new float3(bounds.x, extents.y, extents.z);
            hull[7] = new float3(extents.x, extents.y, extents.z);

            return hull;
        }

        // -- internal helpers -----------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBrickSolid(in BrickRef brickRef)
        {
            if (brickRef.IsUniform)
                return brickRef.UniformMaterial != VoxelDimensions.MaterialEmpty;

            return brickRef.IsMixed; // Mixed bricks are allocated in the pool and thus solid.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSolidAtBrick(in RegionTable table, in BrickPool pool, int3 brickCoord)
        {
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

            if (!IsBrickSolid(brickRef))
                return false;

            // For mixed bricks, confirm at least one voxel is occupied.
            if (brickRef.IsMixed)
            {
                int occOffset = pool.OccupancyOffset(brickRef.PoolIndex);
                var occArray = pool.Occupancy;
                ulong acc = 0UL;
                for (int w = 0; w < VoxelDimensions.OccupancyWordsPerBrick; w++)
                    acc |= occArray[occOffset + w];

                if (acc == 0UL)
                    return false;
            }

            return true;
        }
    }
}
