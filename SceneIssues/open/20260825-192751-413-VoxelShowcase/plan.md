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
- [ ] Obtain a real green `ci/single-test` result for `SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings`.
- [ ] Run additional GPU density/topology oracle and arena-bridge coverage if the focused run or current branch changes require it.
- [ ] Replay/benchmark the assigned VoxelShowcase pose and verify missing geometry converges correctly; record measured performance rather than claiming the aspirational 1000-FPS target without evidence.
- [ ] If current player evidence still misses the requested frame target, profile that measured state and make the next smallest causal optimization rather than guessing from historical bottlenecks.
- [ ] After verified production/test work, update terminal `issue.json` fields and move the capture to `SceneIssues/closed/` in a separate bookkeeping commit.
- [ ] Integrate the verified terminal branch into current `master` and verify the closed capture plus green targeted CI there.

## Current blocker

All available connector-authored write paths to `ci-test/fixes/agent-2` suppress the repository's push-triggered `Tests (single)` workflow. Contents-API request commits `1ad552ec0d42187eb660525848b8815bc3aa7297` and `6c0317b29b13f165235c936f31565e113f0eab25`, plus low-level Git-ref request commit `9ce7b8049f65108bf2134679f35f26a98f1cc161`, produced neither a `ci/single-test` status nor an Actions run. Older human-authored pushes on the same branch did trigger `.github/workflows/tests-single.yml`, so the branch/workflow definition is valid and the missing event is specific to this execution path. The local shell has no repository checkout, no GitHub CLI/authentication, and no network path to GitHub, so it cannot substitute a normal authenticated `git push`.

The connector now exposes Actions reruns, but experiment 006 confirmed they cannot establish current-head evidence. `Tests (single)` reruns use the historical event checkout and therefore remain pinned to the old request SHA. The only old replay found that explicitly checks out current `ci-test/fixes/agent-2` is hardcoded to a different assigned capture, while historical GPU oracle one-shots check out the retired shared `fixes` branch and write into the old `014011` capture. Reusing any of those would violate branch/capture scope and still would not produce `ci/single-test` on request `9ce7b8049f65108bf2134679f35f26a98f1cc161`.

`fixes/agent-2` remains current with `master`: `master` at `bfccb29f34f2373ae7cafac5a38e21a7c2e9ba86` is an ancestor of the feature branch. No master merge is currently required.

## Current findings

- The arena SubUpdates fix previously moved the full Showcase from 10–17 FPS with 200–670 permanently missing visible chunks to converged ~5–6 ms frames with missing-visible reaching zero.
- Later SmallVoxelShowcase player measurements reached roughly 920 FPS stationary and 770 FPS walking after LOD/visibility corrections, so a ~1000-FPS result is plausible only as a measured target, not something source inspection can honestly certify.
- The GPU Transvoxel path was validated through density/topology/material/boundary-ownership and shared-arena tests before the later unconditional production rollback. The current fix restores that already-tested cutover rather than introducing a second renderer.
- No historical result justifies another unmeasured production optimization before current CI/player replay.
- Existing Actions reruns cannot be repurposed safely as current-head proof for this assignment; no rerun was launched against another capture or retired branch.

## Constraints

- Do not introduce a second renderer or bypass `VoxelSurfaceScheduler` / `SurfaceGeometryArena`.
- Do not enable GPU extraction for step 4 or block HLOD until their dedicated parity contracts support it.
- Preserve CPU fallback for hardware without compute/async-counter support and for content the GPU path intentionally declines.
- Never edit `.github/test-request.json` on `fixes/agent-2`; targeted requests belong only on `ci-test/fixes/agent-2`.
- Until CI and replay evidence exist, keep this capture under `SceneIssues/open/` with `status: open`.
