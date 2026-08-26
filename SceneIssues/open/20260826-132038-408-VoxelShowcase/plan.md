# Plan — unify near/far grass presentation

## Reported defect

At the saved VoxelShowcase camera, grass on the right reads as a larger stretched pattern while the left uses the intended tighter texture. The user explicitly calls out two different grass-texturing methods and expects one consistent presentation.

## Evidence and hypotheses

1. `SmoothSurface.shader` samples the near field from the shared albedo texture array in world/voxel space (`positionWS / _VoxelSize`) using the material sampling/surface tables.
2. `_VoxelSize` is bound by `VoxelRenderPass` to the constant base voxel size (0.1 m), so near-field LOD cell size does not change the texture coordinate scale. The initial per-LOD `_VoxelSize` hypothesis is falsified.
3. `VoxelFarTerrain` resolves the same semantic material ID as the near field but discards it after resolving one albedo colour. Its mesh stores only vertex colour, and `FarTerrain.shader` does not sample the shared texture array. A coarse clipmap therefore produces broad interpolated grass colour that can read as a stretched second texture system.
4. The sibling SceneIssue `20260825-032832-253-VoxelShowcase` separately investigates thin LOD/transition contour artifacts. Its retained experiment shows moving the fine-detail band does not remove those strips, so this capture should not be fixed by another LOD-distance tweak.

## Planned minimal fix

- Preserve the far mesh's semantic material ID in a dedicated UV channel while retaining vertex colour as the base/fallback tone.
- Bind the renderer-owned albedo texture array plus the authoritative material sampling/surface rows onto the far-terrain material without duplicating texture assets in the showcase.
- Make `FarTerrain.shader` use the same world-space coordinate basis and material texture scale/weight as `SmoothSurface.shader` when the shared texture array is available; keep existing vertex-colour rendering as the safe startup fallback.
- Add a focused regression that loads VoxelShowcase and proves every published far ring carries semantic material identity needed for shared texture sampling, plus a shader/material contract assertion for the shared albedo input.

## Verification

1. Run the focused regression through `ci-test/fixes/agent-5` and verify the worker result is green for the exact source commit.
2. Replay the existing capture at its saved camera pose using the repository SceneIssue replay harness; do not create a new capture.
3. Preserve replay/CI evidence in this capture.
4. Use the repository terminal bookkeeping command to close/move the issue in a separate bookkeeping commit, integrate the verified fix into current `master`, and push the verified terminal state.
