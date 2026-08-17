#!/usr/bin/env python3
from pathlib import Path

CACHE = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
LOD = Path('Assets/Tests/PlayMode/LodRenderingTests.cs')


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'{label}: expected exactly one match, found {count}')
    return text.replace(old, new, 1)


cache = CACHE.read_text()

cache = replace_once(
    cache,
    '''            _exactClassificationJobScheduled = false;\n            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;\n            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;\n            _build.SnapshotTaken = true;\n''',
    '''            _exactClassificationJobScheduled = false;\n            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;\n            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;\n            if (SourceStep == FeaturePreservingFallbackStep)\n                Step4FalseEmptyDiagnostics.RecordExactClassification(\n                    _build.HasOwnedSolid, _buildProfileBlocks.Length != 0);\n            _build.SnapshotTaken = true;\n''',
    'exact classification diagnostic')

phase1_old = '''                    _densityJobScheduled = false;\n                    _topologyJobScheduled = false;\n                    _topologyCompactJobScheduled = false;\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n'''
phase1_new = '''                    _densityJobScheduled = false;\n                    _topologyJobScheduled = false;\n                    _topologyCompactJobScheduled = false;\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SourceStep == FeaturePreservingFallbackStep)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n'''
cache = replace_once(cache, phase1_old, phase1_new, 'continuous result diagnostic')

phase2_old = '''                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n'''
phase2_new = '''                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SourceStep == FeaturePreservingFallbackStep)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n'''
cache = replace_once(cache, phase2_old, phase2_new, 'faceted result diagnostic')

cache = replace_once(
    cache,
    '''        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (SupportsFeaturePreservingFallback)\n                FeaturePreservingFallbackScheduleCount++;\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)\n''',
    '''        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (SupportsFeaturePreservingFallback)\n            {\n                FeaturePreservingFallbackScheduleCount++;\n                Step4FalseEmptyDiagnostics.RecordFallbackScheduled();\n            }\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)\n''',
    'fallback schedule diagnostic')

cache = replace_once(
    cache,
    '''                        if (_build.UsedFeaturePreservingFallback)\n                        {\n                            FeaturePreservingFallbackCompleteCount++;\n                            if (_build.HasOwnedSolid)\n                                FeaturePreservingFallbackNonEmptyCount++;\n                        }\n''',
    '''                        if (_build.UsedFeaturePreservingFallback)\n                        {\n                            FeaturePreservingFallbackCompleteCount++;\n                            if (_build.HasOwnedSolid)\n                                FeaturePreservingFallbackNonEmptyCount++;\n                            Step4FalseEmptyDiagnostics.RecordFallbackCompleted(\n                                _build.HasOwnedSolid);\n                        }\n''',
    'fallback completion diagnostic')

cache = replace_once(
    cache,
    '''                _emptyVersions[_build.Coordinate] = _build.SourceVersion;\n                CompletedBuildCount++;\n''',
    '''                if (SourceStep == FeaturePreservingFallbackStep)\n                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication();\n                _emptyVersions[_build.Coordinate] = _build.SourceVersion;\n                CompletedBuildCount++;\n''',
    'ready-empty diagnostic')

cache = replace_once(
    cache,
    '''            if (_build.UsedFeaturePreservingFallback)\n                FeaturePreservingFallbackPublishCount++;\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n''',
    '''            if (_build.UsedFeaturePreservingFallback)\n            {\n                FeaturePreservingFallbackPublishCount++;\n                if (SourceStep == FeaturePreservingFallbackStep)\n                    Step4FalseEmptyDiagnostics.RecordFallbackPublished();\n            }\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n''',
    'fallback publish diagnostic')

CACHE.write_text(cache)

lod = LOD.read_text()
lod = replace_once(
    lod,
    '''        public IEnumerator CastleKeepsVoxelGeometryAcrossEveryLodBand()\n        {\n            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(\n''',
    '''        public IEnumerator CastleKeepsVoxelGeometryAcrossEveryLodBand()\n        {\n            Step4FalseEmptyDiagnostics.Reset();\n            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(\n''',
    'diagnostic reset')

lod = replace_once(
    lod,
    '''                      + $"fallback=s:{metrics.Step4FeatureFallbackScheduled}/"\n                      + $"c:{metrics.Step4FeatureFallbackCompleted}/"\n                      + $"n:{metrics.Step4FeatureFallbackNonEmpty}/"\n                      + $"p:{metrics.Step4FeatureFallbackPublished} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"\n''',
    '''                      + $"fallback=s:{metrics.Step4FeatureFallbackScheduled}/"\n                      + $"c:{metrics.Step4FeatureFallbackCompleted}/"\n                      + $"n:{metrics.Step4FeatureFallbackNonEmpty}/"\n                      + $"p:{metrics.Step4FeatureFallbackPublished} "\n                      + $"step4Lifecycle={Step4FalseEmptyDiagnostics.Current} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"\n''',
    'diagnostic failure output')

LOD.write_text(lod)
print('step4 lifecycle diagnostic patch applied')
