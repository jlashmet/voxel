# Plan — restore validated GPU surface extraction for the Showcase

## Problem

The capture reports sub-100 FPS traversal, slow terrain fill, and visible holes while chunks are meshed in `Assets/Scenes/VoxelShowcase.unity`, with a goal of leaving enough frame budget for game logic. Current production code contained the existing GPU Transvoxel implementation but `CpuTransvoxelChunkCache.GpuCutoverDisabled` hard-disabled it for every ring, forcing exact terrain back through CPU extraction and CPU-to-GPU geometry upload.

Historical player profiling already showed that settled drawing is not the dominant cost: the renderer can submit the full solid set cheaply, while active CPU surface builds consume the frame and delay visible coverage. The GPU extractor has CPU-oracle parity coverage, writes directly into the shared surface arena, uses asynchronous counter readback, and supports exact steps 1 and 2; step 4 and block HLOD intentionally remain CPU/coarse paths.

## Approach

- [x] Trace the active Showcase surface path and identify the unconditional production rollback.
- [x] Add/confirm a focused policy regression for exact-ring GPU cutover.
- [x] Restore the previously validated GPU cutover default while retaining `VOXEL_DISABLE_GPU_CUTOVER=1` and existing CPU fallbacks.
- [x] Confirm branch freshness: current `master` is an ancestor of `fixes/agent-2`; feature is 21 commits ahead and 0 behind.
- [ ] Obtain a real green `ci/single-test` result for `SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings`.
- [ ] Run additional GPU density/topology oracle and arena-bridge coverage if the focused run or current branch changes require it.
- [ ] Replay/benchmark the assigned VoxelShowcase pose and verify missing geometry converges correctly; record measured performance rather than claiming the aspirational 1000-FPS target without evidence.
- [ ] After verified production/test work, update terminal `issue.json` fields and move the capture to `SceneIssues/closed/` in a separate bookkeeping commit.
- [ ] Integrate the verified terminal branch into current `master` and verify the closed capture plus green targeted CI there.

## Current blocker

The connected GitHub write paths do not emit the repository's push-triggered `Tests (single)` workflow. Contents-API request commits `1ad552ec0d42187eb660525848b8815bc3aa7297` and `6c0317b29b13f165235c936f31565e113f0eab25` produced no run. A separate low-level Git commit/ref-update retry, request commit `9ce7b8049f65108bf2134679f35f26a98f1cc161` based on source `793dfffed3cb890b8d1eb69b3152f46c729f6f36`, also has no `ci/single-test` status and zero Actions runs for its exact head SHA. Older human-authored pushes on this same CI branch did trigger `.github/workflows/tests-single.yml`, so closure is blocked on an accepted Actions trigger rather than a test failure. The connected GitHub capability exposes no workflow-dispatch action.

## Constraints

- Do not introduce a second renderer or bypass `VoxelSurfaceScheduler` / `SurfaceGeometryArena`.
- Do not enable GPU extraction for step 4 or block HLOD until their dedicated parity contracts support it.
- Preserve CPU fallback for hardware without compute/async-counter support and for content the GPU path intentionally declines.
- Never edit `.github/test-request.json` on `fixes/agent-2`; targeted requests belong only on `ci-test/fixes/agent-2`.
- Until CI and replay evidence exist, keep this capture under `SceneIssues/open/` with `status: open`.
