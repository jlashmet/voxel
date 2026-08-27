# Plan — unify near/far grass presentation

## Defect and acceptance
At the saved VoxelShowcase camera, right/far grass reads as a second texturing system: its grass motifs are roughly an order of magnitude larger than the tighter left/near grass. Accept only when the original pose/FOV shows one consistent grass texel density and the focused runtime regressions are green.

## Competing hypotheses and evidence
1. **Different material identity/policy.** Attempt 1 shared the texture array and UV math but reverse-looked up material from interpolated RGB; replay still failed. Attempt 2 carries the exact semantic material ID in `uv2`; the behavioral material-ID regression is green on its tested source, but the prior saved-pose replay still showed the scale mismatch.
2. **Different world-to-voxel scale.** `SmoothSurface` receives `_VoxelSize=0.1` inside `VoxelRenderPass`. `FarTerrain` is a separate `Graphics.DrawMesh` path yet uses the same global to convert world metres to voxel coordinates. If it sees the default/another global value of `1`, its texture is exactly 10x larger, matching the captured symptom. This hypothesis is falsified if a far material with an owned `0.1` scale still replays stretched.
3. **Different stylized colour treatment.** Far terrain still omits SmoothSurface's luminance-only/material-variation block. That can change contrast/chroma, but not plausibly the observed ~10x motif size; defer unless scale ownership is falsified.

## Attempt 3
- Red-contract source lineage: runtime test `FarTerrainOwnsCanonicalBaseVoxelScaleOnItsMaterial` at `d82cab960822c43f6878da2bbc0cd5c4faa92d21`.
- Minimal production fix: `ff51504447a4f8644b49776a5fa97a52478fb27c` gives `VoxelEngine/FarTerrain` a material-local `_VoxelSize` default of `0.1`, so ordinary far draws cannot inherit the wrong world-to-voxel scale from render-pass ordering.
- No new allocations, mesh work, texture samples, or per-frame CPU work.

## Remaining gates
Run the exact scale regression and existing material-ID regression on the production source, then replay `issue.json` for 45s and inspect the original 1928x836 view. If the visual still fails, attempt 3 is the third failed production attempt, so isolate the presentation in a minimal render reproduction before changing production again. On visual success, commit `verification-final.png`, complete issue bookkeeping, move the assigned capture to closed per the explicit task, recheck current master, and merge only this branch.
