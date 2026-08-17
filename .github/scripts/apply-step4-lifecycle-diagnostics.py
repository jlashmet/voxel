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
    '''            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;\n            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;\n            _build.SnapshotTaken = true;\n''',
    '''            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;\n            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;\n            if (SourceStep == FeaturePreservingFallbackStep)\n                Step4FalseEmptyDiagnostics.RecordExactClassification(\n                    _build.HasOwnedSolid, _buildProfileBlocks.Length != 0);\n            _build.SnapshotTaken = true;\n''',
    'exact classification diagnostic')

phase1_old = '''                    _densityJobScheduled = false;\n                    _topologyJobScheduled = false;\n                    _topologyCompactJobScheduled = false;\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n'''
phase1_new = '''                    _densityJobScheduled = false;\n                    _topologyJobScheduled = false;\n                    _topologyCompactJobScheduled = false;\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SourceStep == FeaturePreservingFallbackStep)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n'''
cache = replace_once(cache, phase1_old, phase1_new, 'continuous result diagnostic')

phase2_old = '''                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n'''
phase2_new = '''                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (SourceStep == FeaturePreservingFallbackStep)\n                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(\n                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length);\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n'''
cache = replace_once(cache, phase2_old, phase2_new, 'faceted result diagnostic')

cache = replace_once(
    cache,
    '''        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)\n''',
    '''        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (SourceStep == FeaturePreservingFallbackStep)\n                Step4FalseEmptyDiagnostics.RecordFallbackScheduled();\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)\n''',
    'fallback schedule diagnostic')

cache = replace_once(
    cache,
    '''                        _hlodJobScheduled = false;\n                        _build.HasOwnedSolid = _indices.Length > 0;\n                    }\n                    if (_hlodOverflow[0] != 0)\n''',
    '''                        _hlodJobScheduled = false;\n                        _build.HasOwnedSolid = _indices.Length > 0;\n                        if (_build.UsedFeaturePreservingFallback)\n                            Step4FalseEmptyDiagnostics.RecordFallbackCompleted(\n                                _indices.Length > 0);\n                    }\n                    if (_hlodOverflow[0] != 0)\n''',
    'fallback completion diagnostic')

cache = replace_once(
    cache,
    '''            if (_indices.Length == 0)\n            {\n                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))\n''',
    '''            if (_indices.Length == 0)\n            {\n                if (SourceStep == FeaturePreservingFallbackStep)\n                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication();\n                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))\n''',
    'ready-empty diagnostic')

cache = replace_once(
    cache,
    '''            if (!published) return false;\n\n            entry.LastUsedFrame = frame;\n''',
    '''            if (!published) return false;\n\n            if (SourceStep == FeaturePreservingFallbackStep\n                && _build.UsedFeaturePreservingFallback)\n                Step4FalseEmptyDiagnostics.RecordFallbackPublished();\n            entry.LastUsedFrame = frame;\n''',
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
    '''                      + $"pinReject:{metrics.Step4ExactMetadataPinRejects} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"\n''',
    '''                      + $"pinReject:{metrics.Step4ExactMetadataPinRejects} "\n                      + $"step4Lifecycle={Step4FalseEmptyDiagnostics.Current} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"\n''',
    'diagnostic failure output')
LOD.write_text(lod)
