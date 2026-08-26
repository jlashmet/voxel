# Plan — unify near/far grass presentation

## Reported defect

At the saved VoxelShowcase camera, grass on the right reads as a larger stretched pattern while the left uses the intended tighter texture. The report explicitly calls out two different grass-texturing methods and expects one consistent presentation.

## Evidence and hypotheses

1. `SmoothSurface.shader` samples the near field from the shared albedo texture array in world/voxel space (`positionWS / _VoxelSize`) using the material sampling/surface tables.
2. `_VoxelSize` is bound by `VoxelRenderPass` to the constant base voxel size (0.1 m), so near-field LOD cell size does not change the texture coordinate scale. The initial per-LOD `_VoxelSize` hypothesis is falsified.
3. `VoxelFarTerrain` resolves the same semantic surface material as the near field, but its published mesh retained only that material's albedo as vertex colour. `FarTerrain.shader` therefore had no shared texture-array/material-sampling path and rendered a broad coarse colour field where the near surface rendered authored grass texture.
4. The sibling SceneIssue `20260825-032832-253-VoxelShowcase` separately investigates thin LOD/transition contour artifacts. Its retained experiment shows moving the fine-detail band does not remove those strips, so this capture should not be fixed by another LOD-distance tweak.

## Candidate minimal fix

Implemented in production commit `506d4b37a42639bb1b9d48f1796e7794446d3c40`:

- Keep `VoxelFarTerrain`'s existing authoritative vertex albedo/fallback representation unchanged.
- Have `FarTerrain.shader` resolve that albedo against the renderer-owned `_MaterialAlbedo` table and consume `_MaterialSampling`, `_MaterialSurface`, `_AlbedoTextures`, and `_VoxelSize`.
- Reuse the same dominant-axis/triplanar world-space texture basis, base-voxel coordinate system, material texture scale/weight, and `hitDistance / 350.0` attenuation as `SmoothSurface.shader`.
- Do not introduce a scene-specific grass texture, material, or LOD-distance workaround.

Focused regression authored before production change:
`VoxelEngine.Tests.EditMode.FarTerrainSharedTexturePresentationTests.FarTerrainReusesVoxelSurfaceTextureSamplingContract`.

## Verification state

- [x] Fetch/resume `fixes/agent-5` and inspect repository workflow/capture.
- [x] Record root-cause hypothesis before production code.
- [x] Author focused regression before production code.
- [x] Commit candidate production shader change (`506d4b37a42639bb1b9d48f1796e7794446d3c40`).
- [ ] Obtain an executed pre-fix/fix targeted CI result. **Blocked:** four request publications on `ci-test/fixes/agent-5` produced no Actions run/status; see `experiment-002-ci-trigger-blocked.md`.
- [ ] Replay the existing capture at its saved camera pose. Not attempted because CI verification is a prerequisite.
- [ ] Preserve green CI/replay evidence.
- [ ] Create the separate fixed-status/open-to-closed bookkeeping commit.
- [ ] Integrate verified fix into current `master` and push verified terminal state.

## Blocked-state rule

The capture remains in `SceneIssues/open/` with `issue.json.status` unchanged. Do not populate fixed bookkeeping or move it to `closed/` until `ci/single-test` executes successfully for the exact candidate source and the saved capture is replay-verified.
