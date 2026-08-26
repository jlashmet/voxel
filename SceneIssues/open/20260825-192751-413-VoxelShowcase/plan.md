# Plan — restore validated GPU surface extraction for the Showcase

## Problem

The capture reports sub-100 FPS traversal, slow terrain fill, and visible holes while chunks are meshed in `Assets/Scenes/VoxelShowcase.unity`, with a goal of leaving enough frame budget for game logic. Current production code contained the existing GPU Transvoxel implementation but `CpuTransvoxelChunkCache.GpuCutoverDisabled` hard-disabled it for every ring, forcing exact terrain back through CPU extraction and CPU-to-GPU geometry upload.

Historical player profiling already showed that settled drawing is not the dominant cost: the renderer can submit the full solid set cheaply, while active CPU surface builds consume the frame and delay visible coverage. The GPU extractor has CPU-oracle parity coverage, writes directly into the shared surface arena, uses asynchronous counter readback, and supports exact steps 1 and 2; step 4 and block HLOD intentionally remain CPU/coarse paths.

## Approach

- [x] Trace the active Showcase surface path and identify the unconditional production rollback.
- [x] Add/confirm a focused policy regression for exact-ring GPU cutover.
- [x] Restore the previously validated GPU cutover default while retaining `VOXEL_DISABLE_GPU_CUTOVER=1` and existing CPU fallbacks.
- [x] Review historical player/oracle evidence to determine whether another source change is justified before current measurements. It is not; current Unity/player evidence is the next required decision point.
- [x] Audit available Actions reruns for a sanctioned current-head validation path. None can test this capture/current request without using another capture, the retired shared branch, or an old event SHA.
- [ ] Obtain a current authoritative green `ci/single-test` result for `SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings`.
- [ ] Run additional GPU density/topology oracle and arena-bridge coverage only if the focused run or current source changes require it.
- [ ] Run the existing `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` path so the production VoxelShowcase is exercised while moving and its real-player validation checks coverage/performance.
- [ ] Replay the assigned saved camera through `tools/showcase-player-capture.sh --scene-issue`, inspect the resulting screenshots/logs, and verify the marked view converges without missing geometry.
- [ ] If current player evidence still misses the requested performance goal, profile that measured state and make the next smallest causal optimization rather than guessing from historical bottlenecks.
- [ ] After verified production/test work, update terminal `issue.json` fields and move the capture to `SceneIssues/closed/` in a separate bookkeeping commit.
- [ ] Integrate the verified terminal branch into current `master` and verify the closed capture plus green targeted CI there.

## Current validation state

The earlier CI blocker diagnosis was incomplete. Experiment 007 rechecked the Actions history and found that contents-API request `6c0317b29b13f165235c936f31565e113f0eab25` eventually produced `Tests (single)` run `32989108800` after delayed event admission. That run executed exactly one Unity test case for `VoxelEngine.Tests.EditMode.GpuLod2CutoverPolicyTests.SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings` on Unity 6000.5.6f1 and passed. This proves the connector path is usable, but that older run is not final evidence because a newer request existed afterward.

For current-head validation, `ci-test/fixes/agent-2` was reset to feature head `51c11767ef88a094ccf9d10a9c0976c3f55c8577` and authoritative policy request `8bb029535005fbb9fde1365e39c7b41461ecc407` was pushed. A CI-branch-only exact-camera replay workflow for this same capture was then pushed as `c7bc806567c007f3cbc0310942a8a799ad88627a`. Neither changes production code or creates another SceneIssue. Only the newest authoritative test request may satisfy the targeted-CI gate; the exact replay is independent visual evidence.

`fixes/agent-2` remains current with `master`: `master` at `bfccb29f34f2373ae7cafac5a38e21a7c2e9ba86` is an ancestor of the feature branch. No master merge is currently required.

## Current findings

- The arena SubUpdates fix previously moved the full Showcase from 10–17 FPS with 200–670 permanently missing visible chunks to converged ~5–6 ms frames with missing-visible reaching zero.
- Later SmallVoxelShowcase player measurements reached roughly 920 FPS stationary and 770 FPS walking after LOD/visibility corrections, so a ~1000-FPS result is plausible only as a measured target, not something source inspection can honestly certify.
- The GPU Transvoxel path was validated through density/topology/material/boundary-ownership and shared-arena tests before the later unconditional production rollback. The current fix restores that already-tested cutover rather than introducing a second renderer.
- No historical result justifies another unmeasured production optimization before current CI/player replay.
- Actions request delivery from connector-authored pushes can be delayed; a missing run immediately after push is not proof that the event was suppressed.

## Constraints

- Do not introduce a second renderer or bypass `VoxelSurfaceScheduler` / `SurfaceGeometryArena`.
- Do not enable GPU extraction for step 4 or block HLOD until their dedicated parity contracts support it.
- Preserve CPU fallback for hardware without compute/async-counter support and for content the GPU path intentionally declines.
- Never edit `.github/test-request.json` on `fixes/agent-2`; targeted requests belong only on `ci-test/fixes/agent-2`.
- Until current CI, traversal, and replay evidence exist, keep this capture under `SceneIssues/open/` with `status: open`.
