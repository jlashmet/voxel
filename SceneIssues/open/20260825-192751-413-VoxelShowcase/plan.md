# Plan — restore validated GPU surface extraction for the Showcase

## Problem

The capture reports sub-100 FPS traversal, slow terrain fill, and visible holes while chunks are meshed in `Assets/Scenes/VoxelShowcase.unity`, with a goal of leaving enough frame budget for game logic. Current production code contained the existing GPU Transvoxel implementation but `CpuTransvoxelChunkCache.GpuCutoverDisabled` hard-disabled it for every ring, forcing exact terrain back through CPU extraction and CPU-to-GPU geometry upload.

Historical player profiling already showed that settled drawing is not the dominant cost: the renderer can submit the full solid set cheaply, while active CPU surface builds consume the frame and delay visible coverage. The GPU extractor has CPU-oracle parity coverage, writes directly into the shared surface arena, uses asynchronous counter readback, and supports exact steps 1 and 2; step 4 and block HLOD intentionally remain CPU/coarse paths.

## Approach

- [x] Trace the active Showcase surface path and identify the unconditional production rollback.
- [x] Add/confirm a focused policy regression for exact-ring GPU cutover.
- [x] Restore the previously validated GPU cutover default while retaining `VOXEL_DISABLE_GPU_CUTOVER=1` and existing CPU fallbacks.
- [x] Review historical player/oracle evidence to determine whether another source change is justified before current measurements. It is not; current Unity/player evidence is the next required decision point.
- [x] Audit available Actions reruns for a sanctioned current-head validation path.
- [x] Obtain a current authoritative green `ci/single-test` result for `SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings` (`8bb029535005fbb9fde1365e39c7b41461ecc407`, run `32991459621`).
- [x] Keep the existing GPU density/topology/material/boundary-ownership and arena-bridge coverage as the parity basis; no new extractor implementation was introduced, so no additional oracle source change is currently required.
- [ ] Complete the already-admitted `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` request (`c59ca85fc9e81b09577b5a6f6c3d143d42438446`, run `32993732467`) so the production VoxelShowcase is exercised while moving across multiple region boundaries.
- [x] Replay the assigned saved camera through `tools/showcase-player-capture.sh --scene-issue`, inspect the resulting screenshots/logs, and verify the marked view converges without missing geometry (`c7bc806567c007f3cbc0310942a8a799ad88627a`, run `32991641843`).
- [ ] Run `ShowcaseTraversalPerformanceTests.ConvergedTerrainAndCastleRetainHighSteadyStateRenderThroughput` on the same production lineage to separate converged raster cost from whole-player/background-convergence cost.
- [ ] If current player/render evidence still misses the requested performance headroom, profile that measured state and make the next smallest causal optimization rather than guessing from historical bottlenecks.
- [ ] After verified production/test work and measured acceptance, update terminal `issue.json` fields and move the capture to `SceneIssues/closed/` in a separate bookkeeping commit.
- [ ] Integrate the verified terminal branch into current `master` and verify the closed capture plus relevant green targeted CI there.

## Current validation state

The authoritative exact-ring cutover request `8bb029535005fbb9fde1365e39c7b41461ecc407` completed successfully in `Tests (single)` run `32991459621`, executing exactly `VoxelEngine.Tests.EditMode.GpuLod2CutoverPolicyTests.SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings` on Unity 6000.5.6f1. The production fix remains the small restoration of the previously validated exact-ring GPU default rather than a new renderer.

The exact saved-camera replay at `c7bc806567c007f3cbc0310942a8a799ad88627a` completed successfully in run `32991641843`. It freshly baked the Showcase, built the standalone player, verified the frozen recorded pose, ran uncapped for 90 seconds, and captured eight screenshots. The original marked overlay was `FPS 105 / MIN 105 / MAX 166`; the converged replay showed approximately `FPS 163 / MIN 131 / MAX 187`. From 60–89 seconds the replay held `visible=709`, `missingMax=0`, `drops=0`, and `reappeared=0`. The final 20 FPS samples ranged about 154–184 FPS and averaged about 168 FPS. This resolves the captured sub-100/missing-geometry symptom at that pose, but it does not satisfy or prove the capture's requested ~1000-FPS headroom.

Replay telemetry also showed ring residency and arena population continuing to increase through the 90-second run after visible coverage had stabilized. Typical scheduler/admission maxima in the late window were around 0.6–1.1 ms, so the remaining whole-player frame time cannot yet be assigned solely to meshing or solely to raster submission. The existing converged steady-throughput regression is therefore required before another optimization is chosen.

A current production moving request was issued from feature production state `b53dc0255767d15a78f24efdceb1b2a339c60410`: CI commit `c59ca85fc9e81b09577b5a6f6c3d143d42438446` requests `VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap`, and GitHub admitted it as run `32993732467`. It remains queued. After the previous self-hosted workflow released the machine, repeated repository-wide checks showed six queued self-hosted workflows and zero active workflows. This is a runner-availability blocker, not a failed traversal and not a missing Actions event. Do not reissue or mutate the queued request while it is valid.

`master` was rechecked at `bfccb29f34f2373ae7cafac5a38e21a7c2e9ba86` before this evidence update and remained an ancestor of `fixes/agent-2`. The feature branch has only advanced with SceneIssue evidence documentation since the tested production state; no new production or test implementation has been added after the green cutover/replay evidence.

## Current findings

- The original marked view now converges with no missing visible surfaces and materially better FPS than the capture, but current full-player throughput is roughly ~167 FPS rather than ~1000 FPS.
- The arena SubUpdates fix previously moved the full Showcase from 10–17 FPS with 200–670 permanently missing visible chunks to converged ~5–6 ms frames with missing-visible reaching zero.
- Later SmallVoxelShowcase player measurements reached roughly 920 FPS stationary and 770 FPS walking after LOD/visibility corrections, so a ~1000-FPS result is plausible only as a measured target, not something source inspection can honestly certify.
- The current solid renderer already batches hundreds of visible chunks into a bounded set of procedural instanced submissions and binds shared material/arena state once per pass; do not assume draw-call count is the remaining bottleneck without the steady render test.
- The GPU Transvoxel path was validated through density/topology/material/boundary-ownership and shared-arena tests before the later unconditional production rollback. The current fix restores that already-tested cutover rather than introducing a second renderer.
- Actions request delivery and self-hosted runner admission can both be delayed. A valid queued run is not a failure and must not be replaced merely to reset its timestamp.

## Constraints

- Do not introduce a second renderer or bypass `VoxelSurfaceScheduler` / `SurfaceGeometryArena`.
- Do not enable GPU extraction for step 4 or block HLOD until their dedicated parity contracts support it.
- Preserve CPU fallback for hardware without compute/async-counter support and for content the GPU path intentionally declines.
- Never edit `.github/test-request.json` on `fixes/agent-2`; targeted requests belong only on `ci-test/fixes/agent-2`.
- Until moving traversal, steady-throughput measurement, and any required follow-up optimization are verified, keep this capture under `SceneIssues/open/` with `status: open`.
