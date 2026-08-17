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
- [x] Validate `SurfaceRingBuildAdmissionTests.FrustumVisibleDemandBypassesBackgroundPrefetchBacklog` in Unity; PR run 32019198712 passes the focused regression.
- [x] Remeasure production coarse coverage/convergence with the visible-demand priority path active. PR run 32019741845 (`63e52315`) still has 48 silhouette holes and at 10.00 s has 126/5,672 resident, 4,257 dirty, 1,567 missing visible, queue p95 4,300.35 ms and build p95 911.84 ms; priority promotion alone does not solve coverage.
- [x] Identify the next measured scheduler hotspot: run 32019741845 records `BuildSelectionTiming.p95 = 0.52 ms` while the entire renderer-wide solid-build budget is 0.50 ms, so ranking up to 64 already-visible priority records can consume the whole frame's geometry budget.
- [x] Make frustum-visible priority admission constant-time in the normal case: take the first current FIFO demand after at most eight stale-motion checks instead of ranking up to 64 visible holes; keep the background FIFO and the 0.50 ms global build budget unchanged.
- [x] Validate the constant-time visible selector in EditMode and remeasure production selection timing/coarse coverage. PR run 32022085431 (`f0e0b689`) passes the affected EditMode gate and drops selection p95 from 0.52 ms to 0.02 ms with the unchanged 0.50 ms build budget; at 10.00 s production is still only 141/5,672 resident with 4,264 dirty, 16 running, 104 visible and 1,543 missing visible chunks, so the selector hotspot is fixed but coverage is not.
- [ ] Fix the remaining coarse-coverage defect: exact-head PR run 32022085431 still has 50 visible coarse silhouette holes and step 4 again ends `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0` after 20 s; step 4 uses the exact COW snapshot path (`LevelForStride(4) == -1`), so the disappearance is downstream of exact snapshot admission rather than lossy mip sampling.
- [ ] Add a focused step-4 false-empty regression proving an exact owned solid that falls between four-voxel lattice samples cannot be published as an authoritative empty chunk.
- [ ] Repair the proven step-4 false-empty path with feature-preserving subcell geometry without changing LOD distances, global frame budgets, or fidelity thresholds.

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
- [ ] Confirm the new per-fixture PlayMode shard layout no longer hits the Unity RSS watchdog on the current head. Crash/session isolation works and later shards continue, but the already-single-method `MemoryStaysWithinTierBudgetOverTwoHours` and `ContinuousTraversalOverKilometresShowsNoGaps` shards still hit approximately 14,376 MB and 14,339 MB respectively against the unchanged 14,336 MB ceiling in PR run 32022085431; further YAML splitting cannot solve those two tests.
- [x] Classify current-head PR run 32014802229 failures without masking them: rendering-repair blockers are coarse/scheduler convergence (`CastleExteriorLookdevTests`, `ShowcasePerformanceTests`, `LodRenderingTests`, `TerrainLookdevScreenshotTests` and the async-geometry published-baseline fixtures); `LodVisualFidelityTests` is currently blocked by its `RenderRequest` destination setup rather than a fidelity threshold; `ShowcaseNoStutterTests` is a validation-path failure because its measured window never observes production build work. Baseline/non-repair failures reproduced independently include `DistantAlterationTests` length validation, `FallingVoxelPhysicsTests` debris lifetime, far-terrain relief/ground-ahead assertions, plus the two synthetic residency-memory watchdog tests above.
- [x] Reconcile the stale passive-discovery and batch-flush EditMode contracts exposed by PR run 32019198712; immutable discovery now correctly expects no immediate dirty build work, and the architecture gate checks the single non-blocking flush after pre-admission visibility collection.
- [x] Validate the corrected passive-discovery and batch-flush contracts on the clean current head; PR run 32019741845 passes all 651 affected EditMode tests.
- [x] Repair the two reproducible validation-path blockers from run 32019741845 without changing acceptance thresholds: explicitly render `Camera.main` into a created target inside the no-stutter measurement window, and provide a created destination for the fidelity fixture's bootstrap URP `RenderRequest`.
- [x] Validate the corrected no-stutter and LOD-fidelity harnesses on the clean current head. PR run 32022085431 proves both fixtures now exercise real production rendering: no-stutter reaches a live renderer and fails on convergence (`dirty=4264`, `running=16`, `missingVisible=818`, `visible=131`), while fidelity gets past bootstrap `RenderRequest` setup and fails because LOD 1/view 0 never stabilizes (centre-step mask 0). The harness blockers are resolved; the renderer acceptance failures remain open.

### G. Current-head acceptance validation

- [x] Run/confirm `FarTerrainTopologyReuseTests.RebuildAfterCameraSnap_ReusesExistingIndexTopology`.
- [ ] Run/confirm `ShowcaseNoStutterTests.BakedStartup_NeverBuildsCastleDuringPlayAndNeverStallsRendering` (PR run 32022085431 now exercises real rendering but still fails convergence with `dirty=4264`, `running=16`, `missingVisible=818`, `visible=131`; frame-percentile acceptance is therefore not yet creditable).
- [ ] Run/confirm `ShowcasePerformanceTests.FullShowcaseConvergesWithinTenSecondsWithoutLaterStalls`.
- [ ] Run/confirm the production LOD rendering/fidelity suite, including LOD 1/2/4/8 image-space fidelity.
- [x] Confirm production step-8 HLOD no longer reports output overflow.
- [ ] Confirm production step-8/coarse rendering publishes complete visible coverage with no coarse holes.
- [x] Record the current failed convergence measurement: at 10.00 s the renderer had 128/5,672 resident chunks, 5,483 dirty chunks, 14 running jobs, 1,591 missing visible chunks, queue p95 4,356.37 ms and build p95 890.13 ms. This is evidence for the next scheduler/coverage repair, not an accepted performance result.
- [x] Record the post-lifecycle-dedup measurement from PR run 32014802229: at 10.00 s the renderer had 131/5,672 resident chunks, 4,225 dirty chunks, 16 running jobs, 1,567 missing visible chunks, queue p95 4,578.88 ms and build p95 1,036.66 ms. The duplicate-generation fix reduces stale dirty work but does not solve ring-demand starvation.
- [x] Record the visible-priority measurement from PR run 32019741845: at 10.00 s the renderer has 126/5,672 resident, 4,257 dirty, 15 running jobs, 107 visible and 1,567 missing visible chunks; prepare p95 2.67 ms, worker/select p95 0.52/0.52 ms, queue p95 4,300.35 ms and build p95 911.84 ms. This proves priority queue ordering is not enough because selection itself saturates the 0.50 ms build budget.
- [x] Record the constant-time-selector measurement from PR run 32022085431: at 10.00 s the renderer has 141/5,672 resident, 4,264 dirty, 16 running jobs, 104 visible and 1,543 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.51/0.02 ms, queue p95 4,073.81 ms and build p95 1,036.84 ms. Selection no longer consumes the frame budget, but worker/build throughput and false-empty coarse publication still prevent convergence.
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
- `93ea3f34` — frustum-visible missing/stale demand is promoted ahead of the 360-degree background prefetch FIFO without changing global frame budgets.
- `6678ca3e` — EditMode validation contracts aligned with passive discovery and the pre-admission visibility / single-flush frame order.
- `2bca9841` — visible-demand selection made constant-time in the normal case; no-stutter and LOD-fidelity batchmode render targets repaired without changing acceptance thresholds.

PR #88 run 32022085431 (`f0e0b689`) validates the constant-time selector and both repaired validation harnesses on the clean exact head, but production PlayMode remains red. Selection p95 falls from 0.52 ms to 0.02 ms without changing the 0.50 ms budget, while the 10-second showcase still has 1,543 missing visible chunks and coarse lookdev reports 50 holes. Step 4 again converges to `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0`, confirming the remaining coarse disappearance is not queue backlog. Static tracing shows exact classification preserves owned solids, while step-4 faceted/continuous generation samples the four-voxel lattice and zero-geometry completion is recorded as authoritative empty; the next gate is a focused regression for that false-empty path before any production geometry fallback is enabled. The no-stutter and fidelity fixtures now reach production rendering and fail on convergence/coverage rather than their former batchmode setup blockers.

PR #88 is a draft validation vehicle only. Do not merge it merely to obtain a green check; use its Unity results to complete section G and drive the next measured repair.
