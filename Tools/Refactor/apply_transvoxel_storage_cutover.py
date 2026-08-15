from pathlib import Path
import re

CACHE = Path("Assets/VoxelEngine/Rendering/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
SCHEDULER = Path("Assets/VoxelEngine/Rendering/SurfaceExtraction/VoxelSurfaceScheduler.cs")


def replace_exact(text: str, old: str, new: str, expected: int = 1) -> str:
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"expected {expected} occurrences, found {count}: {old[:120]!r}")
    return text.replace(old, new)


def sub_once(text: str, pattern: str, replacement: str) -> str:
    result, count = re.subn(pattern, replacement, text, count=1, flags=re.S)
    if count != 1:
        raise RuntimeError(f"expected one regex match, found {count}: {pattern[:120]!r}")
    return result


s = CACHE.read_text()

s = replace_exact(s, "using VoxelEngine.Core.Occupancy;\n", "")

s = sub_once(
    s,
    r"private void AppendTransitionFaces\(ref RegionTable table, in BrickPool pool,\s+in MaterialPalette palette,",
    "private void AppendTransitionFaces(IRegionReadSource source,\n                                           in MaterialPalette palette,",
)
s = replace_exact(
    s,
    "SnapshotTransitionFace(ref table, in pool, in palette, face);",
    "SnapshotTransitionFace(source, in palette, face);",
)
s = sub_once(
    s,
    r"private void SnapshotTransitionFace\(ref RegionTable table, in BrickPool pool,\s+in MaterialPalette palette, int face\)",
    "private void SnapshotTransitionFace(IRegionReadSource source,\n                                            in MaterialPalette palette, int face)",
)

s = sub_once(
    s,
    r"public void Prepare\(ref RegionTable table, in BrickPool pool, in MaterialPalette palette,",
    "public void Prepare(IRegionReadSource source, in MaterialPalette palette,",
)
s = replace_exact(s, "DropNoLongerResident(ref table);", "DropNoLongerResident(source);")
s = replace_exact(
    s,
    "ScheduleDensityJob(ref table, in pool, in palette, voxelSize);",
    "ScheduleDensityJob(source, in palette, voxelSize);",
)
s = replace_exact(
    s,
    "AppendTransitionFaces(ref table, in pool, in palette,\n                                          camera, voxelSize);",
    "AppendTransitionFaces(source, in palette, camera, voxelSize);",
)

s = sub_once(
    s,
    r"private void ScheduleMipDensityJob\(ref RegionTable table, in BrickPool pool,\s+in MaterialPalette palette, float voxelSize\)",
    "private void ScheduleMipDensityJob(IRegionReadSource source,\n                                           in MaterialPalette palette, float voxelSize)",
)
s = sub_once(
    s,
    r"private void ScheduleDensityJob\(ref RegionTable table, in BrickPool pool,\s+in MaterialPalette palette, float voxelSize\)",
    "private void ScheduleDensityJob(IRegionReadSource source,\n                                        in MaterialPalette palette, float voxelSize)",
)
s = replace_exact(
    s,
    "ScheduleMipDensityJob(ref table, in pool, in palette, voxelSize);",
    "ScheduleMipDensityJob(source, in palette, voxelSize);",
)

sample_call = (
    "if (VoxelMipSampler.TrySample(ref table, in pool, voxel, mipLevel,\n"
    "                                              out bool sampled, out byte sampledMaterial))"
)
s = replace_exact(
    s,
    sample_call,
    "if (TrySampleWorld(source, ref cursor, voxel, mipLevel,\n"
    "                                   out bool sampled, out byte sampledMaterial))",
    expected=2,
)

s = replace_exact(s, "VoxelMipSampler.LevelForStride", "VoxelReadGrid.LevelForStride", expected=4)

s = replace_exact(
    s,
    "int mipLevel = VoxelReadGrid.LevelForStride(halfStep);\n\n            for (int v = 0; v < FaceSamplesPerAxis; v++)",
    "int mipLevel = VoxelReadGrid.LevelForStride(halfStep);\n            RegionSampleCursor cursor = default;\n\n            for (int v = 0; v < FaceSamplesPerAxis; v++)",
)
s = replace_exact(
    s,
    "int mipLevel = VoxelReadGrid.LevelForStride(SourceStep);\n            bool anySolid = false;",
    "int mipLevel = VoxelReadGrid.LevelForStride(SourceStep);\n            RegionSampleCursor cursor = default;\n            bool anySolid = false;",
)

s = replace_exact(
    s,
    "int3 cacheOrigin = chunkBrickOrigin - BrickCachePadding;\n\n            for (int z = 0; z < BrickCacheEdge; z++)",
    "int3 cacheOrigin = chunkBrickOrigin - BrickCachePadding;\n            RegionSampleCursor cursor = default;\n\n            for (int z = 0; z < BrickCacheEdge; z++)",
)
s = replace_exact(
    s,
    "_densityBricks[cacheIndex] = SnapshotBrick(ref table, in pool, worldBrick);",
    "_densityBricks[cacheIndex] = SnapshotBlock(source, ref cursor, worldBrick);",
)

snapshot_pattern = r"        private TransvoxelDensityBrick SnapshotBrick\(ref RegionTable table, in BrickPool pool,\s+int3 worldBrick\)\n        \{.*?\n        \}\n\n        private bool StepCells"
snapshot_replacement = """        private TransvoxelDensityBrick SnapshotBlock(IRegionReadSource source,\n                                                      ref RegionSampleCursor cursor,\n                                                      int3 worldBlock)\n        {\n            if (!TryAcquireWorldBlock(source, ref cursor, worldBlock, out RegionReadView region)\n                || !region.TryGetWorldBlock(worldBlock, out VoxelReadBlock block)\n                || block.Kind == VoxelReadBlockKind.Empty)\n                return default;\n\n            if (block.Kind == VoxelReadBlockKind.Uniform)\n            {\n                return new TransvoxelDensityBrick\n                {\n                    Kind = 1,\n                    UniformMaterial = block.UniformMaterial,\n                    MixedOffset = 0\n                };\n            }\n\n            int mixedOffset = _densityMixedVoxels.Length;\n            int nextLength = mixedOffset + VoxelReadGrid.VoxelsPerBlock;\n            _densityMixedVoxels.ResizeUninitialized(nextLength);\n            _densityMixedSurfaceSemantics.ResizeUninitialized(nextLength);\n            _densityMixedBoundarySamples.ResizeUninitialized(nextLength);\n            if (!region.TryCopyWorldBlock(\n                    worldBlock,\n                    _densityMixedVoxels.AsArray(),\n                    _densityMixedSurfaceSemantics.AsArray(),\n                    _densityMixedBoundarySamples.AsArray(),\n                    mixedOffset))\n                throw new InvalidOperationException($\"Failed to snapshot Storage read block {worldBlock}.\");\n\n            return new TransvoxelDensityBrick\n            {\n                Kind = 2,\n                UniformMaterial = 0,\n                MixedOffset = mixedOffset\n            };\n        }\n\n        private bool StepCells"""
s = sub_once(s, snapshot_pattern, snapshot_replacement)

s = sub_once(
    s,
    r"private void DropNoLongerResident\(ref RegionTable table\)",
    "private void DropNoLongerResident(IRegionReadSource source)",
)
s = replace_exact(
    s,
    "if (AnyOverlappedRegionResident(ref table, chunk)) continue;",
    "if (AnyOverlappedRegionResident(source, chunk)) continue;",
)
s = sub_once(
    s,
    r"private bool AnyOverlappedRegionResident\(ref RegionTable table, int3 chunk\)",
    "private bool AnyOverlappedRegionResident(IRegionReadSource source, int3 chunk)",
)
s = replace_exact(
    s,
    "if (table.IsResident(new int3(x, y, z))) return true;",
    "if (source.IsRegionResident(new int3(x, y, z))) return true;",
)

# Storage read-grid vocabulary replaces physical brick-layout constants throughout the cache.
for old, new in (
    ("VoxelDimensions.VoxelsPerBrick", "VoxelReadGrid.VoxelsPerBlock"),
    ("VoxelDimensions.BrickEdgeLog2", "VoxelReadGrid.BlockEdgeLog2"),
    ("VoxelDimensions.BrickEdgeMask", "VoxelReadGrid.BlockEdgeMask"),
    ("VoxelDimensions.BrickEdge", "VoxelReadGrid.BlockEdge"),
    ("VoxelDimensions.MaterialEmpty", "VoxelGrid.MaterialEmpty"),
    ("VoxelDimensions.RegionVoxelEdge", "VoxelGrid.RegionVoxelEdge"),
):
    s = s.replace(old, new)

cursor_helpers = """
        private struct RegionSampleCursor
        {
            public bool HasLookup;
            public bool Resident;
            public int3 RegionCoord;
            public RegionReadView View;
        }

        private static bool TryAcquireWorldBlock(IRegionReadSource source,
                                                 ref RegionSampleCursor cursor,
                                                 int3 worldBlock,
                                                 out RegionReadView view)
        {
            int3 regionCoord = worldBlock >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            return TryAcquireRegion(source, ref cursor, regionCoord, out view);
        }

        private static bool TrySampleWorld(IRegionReadSource source,
                                           ref RegionSampleCursor cursor,
                                           int3 worldVoxel, int level,
                                           out bool occupied, out byte material)
        {
            int3 regionCoord = new(
                FloorDiv(worldVoxel.x, VoxelGrid.RegionVoxelEdge),
                FloorDiv(worldVoxel.y, VoxelGrid.RegionVoxelEdge),
                FloorDiv(worldVoxel.z, VoxelGrid.RegionVoxelEdge));
            if (!TryAcquireRegion(source, ref cursor, regionCoord, out RegionReadView region))
            {
                occupied = false;
                material = VoxelGrid.MaterialEmpty;
                return false;
            }

            int3 localVoxel = worldVoxel - regionCoord * VoxelGrid.RegionVoxelEdge;
            return region.TrySample(localVoxel, level, out occupied, out material);
        }

        private static bool TryAcquireRegion(IRegionReadSource source,
                                             ref RegionSampleCursor cursor,
                                             int3 regionCoord,
                                             out RegionReadView view)
        {
            if (!cursor.HasLookup || math.any(cursor.RegionCoord != regionCoord))
            {
                cursor.RegionCoord = regionCoord;
                cursor.HasLookup = true;
                cursor.Resident = source.TryAcquireRegion(regionCoord, out cursor.View);
            }

            view = cursor.View;
            return cursor.Resident;
        }

"""
s = replace_exact(
    s,
    "        private static int GridIndex(int x, int y, int z) =>\n",
    cursor_helpers + "        private static int GridIndex(int x, int y, int z) =>\n",
)

for forbidden in ("RegionTable", "BrickPool", "BrickRef", "VoxelMipSampler", "VoxelDimensions."):
    if forbidden in s:
        raise RuntimeError(f"physical Storage dependency remains in Transvoxel cache: {forbidden}")

CACHE.write_text(s)

scheduler = SCHEDULER.read_text()
old_call = """                    // Solid extraction is the final remaining physical-storage reader in
                    // Rendering; it is cut over in the next step of this same branch.
                    worker.Prepare(ref table, in pool, in palette, in surfaceCatalogue,
                                   in coatingCatalogue, profileBlocks, camera, voxelSize, frame,
                                   workerBudget);"""
new_call = """                    worker.Prepare(_readSource, in palette, in surfaceCatalogue,
                                   in coatingCatalogue, profileBlocks, camera, voxelSize, frame,
                                   workerBudget);"""
scheduler = replace_exact(scheduler, old_call, new_call)
SCHEDULER.write_text(scheduler)

print("Transvoxel Storage read cutover applied successfully.")
