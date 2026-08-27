# Experiment 007 — corrected detailed-terrain tint regression CI

## Hypothesis
Removing the detailed terrain's 60–300 m sky-colour blend while restoring the far shader's native long-range haze satisfies the corrected rendering invariant and compiles cleanly in Unity.

## What was performed
Ran `VoxelEngine.Tests.EditMode.DetailedTerrainTintRegressionTests.DetailedTerrainDoesNotBlendSkyColourByCameraDistance` through the assigned `ci-test/fixes/agent-7` targeted-CI branch against production/test commit `bc24592304d8c0bdb92ee7647adc5536586e6450`. Request commit: `a80bf59385ae1601ad63696698b6d3bf0b5c1bfa`. GitHub Actions run `33014528240`, job `98329176956`.

## Result
**PASS.** `ci/single-test` completed with `success`; the requested Unity EditMode test passed and the workflow completed successfully.

## What was learned
**Hypothesis confirmed at source/compile level.** Detailed terrain no longer contains the explicit camera-distance sky-colour tint, normal-oriented sky ambient remains, and far terrain retains only its native long-range aerial haze. This does not substitute for the required saved-camera visual replay.

## Next
Replay `SceneIssues/open/20260826-132144-249-VoxelShowcase/issue.json` in the real VoxelShowcase player at the captured camera and inspect the resulting presented frame for the original blue high-detail band.
