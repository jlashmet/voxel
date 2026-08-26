# Plan — restore validated GPU surface extraction for the Showcase

## Problem

The capture reports sub-100 FPS traversal, slow terrain fill, and visible holes while chunks are meshed in `Assets/Scenes/VoxelShowcase.unity`, with a goal of leaving enough frame budget for game logic. Production contained the existing GPU Transvoxel implementation but globally hard-disabled its exact-ring cutover, forcing near terrain back through CPU extraction/upload.

The validated fix restores GPU extraction for exact steps 1 and 2 while retaining CPU/coarse paths for step 4 and block HLOD plus all hardware/content fallbacks. Current evidence shows the original saved pose now converges hole-free and materially faster, but movement and total player throughput still need work before closure.

## Approach

- [x] Trace the active Showcase surface path and identify the unconditional production rollback.
- [x] Add/confirm the focused exact-ring GPU cutover regression.
- [x] Restore the previously validated GPU cutover default while retaining `VOXEL_DISABLE_GPU_CUTOVER=1` and existing CPU fallbacks.
- [x] Review historical player/oracle evidence; preserve the existing density/topology/material/boundary/shared-arena parity basis.
- [x] Obtain authoritative green targeted CI for `SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings` (`8bb029535005fbb9fde1365e39c7b41461ecc407`, run `32991459621`).
- [x] Replay the assigned saved camera through the repository scene-issue replay path (`c7bc806567c007f3cbc0310942a8a799ad88627a`, run `32991641843`): marked view converges with no missing geometry and about 168 FPS average in the late window.
- [x] Run the production moving traversal request (`c59ca85fc9e81b09577b5a6f6c3d143d42438446`, run `32993732467`): it fails at traversal frame 5 with zero visible solid draws; the same artifact's ~150 s player traversal also shows late arena pressure/allocation failures while continuing to render.
- [x] Reject a duplicate simple clipmap-entry fix: current `SurfaceBrickDiscoveryTests.ClipmapMotionReadmitsAlreadyResidentSurfaceIntoFinerLod` already verifies resident surface re-admission after clipmap motion.
- [x] Merge current `master` process/CI updates (`11396a967cc232d2eccbc9c8ba1221f89a1a3a0b`) into the persistent feature branch without copying `.github/test-request.json`; merge commit `da8a846a34f717894f49153e7145aea6c416218d`.
- [ ] Run `ShowcaseTraversalPerformanceTests.ConvergedTerrainAndCastleRetainHighSteadyStateRenderThroughput` on the current master-synced lineage to separate converged render cost from background extraction/publication cost.
- [ ] Diagnose and fix the frame-5 moving coverage discontinuity with the smallest causal change; add focused regression evidence rather than guessing from the late arena state.
- [ ] If steady rendering is healthy but player throughput remains far below requested headroom, target measured extraction/geometry footprint. Historical evidence identifies unimplemented Transvoxel vertex reuse as a likely throughput/arena lever; validate current code/oracles before implementing it.
- [ ] Re-run moving traversal and saved-camera replay after any new production optimization; require stable near/far coverage and record actual player/steady timings.
- [ ] After verified production/test work and measured acceptance, update terminal `issue.json` fields and move the capture to `SceneIssues/closed/` in a separate bookkeeping commit.
- [ ] Recheck current `master`, integrate the verified terminal feature branch, and verify the closed capture plus relevant green CI on remote master.

## Current validation state

The exact-ring cutover request `8bb029535005fbb9fde1365e39c7b41461ecc407` completed successfully in run `32991459621` on Unity 6000.5.6f1. The production change remains a small restoration of an existing validated GPU path, not a second renderer.

The exact saved-camera replay at `c7bc806567c007f3cbc0310942a8a799ad88627a` completed successfully in run `32991641843`. The original overlay was roughly `FPS 105 / MIN 105 / MAX 166`; the converged replay showed approximately `FPS 163 / MIN 131 / MAX 187`. From 60–89 seconds it held `visible=709`, `missingMax=0`, `drops=0`, and `reappeared=0`; the final 20 one-second FPS samples averaged about 168 FPS. This resolves the captured sub-100/missing-geometry symptom at the recorded pose after convergence, but does not prove the aspirational ~1000-FPS headroom.

The moving request `c59ca85fc9e81b09577b5a6f6c3d143d42438446` is no longer queued: run `32993732467` completed as a real `ci/single-test` failure. The PlayMode regression reached a fallback-safe visible state and then hit `VisibleSolidChunks == 0` on traversal frame 5. Because the test sets `m_FlyMode=true`, the direct transform advance is not overwritten by `MovePlayer`; this is a genuine fast-movement coverage failure.

The same run's standalone player continued moving for about 150 seconds without losing every visible draw. It generally rendered around 150–330 FPS but developed temporary missing-visible backlogs and progressive shared-arena pressure: vertex occupancy approached ~97% around 116 seconds, allocation failures started around 112 seconds, and accumulated to roughly 1.3K by the end. Occupancy later moved down under existing relief/retirement, so this is pressure/fragmentation evidence, not proof of an unreleased-lease leak. Historical repository experiments already rejected simply enlarging the arena, aggressive eviction, and power-of-two lease classes; they identified per-chunk extraction/vertex footprint, including missing Transvoxel vertex reuse, as the stronger remaining throughput mechanism.

## Current findings

- Original saved-pose coverage is fixed after convergence and FPS is materially higher than the capture, but full-player throughput is still far below the requested headroom.
- Moving correctness is not yet accepted: the current production traversal fails at frame 5 with zero visible solids.
- Long-run movement also reaches arena pressure, but that occurs much later and should not be conflated with the immediate frame-5 discontinuity.
- A current regression already covers clipmap motion re-admitting resident surface into finer LOD, so a generic entering-edge requeue change would duplicate solved behavior.
- Historical full-Showcase performance improved dramatically when the geometry arena moved to `ComputeBufferMode.SubUpdates`; later SmallVoxelShowcase measurements reached roughly 920 FPS stationary and 770 FPS walking after LOD/visibility fixes.
- Historical profiling then isolated extraction throughput/vertex footprint: roughly 15.4K vertices per chunk versus a 12.5K budget assumption, with Transvoxel reuse metadata present but shared vertices still regenerated. This is a candidate only if the current steady-render test confirms raster submission is already healthy.

## Constraints

- Do not introduce a second renderer or bypass `VoxelSurfaceScheduler` / `SurfaceGeometryArena`.
- Do not enable GPU extraction for step 4 or block HLOD without their dedicated parity contracts.
- Preserve CPU fallback for unsupported hardware and intentionally declined content.
- Never edit `.github/test-request.json` on `fixes/agent-2`; targeted requests belong only on `ci-test/fixes/agent-2`.
- Preserve all prior persistent agent-2 work; no feature-branch force pushes.
- Until moving traversal, steady-throughput measurement, any required causal optimization, and replay verification are green, keep this capture under `SceneIssues/open/` with `status: open`.