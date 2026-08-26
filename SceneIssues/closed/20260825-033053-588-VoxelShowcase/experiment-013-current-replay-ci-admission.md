# Experiment 013 — Current-bake replay CI admission

## Hypothesis
A targeted PlayMode replay of the exact saved camera against the current `VoxelShowcase` bake will reveal whether the originally reported north-field trees still exist, while preserving a rendered verification frame before the semantic interaction assertion.

## What was performed
- Source commit: `b88508d2a456c943999e57acfe581e8e2bb85104`.
- Updated the capture-specific explicit regression to pin the real `Showcase Camera` at the saved position/rotation/FOV for 60 settle frames and write `Artifacts/SingleTest/SceneIssue20260825-033053-588/verification-current-replay.png` plus metrics before the semantic-tree assertion.
- Force-reset `ci-test/fixes/agent-6` to that exact source commit, then committed request `f5e326e75f69b86dde5ab53073016ae75214e943` for only `VoxelEngine.CI.SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot` in PlayMode.
- Checked both commit status and Actions runs for the exact request SHA.

## Result
Inconclusive. The request commit exists at the tip of the assigned CI branch and matches the workflow's `ci-test/**` + `.github/test-request.json` push filter, but GitHub created no workflow run for `f5e326e75f69b86dde5ab53073016ae75214e943` and published no `ci/single-test` status during the admission check. No Unity result or verification artifact therefore exists for this attempt.

## What was learned
The current-bake geometry hypothesis was not tested by this request. A missing workflow run is not a test failure and cannot be used as replay evidence or closure evidence.

## Next
Reuse the same assigned CI branch, reset it to the current feature head, and issue one new request id for the same narrow replay. If the workflow admits, inspect its artifact/result; if it again does not admit, keep the capture open and document the concrete CI admission blocker rather than claiming verification.
