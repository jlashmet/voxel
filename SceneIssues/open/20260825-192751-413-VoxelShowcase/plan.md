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
- [x] Replay the assigned saved camera (`c7bc806567c007f3cbc0310942a8a799ad88627a`, run `32991641843`): marked view converges with no missing geometry and about 168 FPS average in the late window.
- [x] Run the production moving traversal (`c59ca85fc9e81b09577b5a6f6c3d143d42438446`, run `32993732467`): failure at traversal frame 5 with zero visible solid draws.
- [x] Reject a duplicate simple clipmap-entry fix: `SurfaceBrickDiscoveryTests.ClipmapMotionReadmitsAlreadyResidentSurfaceIntoFinerLod` already covers resident surface re-admission after clipmap motion.
- [x] Merge current master process/CI updates into the persistent feature branch; feature is current with master as of `025e88ef6e2d097143607c3018184ddc99cb747c`.
- [x] Run `ShowcaseTraversalPerformanceTests.ConvergedTerrainAndCastleRetainHighSteadyStateRenderThroughput`: request `e540607dacc49c840fe230878c5cab87172c1de6`, run `33006475923`, failed the convergence gate with known=6784 resident=710 dirty=1407 visible=708 missing=27 jobs=2 before throughput timing could begin.
- [ ] Run experiment 010's short movement diagnostic to classify the first zero-draw frame by known→in-band→frustum routing and step-4 readiness before changing production.
- [ ] Diagnose and fix the frame-5 moving coverage discontinuity with the smallest causal change and focused regression evidence.
- [ ] If steady rendering is healthy but player throughput remains far below requested headroom, target measured extraction/geometry footprint. Historical evidence identifies unimplemented Transvoxel vertex reuse as a likely throughput/arena lever; validate current code/oracles before implementing it.
- [ ] Re-run moving traversal, convergence, and saved-camera replay after any new production optimization; require stable near/far coverage and record actual player/steady timings.
- [ ] After verified production/test work and measured acceptance, perform only the worker-side terminal bookkeeping required by `SceneIssues/README.md`, push it, and wait for the coordinator. Do not integrate to master from this worker.

## Current validation state

The exact-ring cutover request `8bb029535005fbb9fde1365e39c7b41461ecc407` completed successfully in run `32991459621` on Unity 6000.5.6f1. The production change remains a small restoration of an existing validated GPU path, not a second renderer.

The saved-camera replay at `c7bc806567c007f3cbc0310942a8a799ad88627a` completed successfully in run `32991641843`. The original overlay was roughly `FPS 105 / MIN 105 / MAX 166`; the converged replay showed approximately `FPS 163 / MIN 131 / MAX 187`. From 60–89 seconds it held `visible=709`, `missingMax=0`, `drops=0`, and `reappeared=0`; the final 20 one-second FPS samples averaged about 168 FPS. This resolves the captured sub-100/missing-geometry symptom at the recorded pose after convergence, but does not prove the aspirational ~1000-FPS headroom.

The moving request `c59ca85fc9e81b09577b5a6f6c3d143d42438446` completed as a real `ci/single-test` failure. The PlayMode regression reached a fallback-safe visible state and then hit `VisibleSolidChunks == 0` on traversal frame 5. Because the test sets `m_FlyMode=true`, the direct transform advance is not overwritten by `MovePlayer`; this is a genuine fast-movement coverage failure.

The converged-throughput request `e540607dacc49c840fe230878c5cab87172c1de6` also completed as a real failure, but before render timing: after the convergence wait it still had 1,407 dirty chunks and two running jobs. This keeps background extraction/build admission in scope and prevents treating the issue as draw submission alone.

The traversal artifact's standalone player continued moving for about 150 seconds without losing every visible draw. It generally rendered around 150–330 FPS but developed temporary missing-visible backlogs and progressive shared-arena pressure. Vertex occupancy approached ~97% around 116 seconds and allocation failures accumulated later; occupancy also recovered under normal relief/retirement, so this is pressure/fragmentation evidence rather than proof of a lease leak.

## Current findings

- Original saved-pose coverage is fixed after convergence and FPS is materially higher than the capture, but full-player throughput is still below requested headroom.
- Moving correctness is not accepted: production traversal fails at frame 5 with zero visible solids.
- The convergence gate also fails because background dirty work remains substantial after the allowed wait.
- Clipmap motion already initiates re-admission discovery, outgoing edge retirement is current-window guarded, and the toroidal slot grid retains still-owned slots; a generic discovery requeue change is not justified.
- Production visibility is collected independently per ring; `SurfaceLodActiveCoverage` is not currently consumed by `VoxelSurfaceScheduler`. That makes LOD fallback/publication continuity a plausible mechanism, but it is not yet proven to be the frame-5 cause.
- Experiment 010 therefore measures the first bad frame's aggregate visibility funnel and step-4 readiness before any scheduler change.
- Historical full-Showcase performance improved dramatically when the geometry arena moved to `ComputeBufferMode.SubUpdates`; later SmallVoxelShowcase measurements reached roughly 920 FPS stationary and 770 FPS walking after LOD/visibility fixes.
- Historical profiling then isolated extraction throughput/vertex footprint, including missing Transvoxel vertex reuse, as a candidate only after current correctness/convergence is repaired.

## Constraints

- Work only on `SceneIssues/open/20260825-192751-413-VoxelShowcase` on `fixes/agent-2`.
- Do not introduce a second renderer or bypass `VoxelSurfaceScheduler` / `SurfaceGeometryArena`.
- Do not enable GPU extraction for step 4 or block HLOD without dedicated parity contracts.
- Preserve CPU fallback for unsupported hardware and intentionally declined content.
- Never edit `.github/test-request.json` on `fixes/agent-2`; targeted requests belong only on `ci-test/fixes/agent-2`.
- Preserve all prior persistent agent-2 work; no feature-branch force pushes.
- Keep this capture open until movement coverage, convergence/performance, and replay verification are accepted.
- When worker-side bookkeeping is ready, push it and wait for the coordinator; do not start another capture or integrate to master.
