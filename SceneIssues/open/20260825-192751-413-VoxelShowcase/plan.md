# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- Capture `screenshot-001.png` marks sub-100 FPS while moving, slow fill, and transient/missing geometry at the exact Showcase camera.
- Pass requires sustained step-1/2 GPU completion, zero eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and exact built-player pose inspection.

## Runtime evidence / hypotheses
- H1 (**confirmed material fix**): demand-scoped shared-mirror recovery, exact footprints, optional empty halo, obsolete-demand cancellation, snapshotless GPU admission, bounded coverage cursors, and concurrent recovery removed global recovery starvation. Exact liveness is green and the player converges to zero missing.
- H2 (**falsified as remaining tail**): compact mirror payload scatter and bounded renderer admission fixed fill/recovery but did not clear traversal. Run `33281099872` failed moving p99 at 75.912 ms.
- H3 (**secondary tail confirmed**): run `33282801017` failed moving p99 at 79.164 ms and recorded a player slice of `water=39.214 ms`, `solid=0.485 ms`. Between-brick deadlines cannot bound the old three-channel water-classification copy; use a borrowed-view material-only query. If that remains material, jobified classification is the alternative.
- H4 (**primary tail selected**): the exact traversal generated regions 199 -> 243 with up to 30 pending. Source inspection shows time-sliced `StepRegion` ends in an unbudgeted `RefreshRegionSummary` over 262,144 bricks. The alternative is renderer staging reuse, but exact renderer/admission and arena-upload peaks do not align with the 79 ms tail.

## Selected integration
- Preserve shared GPU mirror, compact scatter, snapshotless extraction, CPU fidelity fallback for unsupported semantics, stale-build rejection, and arena correctness.
- Rebuild whole-region occupancy summaries word-wise: scan the same authoritative bricks, assemble 64 bits locally, and write each occupied/fully-solid word once. Ordinary mutations keep the single-block updater.
- Classify discovered water bricks through a zero-copy material-presence query while preserving immediate mutation invalidation and pinned water-mesh snapshots.
- CPU world truth, collision, replication, HLOD, budgets, and thresholds are unchanged.

## Remaining gates
- Local policy 5/5, mirror slot/catalogue 21/21, arena/readback 4/4, material query 2/2, bounded-water contract 1/1, and full-region summary semantics/budget 1/1 pass; the latter rebuilt 262,144 bricks in 0.794 ms versus 25 ms. Full-scene PlayMode hit the mandated 6 GB watchdog before assertions and has no product verdict.
- Per developer direction, do not create another separate CI request; continue validation through local Unity/player workflows. Keep the issue open until full traversal and visual gates are green.
