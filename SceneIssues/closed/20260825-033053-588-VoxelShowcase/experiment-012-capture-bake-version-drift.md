# Experiment 012 — Capture/bake version drift

## Hypothesis
The assigned capture was recorded against a different `VoxelShowcase` startup bake than the one currently authoritative on `fixes/agent-6`, so the exact saved view must be replayed against the current bake before treating the originally visible trees as current geometry.

## Action
- Located the original capture commit through the pre-queue path `SceneIssues/20260825-033053-588-VoxelShowcase/issue.json`.
- Compared `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` at that capture commit with the current feature branch bake.
- Checked the later bake-refresh history before making any production change.

## Result
- Capture commit: `121b1ed3f75fd5a179ebbcf9be08987a8c0d551d`, authored/committed `2026-08-25T03:35:35Z` (`Capture new VoxelShowcase scene issues`).
- Capture-era bake blob: `ed15e4d75ef1acc2eab8d845cb431a4ba28afae3`, 23,096,216 bytes.
- Current branch bake blob: `53b8673358e6166445162d64be4f4af89c132a50`, 11,074,525 bytes.
- A later CI commit `70ef06ec585e79001e8253efd2ceab53d8a696e7` refreshed the VoxelShowcase bake at `2026-08-25T11:26:20Z`, after the capture commit.

## Conclusion
Confirmed version drift. The original screenshot cannot by itself establish that the same north-field tree geometry still exists in the current authoritative scene. Before any further production edit, replay/query the exact saved camera against the current startup bake and determine whether the reported geometry is still present. If it is absent, closure must be justified by exact current-scene replay rather than by proxy semantic-tree tests; if it remains, trace that current geometry to its interaction owner.
