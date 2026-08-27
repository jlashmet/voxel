# Experiment 009 — successful exact-camera replay

## Hypothesis
With the corrected `SmoothSurface.shader`, the original VoxelShowcase camera pose should no longer show a distinctly blue high-detail terrain band at the detailed/far handoff.

## Action
Replayed `SceneIssues/open/20260826-132144-249-VoxelShowcase/issue.json` from production/test source SHA `bcd4d034f7429c9f9e627e08b9e1d4836e142cc0` using assigned CI request `4a8d0af0edab8955bbec91ddccc11c81ec74154d` (`agent-7-20260826-132144-detailed-tint-replay-r2`). GitHub Actions run `33018852581`, job `98343889283`, artifact `9625790344`.

The request reran `DetailedSurfaceColourDoesNotShiftTowardSkyWithDistance`, then built the real VoxelShowcase player, pinned the saved scene-issue camera, captured the presented frame sequence for 45 seconds, emitted previews, and uploaded the required artifact.

## Result
**PASS.** The job completed `success` and request SHA `4a8d0af0edab8955bbec91ddccc11c81ec74154d` has `ci/single-test=success`. `RealPlayer/verification-final.png` shows the high-detail foreground as green/material-coloured with brown paths and surface detail; the reported blue cast is not present.

A repository-size preview derived from that successful final frame is committed as `verification-final.png`; the full 1600×900 source frame remains in the run artifact. Full-frame SHA-256: `3f3cf67080095f1d80ab0446b4eb281e1b29da428bca94b30840ade83d112aca`.

## Verdict
The corrected production shader passes both focused GPU behavioral regression and fresh saved-camera standalone-player verification.

## Next
Check the feature-only diff against current master. Do not move this capture to `pending` if unrelated capture/code remains on the persistent agent branch.
