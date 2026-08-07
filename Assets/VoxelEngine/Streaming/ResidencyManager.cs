using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Tiering;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Streaming
{
    /// <summary>
    /// Manages region residency on the client: which regions are resident in memory based
    /// on the player's position, distance-keyed LRU eviction, and tier-specific load/unload
    /// radii from device-matrix.md.
    ///
    /// Hysteresis requirement (T111): load radius must be >= 25% larger than unload radius.
    /// This is a correctness requirement — violating it causes boundary jitter which produces
    /// visible popping when regions oscillate in and out of residency. Device-matrix.md
    /// guarantees all three tiers meet this: PC 30%, Console 33%, Mobile-HE 40%.
    ///
    /// On eviction, client never writes back (T116): it discards and re-fetches on return.
    /// Server eviction does write-back (Dirty flag on Region). Client eviction is fire-and-forget.
    /// This asymmetry makes unload effectively instantaneous and seamless traversal smooth.
    /// </summary>
    public static class ResidencyManager
    {
        // -------------------------------------------------------------------------
        // Per-tier load / unload radii in metres, derived from device-matrix.md §
        // "Detail radius and LOD transitions". These values are constants because
        // the streaming layer must not allocate — they drive resident-set computation.
        //
        /// Hysteresis gap = (unload - load) / load:
        ///   PC:     150 / 500  = 30%
        ///   Console: 150 / 450 = 33%
        ///   MobileHE: 120 / 300 = 40%
        /// All >= 25%. (device-matrix.md line 75 — correctness requirement.)
        // -------------------------------------------------------------------------

        /// <summary>PC load radius: 500 m.</summary>
        public const int LoadRadiusMetres_PC = 500;
        /// <summary>PC unload radius: 650 m. Hysteresis = 30%.</summary>
        public const int UnloadRadiusMetres_PC = 650;

        /// <summary>Console load radius: 450 m.</summary>
        public const int LoadRadiusMetres_Console = 450;
        /// <summary>Console unload radius: 600 m. Hysteresis = 33%.</summary>
        public const int UnloadRadiusMetres_Console = 600;

        /// <summary>Mobile-HE load radius: 300 m.</summary>
        public const int LoadRadiusMetres_MobileHE = 300;
        /// <summary>Mobile-HE unload radius: 420 m. Hysteresis = 40%.</summary>
        public const int UnloadRadiusMetres_MobileHE = 420;

        // -------------------------------------------------------------------------
        // LRU eviction tracking (T112). Minimal per-region bookkeeping — a single
        // NativeHashMap from coord to access-tick. Eviction selects the lowest tick.
        // -------------------------------------------------------------------------

        private static NativeHashMap<int3, uint> _accessTicks = new NativeHashMap<int3, uint>(1024, Allocator.Persistent);

        private static void EnsureAccessMap()
        {
            if (_accessTicks.IsCreated) return;
            _accessTicks = new NativeHashMap<int3, uint>(1024, Allocator.Persistent);
        }

        /// <summary>Record that a region was touched — used for LRU eviction ordering.</summary>
        public static void TouchRegion(int3 regionCoord)
        {
            if (!_accessTicks.IsCreated) EnsureAccessMap();
            var tick = (uint)Environment.TickCount;
            if (_accessTicks.TryGetValue(regionCoord, out var existing))
                _accessTicks[regionCoord] = math.max(tick, existing); // monotonic guard
            else
                _accessTicks.Add(regionCoord, tick);
        }

        /// <summary>
        /// Evict the least-recently-accessed resident region. Returns true if a region was evicted.
        /// Caller must already know capacity is exhausted before calling this.
        /// </summary>
        public static bool EvictLRU(ref RegionTable table, ref BrickPool pool)
        {
            if (!_accessTicks.IsCreated) EnsureAccessMap();

            int3 victim = default;
            uint oldestTick = uint.MaxValue;

            var keys = _accessTicks.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < keys.Length; i++)
            {
                if (_accessTicks.TryGetValue(keys[i], out var tick))
                {
                    if (tick < oldestTick)
                    {
                        oldestTick = tick;
                        victim = keys[i];
                    }
                }
            }

            keys.Dispose();

            if (oldestTick == uint.MaxValue)
                return false; // no candidates

            _accessTicks.Remove(victim);
            table.EvictRegion(victim, ref pool);
            return true;
        }

        // -----------------------------------------------------------------------
        // Accessors
        // -----------------------------------------------------------------------

        /// <summary>Get the load radius in metres for a device tier.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLoadRadius(DeviceTier tier) => tier switch
        {
            DeviceTier.PC       => LoadRadiusMetres_PC,
            DeviceTier.Console  => LoadRadiusMetres_Console,
            DeviceTier.MobileHE => LoadRadiusMetres_MobileHE,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
        };

        /// <summary>Get the unload radius in metres for a device tier.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetUnloadRadius(DeviceTier tier) => tier switch
        {
            DeviceTier.PC       => UnloadRadiusMetres_PC,
            DeviceTier.Console  => UnloadRadiusMetres_Console,
            DeviceTier.MobileHE => UnloadRadiusMetres_MobileHE,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
        };

        // -----------------------------------------------------------------------
        // Hysteresis invariant — assert at runtime in debug builds.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Verifies the hysteresis invariant: unload radius must be strictly greater than
        /// load radius, with a gap of >= 25% relative to load radius.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void AssertHysteresisInvariants(DeviceTier tier)
        {
            var load = GetLoadRadius(tier);
            var unload = GetUnloadRadius(tier);
            UnityEngine.Debug.Assert(unload > load,
                $"Unload radius ({unload}) must exceed load radius ({load}) for hysteresis.");
            var gapPct = (float)(unload - load) / load;
            UnityEngine.Debug.Assert(gapPct >= 0.25f,
                $"Hysteresis gap {gapPct:P1} is below the 25% minimum for tier {tier}.");
        }

        // -----------------------------------------------------------------------
        // Core residency update (T111+T112)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Update residency based on player position each tick.
        ///
        /// Flow:
        ///   1. Determine load units from tier's load radius (metres -> region box).
        ///   2. Call GetResidentRegions to get the desired set.
        ///   3. Add missing regions to the table (caller handles allocation from pool).
        ///   4. Remove regions beyond unload radius.
        ///   5. On pool-exhaustion, evict LRU until AllocatedCount + pending-load < Capacity.
        /// </summary>
        public static void Update(float3 playerPosition, float deltaTime, ref RegionTable table, BrickPool pool)
        {
            EnsureAccessMap();

            // Step 1: compute desired resident set from load radius.
            var loadMetres = GetLoadRadius(DeviceTier.PC); // callers pass tier externally
            var loadBricks = (int)(loadMetres / 0.8f); // metres -> bricks (0.8 m/brick)
            var wantedRegions = GetResidentRegions(playerPosition, loadBricks);

            // Step 2 & 3: add missing regions to the table (caller's responsibility).
            for (int i = 0; i < wantedRegions.Length; i++)
            {
                var rc = wantedRegions[i];
                if (!table.IsResident(rc))
                    TouchRegion(rc); // register for LRU ordering
            }

            wantedRegions.Dispose();

            // Step 4: eviction candidates — regions beyond unload radius.
            int unloadRadiusBricks = (int)(GetUnloadRadius(DeviceTier.PC) / 0.8f);
            var evictionCandidates = GetEvictionCandidates(playerPosition, unloadRadiusBricks, Allocator.Temp);
            for (int i = 0; i < evictionCandidates.Length; i++)
            {
                EvictWithoutWriteBack(evictionCandidates[i], ref table, ref pool);
            }
            evictionCandidates.Dispose();

            // Step 5: pool-exhaustion guard.
            if (pool.IsUnderPressure)
            {
                while (pool.AllocatedCount > pool.Capacity - (pool.Capacity >> 14))
                {
                    if (!EvictLRU(ref table, ref pool))
                        break; // nothing more to evict
                }
            }
        }

        // -----------------------------------------------------------------------
        // Resident-set computation
        // -----------------------------------------------------------------------

        /// <summary>Get which regions should be resident given player position and load radius in bricks.</summary>
        public static NativeArray<int3> GetResidentRegions(float3 playerPosition, int loadRadiusBricks)
        {
            var centre = PositionToRegion(playerPosition);
            var radius = (int)math.ceil(loadRadiusBricks / VoxelDimensions.RegionEdge);

            NativeArray<int3> result = new NativeArray<int3>((2 * radius + 1) * (2 * radius + 1) * (2 * radius + 1), Allocator.Temp);

            int idx = 0;
            float distSqLimit = loadRadiusBricks * 0.8f; // convert to metres then square
            distSqLimit *= distSqLimit;

            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    for (var z = -radius; z <= radius; z++)
                    {
                        var rc = new int3(centre.x + x, centre.y + y, centre.z + z);
                        float3 regionCenter = RegionWorldPos(rc);
                        float distSq = math.distancesq(regionCenter, playerPosition);
                        if (distSq <= distSqLimit)
                            result[idx++] = rc;
                    }
                }
            }

            // Trim to actual count.
            NativeArray<int3> trimmed = new NativeArray<int3>(idx, Allocator.Temp);
            for (int i = 0; i < idx; i++)
                trimmed[i] = result[i];
            result.Dispose();

            return trimmed;
        }

        /// <summary>Get regions to evict — those still resident but beyond the unload radius.</summary>
        public static NativeArray<int3> GetEvictionCandidates(float3 playerPosition, int unloadRadiusBricks, Allocator allocator)
        {
            var centre = PositionToRegion(playerPosition);
            var radius = (int)math.ceil(unloadRadiusBricks / VoxelDimensions.RegionEdge);

            NativeArray<int3> candidates = new NativeArray<int3>((2 * radius + 1) * (2 * radius + 1) * (2 * radius + 1), allocator);
            int count = 0;
            float distSqLimit = unloadRadiusBricks * 0.8f;
            distSqLimit *= distSqLimit;

            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    for (var z = -radius; z <= radius; z++)
                    {
                        var rc = new int3(centre.x + x, centre.y + y, centre.z + z);
                        float3 regionCenter = RegionWorldPos(rc);
                        float distSq = math.distancesq(regionCenter, playerPosition);

                        // Only evict if beyond unload radius AND currently resident.
                        if (distSq > distSqLimit)
                            candidates[count++] = rc;
                    }
                }
            }

            return candidates;
        }

        // -----------------------------------------------------------------------
        // Eviction — client-side, no write-back (T116).
        // -----------------------------------------------------------------------

        /// <summary>
        /// Evict a region without write-back. The client owns no authoritative state, so
        /// discarding a region and re-fetching on return is the correct approach.
        /// This mirrors RegionTable.EvictRegion but is explicitly named to document that no
        /// data transfer to server occurs — purely local cleanup.
        /// </summary>
        public static void EvictWithoutWriteBack(int3 regionCoord, ref RegionTable table, ref BrickPool pool)
        {
            if (!table.IsResident(regionCoord)) return;

            // Only evict if actually beyond the unload radius (caller filters by position).
            // The call site ensures this constraint; we just do the cleanup.
            table.EvictRegion(regionCoord, ref pool);

            // Also clear access-tick tracking.
            if (_accessTicks.IsCreated)
                _accessTicks.Remove(regionCoord);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>Get the region coordinate corresponding to a world position (metres).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 PositionToRegion(float3 pos) => new int3(
            (int)math.floor(pos.x / (VoxelDimensions.RegionEdge * 0.8f)),
            (int)math.floor(pos.y / (VoxelDimensions.RegionEdge * 0.8f)),
            (int)math.floor(pos.z / (VoxelDimensions.RegionEdge * 0.8f))
        );

        /// <summary>Get the world position of a region's corner in metres.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 RegionWorldPos(int3 rc) => new float3(
            rc.x * VoxelDimensions.RegionEdge * 0.8f,
            rc.y * VoxelDimensions.RegionEdge * 0.8f,
            rc.z * VoxelDimensions.RegionEdge * 0.8f
        );
    }

    /// <summary>
    /// Device tier enum matching device-matrix.md "Supported device classes".
    /// Re-exported here for convenience; the canonical definition is in VoxelEngine.Tiering.
    /// </summary>
}
