# Experiment 008 — far draw owns base-voxel scale

## Hypothesis
The remaining ~10x grass-size jump is caused by `FarTerrain.shader` using `_VoxelSize` without owning it. Near `SmoothSurface` receives `0.1` inside `VoxelRenderPass`, but far terrain is drawn separately with `Graphics.DrawMesh`; inheriting `1.0` converts world metres directly to texture coordinates and makes the same texture motifs 10x larger.

## Action and source
- Added behavioral regression `FarTerrainOwnsCanonicalBaseVoxelScaleOnItsMaterial` on test-only source `d82cab960822c43f6878da2bbc0cd5c4faa92d21`. It requires the actual VoxelShowcase-created far material to retain `ShowcaseWorld.VoxelSize` even while the global `_VoxelSize` is deliberately changed to `1`.
- Minimal production change: `ff51504447a4f8644b49776a5fa97a52478fb27c` adds material-local `_VoxelSize ("Base Voxel Size", Float) = 0.1` to `VoxelEngine/FarTerrain`.
- Cost: one material property; no extra samples, allocations, geometry, jobs, or per-frame CPU work.

## Result
CI and saved-pose replay are pending. The existing request for attempt 2 was already queued and is intentionally left untouched per `SceneIssues/README.md`; no competing request is being issued while it is active.

## Verdict / next
If the focused scale regression is green and the original camera no longer shows the stretched right-side grass, promote this fix. If the saved pose still fails, this is the third failed production attempt and the next step is a minimal render reproduction before any further production change.
