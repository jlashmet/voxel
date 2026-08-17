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

### D. Repair missing-terrain completeness without sacrificing fidelity

Treat missing terrain as two independent correctness domains. **Near-field completeness** means the extracted voxel renderer must produce authoritative drawable geometry for demanded near/coarse chunks. **Near/far coverage invariants** mean the far heightfield must continuously cover everything the near renderer is not yet able to draw, with no geometric gaps introduced by hole sizing, ring snapping, or asynchronous publication. A defect in one domain does not invalidate or supersede investigation of the other.

#### D1. Far-field pacing and shared renderer groundwork

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

#### D2. Near-field completeness

- [ ] Fix the remaining coarse-coverage defect: exact-head PR run 32028393584 (`63577998`) still has 48 visible coarse silhouette holes and step 4 again ends `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0` after 20 s. The requested step-4 coverage is therefore being adjudicated as ready/empty rather than remaining queued; the next repair belongs in exact-owned empty/publication semantics, not scheduler priority.
- [x] Add a focused step-4 false-empty regression proving an exact owned solid that falls between four-voxel lattice samples cannot be published as an authoritative empty chunk. PR run 32024887037 (`e53f14ce`) executes the focused EditMode gate and fails exactly because production has no `RequiresFeaturePreservingFallback` guard, proving the regression targets the measured false-empty path.
- [x] Implement the proven step-4 false-empty repair: when exact classification owns solid content but ordinary step-4 topology/faceted output is empty, reuse the existing exact 2-voxel subcell summary/greedy HLOD path before publication. Normal step-4 geometry, LOD distances, the 0.50 ms global build budget and fidelity thresholds remain unchanged.
- [x] Validate the focused step-4 false-empty fallback policy/regression in EditMode on clean exact head `63577998`; PR run 32028393584 passes the affected EditMode gate and the baked startup step.
- [x] Remeasure production step-4/coarse visible coverage with the false-empty fallback active. PR run 32028393584 still reports 48 silhouette holes; `LodRenderingTests` ends with `known=1182/resident=124/dirty=661/visible=0/missing=0/jobs=16`, while step 4 itself is `known=110/resident=7/dirty=0/missing=0/jobs=0` and produces zero visible draws. The fallback policy is valid but does not resolve the production ready-empty state.
- [x] Add and wire cache-lifecycle diagnostics for the step-4 fallback path: exact owned/unowned classification, ordinary non-empty/empty output, fallback schedules/completions/non-empty completions/publications, and final ready-empty publications are exposed in `LodRenderingTests`; this instrumentation does not alter admission, geometry or publication behavior (`3385982f`, `31182c61`).
- [x] Run the lifecycle/visibility diagnostics on the frozen compile-fixed source `4842bd44` (workflow run 32032548787) and identify the first loss stage. Step 4 reaches the correct active ring and camera frustum (`known=110`, `inBand=23`, `frustum=8`) but every requested frustum chunk is current-empty (`ready=0`, `empty=8`), while feature-preserving fallback is never entered (`scheduled/completed/nonEmpty/published=0/0/0/0`). The defect is therefore before fallback meshing/publication: current-empty adjudication occurs without satisfying the fallback admission guard; clipmap routing, frustum culling, HLOD output and successful-upload bookkeeping are not the first failure.
- [x] Determine which fallback-admission predicate is false for those current-empty frustum chunks. Full immutable lifecycle run 32033135300 (`614ea118`) reports `owned:3/unowned:20/ownedProfiles:0/ordinaryNonEmpty:3/ordinaryEmpty:0/ordinaryEmptyProfiles:0/fallback:0/nonEmpty:0/empty:0/published:0/readyEmpty:16`. Every exact-owned snapshot produced ordinary non-empty geometry, no profile-backed exclusion occurred, and the authoritative-empty publications correspond to exact classification returning unowned. The first proven false predicate is therefore exact-owned classification, not profile suppression or fallback execution.
- [x] Identify the first exact-snapshot data-boundary defect behind the production `unowned` classification. `IRegionReadSource.TryPinRegionBlockRefs` only succeeds for currently resident regions, but `ScheduleExactMetadataSnapshot` previously skipped a failed required region pin after clearing the cache, allowing unavailable metadata to be classified and published as authoritative empty. This matches lifecycle run 32033135300 (`unowned:20`, `readyEmpty:16`) with zero revision/payload-pin rejects.
- [x] Add the initial `ExactSnapshotRegionCoverageTests` and reject incomplete required-region metadata instead of treating an unavailable core as authoritative empty (`cfd54a39`, `ad9d7c13`).
- [x] Identify the over-strict halo flaw before crediting that repair: production chunks are at most one storage region wide, but their one-block extraction halo can cross into intentionally non-resident regions. In the showcase, step-4 ground chunks own blocks `[0,32)` in region `y=0` while their halo reaches region `y=-1`; `ShowcaseWorld.SurfaceLayerSpan` clamps residency to `y>=0`, so requiring every padded halo region would retry those chunks forever.
- [x] Correct exact snapshot coverage so only regions intersecting the unpadded owned core are required; available halo metadata is still copied, unavailable halo ranges remain cleared for the optimistic build, and unavailable core metadata still rejects/retries through the existing bounded lifecycle. Add regressions proving a missing core pin rejects the snapshot while the step-4 negative-Y halo is optional. No synchronous completion, LOD distance, frame budget, fallback rule, or geometry threshold changes (`7cad2b26`, `d6b38e5f`, `94029f35`).
- [x] Validate the corrected core/halo exact-region coverage regression in EditMode and rerun the step-4 lifecycle/LOD coverage gate. PR run 32037051272 (`e9fd2779`) passes all four `ExactSnapshotRegionCoverageTests`, and production no longer reaches the old false-empty adjudication (`unowned=0`, `readyEmpty=0`). The production defect is not resolved: step 4 now remains pending with `resident=0`, `dirty=21-23`, `missing=6-8`, and repeated required-core pin rejection (`pinReject=113-140`) over 20 seconds, so the repair correctly refuses incomplete core snapshots but exposes a permanent retry/residency mismatch.
- [ ] Identify why required step-4 core regions are still unavailable to `TryPinRegionBlockRefs` after clipmap demand reaches the camera frustum; add a focused residency/snapshot-admission regression before changing residency or retry policy. The next repair must eliminate the persistent `pinReject` backlog without reintroducing authoritative empties, synchronous completion, or broader residency inflation.

#### D3. Near/far coverage invariants

- [x] Add a cold-start regression requiring continuous published coverage from either drawable near geometry or the far fallback while near streaming is incomplete (`FarFieldCoverageInvariantTests.ColdStartMaintainsContinuousPublishedFallbackCoverage`, `a6000709`).
- [x] Add a world-space handoff regression requiring ring 0's hole never to exceed actual drawable near coverage; generated-region residency is explicitly not sufficient (`FarFieldCoverageInvariantTests.RingZeroHoleNeverExceedsDrawableNearCoverageInWorldSpace`, `a6000709`).
- [x] Add an isolated topology regression requiring independently snapped published parent/child far rings to geometrically overlap (`FarFieldCoverageInvariantTests.PublishedParentChildRingsOverlapAcrossIndependentSnapStates`, `a6000709`).
- [x] Add a movement regression requiring the correctness-critical inner far ring to publish the moved-camera sample no later than any outer ring (`FarFieldCoverageInvariantTests.CameraMovementNeverPublishesOuterRingAheadOfCriticalRing`, `a6000709`).
- [ ] Validate the four near/far coverage regressions in Unity and identify the first failing invariant. Fix that invariant rather than guessing from screenshots; do not close or abandon the independent D2 step-4 `pinReject` investigation.

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
- [ ] Confirm the new per-fixture PlayMode shard layout no longer hits the Unity RSS watchdog on the current head. Crash/session isolation works and later shards continue, but the already-single-method `MemoryStaysWithinTierBudgetOverTwoHours` and `ContinuousTraversalOverKilometresShowsNoGaps` shards still hit 14,342 MB and 14,355 MB respectively against the unchanged 14,336 MB ceiling in PR run 32028393584; further YAML splitting cannot solve those two tests.
- [x] Implement bounded, allocation-free eviction over actual resident regions so historical regions left completely behind the player's current unload cube are eventually considered; add `ResidencyEvictionRegressionTests.BoundedScanEvictsHistoricalResidentLeftBehindPlayer` and correct traversal test brick-pool sizing so its 1 MiB budget is converted to pool slots rather than interpreted as 1,048,576 slots (`2b7ba60c`).
- [x] Isolate the two-hour memory soak by PC/Console/Mobile tier so Unity allocator history from one tier cannot inflate the next tier's RSS; preserve each tier's existing byte budget, correct the auxiliary eviction test pool to 16 slots, and mirror the three fresh-process filters in PR/master workflows (`88610d2b`, `27d0f141`).
- [ ] Validate `ResidencyEvictionRegressionTests.BoundedScanEvictsHistoricalResidentLeftBehindPlayer`, rerun `traversal-continuous`, and run all three per-tier two-hour soak shards below the unchanged 14,336 MB watchdog; only then mark the parent shard/watchdog task complete.
- [x] Classify current-head PR run 32014802229 failures without masking them: rendering-repair blockers are coarse/scheduler convergence (`CastleExteriorLookdevTests`, `ShowcasePerformanceTests`, `LodRenderingTests`, `TerrainLookdevScreenshotTests` and the async-geometry published-baseline fixtures); `LodVisualFidelityTests` is currently blocked by its `RenderRequest` destination setup rather than a fidelity threshold; `ShowcaseNoStutterTests` is a validation-path failure because its measured window never observes production build work. Baseline/non-repair failures reproduced independently include `DistantAlterationTests` length validation, `FallingVoxelPhysicsTests` debris lifetime, far-terrain relief/ground-ahead assertions, plus the two synthetic residency-memory watchdog tests above.
- [x] Reconcile the stale passive-discovery and batch-flush EditMode contracts exposed by PR run 32019198712; immutable discovery now correctly expects no immediate dirty build work, and the architecture gate checks the single non-blocking flush after pre-admission visibility collection.
- [x] Validate the corrected passive-discovery and batch-flush contracts on the clean current head; PR run 32019741845 passes all 651 affected EditMode tests.
- [x] Repair the two reproducible validation-path blockers from run 32019741845 without changing acceptance thresholds: explicitly render `Camera.main` into a created target inside the no-stutter measurement window, and provide a created destination for the fidelity fixture's bootstrap URP `RenderRequest`.
- [x] Validate the corrected no-stutter and LOD-fidelity harnesses on the clean current head. PR run 32022085431 proves both fixtures now exercise real production rendering: no-stutter reaches a live renderer and fails on convergence (`dirty=4264`, `running=16`, `missingVisible=818`, `visible=131`), while fidelity gets past bootstrap `RenderRequest` setup and fails because LOD 1/view 0 never stabilizes (centre-step mask 0). The harness blockers are resolved; the renderer acceptance failures remain open.
- [x] Ensure the master PlayMode matrix bakes the showcase startup world for `VoxelEngine.CI.PlayMode` as well as `VoxelEngine.Tests.PlayMode`; both can open `VoxelShowcase.unity`, and runtime castle authoring is intentionally forbidden (`c331ad9a`).
- [ ] Validate the expanded master bake prerequisite on a clean master/full-suite execution; only mark complete after `VoxelEngine.CI.PlayMode` reaches its tests with `ShowcaseWorld.bytes` present.

### G. Current-head acceptance validation

- [x] Run/confirm `FarTerrainTopologyReuseTests.RebuildAfterCameraSnap_ReusesExistingIndexTopology`.
- [ ] Run/confirm `ShowcaseNoStutterTests.BakedStartup_NeverBuildsCastleDuringPlayAndNeverStallsRendering` (PR run 32028393584 reaches real production rendering but still fails convergence with `dirty=4142`, `running=0`, `uploadMeshes=16`, `uploadBytes=16503436`, `missingVisible=1436`, `visible=161`; frame-percentile acceptance is therefore not yet creditable).
- [ ] Run/confirm `ShowcasePerformanceTests.FullShowcaseConvergesWithinTenSecondsWithoutLaterStalls`.
- [ ] Run/confirm the production LOD rendering/fidelity suite, including LOD 1/2/4/8 image-space fidelity.
- [x] Confirm production step-8 HLOD no longer reports output overflow.
- [ ] Confirm production step-8/coarse rendering publishes complete visible coverage with no coarse holes.
- [x] Record the current failed convergence measurement: at 10.00 s the renderer had 128/5,672 resident chunks, 5,483 dirty chunks, 14 running jobs, 1,591 missing visible chunks, queue p95 4,356.37 ms and build p95 890.13 ms. This is evidence for the next scheduler/coverage repair, not an accepted performance result.
- [x] Record the post-lifecycle-dedup measurement from PR run 32014802229: at 10.00 s the renderer had 131/5,672 resident chunks, 4,225 dirty chunks, 16 running jobs, 1,567 missing visible chunks, queue p95 4,578.88 ms and build p95 1,036.66 ms. The duplicate-generation fix reduces stale dirty work but does not solve ring-demand starvation.
- [x] Record the visible-priority measurement from PR run 32019741845: at 10.00 s the renderer has 126/5,672 resident, 4,257 dirty, 15 running jobs, 107 visible and 1,567 missing visible chunks; prepare p95 2.67 ms, worker/select p95 0.52/0.52 ms, queue p95 4,300.35 ms and build p95 911.84 ms. This proves priority queue ordering is not enough because selection itself saturates the 0.50 ms build budget.
- [x] Record the constant-time-selector measurement from PR run 32022085431: at 10.00 s the renderer has 141/5,672 resident, 4,264 dirty, 16 running jobs, 104 visible and 1,543 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.51/0.02 ms, queue p95 4,073.81 ms and build p95 1,036.84 ms. Selection no longer consumes the frame budget, but worker/build throughput and false-empty coarse publication still prevent convergence.
- [x] Record the step-4-fallback measurement from PR run 32028393584: at 10.01 s the renderer has 123/5,672 resident, 4,221 dirty, 13 running jobs, 103 visible and 1,539 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.52/0.02 ms, visibility p95 1.98 ms, snapshot p95 0.14 ms, compact p95 0.78 ms, merge p95 0.30 ms, upload p95 0.32 ms, queue p95 4,335.45 ms and build p95 1,484.54 ms. The focused fallback does not improve production convergence or step-4 visibility.
- [x] Record the pinned step-4 lifecycle measurement from run 32032548787 (source `4842bd44`): overall LOD failure is `known=1182/resident=135/dirty=645/jobs=15`, while step 4 is idle at `known=110/resident=7/dirty=0/missing=0/jobs=0`; of 110 known step-4 chunks, 23 are in-band, 8 are in the camera frustum, all 8 are current-empty and none are current-ready. Fallback counters remain `0/0/0/0`, proving the first loss is current-empty/fallback-admission semantics rather than HLOD generation or publication.
- [x] Record the corrected-core/halo lifecycle measurement from run 32037051272 (`e9fd2779`): `LodRenderingTests` no longer reaches authoritative empty at step 4 (`owned=0/unowned=0/readyEmpty=0`) because incomplete required-core snapshots are rejected. Instead step 4 remains `resident=0/dirty=23/missing=8/jobs=3`, with metadata attempts/rejections `143/136` and `pinReject=136`; focused diagnostics reproduce 113-140 pin rejects. This validates the core/halo contract but proves required-core residency/pinning is now the first blocking stage.
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
- `63577998` — clean exact-head validation of the step-4 false-empty fallback; EditMode passes, production coarse coverage remains red.
- `3385982f` — diagnostics-only step-4 fallback lifecycle counters added for scheduled/completed/non-empty/published adjudication.
- `31182c61` — final step-4 lifecycle hooks add ready-empty and successful-fallback publication counters without changing renderer behavior.
- `2b7ba60c` — bounded actual-resident eviction reaches historical regions left behind player movement and adds the traversal regression.
- `88610d2b` / `27d0f141` — two-hour memory soak split by tier/process and mirrored in PR/master workflow isolation.
- `c331ad9a` — master full-suite bake now also covers `VoxelEngine.CI.PlayMode`.
- `7cad2b26` / `d6b38e5f` / `94029f35` — exact snapshot coverage refined to require only unpadded core regions while allowing unavailable extraction-halo regions; focused core/halo regressions added.
- `a6000709` — four near/far coverage invariants added against published drawable near geometry and published far-ring triangles; validation pending.

Run 32037051272 on head `e9fd2779` closes the core/halo validation task but does not close production coarse coverage. All `ExactSnapshotRegionCoverageTests` pass and the old `unowned/readyEmpty` false-empty state disappears, proving incomplete core metadata is no longer published as empty. The near-field first loss stage remains persistent required-core pin rejection: step 4 stays dirty/missing with zero resident chunks and 113-140 `pinReject` events in 20 seconds. The D2 task remains to trace clipmap/residency ownership for those required core regions and add a focused regression before changing retry or residency policy.

Separately, D3 now pins the near/far handoff with four regressions. Their Unity validation must identify the first failing coverage invariant before any far-field production behavior changes; a D3 failure does not supersede or close the independent D2 `pinReject` defect.

PR #88 is a draft validation vehicle only. Do not merge it merely to obtain a green check; use its Unity results to complete section G and drive the next measured repair.