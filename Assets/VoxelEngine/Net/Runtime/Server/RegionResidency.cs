using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Streaming.Api;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Network-side region interest policy: hot when an active player is nearby, warm while the
    /// rollback/interest tail is retained, and cold once the region is outside network interest.
    ///
    /// This type deliberately does not load, mutate, write back, or evict world storage directly.
    /// Physical region lifetime belongs to Streaming/Storage; Net computes desired interest state
    /// and forwards residency intent through <see cref="IRegionStreaming"/>.
    /// </summary>
    public static class ServerRegionResidency
    {
        private const int RollbackWindowTicks = 15;

        /// <summary>
        /// Server-side hot radius in metres. Interest radius must never vary by device class.
        /// </summary>
        private const float HotRadiusMetres = 400f;
        private const int WarmLifetimeTicks = 600;
        private const float VoxelSizeMetres = 0.1f;
        private const float RegionWorldEdgeMetres = VoxelGrid.RegionVoxelEdge * VoxelSizeMetres;

        /// <summary>Update network interest states from player positions for the current tick.</summary>
        public static void UpdateRegions(NativeArray<float3> playerPositions,
            ref NativeHashMap<int3, ServerRegionState> regionStates, uint currentTick)
        {
            var keys = regionStates.GetKeyArray(Allocator.Temp);

            foreach (var coord in keys)
            {
                if (!regionStates.TryGetValue(coord, out var state)) continue;

                float3 regionOriginMetres = new float3(
                    state.Coord.x, state.Coord.y, state.Coord.z) * RegionWorldEdgeMetres;

                float minDistToPlayer = float.MaxValue;
                for (int p = 0; p < playerPositions.Length; p++)
                {
                    float d = math.distance(playerPositions[p], regionOriginMetres);
                    if (d < minDistToPlayer)
                        minDistToPlayer = d;
                }

                State newState;
                if (minDistToPlayer <= HotRadiusMetres)
                {
                    newState = State.Hot;
                }
                else if (state.State == State.Warm)
                {
                    uint elapsed = currentTick - state.LastAccessTick;
                    newState = elapsed > WarmLifetimeTicks ? State.Cold : State.Warm;
                }
                else
                {
                    newState = minDistToPlayer <= HotRadiusMetres * 1.5f
                        ? State.Warm
                        : State.Cold;
                }

                state.State = newState;
                state.LastAccessTick = currentTick;
                regionStates[coord] = state;
            }

            keys.Dispose();
        }

        /// <summary>
        /// Reconcile Net's desired residency state through Streaming.Api.
        /// Net never manipulates Storage-owned Region/BrickPool state.
        /// </summary>
        public static void SynchronizeStreaming(
            ref NativeHashMap<int3, ServerRegionState> regionStates,
            IRegionStreaming streaming,
            uint terrainSeed,
            byte requestedMipLevel = 0)
        {
            if (streaming == null) throw new ArgumentNullException(nameof(streaming));

            var keys = regionStates.GetKeyArray(Allocator.Temp);
            foreach (var coord in keys)
            {
                if (!regionStates.TryGetValue(coord, out var state)) continue;

                if (state.State == State.Cold)
                {
                    streaming.Evict(coord);
                }
                else if (!streaming.IsResident(coord))
                {
                    streaming.QueueLoad(new RegionLoadRequest(coord, terrainSeed, requestedMipLevel));
                }
            }
            keys.Dispose();
        }

        /// <summary>Get the current network interest state for a region.</summary>
        public static State GetState(int3 coord, ref NativeHashMap<int3, ServerRegionState> regionStates)
        {
            if (regionStates.TryGetValue(coord, out var state))
                return state.State;

            return State.Cold;
        }
    }

    /// <summary>Server-side network interest states.</summary>
    public enum State : byte
    {
        Hot = 0,
        Warm = 1,
        Cold = 2,
    }

    /// <summary>Per-region state tracked by network interest policy.</summary>
    public struct ServerRegionState : IEquatable<ServerRegionState>
    {
        public int3 Coord;
        public State State;
        public uint LastAccessTick;
        public bool Dirty;

        public bool Equals(ServerRegionState other) =>
            math.all(Coord == other.Coord) && State == other.State;

        public override bool Equals(object obj) =>
            obj is ServerRegionState other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Coord, State);
    }
}
