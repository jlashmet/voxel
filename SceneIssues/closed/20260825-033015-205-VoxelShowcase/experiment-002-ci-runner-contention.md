# Experiment 002 — targeted regression blocked by runner contention

## Hypothesis
The saved-camera fresh-batched-tree regression will execute on the current source and reveal whether the captured player-shot path leaves an intact standing presentation.

## What was performed
Source commit: `c11dd926cbda4676860fd4ff85870b8e5eb1ebea`.

Reset `ci-test/fixes/agent-5` to that source commit and pushed a PlayMode single-test request for `VoxelEngine.CI.SceneIssue20260825033015TreeRenderingTests.CapturedPlayerShot_ReportsTreeCutAndStandingPresentation`. Request commit: `7034cdfd56c49af995590ffba191a6f177d698f2`. Workflow run: `32890129134`.

## Result
`ci/single-test` reported failure, but the requested test never ran. The workflow stopped in `Wait for any running Unity editor` after 60 seconds because an interactive Unity editor and its asset import workers were still running from `/Users/jlashmet/code/voxel`. The bake and requested-test steps were skipped, and no single-test artifact was produced.

## What was learned
**Inconclusive.** This run provides no evidence for or against the rendering hypothesis. It is an infrastructure/contention failure, not a regression failure, and cannot be counted as a behavioral result.

## Next
Push a new request ID on the same assigned CI branch and rerun the exact focused test once the runner guard can acquire Unity. Do not change production behavior until the test itself executes and yields telemetry.
