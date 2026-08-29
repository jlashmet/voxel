# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- The single capture marks top-left performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70. Note: sub-100 FPS while moving, slow fill, transient/missing geometry.
- Pass requires sustained step-1/2 GPU completion, zero eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and inspected exact-pose built-player evidence.

## Runtime evidence / hypotheses
- H1 (**confirmed**): demand-scoped mirror recovery, exact footprints, optional empty halo, and obsolete-demand cancellation removed global recovery starvation while preserving Storage versions.
- H2 (**falsified as complete fix**): compact payload scatter removed fragmented `SetData` fan-out and now compiles on Metal, but exact run `33279094247` still failed traversal at frame 3 (`gpu=8/0`, waits 248) and the built player retained ~191–198 ms admission spikes with 344 missing chunks at 51.4 s. Liveness passed; arena exhaustion remained absent.
- H3 (**next discriminator**): per-chunk CPU exact snapshots, synchronous 1,000–5,832-block coverage walks, and bursty count dispatches create the remaining tail. Preserved local evidence reduced moving p95 from 97.624 to 42.250 ms with 128-block coverage cursors; one count dispatch/frame was not yet validated after the payload fix.

## Selected integration
- Preserve compact 64-record payload upload/scatter and transient count/write retry behavior from `origin/fixes/agent-2`.
- Preserve the takeover branch's snapshotless production admission: GPU resolves/classifies raw mirror bricks; unsupported decorated/faceted/profile content explicitly re-enters the CPU fidelity path.
- Reference-count demand footprints, verify coverage in 128-block cursors, evict only inactive undemanded entries, and admit at most one new count dispatch per frame. Geometry remains GPU-resident through draw.
- CPU world truth, collision, replication, HLOD/water, stale-build rejection, arena budgets, and performance thresholds are unchanged.

## Remaining gates
- Compile/static review, then liveness, arena/no-readback, zero-allocation, and production traversal regressions on the integrated head.
- Inspect exact built-player screenshots/logs. Keep the capture open until exact-SHA targeted CI and built-player gates are green.
