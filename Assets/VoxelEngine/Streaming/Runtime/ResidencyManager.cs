using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Tiering.Api;

namespace VoxelEngine.Streaming.Runtime
{
    /// <summary>
    /// Manages client region-residency policy: desired radius, hysteresis and LRU eviction.
    /// Storage owns the mechanics of making a region resident and releasing its memory.
    /// </summary>
    public static class ResidencyManager
    {
        public const int LoadRadiusMetres_PC = 500;
        public const int UnloadRadiusMetres_PC = 650;
        public const int LoadRadiusMetres_Console = 450;
        public const int UnloadRadiusMetres_Console = 600;
        public const int LoadRadiusMetres_MobileHE = 300;
        public const int UnloadRadiusMetres_MobileHE = 420;

        private const int EvictionScanRegionsPerFrame = 64;
        private static int _evictionScanCursor;
        private static NativeHashMap<int3, uint> _accessTicks =
            new NativeHashMap<int3, uint>(1024, Allocator.Persistent);

        private static void EnsureAccessMap()
        {
            if (_accessTicks.IsCreated) return;
            _accessTicks = new NativeHashMap<int3, uint>(1024, Allocator.Persistent);
        }

        public static void TouchRegion(int3 regionCoord)
        {
            if (!_accessTicks.IsCreated) EnsureAccessMap();
            uint tick = (uint)Environment.TickCount;
            if (_accessTicks.TryGetValue(regionCoord, out uint existing))
                _accessTicks[regionCoord] = math.max(tick, existing);
            else
                _accessTicks.Add(regionCoord, tick);
        }

        /// <summary>Evicts the least-recently-accessed tracked region.</summary>
        public static bool EvictLRU(IRegionResidencyStore storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (!_accessTicks.IsCreated) EnsureAccessMap();

            int3 victim = default;
            uint oldestTick = uint.MaxValue;
            using NativeArray<int3> keys = _accessTicks.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                if (!_accessTicks.TryGetValue(keys[i], out uint tick) || tick >= oldestTick)
                    continue;
                oldestTick = tick;
                victim = keys[i];
            }

            if (oldestTick == uint.MaxValue) return false;

            _accessTicks.Remove(victim);
            storage.EvictRegion(victim);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLoadRadius(DeviceTier tier) => tier switch
        {
            DeviceTier.PC => LoadRadiusMetres_PC,
            DeviceTier.Console => LoadRadiusMetres_Console,
            DeviceTier.MobileHE => LoadRadiusMetres_MobileHE,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetUnloadRadius(DeviceTier tier) => tier switch
        {
            DeviceTier.PC => UnloadRadiusMetres_PC,
            DeviceTier.Console => UnloadRadiusMetres_Console,
            DeviceTier.MobileHE => UnloadRadiusMetres_MobileHE,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
        };

        [Conditional("UNITY_EDITOR")]
        public static void AssertHysteresisInvariants(DeviceTier tier)
        {
            int load = GetLoadRadius(tier);
            int unload = GetUnloadRadius(tier);
            UnityEngine.Debug.Assert(unload > load,
                $"Unload radius ({unload}) must exceed load radius ({load}) for hysteresis.");
            float gapPct = (float)(unload - load) / load;
            UnityEngine.Debug.Assert(gapPct >= 0.25f,
                $"Hysteresis gap {gapPct:P1} is below the 25% minimum for tier {tier}.");
        }

        /// <summary>
        /// Updates desired residency and eviction policy around the player. Missing wanted regions
        /// are only registered for load ordering here; RegionLoader publishes completed loads.
        /// </summary>
        public static void Update(float3 playerPosition, float deltaTime,
                                  IRegionResidencyStore storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            EnsureAccessMap();
            _ = deltaTime;

            int loadMetres = GetLoadRadius(DeviceTier.PC);
            int loadBlocks = (int)(loadMetres / 0.8f);
            using (NativeArray<int3> wantedRegions = GetResidentRegions(playerPosition, loadBlocks))
            {
                for (int i = 0; i < wantedRegions.Length; i++)
                {
                    int3 regionCoord = wantedRegions[i];
                    if (!storage.IsRegionResident(regionCoord))
                        TouchRegion(regionCoord);
                }
            }

            int unloadRadiusBlocks = (int)(GetUnloadRadius(DeviceTier.PC) / 0.8f);
            EvictFarResidents(
                playerPosition, unloadRadiusBlocks, storage, ref _evictionScanCursor,
                EvictionScanRegionsPerFrame);

            StoragePressure pressure = storage.Pressure;
            if (!pressure.IsUnderPressure) return;

            while (pressure.UsedBytes > pressure.CriticalLimitBytes)
            {
                if (!EvictLRU(storage)) break;
                pressure = storage.Pressure;
            }
        }

        public static NativeArray<int3> GetResidentRegions(float3 playerPosition,
                                                            int loadRadiusBlocks)
        {
            int3 centre = PositionToRegion(playerPosition);
            int radius = (int)math.ceil(
                loadRadiusBlocks / (float)VoxelReadGrid.BlocksPerRegionEdge);

            NativeArray<int3> result = new NativeArray<int3>(
                (2 * radius + 1) * (2 * radius + 1) * (2 * radius + 1), Allocator.Temp);

            int index = 0;
            float distanceSquaredLimit = loadRadiusBlocks * 0.8f;
            distanceSquaredLimit *= distanceSquaredLimit;
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            for (int z = -radius; z <= radius; z++)
            {
                int3 regionCoord = new int3(centre.x + x, centre.y + y, centre.z + z);
                float3 regionCenter = RegionWorldPos(regionCoord);
                if (math.distancesq(regionCenter, playerPosition) <= distanceSquaredLimit)
                    result[index++] = regionCoord;
            }

            NativeArray<int3> trimmed = new NativeArray<int3>(index, Allocator.Temp);
            for (int i = 0; i < index; i++) trimmed[i] = result[i];
            result.Dispose();
            return trimmed;
        }

        /// <summary>
        /// Examines at most <paramref name="maxRegionsToScan"/> actual resident regions and
        /// evicts those outside the unload sphere. Unlike the legacy geometric shell query this
        /// eventually reaches regions left far behind the player, while keeping per-frame work
        /// strictly bounded and allocation-free.
        /// </summary>
        public static int EvictFarResidents(float3 playerPosition, int unloadRadiusBlocks,
                                            IRegionResidencyStore storage, ref int scanCursor,
                                            int maxRegionsToScan = EvictionScanRegionsPerFrame)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (maxRegionsToScan <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRegionsToScan));

            float distanceLimit = unloadRadiusBlocks * 0.8f;
            float distanceSquaredLimit = distanceLimit * distanceLimit;
            int evicted = 0;
            int examined = 0;
            while (examined < maxRegionsToScan)
            {
                if (!storage.TryGetNextResidentCoord(ref scanCursor, out int3 regionCoord))
                {
                    scanCursor = 0;
                    break;
                }

                examined++;
                if (math.distancesq(RegionWorldPos(regionCoord), playerPosition)
                    <= distanceSquaredLimit)
                    continue;

                if (!storage.EvictRegion(regionCoord)) continue;
                if (_accessTicks.IsCreated) _accessTicks.Remove(regionCoord);
                evicted++;
            }
            return evicted;
        }

        [Obsolete("Geometric shell candidates cannot discover historical residents left behind the current player. Use EvictFarResidents with an IRegionResidencyStore.")]
        public static NativeArray<int3> GetEvictionCandidates(float3 playerPosition,
                                                               int unloadRadiusBlocks,
                                                               Allocator allocator)
        {
            int3 centre = PositionToRegion(playerPosition);
            int radius = (int)math.ceil(
                unloadRadiusBlocks / (float)VoxelReadGrid.BlocksPerRegionEdge);

            NativeArray<int3> candidates = new NativeArray<int3>(
                (2 * radius + 1) * (2 * radius + 1) * (2 * radius + 1), allocator);
            int count = 0;
            float distanceSquaredLimit = unloadRadiusBlocks * 0.8f;
            distanceSquaredLimit *= distanceSquaredLimit;

            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            for (int z = -radius; z <= radius; z++)
            {
                int3 regionCoord = new int3(centre.x + x, centre.y + y, centre.z + z);
                float3 regionCenter = RegionWorldPos(regionCoord);
                if (math.distancesq(regionCenter, playerPosition) > distanceSquaredLimit)
                    candidates[count++] = regionCoord;
            }

            return candidates;
        }

        public static void EvictWithoutWriteBack(int3 regionCoord,
                                                 IRegionResidencyStore storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (!storage.IsRegionResident(regionCoord)) return;

            storage.EvictRegion(regionCoord);
            if (_accessTicks.IsCreated) _accessTicks.Remove(regionCoord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 PositionToRegion(float3 position)
        {
            float regionMetres = VoxelReadGrid.BlocksPerRegionEdge * 0.8f;
            return new int3(
                (int)math.floor(position.x / regionMetres),
                (int)math.floor(position.y / regionMetres),
                (int)math.floor(position.z / regionMetres));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 RegionWorldPos(int3 regionCoord)
        {
            float regionMetres = VoxelReadGrid.BlocksPerRegionEdge * 0.8f;
            return new float3(
                regionCoord.x * regionMetres,
                regionCoord.y * regionMetres,
                regionCoord.z * regionMetres);
        }
    }
}
