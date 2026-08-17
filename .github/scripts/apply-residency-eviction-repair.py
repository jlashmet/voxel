#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def replace_once(path: str, old: str, new: str) -> None:
    p = ROOT / path
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}\n--- old ---\n{old}")
    p.write_text(text.replace(old, new, 1))


replace_once(
    "Assets/VoxelEngine/Storage/Api/IRegionResidencyStore.cs",
    "using Unity.Mathematics;\n",
    "using Unity.Mathematics;\n",
)
replace_once(
    "Assets/VoxelEngine/Storage/Api/IRegionResidencyStore.cs",
    """        /// <summary>Evicts a resident region and releases its Storage-owned memory.</summary>\n        bool EvictRegion(int3 regionCoord);\n""",
    """        /// <summary>Evicts a resident region and releases its Storage-owned memory.</summary>\n        bool EvictRegion(int3 regionCoord);\n\n        /// <summary>\n        /// Advances an opaque resident-slot cursor to the next currently resident coordinate.\n        /// Returns false when the current pass reaches the end; callers then reset the cursor to\n        /// zero before starting a later bounded pass. No allocation is required.\n        /// </summary>\n        bool TryGetNextResidentCoord(ref int cursor, out int3 regionCoord);\n""",
)

replace_once(
    "Assets/VoxelEngine/Storage/Runtime/RegionTable.cs",
    """        public NativeArray<int3> GetResidentCoords(Allocator allocator) =>\n            _coordToSlot.GetKeyArray(allocator);\n\n        public bool CopyResidentCoords(ref int cursor, NativeArray<int3> destination,\n""",
    """        public NativeArray<int3> GetResidentCoords(Allocator allocator) =>\n            _coordToSlot.GetKeyArray(allocator);\n\n        public bool TryGetNextResidentCoord(ref int cursor, out int3 coord)\n        {\n            cursor = math.clamp(cursor, 0, _regions.Length);\n            while (cursor < _regions.Length)\n            {\n                int slot = cursor++;\n                Region region = _regions[slot];\n                if (!region.IsCreated || _retiredSlots[slot] != 0) continue;\n                coord = region.Coord;\n                return true;\n            }\n\n            coord = default;\n            return false;\n        }\n\n        public bool CopyResidentCoords(ref int cursor, NativeArray<int3> destination,\n""",
)

replace_once(
    "Assets/VoxelEngine/Storage/Runtime/RegionResidencyStore.cs",
    """        public bool EvictRegion(int3 regionCoord)\n        {\n            if (!_table.IsResident(regionCoord)) return false;\n            _table.EvictRegion(regionCoord, ref _pool);\n            return true;\n        }\n""",
    """        public bool EvictRegion(int3 regionCoord)\n        {\n            if (!_table.IsResident(regionCoord)) return false;\n            _table.EvictRegion(regionCoord, ref _pool);\n            return true;\n        }\n\n        public bool TryGetNextResidentCoord(ref int cursor, out int3 regionCoord) =>\n            _table.TryGetNextResidentCoord(ref cursor, out regionCoord);\n""",
)

replace_once(
    "Assets/VoxelEngine/Streaming/Runtime/ResidencyManager.cs",
    """        private static NativeHashMap<int3, uint> _accessTicks =\n            new NativeHashMap<int3, uint>(1024, Allocator.Persistent);\n""",
    """        private const int EvictionScanRegionsPerFrame = 64;\n        private static int _evictionScanCursor;\n        private static NativeHashMap<int3, uint> _accessTicks =\n            new NativeHashMap<int3, uint>(1024, Allocator.Persistent);\n""",
)
replace_once(
    "Assets/VoxelEngine/Streaming/Runtime/ResidencyManager.cs",
    """            int unloadRadiusBlocks = (int)(GetUnloadRadius(DeviceTier.PC) / 0.8f);\n            using (NativeArray<int3> evictionCandidates =\n                   GetEvictionCandidates(playerPosition, unloadRadiusBlocks, Allocator.Temp))\n            {\n                for (int i = 0; i < evictionCandidates.Length; i++)\n                    EvictWithoutWriteBack(evictionCandidates[i], storage);\n            }\n""",
    """            int unloadRadiusBlocks = (int)(GetUnloadRadius(DeviceTier.PC) / 0.8f);\n            EvictFarResidents(\n                playerPosition, unloadRadiusBlocks, storage, ref _evictionScanCursor,\n                EvictionScanRegionsPerFrame);\n""",
)
replace_once(
    "Assets/VoxelEngine/Streaming/Runtime/ResidencyManager.cs",
    """        public static NativeArray<int3> GetEvictionCandidates(float3 playerPosition,\n""",
    """        /// <summary>\n        /// Examines at most <paramref name=\"maxRegionsToScan\"/> actual resident regions and\n        /// evicts those outside the unload sphere. Unlike the legacy geometric shell query this\n        /// eventually reaches regions left far behind the player, while keeping per-frame work\n        /// strictly bounded and allocation-free.\n        /// </summary>\n        public static int EvictFarResidents(float3 playerPosition, int unloadRadiusBlocks,\n                                            IRegionResidencyStore storage, ref int scanCursor,\n                                            int maxRegionsToScan = EvictionScanRegionsPerFrame)\n        {\n            if (storage == null) throw new ArgumentNullException(nameof(storage));\n            if (maxRegionsToScan <= 0)\n                throw new ArgumentOutOfRangeException(nameof(maxRegionsToScan));\n\n            float distanceLimit = unloadRadiusBlocks * 0.8f;\n            float distanceSquaredLimit = distanceLimit * distanceLimit;\n            int evicted = 0;\n            int examined = 0;\n            while (examined < maxRegionsToScan)\n            {\n                if (!storage.TryGetNextResidentCoord(ref scanCursor, out int3 regionCoord))\n                {\n                    scanCursor = 0;\n                    break;\n                }\n\n                examined++;\n                if (math.distancesq(RegionWorldPos(regionCoord), playerPosition)\n                    <= distanceSquaredLimit)\n                    continue;\n\n                if (!storage.EvictRegion(regionCoord)) continue;\n                if (_accessTicks.IsCreated) _accessTicks.Remove(regionCoord);\n                evicted++;\n            }\n            return evicted;\n        }\n\n        [Obsolete(\"Geometric shell candidates cannot discover historical residents left behind the current player. Use EvictFarResidents with an IRegionResidencyStore.\")]\n        public static NativeArray<int3> GetEvictionCandidates(float3 playerPosition,\n""",
)

traversal = ROOT / "Assets/Tests/PlayMode/TraversalStreamingTests.cs"
text = traversal.read_text()
text = text.replace(
    "        private const float k_MovementSpeed = 10f; // m/s -- fast sprint\n",
    "        private const float k_MovementSpeed = 10f; // m/s -- fast sprint\n"
    "        private const int k_TestBrickPoolBytes = 1 << 20;\n"
    "        private static int TestBrickPoolSlots => math.max(\n"
    "            1, k_TestBrickPoolBytes / VoxelDimensions.BytesPerMixedBrick);\n",
    1,
)
old_pool = "new BrickPool(1 << 20, Allocator.Persistent)"
count = text.count(old_pool)
if count != 2:
    raise SystemExit(f"TraversalStreamingTests: expected two oversized pools, found {count}")
text = text.replace(old_pool, "new BrickPool(TestBrickPoolSlots, Allocator.Persistent)")
text = text.replace(" // 1 MB for test.", " // 1 MiB mixed-brick payload budget.", 1)
old_eviction = """                    // Eviction candidates: regions beyond unload radius.\n                    var unloadRadiusBricks = (int)(ResidencyManager.GetUnloadRadius(DeviceTier.PC) / 0.8f);\n                    var evictCandidates = ResidencyManager.GetEvictionCandidates(playerPos, unloadRadiusBricks, Allocator.Temp);\n\n                    foreach (var rc in evictCandidates)\n                        if (table.IsResident(rc))\n                            table.EvictRegion(rc, ref pool);\n\n                    evictCandidates.Dispose();\n"""
new_eviction = """                    // Bounded eviction walks actual resident coordinates, including regions\n                    // that have fallen completely behind the player's current unload cube.\n                    var unloadRadiusBricks = (int)(ResidencyManager.GetUnloadRadius(DeviceTier.PC) / 0.8f);\n                    ResidencyManager.EvictFarResidents(\n                        playerPos, unloadRadiusBricks, residency, ref evictionCursor, 64);\n"""
if text.count(old_eviction) != 1:
    raise SystemExit("TraversalStreamingTests: continuous eviction block changed")
text = text.replace(old_eviction, new_eviction, 1)
text = text.replace("            int totalRegionsLoaded = 0;\n", "            int totalRegionsLoaded = 0;\n            int evictionCursor = 0;\n", 1)
old_tier_eviction = """                        // Eviction.\n                        var unloadRadiusBricks = (int)(ResidencyManager.GetUnloadRadius(tier) / 0.8f);\n                        var evictCandidates = ResidencyManager.GetEvictionCandidates(\n                            playerPos, unloadRadiusBricks, Allocator.Temp);\n\n                        foreach (var rc in evictCandidates)\n                            if (table.IsResident(rc))\n                                table.EvictRegion(rc, ref pool);\n\n                        evictCandidates.Dispose();\n"""
new_tier_eviction = """                        // Eviction scans actual resident coordinates rather than a shell\n                        // centred only on the current player position.\n                        var unloadRadiusBricks = (int)(ResidencyManager.GetUnloadRadius(tier) / 0.8f);\n                        ResidencyManager.EvictFarResidents(\n                            playerPos, unloadRadiusBricks, residency, ref evictionCursor, 64);\n"""
if text.count(old_tier_eviction) != 1:
    raise SystemExit("TraversalStreamingTests: tier eviction block changed")
text = text.replace(old_tier_eviction, new_tier_eviction, 1)
text = text.replace(
    "                float playerPosZ = 0f;\n",
    "                float playerPosZ = 0f;\n                int evictionCursor = 0;\n",
    1,
)
traversal.write_text(text)

regression = ROOT / "Assets/Tests/PlayMode/ResidencyEvictionRegressionTests.cs"
regression.write_text('''using NUnit.Framework;\nusing Unity.Collections;\nusing Unity.Mathematics;\nusing VoxelEngine.Storage.Runtime;\nusing VoxelEngine.Streaming.Runtime;\nusing VoxelEngine.Tiering.Api;\n\nnamespace VoxelEngine.Tests.PlayMode\n{\n    public sealed class ResidencyEvictionRegressionTests\n    {\n        [Test]\n        public void BoundedScanEvictsHistoricalResidentLeftBehindPlayer()\n        {\n            var table = new RegionTable(16, Allocator.Persistent);\n            var pool = new BrickPool(16, Allocator.Persistent);\n            var storage = new RegionResidencyStore(in table, in pool);\n            try\n            {\n                int3 historical = int3.zero;\n                float3 player = new float3(5000f, 64f, 0f);\n                int3 current = ResidencyManager.PositionToRegion(player);\n                table.LoadRegion(historical);\n                table.LoadRegion(current);\n                storage.Refresh(in table, in pool);\n\n                int cursor = 0;\n                int unloadBlocks = (int)(ResidencyManager.GetUnloadRadius(DeviceTier.PC) / 0.8f);\n                for (int pass = 0; pass < 4 && table.IsResident(historical); pass++)\n                    ResidencyManager.EvictFarResidents(\n                        player, unloadBlocks, storage, ref cursor, maxRegionsToScan: 8);\n\n                Assert.False(table.IsResident(historical),\n                    \"A region left behind the player's current unload cube was never considered for eviction.\");\n                Assert.True(table.IsResident(current),\n                    \"Bounded historical eviction removed a region inside the unload radius.\");\n            }\n            finally\n            {\n                table.Dispose();\n                pool.Dispose();\n            }\n        }\n    }\n}\n''')

print("bounded residency eviction repair staged")
