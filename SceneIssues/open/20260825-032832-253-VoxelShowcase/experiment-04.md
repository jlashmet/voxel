# Experiment 04 — first exact-view replay attempt

## Hypothesis

The saved one-frame fixture can be replayed after the fix at the recorded `Showcase Camera` pose, 70° FOV, 1364x836 framing, with all three marked regions available for visual adjudication.

## What was performed

Added `ShowcaseSceneIssue032832ReplayTests.SavedFixtureIsConfiguredForExactReplay` on source commit `3767a8d947d6a074dc9f62482649ff92a41d9aad` and requested it through `ci-test/fixes/agent-4` at request commit `e83d91c68aaf90a257a5a81a8dd69d795f2368a9`. GitHub Actions run: `32890369760`.

## Result

Inconclusive. The job failed before Unity tests because the runner's 60-second safety guard found an interactive Unity editor and import workers still open for `/Users/jlashmet/code/voxel`. The always-run real-player step then reported that this new test filter had no configured capture profile and skipped standalone capture, so no replay artifact was produced.

## What was learned

The production fix was not exercised by this run. Two independent replay prerequisites remain: the runner must be Unity-idle, and the shared `tools/showcase-player-capture.sh` filter table must map this regression to `VoxelShowcase.unity` plus the saved issue fixture. The PlayMode RenderTexture diagnostic alone is not sufficient closure evidence.

## Next

Add the missing shared real-player mapping, preserve the capture's original aspect ratio, then issue a new latest-request-wins CI run on the same assigned CI branch.
