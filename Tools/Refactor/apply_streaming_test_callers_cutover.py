from pathlib import Path


def replace_exact(path: str, old: str, new: str, expected: int = 1) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"{path}: expected {expected}, found {count}: {old[:100]!r}")
    p.write_text(text.replace(old, new))


DISTANT = "Assets/Tests/PlayMode/DistantAlterationTests.cs"
MEMORY = "Assets/Tests/PlayMode/MemoryStabilityTests.cs"
TRAVERSAL = "Assets/Tests/PlayMode/TraversalStreamingTests.cs"

replace_exact(
    DISTANT,
    "            // Evict to cold.\n            ResidencyManager.EvictWithoutWriteBack(regionCoord, ref table, ref pool);",
    "            // Evict to cold.\n            var residency = new RegionResidencyStore(in table, in pool);\n            ResidencyManager.EvictWithoutWriteBack(regionCoord, residency);",
)

replace_exact(
    MEMORY,
    "            var pool = new BrickPool(\n                math.max(1, brickPoolCapacityBytes / VoxelDimensions.BytesPerMixedBrick),\n                Allocator.Persistent);\n",
    "            var pool = new BrickPool(\n                math.max(1, brickPoolCapacityBytes / VoxelDimensions.BytesPerMixedBrick),\n                Allocator.Persistent);\n            var residency = new RegionResidencyStore(in table, in pool);\n",
)
replace_exact(
    MEMORY,
    "                    ResidencyManager.Update(playerPos, k_TickInterval, ref table, pool);",
    "                    residency.Refresh(in table, in pool);\n                    ResidencyManager.Update(playerPos, k_TickInterval, residency);",
)
replace_exact(
    MEMORY,
    "                    // Publish loaded regions.\n                    RegionLoader.PublishLoaded(ref table, ref pool, 0.5f);",
    "                    // Publish loaded regions.\n                    residency.Refresh(in table, in pool);\n                    RegionLoader.PublishLoaded(residency, 0.5f);",
)
replace_exact(
    MEMORY,
    "            // Evict with ResidencyManager (no write-back).\n            ResidencyManager.EvictWithoutWriteBack(regionCoord, ref table, ref pool);",
    "            // Evict with ResidencyManager (no write-back).\n            var residency = new RegionResidencyStore(in table, in pool);\n            ResidencyManager.EvictWithoutWriteBack(regionCoord, residency);",
)

# Traversal has two independent table/pool scopes. Construct one residency store per scope.
replace_exact(
    TRAVERSAL,
    "            var table = new RegionTable(1024, Allocator.Persistent);\n            var pool = new BrickPool(1 << 20, Allocator.Persistent); // 1 MB for test.\n",
    "            var table = new RegionTable(1024, Allocator.Persistent);\n            var pool = new BrickPool(1 << 20, Allocator.Persistent); // 1 MB for test.\n            var residency = new RegionResidencyStore(in table, in pool);\n",
)
replace_exact(
    TRAVERSAL,
    "                    // Simulate region loader publish (0.5 ms budget per device-matrix.md).\n                    float mainThreadWorkMs = RegionLoader.PublishLoaded(ref table, ref pool, 0.5f);",
    "                    // Simulate region loader publish (0.5 ms budget per device-matrix.md).\n                    residency.Refresh(in table, in pool);\n                    float mainThreadWorkMs = RegionLoader.PublishLoaded(residency, 0.5f);",
)
replace_exact(
    TRAVERSAL,
    "                var table = new RegionTable(1024, Allocator.Persistent);\n                var pool = new BrickPool(1 << 20, Allocator.Persistent);\n                float playerPosZ = 0f;",
    "                var table = new RegionTable(1024, Allocator.Persistent);\n                var pool = new BrickPool(1 << 20, Allocator.Persistent);\n                var residency = new RegionResidencyStore(in table, in pool);\n                float playerPosZ = 0f;",
)
replace_exact(
    TRAVERSAL,
    "                        int published = RegionLoader.PublishLoaded(ref table, ref pool, 0.5f);",
    "                        residency.Refresh(in table, in pool);\n                        int published = RegionLoader.PublishLoaded(residency, 0.5f);",
)

for path in (DISTANT, MEMORY, TRAVERSAL):
    text = Path(path).read_text()
    for stale in ("PublishLoaded(ref table, ref pool", "Update(playerPos, k_TickInterval, ref table, pool)",
                  "EvictWithoutWriteBack(regionCoord, ref table, ref pool)"):
        if stale in text:
            raise RuntimeError(f"{path}: stale Streaming physical-storage call remains: {stale}")

print("Streaming test caller cutover applied successfully.")
