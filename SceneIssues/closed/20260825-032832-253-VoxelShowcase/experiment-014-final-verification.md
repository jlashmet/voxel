# Experiment 014 — final verification

## Regression

`VoxelEngine.Tests.EditMode.TransitionMeshJobNormalTests.SlantedFaceFieldEmitsNormalsWithTangentialComponent` passed on exact request `c1cfafb7870ee70b48b46eec1e855b988a9f1100` (Actions run `33021302016`) against production fix commit `44312d34690f8fb3ce0bb62f95dc8368172323d8`.

## Saved-camera replay

The original `issue.json` pose was replayed in the real VoxelShowcase player by request `b2944662de402c9e25aac749d1adddceaeaab709` (run `33021624676`). The PlayMode assertion, real-player capture, screenshot upload, screenshot-presence gate, and final-status publication steps all completed successfully. The job was subsequently marked cancelled when post-run cleanup crossed the workflow's five-minute job timeout; this occurred after the replay screenshots had already been uploaded.

The shorter identical replay request `cce9004b1f39f7d2891ba4acc456778481eda599` (run `33022161812`) then completed successfully. The requested PlayMode test, real-player saved-camera capture, screenshot preview/upload, artifact classification, and final status publication all passed, and the exact commit status `ci/single-test` is `success`.

## Visual inspection

Inspected the loaded replay frames at approximately 25.6 s and 35.6 s plus the generated `verification-final.png`. All three user-marked regions are visually clean: the coarse/flat transition-shading patches are absent and terrain/grass lighting is continuous. The upper-left region retains the expected jagged road/terrain boundary geometry, but no duplicate coarse surface or low-resolution-looking LOD shading strip is visible.

## Verdict

Verified fixed. The evidence supports the isolated cause: transition vertices lacked compatible slope-aware normals, and edge-clamped finite differences also needed sample-span normalization to preserve gradient direction.