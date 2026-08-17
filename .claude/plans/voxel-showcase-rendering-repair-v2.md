# Voxel Showcase Rendering Repair v2

This is the persistent task checklist for the rendering-repair work that started with the two player-visible regressions documented in PR #83 (castle-build frame collapse and poor coarse-LOD fidelity) and continued through the baked-startup work in PR #86.

The original working plan lived in the development conversation rather than a repository markdown file. This file records that plan in the repository so completion state is explicit from this point forward. Do not replace or repurpose the unrelated `caravan-path-fix.md` plan.

## Acceptance rules

- Regression gates come before relaxing or replacing behavior. A failing gate drives the implementation; thresholds are not weakened just to make CI green.
- Player-frame rendering must not synchronously complete worker geometry jobs.
- The baked showcase castle must exist before gameplay starts; Play mode must not fall back to procedural castle authoring.
- Startup/no-stutter gate (`ShowcaseNoStutterTests`):
  - p95 player frame < 18 ms
  - p99 player frame < 25 ms
  - every measured frame < 33.34 ms
  - `FramePathBlockingCompletionViolations == 0`
  - visible solid rendering converges with no missing visible chunks
- Stable rendering gate (`ShowcasePerformanceTests`):
  - convergence within 10 s
  - stable render p95 < 33 ms
  - maximum stable rendering hitch < 100 ms
  - last solid upload < 25 ms
  - no missing visible solid chunks after convergence
- LOD fidelity gate (`LodVisualFidelityTests`): production source steps 1/2/4/8 must be observed at the castle centre; coarse levels must retain architectural edges, regional structure and material distribution against the step-1 reference. Current minimums are edge F1 0.52, edge-density retention 0.50, weakest-region retention 0.35 and colour-histogram overlap 0.82.
- Optimize measured hotspots only. Do not stack speculative presentation changes after the gates pass.

## Plan / task list

### A. Establish regression gates first

- [x] Add a player-loop regression that measures the actual castle/startup rendering window rather than only post-convergence performance.
- [x] Add production LOD 1/2/4/8 fidelity coverage with multiple viewpoints and image-space comparisons.
- [x] Keep diagnostics for synchronous frame-path completion, dirty/running/upload state and missing visible solid chunks.

### B. Remove castle-build frame collapse

- [x] Move expensive procedural castle authoring off the live main-thread voxel store into isolated worker-owned storage.
- [x] Publish castle mutations back to the live world in bounded slices rather than one unbounded frame burst.
- [x] Time-budget publication so a fixed block count cannot consume an unbounded player frame.
- [x] Keep production rendering live while publication/streaming work occurs.
- [x] Own/cancel the background worker lifecycle safely at scene/world teardown.

### C. Keep geometry publication asynchronous

- [x] Move surface/mesh publication off the blocking player-frame path.
- [x] Reject synchronous worker-job completion from the frame path with runtime diagnostics/tests.
- [x] Keep surface rendering work budgeted while the world changes.

### D. Repair far-terrain frame pacing without sacrificing fidelity

- [x] Move far-terrain height sampling off the player frame.
- [x] Make far-terrain height work single-flight and publish at most one completed ring per frame while stale rings remain drawable.
- [x] Reuse persistent height caches for structure/hole presentation refreshes instead of resampling terrain.
- [x] Reuse invariant ring index topology across camera-origin and structure-only refreshes; rebuild indices only when hole topology changes.
- [x] Add an isolated regression proving a snapped ring origin updates vertices without rebuilding the index topology.
- [ ] Validate the isolated far-terrain topology-reuse regression in Unity on the current head.

### E. Eliminate runtime castle startup work

- [x] Add a versioned semantic showcase-world snapshot format through Storage.Api boundaries.
- [x] Bake the finished castle/startup neighborhood offline.
- [x] Restore the baked world before the first gameplay frame.
- [x] Fail explicitly for missing/stale bake data instead of silently falling back to runtime castle generation.
- [x] Keep normal streaming active beyond the baked startup neighborhood.
- [x] Bake the startup artifact in PR and master CI before `VoxelEngine.Tests.PlayMode` runs.

### F. Make validation runnable and trustworthy

- [x] Split the large `VoxelEngine.Tests.PlayMode` assembly into fresh Unity processes to reset retained native scene/rendering allocator state.
- [x] Further isolate scene-heavy Kentridge and LOD/memory ranges after the prior G-M shard reached 14,356 MB against the 14,336 MB watchdog ceiling.
- [ ] Confirm the revised PlayMode shard layout no longer hits the Unity RSS watchdog on the current head.
- [ ] Classify any remaining CI failures as rendering-repair regressions vs unrelated baseline failures; do not mask either category.

### G. Current-head acceptance validation

- [ ] Run/confirm `FarTerrainTopologyReuseTests.RebuildAfterCameraSnap_ReusesExistingIndexTopology`.
- [ ] Run/confirm `ShowcaseNoStutterTests.BakedStartup_NeverBuildsCastleDuringPlayAndNeverStallsRendering`.
- [ ] Run/confirm `ShowcasePerformanceTests.FullShowcaseConvergesWithinTenSecondsWithoutLaterStalls`.
- [ ] Run/confirm the production LOD rendering/fidelity suite, including LOD 1/2/4/8 image-space fidelity.
- [ ] Record the measured frame/render/upload values from the current head against the acceptance limits above.
- [ ] If a relevant gate still fails, identify the measured bottleneck/fidelity defect and fix that next; otherwise stop optimizing.
- [ ] Final affected PR validation is complete with every rendering-repair failure resolved or explicitly classified.

## Current branch continuation

Current continuation work after PR #86:

- `09949f33` — far-terrain ring topology reuse.
- `d50f5e99` — tighter PR PlayMode memory sharding.
- `63813b9c` — mirrored master PlayMode memory sharding.
- `eb880d82` — isolated far-terrain topology-reuse regression.

PR #88 is a draft validation vehicle only. Do not merge it merely to obtain a green check; use its Unity results to complete section G and drive the next measured repair.
