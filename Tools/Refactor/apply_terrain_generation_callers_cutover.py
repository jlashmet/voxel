from pathlib import Path


def replace_exact(path: str, old: str, new: str, expected: int = 1) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"{path}: expected {expected}, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new))


GEN_ORDER = "Assets/Tests/Parity/GenerationOrderHarness.cs"
LOSS = "Assets/Tests/Parity/LossConvergenceTests.cs"
REPLAY = "Assets/Tests/Parity/ReplayHarness.cs"
TENK = "Assets/Tests/Parity/TenThousandEventParityTests.cs"
TERRAIN = "Assets/Tests/Parity/TerrainDeterminismTests.cs"
RUNTIME = "Assets/VoxelEngine/CI/Editor/KentridgeRuntimeCapture.cs"
UNIFIED = "Assets/VoxelEngine/CI/Editor/KentridgeUnifiedCapture.cs"
V2 = "Assets/VoxelEngine/CI/Editor/KentridgeUnifiedCaptureV2.World.cs"

# Standalone parity buffers stay standalone, but Storage owns how Terrain reaches them.
replace_exact(
    GEN_ORDER,
    "                TerrainGenerator.Generate(region, seed, in pool);",
    "                TerrainGenerator.Generate(\n                    new StandaloneRegionGenerationStore(in region), region.Coord, seed);",
)

replace_exact(
    LOSS,
    "            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(regionA, terrainSeed, in poolA);\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(regionB, terrainSeed, in poolB);",
    "            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in regionA), regionA.Coord, terrainSeed);\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in regionB), regionB.Coord, terrainSeed);",
)
replace_exact(
    LOSS,
    "            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(regionA, 42u, in poolA);\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(regionB, 42u, in poolB);",
    "            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in regionA), regionA.Coord, 42u);\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in regionB), regionB.Coord, 42u);",
    expected=2,
)

replace_exact(
    TENK,
    "            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(regionA, TerrainSeed, in poolA);\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(regionB, TerrainSeed, in poolB);",
    "            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in regionA), regionA.Coord, TerrainSeed);\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in regionB), regionB.Coord, TerrainSeed);",
)

# Terrain determinism tests intentionally inspect physical Region buffers after generation.
replacements = [
    (
        "            TerrainGenerator.Generate(regionA, seed, in poolA);\n            TerrainGenerator.Generate(regionB, seed, in poolB);",
        "            TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in regionA), regionA.Coord, seed);\n            TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in regionB), regionB.Coord, seed);",
    ),
    (
        "            TerrainGenerator.Generate(r1, 0u, in pool);\n            TerrainGenerator.Generate(r2, 1u, in pool);",
        "            TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in r1), r1.Coord, 0u);\n            TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in r2), r2.Coord, 1u);",
    ),
    (
        "            TerrainGenerator.Generate(region, seed, in pool);",
        "            TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in region), region.Coord, seed);",
    ),
]
for old, new in replacements:
    replace_exact(TERRAIN, old, new)
replace_exact(
    TERRAIN,
    "            TerrainGenerator.Generate(region, seed, in pool);",
    "            TerrainGenerator.Generate(\n                new StandaloneRegionGenerationStore(in region), region.Coord, seed);",
)

# Replay already owns RegionTables for the generated worlds. Use one store per table lifetime.
replace_exact(
    REPLAY,
    "            var regionA = _tableA.LoadRegion(int3.zero);\n            var regionB = _tableB.LoadRegion(int3.zero);\n\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(regionA, terrainSeed, in _poolA);\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(regionB, terrainSeed, in _poolB);",
    "            var regionA = _tableA.LoadRegion(int3.zero);\n            var regionB = _tableB.LoadRegion(int3.zero);\n            var generationA = new RegionGenerationStore(in _tableA);\n            var generationB = new RegionGenerationStore(in _tableB);\n\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(\n                generationA, regionA.Coord, terrainSeed);\n            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(\n                generationB, regionB.Coord, terrainSeed);",
)

# Offline capture loaders already own one table across their region loops. Drop the unused pool
# parameter from terrain loading and allocate one table-backed generation store per loader call.
for path in (RUNTIME, UNIFIED):
    replace_exact(
        path,
        "                LoadTerrain(minX, maxX, minZ, maxZ, ref table, in pool);",
        "                LoadTerrain(minX, maxX, minZ, maxZ, ref table);",
    )
    replace_exact(
        path,
        "            ref RegionTable table, in BrickPool pool)\n        {",
        "            ref RegionTable table)\n        {",
    )
    replace_exact(
        path,
        "            for (int rz = minRegionZ; rz <= maxRegionZ; rz++)",
        "            var generation = new RegionGenerationStore(in table);\n\n            for (int rz = minRegionZ; rz <= maxRegionZ; rz++)",
    )
    replace_exact(
        path,
        "                Region region = table.LoadRegion(new int3(rx, 0, rz));\n                TerrainGenerator.Generate(in region, Seed, in pool);\n                table.CommitRegion(in region);",
        "                int3 regionCoord = new int3(rx, 0, rz);\n                TerrainGenerator.Generate(generation, regionCoord, Seed);",
    )

# V2 uses abbreviated minRX naming but the same table-backed pattern.
replace_exact(
    V2,
    "                                        ref RegionTable table, in BrickPool pool)\n        {",
    "                                        ref RegionTable table)\n        {",
)
replace_exact(
    V2,
    "            int minRZ = (minZ >> VoxelDimensions.RegionVoxelEdgeLog2) - 1;\n            int maxRZ = (maxZ >> VoxelDimensions.RegionVoxelEdgeLog2) + 1;\n            for (int rz = minRZ; rz <= maxRZ; rz++)",
    "            int minRZ = (minZ >> VoxelDimensions.RegionVoxelEdgeLog2) - 1;\n            int maxRZ = (maxZ >> VoxelDimensions.RegionVoxelEdgeLog2) + 1;\n            var generation = new RegionGenerationStore(in table);\n            for (int rz = minRZ; rz <= maxRZ; rz++)",
)
replace_exact(
    V2,
    "                Region region = table.LoadRegion(new int3(rx, 0, rz));\n                TerrainGenerator.Generate(in region, Seed, in pool);\n                table.CommitRegion(in region);",
    "                int3 regionCoord = new int3(rx, 0, rz);\n                TerrainGenerator.Generate(generation, regionCoord, Seed);",
)

# V2 caller lives in another partial file; remove its now-unused pool argument there too.
v2_core = Path("Assets/VoxelEngine/CI/Editor/KentridgeUnifiedCaptureV2.Core.cs")
core_text = v2_core.read_text()
old = "LoadTerrain(minX, maxX, minZ, maxZ, ref table, in pool);"
if core_text.count(old) != 1:
    raise RuntimeError(f"V2.Core: expected one LoadTerrain call, found {core_text.count(old)}")
v2_core.write_text(core_text.replace(old, "LoadTerrain(minX, maxX, minZ, maxZ, ref table);"))

# Hard failure if any production/test caller retains the removed Terrain signature shape.
for root in (Path("Assets/Tests/Parity"), Path("Assets/VoxelEngine/CI/Editor")):
    for p in root.rglob("*.cs"):
        text = p.read_text(errors="ignore")
        if "TerrainGenerator.Generate" in text and "in pool" in text:
            # Scope to source lines containing the call, avoiding unrelated pool uses in the file.
            for line in text.splitlines():
                if "TerrainGenerator.Generate" in line and "in pool" in line:
                    raise RuntimeError(f"{p}: stale Terrain pool signature remains: {line.strip()}")

print("Terrain generation callers cut over successfully.")
