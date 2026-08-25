# Plan — 20260825-033015-205-VoxelShowcase

## Goal
Fix the VoxelShowcase tree-destruction defect where detached branches/crown fall but a thick rooted main-trunk presentation remains in the reported marked region.

## Scope and constraints
- Work only on capture `20260825-033015-205-VoxelShowcase`.
- Keep production/test work on `fixes/agent-5`; use only `ci-test/fixes/agent-5` for targeted CI requests.
- Preserve detached destruction debris while removing the surviving rooted trunk after the first structural trunk sever.
- Keep semantic collision/query state aligned with presentation; do not create an invisible shootable stump.
- Prefer the smallest change in tree sever classification/presentation rather than changing general batching.
- Do not change `.github/test-request.json` on the feature branch.
- Keep every experiment and replay/verification artifact with this capture.

## Findings / implementation direction
- The batch renderer correctly removes damaged tree bark, leaves, and impostor ranges; stale batching is not the cause.
- The saved pose/circle deterministically hits Willow tree index 12. A normal first shot makes a level-0 trunk cut, releases the batch, and reduces standing LOD0 from 5888 bark / 4200 leaf triangles to 16 / 0.
- Saved-view framebuffer proof still shows a thick rooted trunk in the circle: standing-renderer contribution is 712 pixels before and 159 after. This is the old intentional stump contract, not a later regression fix.
- The existing full destruction test explicitly requires a stump to remain standing. The capture rejects that contract.
- New invariant: a first structural level-0 sever disconnects the remaining tree from the root. Promote the semantic cut to the root-most level-0 trunk ancestor so rendering and collision/query state both retire the rooted tree. The detached presenter should use the actual hit point as the falling body's pivot so the whole remaining tree topples from the shot rather than from ground level.

## Acceptance criteria
- [x] The saved VoxelShowcase capture is traced to the responsible tree destruction/render path.
- [ ] A focused regression is red on the captured first-shot path because the marked region retains too many rooted tree pixels.
- [ ] The focused regression proves the fixed first trunk sever removes at least 90% of standing-tree contribution from the saved circle while preserving visible detached falling geometry.
- [ ] A trunk-severed tree no longer has rooted semantic collision/query geometry after the sever.
- [ ] The existing Showcase destruction lifecycle contract is updated to match whole-tree detachment rather than a persistent stump.
- [ ] The smallest production fix is implemented without weakening unrelated branch-cut behavior or the no-synchronous-batch-rebuild invariant.
- [ ] The focused regression passes through `ci/single-test` on `ci-test/fixes/agent-5` and executes at least one test.
- [ ] The relevant existing destruction test passes after the contract update.
- [ ] The original capture is replay/saved-pose verified after the fix, including its circled region, with durable verification evidence.
- [ ] Production/test work is committed first and its SHA is recorded as `fixCommit`.
- [ ] A separate bookkeeping commit marks the issue fixed and moves the complete capture from `SceneIssues/open/` to `SceneIssues/closed/`.

## Tasks
- [x] Fetch current `origin/master`, create/resume the assigned feature branch, and read repository workflow instructions.
- [x] Inspect the assigned capture note, fixture, and marked region metadata.
- [x] Locate the intact-tree renderer and destruction handoff that owns trunk visibility.
- [x] Record renderer-ownership, runner-contention, structural, and framebuffer experiments.
- [x] Add the saved-pose marked-region regression.
- [ ] Obtain the red result for the strengthened stump-rejecting assertion.
- [ ] Promote first trunk sever to the root-most level-0 branch and pivot falling whole-tree debris at the actual hit.
- [ ] Update the existing full destruction lifecycle test to the new no-rooted-stump contract.
- [ ] Commit/push the production/test fix on `fixes/agent-5` and record that commit SHA.
- [ ] Reset `ci-test/fixes/agent-5` from the exact fix commit, request the focused test, and obtain `ci/single-test` success with at least one executed test.
- [ ] Run the relevant existing destruction lifecycle test through the same targeted-CI loop.
- [ ] Replay/saved-pose verify the original capture and save durable verification artifacts into this capture.
- [ ] Update this plan with final evidence.
- [ ] Create the separate fixed-status/open-to-closed bookkeeping commit and push it.
