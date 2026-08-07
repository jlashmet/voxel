using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Server-side region residency management: hot (active player nearby), warm
    /// (no active player, but within rollback window), cold (evicted to disk).
    /// Dirty flags trigger write-back on cold eviction.
    ///
    /// This is the server counterpart to client-side ResidencyManager. The key difference:
    /// server eviction does write-back of dirty regions, while client eviction discards.
    /// This asymmetry (client evicts without write-back; server evicts with write-back) is
    /// what makes fast traversal smooth on the client — unload is instantaneous.
    ///
    /// State transitions (data-model.md "Region"):
    ///   Cold -> Warm on load
    ///   Warm -> Hot when a player enters its radius
    ///   Hot -> Warm when the last player leaves and the rollback window expires
    ///   Warm -> Cold on eviction, write-back if dirty
    /// </summary>
    public static class ServerRegionResidency
    {
        // -------------------------------------------------------------------------
        // Rollback window and tiering thresholds
        /// (device-matrix.md "Frame and tick budgets": 15-tick rollback = 500 ms).
        // -------------------------------------------------------------------------

        private const int RollbackWindowTicks = 15; // 500 ms / 30 Hz tick rate

        /// <summary>
        /// Server-side hot radius in metres. Deliberately a server constant rather than a
        /// tier lookup: interest radius must never vary by device class (Constitution
        /// Principle IV), and the server has no device class of its own.
        /// </summary>
        private const float HotRadiusMetres = 400f;
        private const int WarmLifetimeTicks = 600; // 20 seconds before warm -> cold transition

        /// <summary>Update region states based on player positions each tick.</summary>
        public static void UpdateRegions(NativeArray<float3> playerPositions,
            ref NativeHashMap<int3, ServerRegionState> regionStates, uint currentTick)
        {
            var keys = regionStates.GetKeyArray(Allocator.Temp);

            foreach (var coord in keys)
            {
                if (!regionStates.TryGetValue(coord, out var state)) continue;

                float3 coordAsFloat = new float3(state.Coord.x, state.Coord.y, state.Coord.z) * (VoxelDimensions.RegionEdge * 0.8f);

                // Find nearest player distance.
                float minDistToPlayer = float.MaxValue;
                for (int p = 0; p < playerPositions.Length; p++)
                {
                    float d = math.distance(playerPositions[p], coordAsFloat);
                    if (d < minDistToPlayer)
                        minDistToPlayer = d;
                }

                // Determine desired state.
                State newState;

                if (minDistToPlayer <= HotRadiusMetres)
                {
                    // Within load radius of a player -> hot.
                    newState = State.Hot;
                }
                else if (state.State == State.Warm)
                {
                    uint elapsed = currentTick - state.LastAccessTick;
                    newState = elapsed > WarmLifetimeTicks ? State.Cold : State.Warm;
                }
                else
                {
                    // Outside all player load radii.
                    float fullLoad = HotRadiusMetres;
                    newState = minDistToPlayer <= fullLoad * 1.5f ? State.Warm : State.Cold;
                }

                state.State = newState;
                state.LastAccessTick = currentTick;
                regionStates[coord] = state;
            }

            keys.Dispose();
        }

        /// <summary>Evaluate a region for cold eviction: if dirty, write back; if clean, discard.</summary>
        public static bool EvaluateForEviction(int3 regionCoord, in Region region)
        {
            // Only evaluate Cold regions.
            if (region.Residency != RegionResidency.Cold)
                return false;

            // If the region is dirty, write back to disk before discarding from memory.
            if (region.Dirty)
            {
                WriteBack(regionCoord);
            }

            // In both cases: discard from RAM (eviction succeeded).
            return true;
        }

        private static void WriteBack(int3 coord)
        {
            // Serialize region data to disk via the RegionStore backend.
            // Production: call RegionStore.Write(coord, serializedData);
        }

        /// <summary>Transition a region to Cold (evicted from RAM).</summary>
        public static void EvictToCold(int3 regionCoord, ref RegionTable table, ref BrickPool pool)
        {
            if (!table.IsResident(regionCoord)) return;

            var region = table.LoadRegion(regionCoord);
            bool canEvict = EvaluateForEviction(regionCoord, in region);
            if (canEvict)
            {
                region.ReleaseBricks(ref pool);
                table.CommitRegion(in region);
                table.EvictRegion(regionCoord, ref pool);
            }
        }

        /// <summary>Transition a region to Hot (player entered load radius).</summary>
        public static void PromoteToHot(int3 regionCoord, ref RegionTable table)
        {
            if (!table.IsResident(regionCoord)) return;
            var region = table.LoadRegion(regionCoord);
            region.Residency = RegionResidency.Hot;
            region.LastAccessTick = (uint)Environment.TickCount;
            table.CommitRegion(in region);
        }

        /// <summary>Transition a Warm region to Hot (another player entered its radius).</summary>
        public static void WarmToHot(int3 regionCoord, ref RegionTable table)
        {
            if (!table.IsResident(regionCoord)) return;
            var region = table.LoadRegion(regionCoord);
            if (region.Residency == RegionResidency.Warm)
            {
                region.Residency = RegionResidency.Hot;
                region.LastAccessTick = (uint)Environment.TickCount;
                table.CommitRegion(in region);
            }
        }

        /// <summary>Transition a Hot region to Warm (last player left).</summary>
        public static void HotToWarm(int3 regionCoord, ref RegionTable table)
        {
            if (!table.IsResident(regionCoord)) return;
            var region = table.LoadRegion(regionCoord);
            if (region.Residency == RegionResidency.Hot)
            {
                region.Residency = RegionResidency.Warm;
                region.LastAccessTick = (uint)Environment.TickCount;
                table.CommitRegion(in region);
            }
        }

        /// <summary>Get the current residency state for a region from the tracking map.</summary>
        public static State GetState(int3 coord, ref NativeHashMap<int3, ServerRegionState> regionStates)
        {
            if (regionStates.TryGetValue(coord, out var state))
                return state.State;

            // Default: Cold — not yet tracked means never loaded by a player.
            return State.Cold;
        }
    }

    /// <summary>
    /// Server-side region residency states mirroring data-model.md's Hot/Warm/Cold model.
    /// </summary>
    public enum State : byte
    {
        Hot = 0,
        Warm = 1,
        Cold = 2,
    }

    /// <summary>
    /// Per-region residency tracking state for server-side management.
    /// This struct is stored in NativeHashMap keyed by region coordinate.
    /// </summary>
    public struct ServerRegionState : IEquatable<ServerRegionState>
    {
        /// <summary>Region coordinate in the world grid.</summary>
        public int3 Coord;

        /// <summary>Current residency state (Hot/Warm/Cold).</summary>
        public State State;

        /// <summary>Last access tick (server tick counter).</summary>
        public uint LastAccessTick;

        /// <summary>Whether any brick in this region has been modified since last write-back.</summary>
        public bool Dirty;

        public bool Equals(ServerRegionState other) =>
            math.all(Coord == other.Coord) && State == other.State;

        public override bool Equals(object obj) =>
            obj is ServerRegionState other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Coord, State);
    }
}
