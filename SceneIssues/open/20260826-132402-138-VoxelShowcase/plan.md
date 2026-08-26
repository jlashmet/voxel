# Plan — 20260826-132402-138-VoxelShowcase

## Goal
Remove the large triangle occluding the doorway in the saved VoxelShowcase view without weakening unrelated arch/castle geometry.

## Scope and constraints
- Work only this assigned capture on `fixes/agent-9`; use only `ci-test/fixes/agent-9` for targeted CI.
- Preserve the original screenshot and capture metadata.
- Prefer a focused geometry invariant over a screenshot golden.
- Do not edit `.github/test-request.json` on the feature branch.
- Do not close the issue until targeted CI and replay verification are both successful.

## Acceptance criteria
- The recorded VoxelShowcase camera pose no longer shows a triangle spanning/occluding the doorway in the circled region.
- A focused regression fails on the broken behavior and passes with the fix.
- The smallest relevant existing test remains green.
- The original capture is replay-verified after the fix.
- Terminal bookkeeping records `status=fixed`, `resolvedUtc`, `resolutionSummary`, `regressionTest`, and the production/test `fixCommit`, then moves the whole capture to `SceneIssues/closed/` in a separate commit.

## Tasks
- [ ] Inspect the original screenshot and replay the saved pose; identify the responsible landmark/subsystem.
- [ ] Record the baseline/source-trace experiment and prove the failure mode.
- [ ] Add a focused regression around the bad doorway topology.
- [ ] Implement the smallest production fix.
- [ ] Commit and push production/test work to `fixes/agent-9`.
- [ ] Refresh from `origin/master`; rerun targeted CI on the integrated head if tested inputs changed.
- [ ] Replay the original capture and save verification evidence.
- [ ] Update experiment files and check off this plan with final validation evidence.
- [ ] Make the separate terminal bookkeeping commit and move the capture open -> closed.
- [ ] Integrate the verified branch into current `master` without force-pushing and verify remote master.
