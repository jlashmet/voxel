# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- Capture `screenshot-001.png` marks the performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70: sub-100 FPS while moving, slow fill, and transient/missing geometry.
- Pass requires sustained step-1/2 GPU completion, zero eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, plus exact built-player pose inspection.

## Runtime evidence / hypotheses
- H1 (**confirmed**): demand-scoped mirror recovery, exact footprints, optional empty halo, and obsolete-demand cancellation removed global recovery starvation while preserving Storage versions.
- H2 (**falsified as complete fix**): compact payload scatter removed fragmented `SetData` fan-out and now compiles on Metal, but exact run `33279094247` still failed traversal at frame 3 (`gpu=8/0`, waits 248) and the built player retained ~191–198 ms admission spikes with 344 missing chunks at 51.4 s. Liveness passed; arena exhaustion remained absent.
- H3 (**confirmed material fix**): snapshotless GPU admission, 128-block coverage cursors, concurrent demand recovery, and one new count dispatch/frame made liveness green and exact player coverage converge to zero missing without GPU fallback. Exact run `33281099872` still failed only moving p99: 75.912 ms versus 25 ms.
- H4 (**leading**): the remaining tail is secondary water discovery, not shared-mirror solid work. On the same exact player, solid admission stayed ~0.5–3 ms while one 32-brick water classification slice reached 29.256 ms. A reused GPU staging-buffer hazard remains the alternative if bounding water does not move p99.

## Selected integration
- Preserve compact 64-record payload upload/scatter and transient count/write retry behavior from `origin/fixes/agent-2`.
- Preserve the takeover branch's snapshotless production admission: GPU resolves/classifies raw mirror bricks; unsupported decorated/faceted/profile content explicitly re-enters the CPU fidelity path.
- Reference-count demand footprints, verify coverage in 128-block cursors, evict only inactive undemanded entries, and admit at most one new count dispatch per frame. Geometry remains GPU-resident through draw.
- CPU world truth, collision, replication, HLOD/water, stale-build rejection, arena budgets, and performance thresholds are unchanged.
- Bound discovery-only water classification against its existing presentation deadline, checking every four bricks while keeping authoritative mutation invalidation immediate.

## Remaining gates
- Integrated head compiles. Local policy 5/5, mirror slot/catalogue 21/21, and arena/no-geometry-readback 4/4 passed. The liveness harness entered PlayMode but exceeded the mandated 6 GB local ceiling before its first assertion, so it has no product verdict.
- Compile and rerun the unchanged migration gate; verify water admission tail, moving p99, and startup fill. Keep the capture open until exact-SHA targeted CI and built-player gates are green.
