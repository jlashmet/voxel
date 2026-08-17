from pathlib import Path

CACHE = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
PLAN = Path(".claude/plans/voxel-showcase-rendering-repair-v2.md")
VALIDATE = Path(".github/workflows/validate-step4-fallback-lifecycle.yml")


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

# There are two ordinary-result branches. If the first marker is present, ensure the faceted-only
# branch also has the same diagnostic before its fallback adjudication.
faceted_old = """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n"""
faceted_new = """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SupportsFeaturePreservingFallback)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n"""
if faceted_new not in cache:
    if cache.count(faceted_old) != 1:
        raise SystemExit(f"faceted ordinary result: expected exactly one unwired match, found {cache.count(faceted_old)}")
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

cache = replace_if_missing(
    cache,
    "Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication",
    """            if (_indices.Length == 0)\n            {\n                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))\n""",
    """            if (_indices.Length == 0)\n            {\n                if (SupportsFeaturePreservingFallback)\n                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication();\n                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))\n""",
    "ready-empty publication",
)

# Publication was partially wired in an earlier attempt on some branch heads; accept that shape.
if "Step4FalseEmptyDiagnostics.RecordFallbackPublished" not in cache:
    old = """            CompletedBuildCount++;\n            if (_build.UsedFeaturePreservingFallback)\n                FeaturePreservingFallbackPublishCount++;\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n"""
    new = """            CompletedBuildCount++;\n            if (_build.UsedFeaturePreservingFallback)\n            {\n                FeaturePreservingFallbackPublishCount++;\n                Step4FalseEmptyDiagnostics.RecordFallbackPublished();\n            }\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n"""
    if cache.count(old) != 1:
        raise SystemExit(f"fallback publication: expected one unwired match, found {cache.count(old)}")
    cache = cache.replace(old, new, 1)
    print("fallback publication: wiring")
else:
    print("fallback publication: already wired")

CACHE.write_text(cache)

plan = PLAN.read_text()
old_plan = "- [x] Add cache-lifecycle diagnostics for the step-4 fallback path that separately count fallback schedules, completions, non-empty completions and publications, and expose those counts in `LodRenderingTests`; this instrumentation does not alter admission, geometry or publication behavior (`3385982f`)."
new_plan = "- [x] Add and wire cache-lifecycle diagnostics for the step-4 fallback path: exact owned/unowned classification, ordinary non-empty/empty output, fallback schedules/completions/non-empty completions/publications, and final ready-empty publications are exposed in `LodRenderingTests`; this instrumentation does not alter admission, geometry or publication behavior (`3385982f` plus lifecycle wiring)."
if new_plan not in plan:
    if plan.count(old_plan) != 1:
        raise SystemExit(f"plan diagnostics task: expected one old task, found {plan.count(old_plan)}")
    plan = plan.replace(old_plan, new_plan, 1)
PLAN.write_text(plan)

validate = VALIDATE.read_text()
cache_path = "      - Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs\n"
if cache_path not in validate:
    old = """    paths:\n      - .github/workflows/validate-step4-fallback-lifecycle.yml\n"""
    new = """    paths:\n      - .github/workflows/validate-step4-fallback-lifecycle.yml\n      - Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs\n      - Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Step4FalseEmptyDiagnostics.cs\n      - Assets/Tests/PlayMode/LodRenderingTests.cs\n"""
    if validate.count(old) != 1:
        raise SystemExit(f"lifecycle workflow paths: expected one old block, found {validate.count(old)}")
    validate = validate.replace(old, new, 1)
VALIDATE.write_text(validate)
