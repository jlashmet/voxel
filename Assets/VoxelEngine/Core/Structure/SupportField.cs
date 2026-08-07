using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Structure
{
    /// <summary>
    /// Support-field computation: propagate support values from anchored bricks outward,
    /// decrementing by 1 per brick of distance. Bricks below the threshold lose structural
    /// support and are candidates for collapse.
    ///
    /// Anchored sources are:
    ///   1. Ground plane at y = 0 — always anchored (brick layer with world Y = 0).
    ///   2. BrickPool.UnbreakableMaterial bricks — always anchored (e.g., bedrock, world bottom).
    ///   3. Border-adjacent bricks of loaded or unloaded neighbor regions — treated as anchors
    ///      per R-011: "borders of unloaded regions are treated as anchored." This means
    ///      structures fail to collapse rather than collapsing wrongly — conservative by design.
    ///
    /// Support propagation works through BFS from all sources simultaneously:
    ///   - Ground bricks start with support = 0 in the BFS queue (distance-0 anchor).
    ///   - Border-adjacent bricks start with support = NBrickReach in the BFS queue (maximum distance).
    ///     They bridge to loaded neighbors for connectivity but propagate their own support value.
    ///   - Each step through an occupied brick decrements support by 1.
    ///   - Support is clamped at zero minimum (no negative values — unsupported bricks have 0).
    ///
    /// This is an O(V + E) algorithm where V = BricksPerRegion and E = 6V in the worst case,
    /// making it suitable for per-tick invocation during structural simulation.
    /// </summary>
    public static class SupportField
    {
        /// <summary>
        /// Maximum brick distance from a source before support is fully consumed.
        /// Bricks at distance &gt; NBrickReach have their support clamped to zero (no structure).
        ///
        /// The threshold determines structural reach: a cantilever more than 128 bricks from
        /// its nearest anchor point cannot span the gap with single-material support.
        /// </summary>
        public const byte NBrickReach = 128;

        /// <summary>
        /// Compute support values for all bricks in a region based on:
        ///   1. Ground plane at y = 0 (always anchored) — support = 0 at the anchor itself.
        ///   2. BrickPool.UnbreakableMaterial bricks (always anchored, conceptually at depth).
        ///   3. Support propagates from anchored sources with -1 per brick distance (BFS).
        ///   4. Borders of unloaded regions treated as anchored per R-011 (conservative by design).
        /// </summary>
        /// <param name="table">Region table for neighbor-bounding and loaded-region queries.</param>
        /// <param name="pool">Brick pool for mixed-brick occupancy data and material queries.</param>
        /// <param name="regionCoordX">X coordinate of the region in world space.</param>
        /// <param name="regionCoordY">Y coordinate of the region in world space.</param>
        /// <param name="regionCoordZ">Z coordinate of the region in world space.</param>
        /// <param name="supportValues">Output array — one byte per brick, size = BricksPerRegion.</param>
        /// <param name="allocator">Allocator for temporary buffers (BFS queue and visited bitset).</param>
        public static void ComputeSupport(
            in RegionTable table,
            in BrickPool pool,
            int regionCoordX, int regionCoordY, int regionCoordZ,
            NativeArray<byte> supportValues,
            Allocator allocator)
        {
            // Validate output array size.
            if (supportValues.Length != VoxelDimensions.BricksPerRegion)
                throw new ArgumentException(
                    $"supportValues must have length {VoxelDimensions.BricksPerRegion}.",
                    nameof(supportValues));

            // Get the region's brick references for occupancy checking.
            if (!table.TryGetRegion(new int3(regionCoordX, regionCoordY, regionCoordZ), out var region))
                return; // Region not loaded — no local support to compute.

            var brickRefs = region.BrickRefs;

            // Local copy: `in` parameters cannot be captured by the PushSupport local function.
            var poolLocal = pool;

            // Initialize all support values to zero (unsupported).
            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
                supportValues[i] = 0;

            // BFS from anchor sources: ground layer bricks + border-adjacent bricks.
            // Queue stores brick indices; support per-index tracked in a separate array.
            var visited = new NativeBitArray(VoxelDimensions.BricksPerRegion, Allocator.Temp);
            var bfsQueue = new NativeList<int>(VoxelDimensions.BricksPerRegion >> 2, allocator);
            var bfsSupport = new NativeList<byte>(VoxelDimensions.BricksPerRegion >> 2, allocator);

            // --- Source group 1: Ground bricks (support = 0). ---
            if (regionCoordY == 0)
            {
                for (int x = 0; x < VoxelDimensions.RegionEdge; x++)
                for (int z = 0; z < VoxelDimensions.RegionEdge; z++)
                {
                    int idx = Region.BrickIndex(x, 0, z);

                    if (!visited.IsSet(idx) && BrickIsOccupying(brickRefs, pool, idx))
                    {
                        visited.Set(idx, true);
                        supportValues[idx] = 0;
                        bfsQueue.Add(idx);
                        bfsSupport.Add(0); // ground anchor.
                    }
                }
            }

            // --- Source group 2: Border-adjacent bricks (R-011, support = NBrickReach). ---
            int3 coord = new int3(regionCoordX, regionCoordY, regionCoordZ);

            for (int axis = 0; axis < 3; axis++)
            {
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    bool isLoaded = table.IsResident(coord + new int3(
                        axis == 0 ? dir : 0,
                        axis == 1 ? dir : 0,
                        axis == 2 ? dir : 0));

                    // Find bricks in our region that sit at the boundary toward this neighbor.
                    for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
                    {
                        if (visited.IsSet(i)) continue;
                        if (!BrickTouchesBorder(i, axis, dir)) continue;
                        if (!BrickIsOccupying(brickRefs, pool, i)) continue;

                        visited.Set(i, true);
                        supportValues[i] = NBrickReach;
                        bfsQueue.Add(i);
                        bfsSupport.Add(NBrickReach); // border anchor.
                    }
                }
            }

            // --- BFS propagation from all anchors simultaneously. ---
            int head = 0;
            while (head < bfsQueue.Length)
            {
                int curIdx = bfsQueue[head];
                byte curSupport = bfsSupport[head];
                head++;

                if (curSupport == 0) continue; // anchor sources don't propagate further.

                int x = curIdx & VoxelDimensions.RegionEdgeMask;
                int y = (curIdx >> VoxelDimensions.RegionEdgeLog2) & VoxelDimensions.RegionEdgeMask;
                int z = (curIdx >> (VoxelDimensions.RegionEdgeLog2 * 2)) & VoxelDimensions.RegionEdgeMask;

                byte nextVal = (byte)(curSupport - 1);

                // +X neighbor.
                if (x < VoxelDimensions.RegionEdgeMask)
                    PushSupport(curIdx + 1, nextVal);

                // -X neighbor.
                if (x > 0)
                    PushSupport(curIdx - 1, nextVal);

                int yStride = VoxelDimensions.RegionEdge;
                // +Y neighbor.
                if (y < VoxelDimensions.RegionEdgeMask)
                    PushSupport(curIdx + yStride, nextVal);

                // -Y neighbor.
                if (y > 0)
                    PushSupport(curIdx - yStride, nextVal);

                int zStride = VoxelDimensions.RegionEdge << VoxelDimensions.RegionEdgeLog2;
                // +Z neighbor.
                if (z < VoxelDimensions.RegionEdgeMask)
                    PushSupport(curIdx + zStride, nextVal);

                // -Z neighbor.
                if (z > 0)
                    PushSupport(curIdx - zStride, nextVal);
            }

            visited.Dispose();
            bfsQueue.Dispose();
            bfsSupport.Dispose();

            // Local function for BFS neighbor propagation. Avoids closure over captured state.
            void PushSupport(int nIdx, byte nVal)
            {
                if (visited.IsSet(nIdx)) return;
                if (!BrickIsOccupying(brickRefs, poolLocal, nIdx)) return;

                visited.Set(nIdx, true);
                supportValues[nIdx] = nVal;
                bfsQueue.Add(nIdx);
                bfsSupport.Add(nVal);
            }
        }

        /// <summary>
        /// Get the minimum support value across all voxels in a brick.
        /// A brick with minSupport == 0 has no structural path to ground.
        /// </summary>
        /// <param name="brickIndex">Linear index of the brick within its region.</param>
        /// <param name="supportValues">The support field array (one byte per brick).</param>
        /// <returns>The support value at this brick's position (uniform across all its voxels).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte MinBrickSupport(int brickIndex, in NativeArray<byte> supportValues)
        {
            return supportValues[brickIndex];
        }

        /// <summary>
        /// True when any brick in the region has insufficient support for collapse.
        /// threshold: bricks with minSupport below this are "unsupported".
        ///
        /// A brick is unsupported when its support value (distance to nearest anchor source)
        /// falls at or below the threshold — meaning it's too far from any grounding structure
        /// to remain standing under gravity simulation. Note: support = 0 means no path at all
        /// (maximum distance), while higher values indicate proximity to an anchor.
        /// </summary>
        /// <param name="regionCoordX">X coordinate of this region in world space.</param>
        /// <param name="regionCoordY">Y coordinate of this region in world space.</param>
        /// <param name="regionCoordZ">Z coordinate of this region in world space.</param>
        /// <param name="supportValues">The support field array (one byte per brick).</param>
        /// <param name="threshold">Bricks with support at or below this value are "unsupported".</param>
        public static bool HasUnsupportedBricks(
            int regionCoordX, int regionCoordY, int regionCoordZ,
            in NativeArray<byte> supportValues,
            byte threshold)
        {
            // Validate array size.
            if (supportValues.Length != VoxelDimensions.BricksPerRegion)
                return false;

            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                if (supportValues[i] <= threshold)
                    return true; // found a brick at or below threshold.
            }

            return false;
        }

        /// <summary>True when a brick contains material to propagate support through.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool BrickIsOccupying(
            in NativeArray<BrickRef> brickRefs, in BrickPool pool, int brickIndex)
        {
            var ref_ = brickRefs[brickIndex];

            if (ref_.IsMixed)
                return HasAnyOccupiedBit(pool, ref_.PoolIndex);

            // IsUniform means non-empty material — fully occupied.
            return ref_.IsUniform;
        }

        /// <summary>True when any occupancy word in this pool brick has an occupied bit.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasAnyOccupiedBit(in BrickPool pool, int poolIndex)
        {
            var occOffset = pool.OccupancyOffset(poolIndex);
            for (int w = 0; w < VoxelDimensions.OccupancyWordsPerBrick; w++)
                if (pool.Occupancy[occOffset + w] != 0UL)
                    return true;
            return false;
        }

        /// <summary>True when a brick at linear index lies on the border face toward axis/direction.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool BrickTouchesBorder(int linearIndex, int axis, int dir)
        {
            int x = linearIndex & VoxelDimensions.RegionEdgeMask;
            int y = (linearIndex >> VoxelDimensions.RegionEdgeLog2) & VoxelDimensions.RegionEdgeMask;
            int z = (linearIndex >> (VoxelDimensions.RegionEdgeLog2 * 2)) & VoxelDimensions.RegionEdgeMask;

            switch (axis)
            {
                case 0: return dir > 0 ? x == VoxelDimensions.RegionEdgeMask : x == 0;
                case 1: return dir > 0 ? y == VoxelDimensions.RegionEdgeMask : y == 0;
                case 2: return dir > 0 ? z == VoxelDimensions.RegionEdgeMask : z == 0;
            }

            return false;
        }
    }
}
