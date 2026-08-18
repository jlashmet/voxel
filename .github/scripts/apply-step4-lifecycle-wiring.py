from pathlib import Path

CACHE = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")


def replace_if_missing(text: str, marker: str, old: str, new: str, label: str) -> str:
    if marker in text:
        print(f"{label}: already wired")
        return text
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one unwired match, found {count}")
    print(f"{label}: wiring")
    return text.replace(old, new, 1)


cache = CACHE.read_text()
cache = replace_if_missing(
    cache,
    "Step4FalseEmptyDiagnostics.RecordExactClassification",
    """            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;\n            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;\n            _build.SnapshotTaken = true;\n""",
    """            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;\n            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;\n            if (SupportsFeaturePreservingFallback)\n                Step4FalseEmptyDiagnostics.RecordExactClassification(\n                    _build.HasOwnedSolid, _buildProfileBlocks.Length != 0);\n            _build.SnapshotTaken = true;\n""",
    "exact classification",
)
cache = replace_if_missing(
    cache,
    "Step4FalseEmptyDiagnostics.RecordOrdinaryResult",
    """                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n""",
    """                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SupportsFeaturePreservingFallback)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n""",
    "continuous ordinary result",
)
faceted_old = """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n"""
faceted_new = """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SupportsFeaturePreservingFallback)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n"""
if faceted_new not in cache:
    if cache.count(faceted_old) != 1:
        raise SystemExit(
            f"faceted ordinary result: expected exactly one unwired match, found {cache.count(faceted_old)}")
    cache = cache.replace(faceted_old, faceted_new, 1)
    print("faceted ordinary result: wiring")
else:
    print("faceted ordinary result: already wired")
cache = replace_if_missing(
    cache,
    "Step4FalseEmptyDiagnostics.RecordFallbackCompleted",
    """                        if (_build.UsedFeaturePreservingFallback)\n                        {\n                            FeaturePreservingFallbackCompleteCount++;\n                            if (_build.HasOwnedSolid)\n                                FeaturePreservingFallbackNonEmptyCount++;\n                        }\n""",
    """                        if (_build.UsedFeaturePreservingFallback)\n                        {\n                            FeaturePreservingFallbackCompleteCount++;\n                            if (_build.HasOwnedSolid)\n                                FeaturePreservingFallbackNonEmptyCount++;\n                            Step4FalseEmptyDiagnostics.RecordFallbackCompleted(\n                                _build.HasOwnedSolid);\n                        }\n""",
    "fallback completion",
)
cache = replace_if_missing(
    cache,
    "Step4FalseEmptyDiagnostics.RecordFallbackScheduled",
    """            if (SupportsFeaturePreservingFallback)\n                FeaturePreservingFallbackScheduleCount++;\n""",
    """            if (SupportsFeaturePreservingFallback)\n            {\n                FeaturePreservingFallbackScheduleCount++;\n                Step4FalseEmptyDiagnostics.RecordFallbackScheduled();\n            }\n""",
    "fallback schedule",
)
ready_anchored = """                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication(\n                        _build.Coordinate, _build.HasOwnedSolid,\n                        _buildProfileBlocks.Length != 0,\n                        _build.UsedFeaturePreservingFallback);\n"""
ready_old = """                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication();\n"""
if ready_anchored in cache:
    print("ready-empty publication: anchored")
elif ready_old in cache:
    if cache.count(ready_old) != 1:
        raise SystemExit(
            f"ready-empty publication: expected one legacy call, found {cache.count(ready_old)}")
    cache = cache.replace(ready_old, ready_anchored, 1)
    print("ready-empty publication: anchoring guard state")
elif "Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication" not in cache:
    old = """            if (_indices.Length == 0)\n            {\n                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))\n"""
    new = """            if (_indices.Length == 0)\n            {\n                if (SupportsFeaturePreservingFallback)\n                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication(\n                        _build.Coordinate, _build.HasOwnedSolid,\n                        _buildProfileBlocks.Length != 0,\n                        _build.UsedFeaturePreservingFallback);\n                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))\n"""
    if cache.count(old) != 1:
        raise SystemExit(
            f"ready-empty publication: expected one unwired match, found {cache.count(old)}")
    cache = cache.replace(old, new, 1)
    print("ready-empty publication: wiring anchored guard state")
else:
    raise SystemExit("ready-empty publication: found an unknown call shape")

if "Step4FalseEmptyDiagnostics.RecordFallbackPublished" not in cache:
    old = """            CompletedBuildCount++;\n            if (_build.UsedFeaturePreservingFallback)\n                FeaturePreservingFallbackPublishCount++;\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n"""
    new = """            CompletedBuildCount++;\n            if (_build.UsedFeaturePreservingFallback)\n            {\n                FeaturePreservingFallbackPublishCount++;\n                Step4FalseEmptyDiagnostics.RecordFallbackPublished();\n            }\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n"""
    if cache.count(old) != 1:
        raise SystemExit(
            f"fallback publication: expected one unwired match, found {cache.count(old)}")
    cache = cache.replace(old, new, 1)
    print("fallback publication: wiring")
else:
    print("fallback publication: already wired")

CACHE.write_text(cache)
