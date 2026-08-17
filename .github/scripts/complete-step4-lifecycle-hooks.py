#!/usr/bin/env python3
from pathlib import Path

path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
text = path.read_text()

old_empty = '''                _emptyVersions[_build.Coordinate] = _build.SourceVersion;\n                CompletedBuildCount++;\n'''
new_empty = '''                _emptyVersions[_build.Coordinate] = _build.SourceVersion;\n                if (SourceStep == FeaturePreservingFallbackStep)\n                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication();\n                CompletedBuildCount++;\n'''
if text.count(old_empty) != 1:
    raise SystemExit(f'expected one ready-empty hook location, found {text.count(old_empty)}')
text = text.replace(old_empty, new_empty, 1)

old_publish = '''            if (_build.UsedFeaturePreservingFallback)\n                FeaturePreservingFallbackPublishCount++;\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n'''
new_publish = '''            if (_build.UsedFeaturePreservingFallback)\n            {\n                FeaturePreservingFallbackPublishCount++;\n                Step4FalseEmptyDiagnostics.RecordFallbackPublished();\n            }\n            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));\n'''
if text.count(old_publish) != 1:
    raise SystemExit(f'expected one fallback-publish hook location, found {text.count(old_publish)}')
text = text.replace(old_publish, new_publish, 1)

path.write_text(text)
print('completed step-4 lifecycle diagnostic hooks')
