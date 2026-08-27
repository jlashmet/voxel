# Experiment 012 — saved-pose replay falsifies far-terrain ownership

## Evidence

Exact CI request `fbdda5534e56a0b48a58ee38faacf89eeea352ef` completed `ci/single-test=success` in run `33095541153`. Its real-player `verification-final.png` still reproduces the report: a fine/dark grass treatment meets a pale, visibly oversized blade/flower treatment at a hard boundary.

The same run logs the ownership state at the stable replay frame: `FAR hole=365.9m inner=409.6m ... coverage=True`, while the captured camera is at `(160.55, 23.75, -1.95)` m and looks steeply downward at valley ground near 22 m elevation. The photographed foreground is therefore well inside the 365.9 m far-terrain hole.

## Result

The stretched grass in the captured frame is **not** drawn by `VoxelFarTerrain`. This falsifies the previous far-shader hypotheses (material lookup, far voxel-scale ownership, publish order, mip generation, and luminance-only parity) as explanations of the recorded defect, even where their focused regressions passed.

## Next discriminator

Identify the two near-field draw owners at the saved camera pose. The hard polygonal boundary suggests either a near-surface LOD/section boundary or a near-field vegetation/overlay mesh. Before another production edit, the regression must exercise the responsible production path rather than assert far-shader state.
