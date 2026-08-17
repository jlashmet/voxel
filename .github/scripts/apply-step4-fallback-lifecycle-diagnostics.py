#!/usr/bin/env python3
from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1))


cache = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"
metrics = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs"
lod_test = "Assets/Tests/PlayMode/LodRenderingTests.cs"

replace_once(
    cache,
    """        public ulong ExactMetadataPinRejectCount { get; private set; }\n        public ulong MaterialPaletteInvalidationCount { get; private set; }\n""",
    """        public ulong ExactMetadataPinRejectCount { get; private set; }\n        // Step-4 false-empty fallback lifecycle diagnostics. These counters do not affect\n        // admission or publication; they distinguish policy selection, worker output and final\n        // visibility when a coarse exact-owned chunk disappears in production.\n        public ulong FeaturePreservingFallbackScheduleCount { get; private set; }\n        public ulong FeaturePreservingFallbackCompleteCount { get; private set; }\n        public ulong FeaturePreservingFallbackNonEmptyCount { get; private set; }\n        public ulong FeaturePreservingFallbackPublishCount { get; private set; }\n        public ulong MaterialPaletteInvalidationCount { get; private set; }\n""",
)

replace_once(
    cache,
    """                        _hlodJobScheduled = false;\n                        _build.HasOwnedSolid = _indices.Length > 0;\n                    }\n                    if (_hlodOverflow[0] != 0)\n""",
    """                        _hlodJobScheduled = false;\n                        _build.HasOwnedSolid = _indices.Length > 0;\n                        if (_build.UsedFeaturePreservingFallback)\n                        {\n                            FeaturePreservingFallbackCompleteCount++;\n                            if (_build.HasOwnedSolid)\n                                FeaturePreservingFallbackNonEmptyCount++;\n                        }\n                    }\n                    if (_hlodOverflow[0] != 0)\n""",
)

replace_once(
    cache,
    """        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)\n""",
    """        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (SupportsFeaturePreservingFallback)\n                FeaturePreservingFallbackScheduleCount++;\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)\n""",
)

replace_once(
    cache,
    """            CompletedBuildCount++;\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n            _desiredVersions.Remove(_build.Coordinate);\n            _queuedAtSeconds.Remove(_build.Coordinate);\n            ResetCompletedBuild();\n            return true;\n""",
    """            CompletedBuildCount++;\n            if (_build.UsedFeaturePreservingFallback)\n                FeaturePreservingFallbackPublishCount++;\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n            _desiredVersions.Remove(_build.Coordinate);\n            _queuedAtSeconds.Remove(_build.Coordinate);\n            ResetCompletedBuild();\n            return true;\n""",
)

replace_once(
    metrics,
    """        public readonly ulong Step4ExactMetadataPinRejects;\n        public readonly ulong MaterialPaletteInvalidations;\n""",
    """        public readonly ulong Step4ExactMetadataPinRejects;\n        public readonly ulong Step4FeatureFallbackScheduled;\n        public readonly ulong Step4FeatureFallbackCompleted;\n        public readonly ulong Step4FeatureFallbackNonEmpty;\n        public readonly ulong Step4FeatureFallbackPublished;\n        public readonly ulong MaterialPaletteInvalidations;\n""",
)

replace_once(
    metrics,
    """            Step4ExactMetadataPinRejects = isStep4 ? solids.ExactMetadataPinRejectCount : 0UL;\n            MaterialPaletteInvalidations = solids.MaterialPaletteInvalidationCount;\n""",
    """            Step4ExactMetadataPinRejects = isStep4 ? solids.ExactMetadataPinRejectCount : 0UL;\n            Step4FeatureFallbackScheduled = isStep4\n                ? solids.FeaturePreservingFallbackScheduleCount : 0UL;\n            Step4FeatureFallbackCompleted = isStep4\n                ? solids.FeaturePreservingFallbackCompleteCount : 0UL;\n            Step4FeatureFallbackNonEmpty = isStep4\n                ? solids.FeaturePreservingFallbackNonEmptyCount : 0UL;\n            Step4FeatureFallbackPublished = isStep4\n                ? solids.FeaturePreservingFallbackPublishCount : 0UL;\n            MaterialPaletteInvalidations = solids.MaterialPaletteInvalidationCount;\n""",
)

replace_once(
    metrics,
    """            ulong step4MetadataRevisionRejects = 0, step4MetadataPinRejects = 0;\n            ulong materialInvalidations = 0, surfaceInvalidations = 0;\n""",
    """            ulong step4MetadataRevisionRejects = 0, step4MetadataPinRejects = 0;\n            ulong step4FallbackScheduled = 0, step4FallbackCompleted = 0;\n            ulong step4FallbackNonEmpty = 0, step4FallbackPublished = 0;\n            ulong materialInvalidations = 0, surfaceInvalidations = 0;\n""",
)

replace_once(
    metrics,
    """                    step4MetadataPinRejects += worker.ExactMetadataPinRejectCount;\n                }\n                materialInvalidations += worker.MaterialPaletteInvalidationCount;\n""",
    """                    step4MetadataPinRejects += worker.ExactMetadataPinRejectCount;\n                    step4FallbackScheduled += worker.FeaturePreservingFallbackScheduleCount;\n                    step4FallbackCompleted += worker.FeaturePreservingFallbackCompleteCount;\n                    step4FallbackNonEmpty += worker.FeaturePreservingFallbackNonEmptyCount;\n                    step4FallbackPublished += worker.FeaturePreservingFallbackPublishCount;\n                }\n                materialInvalidations += worker.MaterialPaletteInvalidationCount;\n""",
)

replace_once(
    metrics,
    """            Step4ExactMetadataPinRejects = step4MetadataPinRejects;\n            MaterialPaletteInvalidations = materialInvalidations;\n""",
    """            Step4ExactMetadataPinRejects = step4MetadataPinRejects;\n            Step4FeatureFallbackScheduled = step4FallbackScheduled;\n            Step4FeatureFallbackCompleted = step4FallbackCompleted;\n            Step4FeatureFallbackNonEmpty = step4FallbackNonEmpty;\n            Step4FeatureFallbackPublished = step4FallbackPublished;\n            MaterialPaletteInvalidations = materialInvalidations;\n""",
)

replace_once(
    lod_test,
    """                      + $\"pinReject:{metrics.Step4ExactMetadataPinRejects} \"\n                      + $\"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/\"\n""",
    """                      + $\"pinReject:{metrics.Step4ExactMetadataPinRejects} \"\n                      + $\"fallback=s:{metrics.Step4FeatureFallbackScheduled}/\"\n                      + $\"c:{metrics.Step4FeatureFallbackCompleted}/\"\n                      + $\"n:{metrics.Step4FeatureFallbackNonEmpty}/\"\n                      + $\"p:{metrics.Step4FeatureFallbackPublished} \"\n                      + $\"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/\"\n""",
)

print("step-4 fallback lifecycle diagnostics applied")
