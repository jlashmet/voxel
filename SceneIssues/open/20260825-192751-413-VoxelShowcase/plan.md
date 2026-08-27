# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The only capture is `screenshot-001.png`; its only marked circle covers the top-left FPS/surface telemetry at the saved `Showcase Camera` pose. Replay ties the complaint to moving-player rendering/streaming cost and visible chunk convergence. Acceptance remains the unchanged 420-frame production traversal plus 45 s saved-pose replay: p95 < 18 ms, p99 < 25 ms, max < 80 ms, zero frame-path blocking completions, streamed movement proven, visible solids every moving frame, and near/far gap <= 5 cm.

## Competing hypotheses / evidence
**Exact near-ring snapshot overhead — supported, now isolated.** Profiling attributed a sampled ~70.83/70.86 ms worker overrun to `Voxel.Surface.Snapshot`. The bounded step-1/2 metadata helper reduced observed snapshot p95 to ~1.76 ms. The earlier timing run was later found to include a non-production GPU-cutover enable, so it did not cleanly measure snapshot-only traversal performance. `d1b458b2...` restores `CpuTransvoxelChunkCache.cs` exactly to master while retaining only the separate near-ring scheduling helper; this isolated CPU candidate is the current discriminator.

**Admission tuning — rejected.** Completion-count and monotonic ramps either lost all visible geometry by frame 5 or still failed the percentile gate. Experiment 020's static 12→8 cap also lost every visible voxel draw on frame 5 in exact run `33123159073`; it is restored to 12.

**Draw/upload/GC and visibility caching — rejected/disfavored.** Upload/GC remain small in stage telemetry; stationary draw throughput is much faster. Bounded moving-visibility reuse regressed to p95 23.40 ms and was removed. A same-frame guard was rejected before CI because the traversal coroutine advances one rendered player frame per sample.

**GPU mesher — not part of this candidate.** Production master hard-disables GPU cutover. The feature now matches that gate exactly. Historical GPU parity/replay evidence remains architectural follow-up only; it is not used to claim this CPU fix.

## Regression / blast radius / cost
`VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` moves ~0.5 m per rendered frame for 420 frames across streamed regions and asserts visible solids, <=5 cm fallback coverage while near geometry is incomplete, zero blocking completions, streaming progress, p95 <18 ms, p99 <25 ms, max <80 ms, then a low-cost stationary tail.

Current production change is render/extraction scheduling only: exact metadata clear/map/compact jobs execute inline only when the padded metadata array is <=6000 entries (step 1/2); larger step-4/8 exact snapshots retain the asynchronous Burst path. No Storage, gameplay/collision authority, world generation, geometry/material/topology semantics, LOD layout, visibility, upload/discovery budgets, arena capacity, build concurrency, or acceptance threshold changes. Cost is a bounded main-thread loop over at most 5832 metadata entries instead of job scheduling/fan-out overhead.

- [x] Inspect the sole marked region and tie it to saved-pose/runtime telemetry.
- [x] Retain the moving traversal behavioral regression and unchanged performance/coverage gates.
- [x] Discriminate snapshot, draw/upload/GC, admission, visibility, and GPU alternatives.
- [x] Reject the 8-build convergence cap from exact behavioral failure.
- [x] Restore production GPU cutover state and isolate the near-ring snapshot change.
- [ ] Green exact-SHA targeted CI plus 45 s saved-pose replay.
- [ ] Commit `verification-final.png`, complete pending metadata, close, merge latest master, and non-force advance master.
