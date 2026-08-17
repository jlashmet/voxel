from pathlib import Path

CACHE = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
PLAN = Path(".claude/plans/voxel-showcase-rendering-repair-v2.md")
VALIDATE = Path(".github/workflows/validate-step4-fallback-lifecycle.yml")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


cache = CACHE.read_text()

# Exact snapshot classification: distinguish owned solid from authoritative empty, and record
# whether authored profile geometry suppresses the ordinary empty fallback.
cache = replace_once(
    cache,
    """            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;\n            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;\n            _build.SnapshotTaken = true;\n""",
    """            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;\n            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;\n            if (SupportsFeaturePreservingFallback)\n                Step4FalseEmptyDiagnostics.RecordExactClassification(\n                    _build.HasOwnedSolid, _buildProfileBlocks.Length != 0);\n            _build.SnapshotTaken = true;\n""",
    "exact classification",
)

# Continuous-topology + faceted branch: record the ordinary result before fallback adjudication.
cache = replace_once(
    cache,
    """                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n""",
    """                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SupportsFeaturePreservingFallback)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n""",
    "continuous ordinary result",
)

# Faceted-only branch: same adjudication counter before fallback.
cache = replace_once(
    cache,
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n""",
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SupportsFeaturePreservingFallback)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n""",
    "faceted ordinary result",
)

# Fallback worker completion: separate non-empty and empty HLOD outcomes.
cache = replace_once(
    cache,
    """                        if (_build.UsedFeaturePreservingFallback)\n                        {\n                            FeaturePreservingFallbackCompleteCount++;\n                            if (_build.HasOwnedSolid)\n                                FeaturePreservingFallbackNonEmptyCount++;\n                        }\n""",
    """                        if (_build.UsedFeaturePreservingFallback)\n                        {\n                            FeaturePreservingFallbackCompleteCount++;\n                            if (_build.HasOwnedSolid)\n                                FeaturePreservingFallbackNonEmptyCount++;\n                            Step4FalseEmptyDiagnostics.RecordFallbackCompleted(\n                                _build.HasOwnedSolid);\n                        }\n""",
    "fallback completion",
)

# Fallback scheduling: step 8 also uses this method, so gate both counters to step 4.
cache = replace_once(
    cache,
    """            if (SupportsFeaturePreservingFallback)\n                FeaturePreservingFallbackScheduleCount++;\n""",
    """            if (SupportsFeaturePreservingFallback)\n            {\n                FeaturePreservingFallbackScheduleCount++;\n                Step4FalseEmptyDiagnostics.RecordFallbackScheduled();\n            }\n""",
    "fallback schedule",
)

# Final empty publication: this captures exact-unowned, profile-empty, and fallback-empty outcomes.
cache = replace_once(
    cache,
    """            if (_indices.Length == 0)\n            {\n                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))\n""",
    """            if (_indices.Length == 0)\n            {\n                if (SupportsFeaturePreservingFallback)\n                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication();\n                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))\n""",
    "ready-empty publication",
)

# Successful GPU publication of fallback geometry.
cache = replace_once(
    cache,
    """            CompletedBuildCount++;\n            if (_build.UsedFeaturePreservingFallback)\n                FeaturePreservingFallbackPublishCount++;\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n""",
    """            CompletedBuildCount++;\n            if (_build.UsedFeaturePreservingFallback)\n            {\n                FeaturePreservingFallbackPublishCount++;\n                Step4FalseEmptyDiagnostics.RecordFallbackPublished();\n            }\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n""",
    "fallback publication",
)

CACHE.write_text(cache)

plan = PLAN.read_text()
plan = replace_once(
    plan,
    "- [x] Add cache-lifecycle diagnostics for the step-4 fallback path that separately count fallback schedules, completions, non-empty completions and publications, and expose those counts in `LodRenderingTests`; this instrumentation does not alter admission, geometry or publication behavior (`3385982f`).",
    "- [x] Add and wire cache-lifecycle diagnostics for the step-4 fallback path: exact owned/unowned classification, ordinary non-empty/empty output, fallback schedules/completions/non-empty completions/publications, and final ready-empty publications are exposed in `LodRenderingTests`; this instrumentation does not alter admission, geometry or publication behavior (`3385982f` plus lifecycle wiring).",
    "plan diagnostics task",
)
PLAN.write_text(plan)

validate = VALIDATE.read_text()
validate = replace_once(
    validate,
    """    paths:\n      - .github/workflows/validate-step4-fallback-lifecycle.yml\n""",
    """    paths:\n      - .github/workflows/validate-step4-fallback-lifecycle.yml\n      - Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs\n      - Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Step4FalseEmptyDiagnostics.cs\n      - Assets/Tests/PlayMode/LodRenderingTests.cs\n""",
    "lifecycle workflow paths",
)
VALIDATE.write_text(validate)
