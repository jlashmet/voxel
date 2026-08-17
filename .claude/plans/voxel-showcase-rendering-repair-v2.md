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
- [x] Validate `FarTerrainTopologyReuseTests.RebuildAfterCameraSnap_ReusesExistingIndexTopology` in Unity on the current branch sequence (passed in PR #88 run 32006271470).
- [x] Fix the measured step-8 feature-preserving HLOD overflow by scaling fixed output capacity with the square of the 2-voxel HLOD linear-resolution increase; continue refusing partial coarse geometry.
- [x] Add a regression tying production HLOD output capacity to the feature-preserving subcell resolution so future fidelity changes cannot retain a stale smaller budget.
- [x] Validate the HLOD capacity-resolution regression in EditMode; the current PR run contains no `Feature-preserving HLOD output overflow` diagnostic.
- [x] Prevent visibility from re-enqueuing the exact authoritative generation already building/awaiting publication; current-head PR run 32014802229 passes the `VisibleCurrentGenerationBuildDoesNotQueueDuplicateAdmission` regression.
- [x] Promote real frustum-visible missing/stale chunks ahead of the 360-degree in-band prefetch FIFO while preserving background streaming and the unchanged global build budget.
- [ ] Validate `SurfaceRingBuildAdmissionTests.FrustumVisibleDemandBypassesBackgroundPrefetchBacklog` in Unity and remeasure production coarse coverage/convergence.
- [ ] Fix the remaining coarse-coverage defect: current-head PR run 32014802229 still observes 44 visible coarse chunks without ready geometry. The measured scheduler defect is in-band prefetch starvation: 4,225 valid dirty records remain after 10 s while bounded 64-record FIFO sampling leaves visible demand at queue p95 4,578.88 ms.

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
- [x] Reconcile the multi-view LOD fixture with baked startup: require the restored castle and forbid Play-mode castle authoring without changing any LOD fidelity threshold.
- [x] Use PR #88 run 32006271470 to measure the revised shards: A-F peaked at 6,761 MB, G-J 3,421 MB, Kentridge 4,220 MB and N-S 4,584 MB; L-M still hit 14,361 MB and T-Z-rest still hit 14,358 MB.
- [x] Split the still-failing L-M and T-Z-rest buckets into fresh Unity processes for `LateJoinTests`, `LodRenderingTests`, `LodVisualFidelityTests`, `MemoryStabilityTests`, `TerrainLookdevScreenshotTests`, `TraversalStreamingTests`, and U-Z; mirror the layout in PR and master workflows.
- [ ] Confirm the new per-fixture PlayMode shard layout no longer hits the Unity RSS watchdog on the current head. PR run 32014802229 proves crash/session isolation works and later shards continue, but the already-single-method `MemoryStaysWithinTierBudgetOverTwoHours` and `ContinuousTraversalOverKilometresShowsNoGaps` shards still hit 14,357 MB and 14,381 MB respectively against the unchanged 14,336 MB ceiling; further YAML splitting cannot solve those two tests.
- [x] Classify current-head PR run 32014802229 failures without masking them: rendering-repair blockers are coarse/scheduler convergence (`CastleExteriorLookdevTests`, `ShowcasePerformanceTests`, `LodRenderingTests`, `TerrainLookdevScreenshotTests` and the async-geometry published-baseline fixtures); `LodVisualFidelityTests` is currently blocked by its `RenderRequest` destination setup rather than a fidelity threshold; `ShowcaseNoStutterTests` is a validation-path failure because its measured window never observes production build work. Baseline/non-repair failures reproduced independently include `DistantAlterationTests` length validation, `FallingVoxelPhysicsTests` debris lifetime, far-terrain relief/ground-ahead assertions, plus the two synthetic residency-memory watchdog tests above.

### G. Current-head acceptance validation

- [x] Run/confirm `FarTerrainTopologyReuseTests.RebuildAfterCameraSnap_ReusesExistingIndexTopology`.
- [ ] Run/confirm `ShowcaseNoStutterTests.BakedStartup_NeverBuildsCastleDuringPlayAndNeverStallsRendering` (current run fails before frame percentiles because it never observes a converged production solid-build window).
- [ ] Run/confirm `ShowcasePerformanceTests.FullShowcaseConvergesWithinTenSecondsWithoutLaterStalls`.
- [ ] Run/confirm the production LOD rendering/fidelity suite, including LOD 1/2/4/8 image-space fidelity.
- [x] Confirm production step-8 HLOD no longer reports output overflow.
- [ ] Confirm production step-8/coarse rendering publishes complete visible coverage with no coarse holes.
- [x] Record the current failed convergence measurement: at 10.00 s the renderer had 128/5,672 resident chunks, 5,483 dirty chunks, 14 running jobs, 1,591 missing visible chunks, queue p95 4,356.37 ms and build p95 890.13 ms. This is evidence for the next scheduler/coverage repair, not an accepted performance result.
- [x] Record the post-lifecycle-dedup measurement from PR run 32014802229: at 10.00 s the renderer had 131/5,672 resident chunks, 4,225 dirty chunks, 16 running jobs, 1,567 missing visible chunks, queue p95 4,578.88 ms and build p95 1,036.66 ms. The duplicate-generation fix reduces stale dirty work but does not solve ring-demand starvation.
- [ ] Record passing frame/render/upload values from the current head against the acceptance limits above.
- [ ] If a relevant gate still fails, identify the measured bottleneck/fidelity defect and fix that next; otherwise stop optimizing.
- [ ] Final affected PR validation is complete with every rendering-repair failure resolved or explicitly classified.

## Current branch continuation

Current continuation work after PR #86:

- `09949f33` — far-terrain ring topology reuse.
- `d50f5e99` — tighter PR PlayMode memory sharding.
- `63813b9c` — mirrored master PlayMode memory sharding.
- `eb880d82` — isolated far-terrain topology-reuse regression.
- `5facd3d6` — multi-view LOD gate aligned with baked-startup contract; fidelity thresholds unchanged.
- `5bd97c37` — production step-8 HLOD output capacity scaled for the 2-voxel feature-preserving representation.
- `5301daf9` — regression tying HLOD output capacity to feature resolution.
- `70e49bf8` — PR workflow isolates each remaining scene-heavy L/M/T fixture after measured RSS kills.
- `b0576ad4` — master workflow mirrors the same per-fixture PlayMode isolation.
- `def488ae` — Unity launcher owns/reaps a process session so a natural Burst/LLVM crash cannot poison later fresh-process shards.
- `41528f01` — production `VoxelRenderPass` exposes an internal diagnostics-only active-pass handle for fidelity tests; no renderer ownership or thresholds changed.
- `cad65015` — visible demand no longer recreates a duplicate dirty record for the same authoritative generation already in flight.

Current-head PR #88 run 32014802229 (`2621fd00`) has green Architecture, EditMode (650/650), bake, CombatPrototype, CI PlayMode, Features and Parity. The isolated PlayMode sequence continued after individual failures, proving the crash-session cleanup, but two single-method synthetic residency tests still exceed the unchanged RSS watchdog. Renderer acceptance remains open: coarse lookdev reports 44 holes, 10-second convergence is 131/5,672 resident with 4,225 dirty and 1,567 missing visible chunks, LOD step 4 does not stabilize, and fidelity capture is blocked by a `RenderRequest` destination setup error. Discovery is already passive and off-band work is already parked; the next measured repair promotes true frustum-visible demand ahead of the valid 360-degree in-band prefetch FIFO that produced 4.58 s queue p95.

PR #88 is a draft validation vehicle only. Do not merge it merely to obtain a green check; use its Unity results to complete section G and drive the next measured repair.
