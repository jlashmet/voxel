from pathlib import Path


def replace_once(path: Path, old: str, new: str, label: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    path.write_text(text.replace(old, new, 1))


cache = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")

replace_once(
    cache,
    """        public ulong FeaturePreservingFallbackPublishCount { get; private set; }\n        public ulong MaterialPaletteInvalidationCount { get; private set; }\n""",
    """        public ulong FeaturePreservingFallbackPublishCount { get; private set; }\n        // Last visibility pass diagnostics. These counters reset every collection pass and never\n        // participate in scheduling; they identify where step-4 ownership disappears.\n        public int LastVisibilityKnownCount { get; private set; }\n        public int LastVisibilityInBandCount { get; private set; }\n        public int LastVisibilityFrustumCount { get; private set; }\n        public int LastVisibilityReadyCount { get; private set; }\n        public int LastVisibilityEmptyCount { get; private set; }\n        public ulong MaterialPaletteInvalidationCount { get; private set; }\n""",
    "visibility counter properties")

replace_once(
    cache,
    """        public void BeginVisibilityCollection()\n        {\n            _visible.Clear();\n            MissingVisibleCount = 0;\n        }\n""",
    """        public void BeginVisibilityCollection()\n        {\n            _visible.Clear();\n            MissingVisibleCount = 0;\n            LastVisibilityKnownCount = 0;\n            LastVisibilityInBandCount = 0;\n            LastVisibilityFrustumCount = 0;\n            LastVisibilityReadyCount = 0;\n            LastVisibilityEmptyCount = 0;\n        }\n""",
    "visibility counter reset")

replace_once(
    cache,
    """            if (!_known.Contains(coordinate)) return;\n\n            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);\n            if (!WithinRingBand(bounds, cameraPosition))\n""",
    """            if (!_known.Contains(coordinate)) return;\n            LastVisibilityKnownCount++;\n\n            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);\n            if (!WithinRingBand(bounds, cameraPosition))\n""",
    "known visibility count")

replace_once(
    cache,
    """                if (_dirty.Contains(coordinate)) ParkDirty(coordinate);\n                return;\n            }\n\n            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);\n""",
    """                if (_dirty.Contains(coordinate)) ParkDirty(coordinate);\n                return;\n            }\n            LastVisibilityInBandCount++;\n\n            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);\n""",
    "in-band visibility count")

replace_once(
    cache,
    """            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;\n\n            // Background prefetch above remains intentionally 360 degrees. Once a chunk is in\n""",
    """            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;\n            LastVisibilityFrustumCount++;\n            if (currentReady) LastVisibilityReadyCount++;\n            if (currentEmpty) LastVisibilityEmptyCount++;\n\n            // Background prefetch above remains intentionally 360 degrees. Once a chunk is in\n""",
    "frustum ready-empty counts")

print("patched cache-side step4 visibility ownership counters")
