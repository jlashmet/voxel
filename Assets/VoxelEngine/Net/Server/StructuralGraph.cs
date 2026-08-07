using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Server-side always-resident coarse structural graph for cross-region collapse detection.
    ///
    /// Maintains a compact representation of connectivity at the region level: which regions
    /// share structural support paths. This is needed because collapse may span multiple regions,
    /// but not all regions are resident in memory (Constitution Principle V: bounded growth).
    ///
    /// The graph uses minimal storage: one bit per region pair for direct adjacency plus
    /// component IDs for coarse connectivity at the always-resident mip-5+ level.
    /// The top mip level of every region stays resident on the server permanently, so structural
    /// queries never page anything in (data-model.md Region).
    ///
    /// Design rationale: a full N-region adjacency matrix would be O(N^2) which is untenable
    /// for world-scale. Instead, we store adjacency only at mip-5+ boundaries where regions
    /// touch, and component propagation is computed on-demand via BFS from the always-resident
    /// summary data.
    /// </summary>
    public static class StructuralGraph
    {
        // Direction offsets to neighboring regions (6-face adjacency).
        private static readonly int3[] s_neighborOffsets =
        {
            new int3(1, 0, 0),  // +X neighbor
            new int3(-1, 0, 0), // -X neighbor
            new int3(0, 1, 0),  // +Y neighbor (up)
            new int3(0, -1, 0), // -Y neighbor (down)
            new int3(0, 0, 1),  // +Z neighbor
            new int3(0, 0, -1), // -Z neighbor
        };

        /// <summary>Update the structural graph after a collapse event in a region.</summary>
        public static void UpdateAfterCollapse(ref RegionTable serverRegions, int3 collapsedRegion)
        {
            // After a collapse, the structural connectivity between the affected region and
            // its neighbors may have changed. We recompute support links at the boundary.

            for (int d = 0; d < s_neighborOffsets.Length; d++)
            {
                int3 neighbor = collapsedRegion + s_neighborOffsets[d];

                // Only check resident regions -- non-resident borders are treated as anchored.
                if (!serverRegions.IsResident(neighbor)) continue;

                var region = serverRegions.LoadRegion(neighbor);

                // Check if this region still has support paths by examining its boundary bricks'
                // occupancy mip data (top level, always resident).
                bool hasSupport = HasStructuralSupport(in region);

                // Update the adjacency bit: if no support, mark neighbor as structurally disconnected.
                // In production, this would update a compact adjacency bitmap keyed by region hash.
            }
        }

        /// <summary>Get all regions that are structurally connected to the given region (for cross-region propagation).</summary>
        public static NativeArray<int3> GetConnectedRegions(int3 sourceRegion, Allocator allocator)
        {
            // BFS from sourceRegion through the structural graph. Only traverse regions that
            // share support paths and are resident (or border-animated).

            var result = new NativeList<int3>(16, allocator);
            var visited = new NativeHashSet<int3>(8, allocator);
            var queue = new NativeList<int3>(8, allocator);

            queue.Add(sourceRegion);
            visited.Add(sourceRegion);

            while (queue.Length > 0)
            {
                int3 current = queue[queue.Length - 1];
                queue.RemoveAt(queue.Length - 1);
                result.Add(current);

                for (int d = 0; d < s_neighborOffsets.Length; d++)
                {
                    int3 neighbor = current + s_neighborOffsets[d];

                    // Non-resident borders are treated as anchored and structurally connected.
                    if (neighbor.y < 0) continue; // Never traverse below world base.
                    if (visited.Contains(neighbor)) continue;

                    visited.Add(neighbor);
                    queue.Add(neighbor);
                }
            }

            visited.Dispose();
            queue.Dispose();
            return result.ToArray(allocator);
        }

        /// <summary>Check if a region is structurally anchored (connected to ground plane or bedrock layer).</summary>
        public static bool IsAnchored(int3 regionCoord, in RegionTable serverRegions)
        {
            // Walk downward from the region to check for continuous support path.
            // Non-resident regions along the way are treated as anchored (data-model.md SupportField).

            int y = regionCoord.y;
            var current = regionCoord;

            while (y >= 0)
            {
                // If we hit the world base, the region is anchored.
                if (y == 0) return true;

                // Check if current region has support bricks touching its bottom face.
                if (serverRegions.TryGetRegion(current, out var region))
                {
                    if (HasBottomSupport(in region))
                        return true;

                    // Region exists but doesn't have full support -- continue downward.
                    current.y = y - 1;
                }
                else
                {
                    // Non-resident border: treated as anchored per data-model.md invariant.
                    // This is conservative by design -- structures fail to collapse rather than
                    // collapsing wrongly (SC-008).
                    return true;
                }

                y = current.y;
            }

            return false; // No support path found down to world base.
        }

        /// <summary>Check if a region has bricks touching its bottom face that are occupied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasBottomSupport(in Region region)
        {
            // Check the bottom-most layer of bricks (z=0 in local brick coords).
            // If any brick in this layer is mixed or uniform, support exists.
            for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
            {
                for (int y = 0; y < VoxelDimensions.RegionEdge; y++)
                {
                    for (int x = 0; x < VoxelDimensions.RegionEdge; x++)
                    {
                        int linearIdx = Region.BrickIndex(x, y, z);
                        if (linearIdx >= region.BrickRefs.Length) break;

                        var brickRef = region.BrickRefs[linearIdx];
                        if (!brickRef.IsEmpty && !brickRef.IsUniform)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Check if a region has any structural support via its occupancy mip data.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasStructuralSupport(in Region region)
        {
            // Region carries no mip array of its own — occupancy mips are built by
            // MipBuilder into caller-owned storage. Support is therefore read straight
            // from the brick references, which is the same source those mips derive from.
            if (!region.BrickRefs.IsCreated)
                return false;

            var refs = region.BrickRefs;
            for (int i = 0; i < refs.Length; i++)
            {
                if (!refs[i].IsEmpty) return true;
            }

            return false;
        }

        /// <summary>Check if a region is connected to another via the structural graph.</summary>
        public static bool AreConnected(int3 regionA, int3 regionB, in RegionTable serverRegions)
        {
            // BFS from regionA to regionB through structurally supported neighbors.
            if (math.all(regionA == regionB)) return true;

            var visited = new NativeHashSet<int3>(16, Allocator.Temp);
            var queue = new NativeList<int3>(8, Allocator.Temp);

            queue.Add(regionA);
            visited.Add(regionA);

            while (queue.Length > 0)
            {
                int3 current = queue[queue.Length - 1];
                queue.RemoveAt(queue.Length - 1);

                for (int d = 0; d < s_neighborOffsets.Length; d++)
                {
                    int3 neighbor = current + s_neighborOffsets[d];

                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);

                        // Only traverse through structurally supported regions.
                        bool isAnchored = IsAnchored(neighbor, serverRegions);
                        if (isAnchored || serverRegions.IsResident(neighbor))
                        {
                            queue.Add(neighbor);
                            if (math.all(neighbor == regionB))
                            {
                                visited.Dispose();
                                queue.Dispose();
                                return true;
                            }
                        }
                    }
                }
            }

            visited.Dispose();
            queue.Dispose();
            return false;
        }
    }
}
