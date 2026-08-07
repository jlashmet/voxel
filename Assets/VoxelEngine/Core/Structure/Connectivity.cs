using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Structure
{
    /// <summary>
    /// Flood-fill connectivity analysis over brick occupancy masks.
    ///
    /// Uses 6-connectivity (faces only, not edges or corners) to determine which bricks
    /// form continuous structural clusters. A connected cluster's anchor status is determined
    /// by whether it touches the ground plane (y = 0) or another anchored region.
    ///
    /// This is the substrate for support propagation (<see cref="SupportField"/>): disconnected
    /// clusters are individually evaluable for collapse. Each pass operates on a single loaded
    /// region and does not cross into unloaded regions — that boundary handling belongs to the
    /// caller (streaming layer), which ensures border bricks are treated as anchored per
    /// data-model.md's SupportField invariant (R-011).
    ///
    /// All operations are integer and bitwise. No floating-point arithmetic. Constitution
    /// Principle III (Determinism) requires cross-client agreement on cluster membership;
    /// this flood-fill is deterministic because it visits bricks in a fixed linear-index order
    /// and uses only 6-connectivity face checks, with neighbors visited in the fixed order
    /// (+X, -X, +Y, -Y, +Z, -Z).
    /// </summary>
    public static class Connectivity
    {
        // -- flood fill ------------------------------------------------------------

        /// <summary>
        /// Flood-fill from a starting brick, returning all bricks in the same connected component.
        /// Uses 6-connectivity (face-adjacent only). Occupancy masks determine walkable voxels.
        /// Returns NativeList of int3 brick coordinates within the region.
        ///
        /// A brick is "occupying" (i.e., part of the structural graph) when:
        ///   - it is mixed (<see cref="BrickRef.IsMixed"/>), with at least one occupied bit in its
        ///     pool occupancy, or
        ///   - it is uniform and non-empty (<see cref="BrickRef.IsUniform"/>&amp;&amp; !<see cref="BrickRef.IsEmpty"/>
        ///     — encoded as BrickRef value &lt; 0 where the material value is non-zero).
        /// Empty bricks (value == -1) have no material and cannot be structurally connected.
        ///
        /// Two bricks are face-adjacent when they differ by exactly 1 in one coordinate axis
        /// and share a face (not edge or corner). The six neighbors of brick (x, y, z) within
        /// the region boundary are: (x&#xb1;1, y, z), (x, y&#xb1;1, z), (x, y, z&#xb1;1).
        ///
        /// This is an O(n) BFS where n &le; <see cref="VoxelDimensions.BricksPerRegion"/> (262,144)
        /// for the worst case of a fully-occupied region. Memory: NativeList&lt;int&gt; queue +
        /// NativeBitArray visited — both bounded by BricksPerRegion entries each.
        /// </summary>
        /// <param name="brickRefs">The region's brick reference array (one per brick).</param>
        /// <param name="pool">Brick pool providing occupancy data for mixed bricks.</param>
        /// <param name="startBrickIndex">Linear index [0..262143] of the starting brick within the region.</param>
        /// <param name="allocator">Allocator for the returned NativeList.</param>
        /// <returns>A NativeList of int3 (x, y, z) brick coordinates belonging to the same component.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when startBrickIndex is out of range.</exception>
        public static NativeList<int3> FloodFill(
            in NativeArray<BrickRef> brickRefs,
            in BrickPool pool,
            int startBrickIndex,
            Allocator allocator)
        {
            if ((uint)startBrickIndex >= (uint)VoxelDimensions.BricksPerRegion)
                throw new ArgumentOutOfRangeException(
                    nameof(startBrickIndex), "Must be in [0 .. BricksPerRegion).");

            // Validate the start brick is occupying (has material to connect from).
            if (!BrickHasOccupation(brickRefs, pool, startBrickIndex))
                return new NativeList<int3>(0, allocator);

            var visited = new NativeBitArray(VoxelDimensions.BricksPerRegion, Allocator.Temp);
            var bfs     = new NativeList<int>(VoxelDimensions.BricksPerRegion >> 2, Allocator.Temp);
            var result  = new NativeList<int3>(VoxelDimensions.BricksPerRegion >> 2, allocator);

            // BFS seed.
            visited.Set(startBrickIndex, true);
            bfs.Add(startBrickIndex);

            int head = 0;
            while (head < bfs.Length)
            {
                int curIdx = bfs[head++];
                result.Add(BrickCoords(curIdx));

                // Enqueue face-adjacent neighbors in ascending-index order for determinism.
                int x = curIdx & VoxelDimensions.RegionEdgeMask;
                int y = (curIdx >> VoxelDimensions.RegionEdgeLog2) & VoxelDimensions.RegionEdgeMask;
                int z = (curIdx >> (VoxelDimensions.RegionEdgeLog2 * 2)) & VoxelDimensions.RegionEdgeMask;

                // +X neighbor.
                if (x < VoxelDimensions.RegionEdgeMask) EnqueueIfOccupying(brickRefs, pool, visited, ref bfs, curIdx + 1);

                // -X neighbor.
                if (x > 0) EnqueueIfOccupying(brickRefs, pool, visited, ref bfs, curIdx - 1);

                // +Y neighbor.
                int yStride = VoxelDimensions.RegionEdge;
                if (y < VoxelDimensions.RegionEdgeMask) EnqueueIfOccupying(brickRefs, pool, visited, ref bfs, curIdx + yStride);

                // -Y neighbor.
                if (y > 0) EnqueueIfOccupying(brickRefs, pool, visited, ref bfs, curIdx - yStride);

                // +Z neighbor.
                int zStride = VoxelDimensions.RegionEdge << VoxelDimensions.RegionEdgeLog2;
                if (z < VoxelDimensions.RegionEdgeMask) EnqueueIfOccupying(brickRefs, pool, visited, ref bfs, curIdx + zStride);

                // -Z neighbor.
                if (z > 0) EnqueueIfOccupying(brickRefs, pool, visited, ref bfs, curIdx - zStride);
            }

            visited.Dispose();
            bfs.Dispose();
            return result;
        }

        /// <summary>
        /// Tag all bricks in a region with a connected component ID. Writes into componentIds array
        /// (one entry per brick in the region, size = BricksPerRegion). Returns the total number
        /// of unique components found.
        ///
        /// Every brick — occupied or not — receives a component ID:
        ///   - Occupied bricks get IDs in [1 .. count], partitioning the space into connected sets.
        ///   - Empty bricks get ID 0 (unassigned) because they have no structural role.
        ///
        /// Uniform-occupied bricks (encoded as BrickRef &lt; 0, material != 0) are treated as
        /// fully-solid (all 512 voxels occupied), so they bridge between mixed brick clusters
        /// that would otherwise appear disconnected in the occupancy representation.
        ///
        /// Algorithm: iterate all bricks in ascending index order. When an unassigned occupied
        /// brick is found, start a BFS flood-fill from it — that's one component. Continue until
        /// every brick has been visited or assigned.
        ///
        /// Complexity: O(V + E) where V = BricksPerRegion and E = 6V in the worst case (fully-
        /// occupied region). The adjacency list is implicit from lattice topology.
        /// </summary>
        /// <param name="brickRefs">The region's brick reference array.</param>
        /// <param name="pool">Brick pool for mixed-brick occupancy data.</param>
        /// <param name="componentIds">Output array — one int per brick, size must be BricksPerRegion.</param>
        /// <returns>The total number of unique connected components found.</returns>
        public static int LabelComponents(
            in NativeArray<BrickRef> brickRefs,
            in BrickPool pool,
            NativeArray<int> componentIds)
        {
            if (componentIds.Length != VoxelDimensions.BricksPerRegion)
                throw new ArgumentException(
                    $"componentIds must have length {VoxelDimensions.BricksPerRegion}.",
                    nameof(componentIds));

            int componentCount = 0;

            var visited   = new NativeBitArray(VoxelDimensions.BricksPerRegion, Allocator.Temp);
            var bfs       = new NativeList<int>(64, Allocator.Temp);

            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                // Skip bricks that are already assigned or not occupying.
                if (componentIds[i] != 0) continue;
                if (!BrickHasOccupation(brickRefs, pool, i)) continue;

                componentCount++;
                int compId = componentCount;

                visited.Set(i, true);
                bfs.Add(i);
                componentIds[i] = compId;

                int head = 0;
                while (head < bfs.Length)
                {
                    int curIdx = bfs[head++];

                    int x = curIdx & VoxelDimensions.RegionEdgeMask;
                    int y = (curIdx >> VoxelDimensions.RegionEdgeLog2) & VoxelDimensions.RegionEdgeMask;
                    int z = (curIdx >> (VoxelDimensions.RegionEdgeLog2 * 2)) & VoxelDimensions.RegionEdgeMask;

                    if (x < VoxelDimensions.RegionEdgeMask) TagOrEnqueue(brickRefs, pool, visited, ref bfs, componentIds, curIdx + 1, compId);
                    if (x > 0)                                TagOrEnqueue(brickRefs, pool, visited, ref bfs, componentIds, curIdx - 1, compId);

                    int yStride = VoxelDimensions.RegionEdge;
                    if (y < VoxelDimensions.RegionEdgeMask) TagOrEnqueue(brickRefs, pool, visited, ref bfs, componentIds, curIdx + yStride, compId);
                    if (y > 0)                                TagOrEnqueue(brickRefs, pool, visited, ref bfs, componentIds, curIdx - yStride, compId);

                    int zStride = VoxelDimensions.RegionEdge << VoxelDimensions.RegionEdgeLog2;
                    if (z < VoxelDimensions.RegionEdgeMask) TagOrEnqueue(brickRefs, pool, visited, ref bfs, componentIds, curIdx + zStride, compId);
                    if (z > 0)                                TagOrEnqueue(brickRefs, pool, visited, ref bfs, componentIds, curIdx - zStride, compId);
                }

                bfs.Clear();
            }

            visited.Dispose();
            bfs.Dispose();
            return componentCount;
        }

        /// <summary>
        /// Check if a brick's connected component is anchored (touches the ground plane or
        /// an unrebreakable structure). Ground plane is at y = 0.
        ///
        /// A component is anchored when any of:
        ///   1. Any occupied brick in the component has world Y coordinate = 0 (ground contact).
        ///      World Y = regionCoordY * <see cref="VoxelDimensions.RegionEdge"/> + (localBrickY * BrickEdge);
        ///      ground at Y = 0 requires regionCoordY == 0 and local bricks in the bottom layer.
        ///   2. Any brick in the component is adjacent to a loaded neighbor region that shares
        ///      connectivity (cross-region anchoring — caller queries <see cref="RegionTable"/>).
        ///   3. The brick lies on an unloaded-border per R-011 (conservative: structures fail to
        ///      collapse rather than collapsing wrongly).
        ///
        /// For single-region analysis, only condition (1) applies directly. Cross-region
        /// anchoring requires querying neighboring <see cref="RegionTable"/> entries.
        /// </summary>
        /// <param name="table">Region table for cross-region neighbor queries.</param>
        /// <param name="brickRefs">The region's brick reference array.</param>
        /// <param name="pool">Brick pool for mixed-brick occupancy data.</param>
        /// <param name="regionCoordX">X coordinate of this region in world space.</param>
        /// <param name="regionCoordY">Y coordinate of this region in world space.</param>
        /// <param name="regionCoordZ">Z coordinate of this region in world space.</param>
        /// <param name="componentId">The component ID to check (from <see cref="LabelComponents"/> output).</param>
        /// <returns>True if the component touches the ground plane at y = 0 or another anchored source.</returns>
        public static bool IsComponentAnchored(
            in RegionTable table,
            in NativeArray<BrickRef> brickRefs,
            in BrickPool pool,
            int regionCoordX, int regionCoordY, int regionCoordZ,
            int componentId)
        {
            // Condition 1: ground plane (y == 0).
            if (regionCoordY == 0)
            {
                var floodResult = FloodFill(brickRefs, pool, 0, Allocator.Temp);
                for (int i = 0; i < floodResult.Length; i++)
                {
                    if (floodResult[i].y == 0)
                    {
                        floodResult.Dispose();
                        return true; // bottom-layer occupied — touches ground.
                    }
                }

                floodResult.Dispose();
            }

            // Condition 2 & 3: border anchoring (loaded and unloaded neighbors).
            int3 coord = new int3(regionCoordX, regionCoordY, regionCoordZ);

            for (int axis = 0; axis < 3; axis++)
            {
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    // Border-anchoring: any occupying brick on the boundary face toward this
                    // neighbor qualifies. Loaded neighbors contribute connectivity; unloaded ones
                    // qualify per R-011 regardless of their content.
                    if (ComponentTouchesBorder(brickRefs, pool, axis, dir))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Get the component ID for a specific brick within a region.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetComponentId(
            int brickIndex,
            in NativeArray<int> componentIds)
        {
            return componentIds[brickIndex];
        }

        // -- private helpers -------------------------------------------------------

        /// <summary>Convert a linear brick index to int3 coordinates within the region.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 BrickCoords(int linearIndex) => new(
            linearIndex & VoxelDimensions.RegionEdgeMask,
            (linearIndex >> VoxelDimensions.RegionEdgeLog2) & VoxelDimensions.RegionEdgeMask,
            (linearIndex >> (VoxelDimensions.RegionEdgeLog2 * 2)) & VoxelDimensions.RegionEdgeMask);

        /// <summary>True when the brick at linear index has material to connect.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool BrickHasOccupation(
            in NativeArray<BrickRef> brickRefs, in BrickPool pool, int brickIndex)
        {
            var ref_ = brickRefs[brickIndex];

            if (ref_.IsMixed)
                return BrickHasPoolOccupation(pool, ref_.PoolIndex);

            // IsUniform implies non-empty material — that brick is fully occupied.
            return ref_.IsUniform;
        }

        /// <summary>True when any occupancy word in this pool brick has an occupied bit.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool BrickHasPoolOccupation(in BrickPool pool, int poolIndex)
        {
            var occOffset = pool.OccupancyOffset(poolIndex);
            for (int w = 0; w < VoxelDimensions.OccupancyWordsPerBrick; w++)
                if (pool.Occupancy[occOffset + w] != 0UL)
                    return true;
            return false;
        }

        /// <summary>Enqueue a neighbor into BFS if it is occupying and unvisited.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnqueueIfOccupying(
            in NativeArray<BrickRef> brickRefs, in BrickPool pool,
            NativeBitArray visited, ref NativeList<int> bfs, int neighborIdx)
        {
            if (visited.IsSet(neighborIdx)) return;
            if (!BrickHasOccupation(brickRefs, pool, neighborIdx)) return;

            visited.Set(neighborIdx, true);
            bfs.Add(neighborIdx);
        }

        /// <summary>Enqueue a neighbor into BFS and tag it with a component ID.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TagOrEnqueue(
            in NativeArray<BrickRef> brickRefs, in BrickPool pool,
            NativeBitArray visited, ref NativeList<int> bfs,
            NativeArray<int> componentIds, int neighborIdx, int compId)
        {
            if (visited.IsSet(neighborIdx)) return;
            if (!BrickHasOccupation(brickRefs, pool, neighborIdx)) return;

            visited.Set(neighborIdx, true);
            bfs.Add(neighborIdx);
            componentIds[neighborIdx] = compId;
        }

        /// <summary>True when any occupying brick touches the boundary face toward axis/direction.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ComponentTouchesBorder(
            in NativeArray<BrickRef> brickRefs, in BrickPool pool, int axis, int dir)
        {
            // Scan the boundary slice at this axis/direction for any occupying brick.
            switch (axis)
            {
                case 0: // X-axis boundary.
                    {
                        int x = dir > 0 ? VoxelDimensions.RegionEdgeMask : 0;
                        for (int y = 0; y < VoxelDimensions.RegionEdge; y++)
                        for (int z = 0; z < VoxelDimensions.RegionEdge; z++)
                            if (BrickHasOccupation(brickRefs, pool, Region.BrickIndex(x, y, z)))
                                return true;
                    } break;

                case 1: // Y-axis boundary.
                    {
                        int y = dir > 0 ? VoxelDimensions.RegionEdgeMask : 0;
                        for (int x = 0; x < VoxelDimensions.RegionEdge; x++)
                        for (int z = 0; z < VoxelDimensions.RegionEdge; z++)
                            if (BrickHasOccupation(brickRefs, pool, Region.BrickIndex(x, y, z)))
                                return true;
                    } break;

                case 2: // Z-axis boundary.
                    {
                        int z = dir > 0 ? VoxelDimensions.RegionEdgeMask : 0;
                        for (int x = 0; x < VoxelDimensions.RegionEdge; x++)
                        for (int y = 0; y < VoxelDimensions.RegionEdge; y++)
                            if (BrickHasOccupation(brickRefs, pool, Region.BrickIndex(x, y, z)))
                                return true;
                    } break;
            }

            return false;
        }
    }
}
