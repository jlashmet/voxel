# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- Capture `screenshot-001.png` marks the performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70: sub-100 FPS while moving, slow fill, and transient/missing geometry.
- Pass requires sustained step-1/2 GPU completion, zero eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, plus exact built-player pose inspection.

## Runtime evidence / hypotheses
- H1 (**confirmed**): demand-scoped mirror recovery, exact footprints, optional empty halo, and obsolete-demand cancellation removed global recovery starvation while preserving Storage versions.
- H2 (**falsified as complete fix**): compact payload scatter removed fragmented `SetData` fan-out and now compiles on Metal, but exact run `33279094247` still failed traversal at frame 3 (`gpu=8/0`, waits 248) and the built player retained ~191–198 ms admission spikes with 344 missing chunks at 51.4 s. Liveness passed; arena exhaustion remained absent.
- H3 (**confirmed material fix**): snapshotless GPU admission, 128-block coverage cursors, concurrent demand recovery, and one new count dispatch/frame made liveness green and exact player coverage converge to zero missing without GPU fallback. Exact run `33281099872` still failed only moving p99: 75.912 ms versus 25 ms.
- H4 (**confirmed owner, first fix falsified**): the remaining tail is secondary water work, not shared-mirror solid work. Run `33282801017` kept liveness green but failed moving p99 at 79.164 ms; exact player telemetry recorded `water=39.214 ms`, `solid=0.485 ms`, with the arena-upload peak on another frame. Deadline checks between bricks cannot bound one synchronous three-channel payload copy/scan.
- H5 (**selected**): query the borrowed region's material bytes directly for water ids, avoiding material/semantic/boundary copies. Alternative is bounded jobified classification if the zero-copy query does not remove the tail.

## Selected integration
- Preserve compact 64-record payload upload/scatter and transient count/write retry behavior from `origin/fixes/agent-2`.
- Preserve the takeover branch's snapshotless production admission: GPU resolves/classifies raw mirror bricks; unsupported decorated/faceted/profile content explicitly re-enters the CPU fidelity path.
- Reference-count demand footprints, verify coverage in 128-block cursors, evict only inactive undemanded entries, and admit at most one new count dispatch per frame. Geometry remains GPU-resident through draw.
- CPU world truth, collision, replication, HLOD/water, stale-build rejection, arena budgets, and performance thresholds are unchanged.
- Keep discovery-only water classification on its existing presentation deadline and replace its full render-payload copy with a zero-copy material-presence query. Authoritative mutation invalidation remains immediate.

## Remaining gates
- Local policy 5/5, mirror slot/catalogue 21/21, arena/no-geometry-readback 4/4, and post-master water deadline contract 1/1 passed. Local full-scene PlayMode exceeded the mandated 6 GB ceiling before assertions, so it has no product verdict.
- Validate the material query and bounded-water contract locally. Another unchanged exact migration request requires explicit transport authorization; keep the capture open until exact-SHA targeted CI and built-player gates are green.
