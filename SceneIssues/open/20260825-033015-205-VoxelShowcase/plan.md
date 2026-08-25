# Plan — 20260825-033015-205-VoxelShowcase

## Goal
Fix the VoxelShowcase tree-destruction defect where detached branches fall but the main trunk remains rendered after the tree has been destroyed.

## Scope and constraints
- Work only on capture `20260825-033015-205-VoxelShowcase`.
- Keep production/test work on `fixes/agent-5`; use only `ci-test/fixes/agent-5` for targeted CI requests.
- Preserve detached destruction debris while removing the stale intact-tree/trunk rendering state.
- Prefer the smallest ownership/state fix in the responsible tree rendering/destruction subsystem.
- Do not change `.github/test-request.json` on the feature branch.
- Keep every experiment and replay/verification artifact with this capture.

## Acceptance criteria
- [ ] The saved VoxelShowcase capture is traced to the responsible tree destruction/render path.
- [ ] A focused regression deterministically proves that destroying a tree removes/deactivates the intact trunk render representation while retaining the intended detached debris behavior.
- [ ] The smallest production fix is implemented without weakening unrelated rendering or destruction behavior.
- [ ] The focused regression passes through `ci/single-test` on `ci-test/fixes/agent-5` and executes at least one test.
- [ ] The original capture is replay-verified after the fix, including its circled region, with durable verification evidence.
- [ ] Production/test work is committed first and its SHA is recorded as `fixCommit`.
- [ ] A separate bookkeeping commit marks the issue fixed and moves the complete capture from `SceneIssues/open/` to `SceneIssues/closed/`.

## Tasks
- [x] Fetch current `origin/master`, create/resume the assigned feature branch, and read repository workflow instructions.
- [x] Inspect the assigned capture note, fixture, and marked region metadata.
- [ ] Locate the intact-tree renderer and destruction handoff that owns trunk visibility.
- [ ] Record the initial reproduction/diagnostic experiment.
- [ ] Add the focused regression.
- [ ] Implement the fix.
- [ ] Commit/push production and regression changes to `fixes/agent-5`.
- [ ] Reset/create `ci-test/fixes/agent-5` from the exact fix commit, add the targeted test request there, and obtain `ci/single-test` success.
- [ ] Replay-verify the original capture and record the result.
- [ ] Update this plan with final evidence.
- [ ] Create the separate fixed-status/open-to-closed bookkeeping commit and push it.
