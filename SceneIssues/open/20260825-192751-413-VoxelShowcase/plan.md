# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The only capture is `screenshot-001.png`; its only marked circle covers the top-left FPS/surface telemetry at the saved `Showcase Camera` pose. Replay ties the complaint to moving-player rendering/streaming cost and visible chunk convergence. Acceptance is the unchanged production traversal regression plus 45 s saved-pose replay: p95 < 18 ms, p99 < 25 ms, zero frame-path blocking completions, streamed-region movement proven, visible solids every moving frame, and near/far gap <= 5 cm.

## Competing hypotheses / evidence
**Exact near-ring snapshot overhead — supported, retained, insufficient alone.** Profiling attributed a sampled ~70.83/70.86 ms worker overrun to `Voxel.Surface.Snapshot`. Inlining the existing clear/map/compact metadata bodies only for bounded step-1/2 exact grids reduced snapshot p95 to ~1.76 ms, but exact traversal still failed at ~20.16 ms p95 / ~28.33 ms p99.

**Draw/upload/GC and visibility-cache variants — disfavored/rejected.** Upload/GC remain small in stage telemetry and stationary draw throughput is much faster. Historical inline-frustum and cadence-throttled visibility experiments did not help. The bounded moving-visibility exact run preserved coverage but regressed to p95 23.40 ms; it is removed. A provisional same-frame guard was rejected before CI because coroutine `yield return null` advances the measured traversal across player frames, so its causal premise did not match the timed path.

**Admission shape — ramps rejected; static concurrency is an amplifier.** Exact completion-count ramping lost every visible draw by frame 5. Monotonic ramping preserved coverage but still failed at p95 20.73 / p99 25.10 and worsened replay FPS. Separately, a 12→8 diagnostic reduced worker/snapshot pressure by ~27%, but the older cap-only configuration was red. The later snapshot fix removed another ~2.7 ms from the same worker path, so the untried combination of bounded near-ring snapshot work plus a static convergence cap of 8 is the selected final discriminator.

**Existing GPU mesher — validated historically, not blindly re-enabled.** The retained step-1/2 GPU extractor previously passed graphics-enabled CPU/GPU density/material/normal/ownership parity and a production-player replay with zero missing sections. Production was later hard-disabled by a dedicated rollback commit without a checked-in failing GPU reproduction. Re-enabling that hard gate would broaden this change beyond the measured current CPU path; keep it as the next architectural direction only if the bounded CPU candidate is falsified.

## Regression / blast radius / cost
`VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` moves ~0.5 m per rendered frame for 420 frames across streamed regions and asserts visible solids, <=5 cm fallback coverage while near geometry is incomplete, zero blocking completions, streaming progress, p95 <18 ms, p99 <25 ms, max <80 ms, then a low-cost stationary tail.

Final candidate is render/extraction scheduling only: retain bounded step-1/2 exact metadata inlining; restore `VoxelRenderPass.cs` and `VoxelSurfaceScheduler.cs` to current `origin/master`; change only `SurfaceMaxConcurrentBuildsConverging` 12→8. Converged background ceiling stays 1. No Storage, gameplay/collision authority, world generation, geometry/material/topology semantics, LOD layout, upload/discovery budgets, arena capacity, or acceptance threshold changes. Cost is up to one-third less cold-view CPU extraction parallelism; the traversal regression directly catches any resulting coverage/load regression.

- [x] Inspect the sole marked region and tie it to saved-pose/runtime telemetry.
- [x] Retain the moving traversal behavioral regression and unchanged performance/coverage gates.
- [x] Discriminate snapshot, draw/upload/GC, admission, visibility, and GPU alternatives.
- [x] Retain the measured near-ring snapshot fix; remove falsified visibility experiments.
- [x] Apply the untried snapshot + static-8 convergence combination.
- [ ] Green exact-SHA targeted CI plus 45 s saved-pose replay.
- [ ] Commit `verification-final.png`, complete pending metadata, close, merge latest master, and non-force advance master.
