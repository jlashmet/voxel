from pathlib import Path


def replace_exact(path: str, old: str, new: str, expected: int = 1) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"{path}: expected {expected} occurrences, found {count}: {old!r}")
    p.write_text(text.replace(old, new))


# Focused architecture tests call the cache only once; an inline read source keeps setup local.
replace_exact(
    "Assets/Tests/EditMode/VoxelSurfaceArchitectureTests.cs",
    "cache.Prepare(ref table, in pool, in palette, in surfaces, in coatings, store,",
    "cache.Prepare(new RegionReadSource(in table, in pool), in palette, in surfaces, in coatings, store,",
)
replace_exact(
    "Assets/Tests/EditMode/VoxelSurfaceArchitectureTests.cs",
    "cache.Prepare(ref table, in pool, in palette, in custom, in coatings, null,",
    "cache.Prepare(new RegionReadSource(in table, in pool), in palette, in custom, in coatings, null,",
)

# Offline captures may prepare thousands of slices; construct one borrowed read source per capture.
captures = [
    (
        "Assets/VoxelEngine/CI/Editor/KentridgeRuntimeCapture.cs",
        "int previousDirty = int.MaxValue;",
        "var readSource = new RegionReadSource(in table, in pool);\n                int previousDirty = int.MaxValue;",
        "smoothCache.Prepare(ref table, in pool, in materialPalette,",
        "smoothCache.Prepare(readSource, in materialPalette,",
    ),
    (
        "Assets/VoxelEngine/CI/Editor/KentridgeUnifiedCaptureV2.Core.cs",
        "int previousDirty = int.MaxValue;",
        "var readSource = new RegionReadSource(in table, in pool);\n                int previousDirty = int.MaxValue;",
        "cache.Prepare(ref table, in pool, in materialPalette,",
        "cache.Prepare(readSource, in materialPalette,",
    ),
    (
        "Assets/VoxelEngine/CI/Editor/KentridgeCaptureImpl.cs",
        "int previousDirty = int.MaxValue;",
        "var readSource = new RegionReadSource(in table, in pool);\n                int previousDirty = int.MaxValue;",
        "cache.Prepare(ref table, in pool, in materialPalette,",
        "cache.Prepare(readSource, in materialPalette,",
    ),
    (
        "Assets/VoxelEngine/CI/Editor/KentridgeUnifiedCapture.cs",
        "int previousDirty = int.MaxValue;",
        "var readSource = new RegionReadSource(in table, in pool);\n                int previousDirty = int.MaxValue;",
        "cache.Prepare(ref table, in pool, in materials,",
        "cache.Prepare(readSource, in materials,",
    ),
]

for path, marker, replacement, old_call, new_call in captures:
    replace_exact(path, marker, replacement)
    replace_exact(path, old_call, new_call)

remaining = []
for p in Path("Assets").rglob("*.cs"):
    text = p.read_text(errors="ignore")
    if "Prepare(ref table, in pool" in text:
        remaining.append(str(p))
if remaining:
    raise RuntimeError("old Transvoxel-style Prepare calls remain: " + ", ".join(remaining))

print("Transvoxel caller cutover applied successfully.")
