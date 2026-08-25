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
- Keep experiment and replay evidence with this capture.

## Findings / final implementation
- The batch renderer correctly removes damaged tree bark, leaves, and impostor ranges; stale batching was not the cause.
- The saved pose/circle deterministically hits Willow tree index 12. A normal first shot makes a level-0 trunk cut and releases that tree from its spatial batch.
- Before the fix, the saved-view target dropped from 5888 bark / 4200 leaf triangles to 16 / 0 but still left the thick rooted stump shown in the reported circle.
- The old full destruction contract explicitly expected such a stump. The capture requires the opposite ownership transition.
- `TreeSkeletonTopology` now treats any direct level-0 structural cut as retirement of the entire rooted branch topology while preserving the exact direct-cut index as the gameplay event.
- `ProceduralTreeDetachedLimbPresenter` owns the entire remaining skeleton after the first level-0 sever and pivots that falling body at the actual hit point.
- Renderer and collision/query state now agree: the rooted dynamic presentation is destroyed and sweeps through the old root no longer hit the severed tree.
- The initial 90%-of-all-tree-pixels acceptance metric was invalid because the recorded circle also contains a different pink-leaf procedural tree behind the target. The final regression therefore gates target ownership directly and keeps the saved framebuffer/all-tree pixel count as replay diagnostics.

## Verified evidence
- Red proof: workflow `32892353422`, request commit `43aab0630933d7ff3ac13f358f9d7024d7c1049f`, exactly one focused test failed at standing pixels `712 -> 159` versus allowed `72`.
- First production attempt: workflow `32895437301`, request commit `abca235da6f9d67035c2ef065a609a9c72a805f6`, exactly one test failed the same over-broad pixel gate at `712 -> 155`; shutdown evidence showed the target rooted object was already absent.
- Corrected saved-view verification: workflow `32895976717`, request commit `766e212f78c6d8fa16eb142a930ad0924587c4cf`, exactly one test passed. `targetStandingPresentationAfter=False`, bark `5888 -> 0`, leaves `4200 -> 0`; saved framebuffer inspection shows no thick rooted target stump.
- Showcase destruction lifecycle: workflow `32896212772`, request commit `7a70a7439fc09c413bf6ffbf3bc0074ec30acab1`, exactly one test passed. Branch debris and whole-tree debris visibly fall, move/rotate, and expire; rooted presentation is false after trunk sever.
- Tree batch/query regression: workflow `32896511398`, request commit `d6e20951fd75e743bc99265fbc5aebc8858a5ce6`, exactly one test passed. Healthy batching/one-tree release remain intact, all rooted topology retires on the structural sever, and the old root is not collision-queryable.
- Durable replay summary is stored in `verification-fixed.txt`; experiment details are in experiments 005–009.
- Production/test fix SHA for bookkeeping: `eaead8ede86cbf90e36ead8d92ddbc4a34083aa9`.

## Acceptance criteria
- [x] The saved VoxelShowcase capture is traced to the responsible tree destruction/render path.
- [x] A focused regression is red on the captured first-shot path and deterministically reproduces the rooted-stump defect.
- [x] The fixed saved-pose regression proves the structurally severed target has no rooted standing presentation while preserving visible detached falling geometry.
- [x] A trunk-severed tree no longer has rooted semantic collision/query geometry after the sever.
- [x] The existing Showcase destruction lifecycle contract is updated to whole-tree detachment rather than a persistent stump.
- [x] The production fix preserves unrelated branch-cut behavior and the no-synchronous-batch-rebuild invariant.
- [x] The focused regression passes through `ci/single-test` on `ci-test/fixes/agent-5` and executes exactly one test.
- [x] The relevant Showcase destruction lifecycle test passes and executes exactly one test.
- [x] The batching/query regression passes and executes exactly one test.
- [x] The original capture is replay/saved-pose verified after the fix, including its circled region, with durable evidence in the capture.
- [x] Production/test work is committed first and its SHA is recorded for `fixCommit`.
- [ ] A separate bookkeeping commit marks the issue fixed and moves the complete capture from `SceneIssues/open/` to `SceneIssues/closed/`.

## Tasks
- [x] Fetch current `origin/master`, resume the assigned feature branch, and read repository workflow instructions.
- [x] Inspect the assigned capture note, fixture, screenshot, and marked region metadata.
- [x] Locate the intact-tree renderer and destruction handoff that owns trunk visibility.
- [x] Record renderer-ownership, runner-contention, structural, framebuffer, failed-fix, and verification experiments.
- [x] Add and prove the saved-pose regression red.
- [x] Retire all rooted topology on the first structural level-0 direct cut while preserving the exact cut event.
- [x] Pivot detached whole-tree debris at the actual hit point.
- [x] Update the existing full destruction lifecycle test to the no-rooted-stump contract.
- [x] Commit/push the production/test fix on `fixes/agent-5`; bookkeeping fix SHA is `eaead8ede86cbf90e36ead8d92ddbc4a34083aa9`.
- [x] Obtain focused saved-view `ci/single-test` success with exactly one executed test.
- [x] Obtain Showcase destruction lifecycle `ci/single-test` success with exactly one executed test.
- [x] Obtain batching/query `ci/single-test` success with exactly one executed test.
- [x] Replay/saved-pose verify the original capture and preserve durable verification evidence.
- [x] Update this plan with final evidence.
- [ ] Create the separate fixed-status/open-to-closed bookkeeping commit and push it.
