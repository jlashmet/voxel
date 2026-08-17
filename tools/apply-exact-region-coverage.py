#!/usr/bin/env python3
from pathlib import Path

CACHE = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
PLAN = Path('.claude/plans/voxel-showcase-rendering-repair-v2.md')


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'{label}: expected exactly one match, found {count}')
    return text.replace(old, new, 1)


cache = CACHE.read_text()
cache = replace_once(
    cache,
    '''        private JobHandle _exactMetadataJobHandle;\n        private bool _exactMetadataJobScheduled;\n        private bool _exactMetadataReady;\n''',
    '''        private JobHandle _exactMetadataJobHandle;\n        private bool _exactMetadataJobScheduled;\n        private bool _exactMetadataReady;\n        private ExactSnapshotRegionCoverage _exactMetadataRegionCoverage;\n''',
    'coverage field')

cache = replace_once(
    cache,
    '''                _exactMetadataJobScheduled = false;\n                ExactMetadataCompleteCount++;\n                if (!PinnedRegionMetadataCurrent())\n''',
    '''                _exactMetadataJobScheduled = false;\n                ExactMetadataCompleteCount++;\n                if (!_exactMetadataRegionCoverage.IsComplete)\n                {\n                    // A failed region metadata pin means this exact snapshot is unavailable,\n                    // never that the cleared cache range is authoritatively empty. Waited jobs\n                    // are already complete here, so release every successful pin and retry the\n                    // generation through the existing bounded discard/requeue lifecycle.\n                    ExactMetadataPinRejectCount++;\n                    ReleasePinnedRegionMetadataImmediate();\n                    _discardBuildAfterPinRelease = true;\n                    AccumulateSnapshotSlice(sliceStart, completed: false);\n                    return false;\n                }\n                if (!PinnedRegionMetadataCurrent())\n''',
    'coverage rejection')

cache = replace_once(
    cache,
    '''            _pinnedRegionSource = source;\n            _exactMixedBrickIndices.Clear();\n\n            JobHandle clearHandle = new ExactBrickMetadataClearJob\n''',
    '''            _pinnedRegionSource = source;\n            _exactMixedBrickIndices.Clear();\n            _exactMetadataRegionCoverage.Reset();\n\n            JobHandle clearHandle = new ExactBrickMetadataClearJob\n''',
    'coverage reset')

cache = replace_once(
    cache,
    '''                int3 regionCoord = new(rx, ry, rz);\n                if (!source.TryPinRegionBlockRefs(regionCoord, out PinnedRegionBlockRefs pinned))\n                    continue;\n                if (_pinnedRegionCount >= MaxExactSnapshotRegions)\n''',
    '''                int3 regionCoord = new(rx, ry, rz);\n                bool pinnedRegion = source.TryPinRegionBlockRefs(\n                    regionCoord, out PinnedRegionBlockRefs pinned);\n                _exactMetadataRegionCoverage.RecordRequiredRegion(pinnedRegion);\n                if (!pinnedRegion) continue;\n                if (_pinnedRegionCount >= MaxExactSnapshotRegions)\n''',
    'coverage record')
CACHE.write_text(cache)

plan = PLAN.read_text()
old = '''- [ ] Add a focused exact-classification regression reproducing a step-4 castle solid that exists in the exact COW source but is classified unowned; identify and fix only the first classifier/data-boundary defect proven by that regression, then rerun the lifecycle/LOD coverage gate before any broader coarse-geometry change.\n'''
new = '''- [x] Identify the first exact-snapshot data-boundary defect behind the production `unowned` classification. `IRegionReadSource.TryPinRegionBlockRefs` only succeeds for currently resident regions, but `ScheduleExactMetadataSnapshot` previously skipped a failed required region pin after clearing the cache, allowing unavailable metadata to be classified and published as authoritative empty. This matches lifecycle run 32033135300 (`unowned:20`, `readyEmpty:16`) with zero revision/payload-pin rejects.\n- [x] Add `ExactSnapshotRegionCoverageTests`: every padded-cache region intersection is required, and a failed exact region metadata pin makes the optimistic snapshot incomplete rather than empty (`cfd54a39`).\n- [x] Repair exact snapshot acquisition so incomplete required-region coverage is rejected after already-scheduled metadata jobs finish and retried through the existing bounded discard/requeue lifecycle; no synchronous completion, LOD distance, frame budget, or geometry threshold changes.\n- [ ] Validate the exact-region coverage regression in EditMode and rerun the step-4 lifecycle/LOD coverage gate; only mark the production false-empty defect resolved if `unowned/readyEmpty` drops for the castle view without introducing blocking completion or coverage regressions.\n'''
if old not in plan:
    raise SystemExit('plan task marker not found')
PLAN.write_text(plan.replace(old, new, 1))
print('exact region coverage repair applied')
