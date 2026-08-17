#!/usr/bin/env python3
from pathlib import Path

path = Path('.claude/plans/voxel-showcase-rendering-repair-v2.md')
text = path.read_text()
old = "- [ ] Identify the exact ready-empty adjudication cause before the next geometry change: distinguish whether production step-4 castle chunks enter the feature-preserving fallback and still finish empty, never enter it because exact ownership is false, or are excluded by authored-profile handling. Add a cache-lifecycle regression/diagnostic for the proven cause before changing coarse geometry again."
new = "- [x] Add step-4 fallback lifecycle diagnostics that separately count fallback scheduling, worker completion, non-empty HLOD output and successful GPU publication; expose the counters through `VoxelSurfaceMetrics` and the existing LOD failure diagnostic without changing renderer behavior.\n- [ ] Use the lifecycle counters to identify the exact ready-empty adjudication cause before the next geometry change: determine whether production step-4 castle chunks never enter fallback, finish fallback empty, or produce non-empty geometry that fails to publish/remain visible. Add a focused regression for the proven cause before changing coarse geometry again."
if text.count(old) != 1:
    raise SystemExit(f'expected one plan task match, found {text.count(old)}')
path.write_text(text.replace(old, new, 1))
