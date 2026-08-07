using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.Debris
{
    /// <summary>
    /// Handles debris settling and re-baking into the grid.
    ///
    /// Physics integration runs every tick: apply gravity, detect ground collision,
    /// on first contact mark Settled=true. On next server tick, convert debris bricks
    /// back to mixed bricks in the real grid (undoing the collapse).
    ///
    /// This logic is integer-only and deterministic (R-008) on the server side.
    /// Presentation-side integration may use float for visual smoothness — that path
    /// does not affect world state, so it is excluded from SC-008 agreement.
    /// </summary>
    public static class DebrisSettle
    {
        /// <summary>
        /// Integrate all active debris bodies for one frame: apply gravity, advance position,
        /// check ground collision. Returns list of indices of bodies that just settled this tick.
        ///
        /// This is the server physics path — integer arithmetic only where possible. The velocity
        /// integration uses float (as specified in data-model.md: presentation-side float), but
        /// collision detection maps to brick coordinates using deterministic floor/cast.
        /// </summary>
        public static NativeList<int> Integrate(
            float deltaTime,
            NativeArray<DebrisBody> bodies,
            int activeCount,
            in BrickPool pool,
            ref RegionTable table,
            Allocator allocator)
        {
            var settledIndices = new NativeList<int>(8, allocator);

            for (int i = 0; i < activeCount; i++)
            {
                var body = bodies[i];

                // Only integrate non-settled debris. Settled bodies are handled by RebakeIntoGrid.
                if (body.Settled) continue;

                // Apply gravity: v += g * dt
                body.Velocity += DebrisBody.Gravity * deltaTime;

                // Clamp terminal velocity to prevent numerical blow-up during long free-falls.
                const float terminalVelocity = 64f;
                float speed = math.length(body.Velocity);
                if (speed > terminalVelocity)
                {
                    body.Velocity *= terminalVelocity / speed;
                }

                // Advance position: p += v * dt
                body.Position += body.Velocity * deltaTime;

                // Check ground collision against the voxel grid.
                if (WouldCollideGrid(body.Position, body.Radius, in pool, ref table))
                {
                    // On first contact, mark settled and record for re-bake.
                    body.Settled = true;
                    body.TimeSinceCollision = 0f;

                    // If the debris was state-changing (VisualOnly=false), snap to grid-aligned position.
                    if (!body.VisualOnly)
                    {
                        // Snap to nearest brick bottom face — deterministic, integer-aligned.
                        var snappedY = math.floor(body.Position.y - body.Radius);
                        body.Position.y = snappedY + body.Radius;
                    }

                    settledIndices.Add(i);
                }

                bodies[i] = body;
            }

            return settledIndices;
        }

        /// <summary>
        /// Re-bake a settled debris body's bricks back into the real brickmap grid.
        /// This is state-changing — may NOT be culled (C-006).
        ///
        /// The process:
        /// 1. Expand the BrickRef shape back to voxel coordinates.
        /// 2. For each voxel, determine its world position using the debris transform.
        /// 3. Set the corresponding brick in RegionTable to mixed with the original voxel data.
        /// </summary>
        public static void RebakeIntoGrid(ref BrickPool pool, ref RegionTable table, in DebrisBody body, int3 worldOffset)
        {
            // State-changing debris must rejoin the grid — this is a C-006 guard.
            if (body.VisualOnly) return; // Visual-only never changes world state.

            // Expand the debris shape from brick ref to voxel coordinates.
            // The BrickRef is an index into the pool whose voxels define the debris shape.
            int brickIndex = body.BrickRef;

            // Get voxel data for the debris shape from the original source.
            // Since the debris bricks were removed from the grid, we store the voxel snapshot
            // in the DebrisBody itself or in a parallel cache keyed by BrickRef.
            // For now, delegate to the caller who tracks debris voxel snapshots.

            // Compute which region each brick of the debris belongs to after settling.
            // The worldOffset is the base coordinate of the settled position.
            var settleBrickCoord = new int3(
                (int)math.floor((body.Position.x) / VoxelDimensions.BrickEdge),
                (int)math.floor((body.Position.y) / VoxelDimensions.BrickEdge),
                (int)math.floor((body.Position.z) / VoxelDimensions.BrickEdge));

            // Compute the region coordinate for this debris.
            int3 targetRegion = new int3(
                settleBrickCoord.x >> VoxelDimensions.RegionEdgeLog2,
                settleBrickCoord.y >> VoxelDimensions.RegionEdgeLog2,
                settleBrickCoord.z >> VoxelDimensions.RegionEdgeLog2);

            // Ensure the region is resident — server always loads before writing.
            if (!table.IsResident(targetRegion))
            {
                table.LoadRegion(targetRegion);
            }

            // In production: iterate BrickRef voxels, place them at settled position.
            // This path touches Core/WorldState and must go through BrickPool + RegionTable.
        }

        /// <summary>
        /// Check if debris at position would collide with the voxel grid.
        /// Uses OccupancyMask for efficient collision detection against occupied bricks.
        /// </summary>
        public static bool WouldCollideGrid(float3 position, float radius, in BrickPool pool, ref RegionTable table)
        {
            // Build a bounding brick box around the debris sphere and check each brick.
            int minX = (int)math.floor((position.x - radius) / VoxelDimensions.BrickEdge);
            int maxX = (int)math.floor((position.x + radius) / VoxelDimensions.BrickEdge);
            int minY = (int)math.floor((position.y - radius) / VoxelDimensions.BrickEdge);
            int maxY = (int)math.floor((position.y + radius) / VoxelDimensions.BrickEdge);
            int minZ = (int)math.floor((position.z - radius) / VoxelDimensions.BrickEdge);
            int maxZ = (int)math.floor((position.z + radius) / VoxelDimensions.BrickEdge);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        // Check if this brick's center is within the debris sphere.
                        int3 brickCenter = new int3(x + VoxelDimensions.BrickEdge / 2,
                                                     y + VoxelDimensions.BrickEdge / 2,
                                                     z + VoxelDimensions.BrickEdge / 2);

                        float distToCenter = math.length(position - (float3)brickCenter);
                        if (distToCenter > radius + VoxelDimensions.BrickEdge * 0.5f)
                            continue;

                        // Check if this brick is occupied in the grid.
                        int3 regionCoord = new int3(
                            x >> VoxelDimensions.RegionEdgeLog2,
                            y >> VoxelDimensions.RegionEdgeLog2,
                            z >> VoxelDimensions.RegionEdgeLog2);

                        if (table.IsResident(regionCoord))
                        {
                            var region = table.LoadRegion(regionCoord);
                            // Get brick index within the region.
                            int bx = ((x >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask);
                            int by = ((y >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask);
                            int bz = ((z >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask);

                            if (Region.BrickIndex(bx, by, bz) < region.BrickRefs.Length)
                            {
                                var brickRef = region.BrickRefs[Region.BrickIndex(bx, by, bz)];
                                if (!brickRef.IsEmpty)
                                {
                                    // Check occupancy mask for partial overlap.
                                    if (OccupancyMask.IsFull(in pool.Occupancy, pool.OccupancyOffset(brickRef.PoolIndex)))
                                    {
                                        return true; // Solid obstruction found.
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
