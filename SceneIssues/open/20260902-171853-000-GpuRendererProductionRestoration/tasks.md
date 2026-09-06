# GPU-only VoxelShowcase — execution checklist

**User-authorized rewrite:** 2026-09-05 (America/Los_Angeles). **Plan:** [plan.md](plan.md).

**Deliverables:** a correct production GPU voxel backend, physical removal of the CPU-only rendering backend, and the complete VoxelShowcase measured toward 1,000 FPS (1.00 ms per frame), or the closest verified result under the unchanged benchmark contract below.

## Execution rules

The old requirement to make CPU VoxelShowcase production-quality before GPU diagnosis is **withdrawn**, not completed. GPU testing is the immediate priority. CPU comparisons may temporarily diagnose identical inputs; CPU polishing, a passing CPU image, or a CPU-only module run cannot stand in for GPU progress. Shared visual defects remain required final work, investigated while the GPU path is active.

Work the next unchecked non-blocked item. Profiling starts with the first GPU replay and may proceed alongside correctness; the section order does not defer measurement until the end. Preserve queued/running requests. Existing request `560b0c08f022c42faa9c6877e63d109083eb2dc9` / run `34005604349` is a pre-rewrite CPU diagnostic and was queued at this revision. Its result may verify artifact isolation but cannot fulfill G03/G04. Do not submit another CPU-only replay ahead of GPU restoration. No manual enumeration of repository-derived module/player targets or alternate workflows/CI branches.

Each completed item needs exact feature SHA, request/run IDs, relevant executed tests and retained artifact paths. A failing run is not green because it contains a screenshot. After two materially different fixes fail the same assertion/symptom, isolate the minimal production-faithful repro before another fix. Keep investigation and profiles bounded; no scene-name/material-ID hacks, hidden content, or weaker tolerances.

## Retained evidence — not final GPU acceptance

| Proven historical result | Evidence / limitation |
| --- | --- |
| Shared frustum taper divergence and repair | `da3f5be...` / run `33999899224`: eight intended failures. `e4e2f997...` / run `34003412217`: 657 module EditMode, three PlayMode and eight repeated focused passes. See `frustum-geometry-evidence.md`. |
| The repaired full scene still fails visual acceptance | The same run has zero GPU requests/publications. Left taper and right-hand masses remain blockout quality; zero-sample frame timings are not performance proof. |
| GPU is not universally broken for a single chunk | Historical `6451cf98...` / run `33929485980`: 41 solids, 114 exposed faces, 456 vertices, 684 indices, publication 1, fallback 0, visible 1, missing 0. Reconcile and rerun on current source. |
| Prior density, semantics, transition and mirror work | Preserve `gpu-density-oracle-history.md`; rerun the applicable invariants after restoration. Historical checkmarks do not certify the new final backend. |
| Required artifact preservation defect | FarWorld/Water outputs collided in run `34003412217`. Current branch contains a repair/probe under request `560b0c08...`: filesystem checks recorded four before-fix failures and five after-fix passes; fourteen Unity probe cases await CI. Inspect terminal player evidence rather than implementing the same fix again. |

## Current local evidence (2026-09-06)

User direction is now local harness/testing and screenshot review, with no further origin pushes.
`Artifacts/LocalGpuShowcase/753a21241-local-harness/` retains the completed 180-second,
1920x1080/scale-1, non-development Metal VoxelShowcase capture (11 PNGs), player/build logs,
source/settings delta and diagnostic timing summary. Visual classification: **unacceptable**.
G08/G09 defects: near terrain/structure surfaces missing despite `missingVisible=0`, floating
castle tower rings and vegetation, flat cyan water regions, blockout-like grey far masses and
terrain cracks. Traversal retains severe omissions. Fence completion is currently counted as
host publication without proving successful allocation/live GPU geometry; these metrics cannot
certify coverage. Do not hide content or accept the throughput as a finished-scene benchmark.

Diagnostic 60–89s windows reconstruct ~246.5 FPS, per-window p95 4.30–4.50 ms; 100–180s traversal
windows reconstruct ~101.4 FPS, per-window p95 8.49–20.99 ms. These are rounded window-log
summaries with screenshot/log overhead, **not** aggregate percentiles or the repeated benchmark.
Local targeted Metal tests completed in 11 seconds: six passed (three publication transactions
and all three analytic faceted cases), two failed (Exhausted/TooLarge both still return Stale).
The batch-counter alias hypothesis is therefore falsified as the sole cause.

The production allocator also used a 36-byte descriptor against the host/mesher's 44-byte
buffer. A two-record identity regression failed on the second handle before a shared HLSL
layout fixed it. The stride-only replay completed (11 captures) but remained unacceptable.
Capacity diagnostics then proved correct desired/decoded generations; moving only the default
Stale write did not fix the branch-local writes. Computing the classification first and writing
status once before ownership mutation passes all nine focused Metal cases, including both
capacity failures, stale rejection, prior-live preservation, explicit commit, multi-record identity
and the three analytic faceted styles. Evidence: `stride-fixed/allocator-classified-status.xml`.
G10 remains incomplete until full publication/recovery/scene behavior is proven. The exact
allocator-fixed 180-second replay also completed with 11 screenshots and remained **unacceptable**.
Its diagnostic stationary/traversal windows were ~194/~103 FPS (per-window p95 5.41–5.79ms /
8.49–21.29ms). Four additional prepared-batch/publication regressions passed with no skips in
`allocator-fixed/production-pipeline.xml`. Local code commits: `968a06ced`, `dc0ced3e2`; not pushed.
The next bounded trace completed in `publication-trace/`: at 60s, 461 visible handles,
295 live-ready records, 208 empty records and 253 indirect instances. All nonempty live visible
records reached compaction, but the vertex arena had one free page; 259 allocations returned
Exhausted by 65s. Example: handle408, origin(640,0,640), step2 requested156812 vertices.
Temporary synchronous instrumentation is archived as `diagnostic-source.diff` and removed from
runtime; this run is excluded from performance acceptance. G10/G11 must fix bounded capacity
recovery and host readiness, without increasing budgets or hiding demand.

A second failure was independent of allocation: direct SV_InstanceID metadata addressing did
not include indirect bucket prefixes on local Metal. The explicit GPU bucket-prefix fix restores
most castle facade/towers in the 80-second `explicit-bucket-offset/` standalone capture (five PNGs).
The real SmoothSurface vertex-shader raster regression with three separated buckets fails on
old shaders at handle1 (`draw-before.xml`) and passes with explicit prefixes (`draw-projection.xml`).
The existing 600-handle compaction regression also passes. Final combined run: 11 passed, zero failed/skipped in 13 seconds
(`draw-after.xml`), covering draw compaction/raster and publication transactions/pump.
Visual classification remains **unacceptable**: capacity holes, fragmented structures, cyan water
and blockout far terrain/seams remain. Late GPU frame timing windows report zero samples;
no throughput or visual acceptance is claimed.

The `category-trace/` 85-second player run completed with four reviewed screenshots; still
**unacceptable**. Before70s, 849 requests emitted requested counts of36,184,979 regular vertices,
26,342,636 faceted vertices and6,220 decoration vertices, excluding later transitions/profiles.
315 step2 requests account for30,655,946 regular and19,388,944 faceted vertices. This is demand,
not simultaneous live occupancy. Decoration pressure is falsified; faceted merging alone cannot
resolve the demand. Before choosing compaction, compare a captured high-count step2 chunk's
prepared density and pre-page output against its actual occupied boundary (origin0,256,512:
216691 regular vertices). Scheduler arena relief currently monitors CPU geometry failures only;
GPU failure recovery and truthful readiness remain required G10/G11 work. Temporary synchronous
instrumentation was archived as `category-trace/diagnostic-source.diff` and removed from runtime.


The high-count diagnostic found **invalid prepared layouts**, so the preceding category totals
cannot establish legitimate demand or justify compression/budget changes. `chunk-trace-adaptive/`
captured origin(-256,128,-128), step2, extractor cacheEdge18 with only1000 prepared entries
(edge10), rather than5832. Lane reuse retained its initial layout and allowed incompatible
extractors to share a batch. Fix groups compatible layouts, recreates incompatible idle resources
after completion, and guards count/write against mismatches. Boundary regression alternates
edges4/6 and failed before (expected256 vertices, got0); all13 focused tests pass after in17s,
zero skipped (`layout-boundary-before.xml`, `layout-after.xml`). The earlier all-solid fixture
passed before and did not expose this failure. Diagnostic binaries/patch retained; readbacks removed.
`layout-fixed/` completed180 seconds, player exit0,11 screenshots. Castle upper walls no longer
fragment into floating strips. Traversal still has missing ground, terrain bands, cyan water and
grey blockout far structures: **unacceptable**. Its `diagnostic-summary.json` retains rounded
per-window timings and caveats; no full-coverage or performance acceptance. Reassess GPU capacity
and live publication with corrected layouts before implementing compression/recovery changes.


Corrected-layout replay `layout-coverage-trace/` completed180s, exit0,11 screenshots. At60s,
538/538 visible handles were live-ready (491 nonempty draws),7454 vertex pages remained and
zero failures occurred. At120s, only45/69 visible handles were live-ready,2 pages remained,
and138 failures had accumulated. At165s,171/172 were ready,1 page remained,273 failures.
Batch logs through175s contain1345 Ready and290 Exhausted results. Thus traversal pressure
and lost retry are real even after layout repair. G10/G12 next require explicit outcomes,
safe reclamation and retry. Existing parser/retry branch are disconnected; CPU-arena relief does
not observe GPU failures. Resolve any asynchronous render-control status-channel contract in the
active plan before implementation, preserving the no-blocking/no-geometry-readback constraints.
Temporary readbacks archived/removed; `diagnostic-summary.json` retains exact snapshots. Visual
classification remains **unacceptable**, including missing ground and far/water defects.


Render-control contract is now explicit: G05 prohibits geometry/count readback; G10–G13 permit
only16 bytes per chunk of asynchronous status/handle/generation feedback. Production exports a
compact record after finalization, retains lane buffers through callback completion (including
retired-lane deferred disposal), and only marks Ready as successful. Exhausted enters retry and
triggers bounded offscreen GPU-only eviction; budgets, visible demand and authoritative state
are unchanged. The automatic pending-publication bridge remains: explicit host approval,
permanent-error handling and final last-consumer retirement still require G10/G11 completion.
`outcome-recovery/final-tests.xml`:22 passed,zero skipped/failed,in15 seconds. Includes real GPU
exhaustion -> async16-byte feedback -> page reclamation -> successful retry, compact independent
record identity and the updated no-blocking/no-count-transfer architecture assertion.
`outcome-recovery/` completed180 seconds,exit0,11 screenshots,no transaction rejection errors.
Visual classification remains **unacceptable**: terrain gaps/bands, grey far masses, cyan water.
Diagnostic window timings/source delta are archived; no benchmark or full coverage acceptance.



Current local publication replacement deletes the automatic pump/kernel, assigns a unique renderer
attempt generation, and requires host source/configuration approval before commit. Cancellation
aborts exact identities, including results arriving after their context was released.
`approval-identity-tests.xml`:25 passed. `approval-module/`:48s,exit0,8 captures, production
initial/traversal/edit/settled/restart passed with zero fallback/rejections. `approval-showcase/`:
180s,exit0,11 captures; reviewed60s/165s. **Unacceptable**: terrain banding/seams, coarse grey
far geometry, cyan water and poor terrain integration. These remain G07–G09 defects.

Finalization previously ignored actual write totals. `write-finalization-before.xml`:all5 real-GPU
missing/short/overflow transaction cases failed. GPU-side count comparison now rejects incomplete
candidates, retires their pending pages and preserves prior live geometry; only failure status
crosses to the CPU. `write-finalization-after.xml`:30 passed,zero skipped/failed,18s guarded run.
`write-finalization-module/`:48s,exit0,8 captures, all module stages passed; final42s screenshot
reviewed. `write-finalization-showcase/`:180s,exit0,11 captures;165s reviewed, still unacceptable.
This first strict run mapped write mismatches to generic retryable Failed, so quiet logs do not
prove zero write-count failures. Dedicated `WriteFailed` status now remains distinct/nonretryable
and logs rejection. `write-finalization-distinct-tests.xml`:31 passed in18s.
`write-status-module/`:48s,exit0,8 captures; initial/traversal/edit/settled/restart passed with
zero fallback/missing/transaction rejections,42s screenshot reviewed. `write-status-showcase/`:
180s,exit0,11 captures,zero transaction rejections/exceptions;60s/150s/165s reviewed.
**Unacceptable**: terrain banding/seams, coarse grey masses, cyan water, and a large featureless
foreground surface during traversal. No observed write-count failure in this run; payload/coverage
correctness remains unproven. Diagnostic CPU window p50 median:4.885ms stationary,8.65ms walking;
window p95 ranges5.12–5.41ms /8.30–20.91ms, worst15.22ms /30.52ms respectively. Not performance
acceptance: mixed backend, incomplete coverage and viewpoint differences remain. Exact local
source patches/hashes and diagnostic JSON are beside each run.
`write-finalization-bridge-final.xml`:4 PlayMode arena tests passed in15s. Earlier4 failed before
explicit handle acquisition; then1 failed because its legacy snapshot was absent from production
coordinate lookup. Fixtures now acquire handles, model successful bookkeeping writes, explicitly
commit, and publish canonical voxel input through the real mirror. Assertions were preserved.
Source audit for G11: `GpuSurfacePageArena.RetirementDelayFrames` is still4;
`GpuSurfaceExtractionContext.Release` releases persistent coverage and calls `EndExtraction`
without waiting for an in-flight lane's consumption. Next regression must cancel submitted work
and prove mirror leases remain owned until real GPU completion, then cover last-draw retirement.
G10 remains open for remaining transaction work;
this is no proof of payload correctness, final draw lifetime, complete coverage or performance.

User asks whether the recent far-object/terrain system is active: **yes**. `VoxelShowcase.cs`
creates `VoxelFarTerrain` at12km and `ShowcaseFarFeatureRuntime`; the latter uses the canonical
`FeaturePresentationCatalogueBaker` manifest, selection/state adapters and
`ProceduralFarFeatureRenderer`. Final player log confirms6 terrain rings and1480/1481 selected
semantic instances at12km. During traversal `hole=0m coverage=False` recurs, despite completed outer
terrain rings. `HoleRadiusMetres` explicitly closes its cutout while near publication is incomplete.
This is a near/far integration candidate, not proof that every coarse artifact is GPU meshing.
Also audit semantic near handoff: Showcase submits all selected tiers (including Mid) without a
visible near-residency filter; distinguish intended fallback from duplicate nearby presentation.
Investigate this existing system before replacing far presentation; preserve its content/range.

Far ownership experiments on d107d8e31: `far-owner-probe/`55s exited1 (8 captures, minimum9),
then `far-owner-probe-complete/`65s exited0 with10 captures. Reviewed25s/35s/45s: grey mountain
and crude side structures disappear with only semantic far features suppressed, then return.
The terrain remains. Existing probe restores visibility; no content removal is an accepted fix.
`far-owner-distance/`65s exited0/10 captures with temporary bounded bounds-ray logging. Candidate
CC9F50C170E9C507, bake-4DF345372F3D7230, Mid, center(-59.95,35.85,18.05)m,
extents(50.05,14.05,52.05)m, bounds-ray distance60.79m matches the mountain landmark.
Right candidate EDDA2E49B0BB89BE, Mid, bounds-ray distance102.10m. These are bounds candidates,
not exact triangle intersections. Runtime source and issue metadata restored after experiments.

Offline bake decode following `ShowcaseWorldBakeCodec` and `SemanticRegionSnapshotCodec`:
v3,seed1592594996,radius8,199 regions; mountain center region(-2,0,0) is present. At x=-600,z=200,
y220 contains material13, but y250/300/350/400/450/480 are uniform air. Current mountain frustum
spans these central heights. This supports stale baked content as a prerequisite to near/far handoff;
next verify with production Storage and catalogue evaluation before rebuilding. Do not simply
suppress the proxy and thereby hide a missing mountain. Diagnostic JSON, snapshots and patches
are under `Artifacts/LocalGpuShowcase/`; all these runs remain excluded from visual/performance acceptance.

Startup bake repair on4a80ddb57: production Storage regression restores region(-2,0,0),
verifies its semantic hash, and fails at current mountain center(-600,358,200). Existing
`ShowcaseWorldBaker.BakeShowcaseWorld` regenerates199 regions in144s, peak10598MB, no swap
growth; initial6GB-guard attempt was killed before writing. Harness-standard12GB process
ceiling retained8GB free floor and512MB swap-growth guard; production budgets unchanged.
`mountain-rebake/acceptance-after.xml`: both mountain production tests pass after regeneration,
including the previously failing occupancy assertion. `mountain-rebake-showcase/` normal180s
completed with12 captures, no transaction rejections/exceptions. Reviewed15s/60s/150s remains
**unacceptable**: terrain bands, coarse side structures, huge flat traversal foreground.
Stationary missingVisible reached0; traversal reopened missing coverage and closed terrain hole.
`mountain-rebake-probe/`65s completed11 captures. Reviewed25s/35s/45s confirms detailed dark
mountain and roofed houses appear while semantic proxies are suppressed, then grey mountain and
coarse blocks cover them again after restoration. Original visibility and issue metadata restored.
Next G08 work: bounded per-object publication handoff, retaining proxies until their detailed
replacement is ready and restoring on eviction/invalidation. Diagnostic suppression is not acceptance.

G08 handoff is now connected locally: Showcase far features submit in the voxel render pass
using the same camera's selected near draw set. `SurfaceDiscoveryCoverage` retains512-bit
completed surface-discovery masks per resident region (hard cap1024; overflow remains unknown).
Unknown/partial discovery, invalidation and eviction retain/restore proxies. Regional changes
rescan through the existing bounded job pipeline; no GPU output affects authoritative state.
All14 final domain/presentation/lifecycle tests pass (`replacement-final-lifecycle-tests.xml`,16s). The final48s module (`replacement-module-final/`) completed
with8 captures and full two-instance replacement, edit restoration, restart and zero GPU error
counters. Reviewed42s shows the real WorldBuilder landmark. Normal180s Showcase completed12
captures/no transaction errors;60s mountain replacement improves, but side blocks/150s traversal
obstructions remain **unacceptable**. Diagnostic CPU window p50 medians6.6/10.2ms are not acceptance.
The65s owner probe completed11 captures; original visibility/issue metadata restored. At60s the
remaining proxies are mountainCC9F50C170E9C507 and summit placeholderE1FB25FC632AB90C.
Wider-view screenshot64.9s shows ramp-shaped source primitives rendered as large walls;
`BuildGeometryMesh` defaults Ramp and most shapes to `AppendBox`. Canonical proxy ramp geometry
is now a proven next G08 defect. `RampContains` uses `Primitive.Direction`, which the
far geometry contract currently does not carry for ramps (only frusta resolve direction).
Bake-directory inspection also proves summit upper region(-2,1,0) absent after regeneration;
the only y!=0 records are(0,1,0)/(0,1,1). Add a production summit occupancy regression and
repair generic feature vertical baking/residency; do not cull its proxy over absent content. Also audit traversal discovery rescan invalidation and global
change-feed gating; they can restore unrelated proxies. Existing module/tableau fidelity notes
remain: production near/far realization is tested through the SolidGpu module's real catalogue.

## 1. Get the actual GPU path running now

- [ ] **G01 — Reconcile the retained production GPU implementation.** Inspect current code and historical merge parent `a0ac0f5e...`; retain compatible proven changes rather than blindly restoring old files. Trace the real scheduler -> mirror -> extraction -> page publication -> URP draw route. Record concrete compile/runtime blockers and fix only prerequisites to this route. Do not begin another CPU visual-polish cycle.
- [ ] **G02 — Make GPU validation genuinely GPU-enabled.** Audit `GpuSurfaceProductionPolicy`, `VOXEL_DISABLE_GPU_CUTOVER`, player-capture scripts, persistent test runner, module validation and scene bootstrap. Remove contradictory CPU-forcing for GPU proof; fail the proof explicitly on unsupported capability or CPU takeover. The final migration removes the obsolete switch entirely (G18). Keep ordinary production scene composition, rendering and effects. Identify each affected `.asmdef` and its owned tests/validation scene before implementation.
- [ ] **G03 — Prove the restored module path on an exact SHA.** Restore/update the existing Rendering-owned GPU validation scene under `Assets/VoxelEngine/Rendering/Validation/` and its executable scenario. Exercise canonical storage/materials and the production GPU renderer, first for the bounded fixture and then multiple chunks. Show GPU dispatches, successful publications and live visible GPU-owned geometry, not just nonzero requests. Require zero CPU extraction/fallback for the tested workload. Retain the real player log and images separately per scene/scenario.
- [ ] **G04 — Capture and profile full GPU VoxelShowcase immediately.** On the same source as G03, replay the complete scene with fixed seed, camera/route, resolution and quality. Keep castle, mountain, town, water, vegetation, far world and normal updates enabled. Capture startup and settled views, label defects shared versus GPU-only, and report the first measured full-frame distribution, GPU coverage and fallback counts. An imperfect GPU image is useful diagnosis, not completion. Do not wait for all later correctness tasks before obtaining this evidence.

## 2. Complete geometry and visual correctness with GPU active

- [ ] **G05 — Preserve an independent semantic oracle without retaining a second renderer.** Before deleting the CPU backend, record bounded canonical input/output fixtures, semantic expectations and provenance for supported surfaces. During transition compare the same prepared inputs, density, count/prefix and pre-page geometry. Convert essential regressions to frozen reference data and independent canonical/property checks; final tests must not require the retired CPU mesher or embed a copy of it. Generated geometry and extraction-count readback are test-only. G10–G13 permit only a bounded asynchronous status/handle/generation channel for render control; never block or derive authoritative state from it. **In progress:** the existing real-kernel Planar/Sharp/Cubic half-brick regression now checks analytic boundary positions, indexed winding, complementary unit-face coverage and duplicate triangles without invoking CPU meshing. Local Metal execution passed all three styles in `Artifacts/LocalGpuShowcase/753a21241-local-harness/gpu-regressions.xml` on 2026-09-06; this does not complete the remaining smooth/coating/transition oracle migration.
- [ ] **G06 — Restore all supported reconstruction/material semantics.** Validate smooth, rounded, planar, sharp and cubic/faceted surfaces; material classification, coatings, authored boundaries, decoration/profile handling and mixed/uniform/empty inputs. First identify the earliest divergence, then add fail-before/pass-after behavioral proof. GPU parity does not bless defects already present in shared authored data or presentation.
- [ ] **G07 — Eliminate CPU-dependent LOD coverage.** Current `CpuTransvoxelChunkCache.SupportsGpuSurfaceStep` admits only steps 1/2; step 4 uses feature-preserving CPU fallback and step 8 block HLOD. Inventory every ring/representation actually used by VoxelShowcase and affected consumers, including coarser mip work. Implement the required GPU equivalents before deleting those paths. Test real mixed-LOD batches, logical extent versus physical stride, transition faces, negative-shell ownership and nonresident frontier halos. Do not disable coarse rings or reduce draw distance to claim GPU-only coverage.
Summit residency follow-up: production Storage regression reproduced missing upper region
before the fix (2 passed/1 failed). Shared terrain-column residency now includes finite explicit
CPU catalogue footprints; the production baker emits200 regions instead of199 (147s,
peak8771MB under the existing harness12GB guard). Both module-local runtime queue boundary
checks and all3 production landmark checks pass. New source/player evidence is under
`Artifacts/LocalGpuShowcase/summit-showcase/`. This does not close G08: add a focused
Showcase-owned streaming player scene, audit composed-child bounds and vertically separated
features against the existing per-column cap, then resolve remaining far shape/coverage defects.

Far ramp shape correction: signed direction and canonical run-cell count now survive the
adapter. Rendering emits a constant10-vertex closed profile instead of an AABB. Simple wedges
failed2 steep occupancy comparisons; the cell-centre profile passes those plus shallow,
negative-axis, vertical/one-cell, frustum and consumer tests (21/21). The new production
WorldBuilder catalogue consumer `FarGeneratedLandmarkValidation` passed28s/7 captures;
Showcase-authored environment/sun replaced the initial unreadable fixture lighting. Reviewed
12s shows sloped switchbacks, but coarse massing, support forms and material separation remain
unacceptable. Artifacts: `ramp-final-tests.xml`, `ramp-module-final/`, `ramp-showcase/`.
Normal180s Showcase completed12 captures/no transaction errors; reviewed149.9s retains large
flat traversal obstructions. Composition-owned scene coverage and the other G08 defects remain open.

- [ ] **G08 — Resolve every missing/white/malformed GPU-visible region.** Use chunk/source revision and actual draw owner to separate voxel surfaces, far terrain and semantic far features. Preserve canonical shape parameters, materials/coatings and ordered carve semantics where required; resolve near/far overlap with correct coverage ownership, not distance-only hiding or global convergence. Reuse existing production systems. The existing bounded normal/disabled/restored probe is optional diagnosis, with restoration/opt-in regressions; disabled output never passes acceptance. Retain frustum topology/cache regressions and independent FarWorld artifacts.
- [ ] **G09 — Accept complete GPU visuals during real use.** Inspect exact-source stationary, multiple-angle, traversal and edit captures for silhouettes, grounding, materials, seams, holes, stale chunks, near/far handoff and blockout appearance. Explain any startup incompleteness and measure convergence. All final views must meet the repository's production-quality bar without CPU fallback or removed scene content. VoxelShowcase is the visual target; do not take over another SceneIssue.

Traversal obstruction attribution: added repeatable literal runtime arguments to the existing
standalone harness and a diagnostic-only `farfeatures` subsystem switch. Normal content settings
are unchanged. `obstruction-no-farterrain-diagnostic/` completed180s/12 captures, exit0 with the
required disable log and no exceptions. Reviewed75s confirms distant clipmap hills disappear;
reviewed150s retains the near-identical dark/green obstruction. This falsifies far terrain as its
direct source. `obstruction-no-farfeatures-diagnostic/` also completed180s/12 captures, exit0.
Reviewed120s/150s no longer show the flat obstruction with terrain active, attributing it to semantic
far rendering. Terrain aliasing and near gaps remain. `obstruction-full-handoff-trace/` restores all
content, completed180s/12 captures/exit0, and reviewed150s reproduces the obstruction. Existing
trace identifies mountain CC9F50C170E9C507 at90–110s but truncates the larger150s retained set
to four entries. Exact obstructing primitive remains unproven. Next diagnostic must report nearest
camera-ray/proxy intersections, distinguishing geometry, transform/submission and handoff causes.
These subsystem-isolation runs are excluded from visual/performance acceptance. Harness changes
provide orchestration only; no production geometry or replacement validation scene is introduced.

## 3. Make publication, residency and lifetime reliable

- [ ] **G10 — Make completion and publication explicit and two-phase.** Pending Allocate/Write may become live only through successful Commit; `Exhausted`, `Stale`, `TooLarge`, cancellation and failed writes Abort exactly once. Preserve prior live geometry until a valid replacement commits. Separate renderer build generation from storage/mirror source generation. Retest deterministic duplicate-command coalescing and cancel/release/reacquire behavior.
Queued cancellation follow-up: two host-lifecycle regressions proved that Release/Dispose
left descriptors queued, including a disposed prefix extractor/tables. Release/retry now revoke
unsubmitted descriptors, compact onto a surviving owner, reject stale admission and prune stale
submission tokens. All8 cancellation/coordinator/prepared-GPU tests pass, including5 focused
queue regressions. The production48s module passed edit/handoff/restart with8 captures and zero
fallback/failure counters; reviewed42s shows stable geometry. Evidence: `queued-cancel-final.xml`,
`queued-cancel-module/`, `queued-cancel-showcase/`. This fixes unsubmitted ownership only;
submitted resource/mirror leases, world-reset isolation and last-draw retirement remain open.
Normal180s Showcase passed12 captures/no transaction errors; reviewed150s still has the large
flat traversal obstruction. No visual or final performance acceptance.

Submitted-resource ownership follow-up: a per-resource reference count now separates
logical disposal from physical release for mirror, extractor, lookup tables and page arena.
Each submitted lane retains those exact owners and independent footprint/region readers until
its ordered callback, including retired worlds and exceptional submission. Callback world epochs
prevent old reader decrements from changing new-world ownership. All19 focused tests pass
(`submitted-lifetime-reader-ownership.xml`), including real GPU teardown, batch-only readers after both contexts release, and eventual allocation release.
`submitted-lifetime-readers.log` records a native Burst compiler crash/no XML, not a pass; the
exception fixture timing correction is retained in the intermediate failed XML. The first module build also crashed in native Burst compilation; its retry passed normal
edit/handoff/restart. Final submission-exception feedback transfers one control-status word
only; it never transfers geometry/counts. Full source-reset isolation, bounded retired-world
pressure and last-draw retirement remain G11 gates. Final48s module passed8 captures and
edit/handoff/restart; final180s Showcase passed12 captures/no transaction errors but still has
large flat traversal obstructions. Evidence is in `submitted-lifetime-module-verified/` and
`submitted-lifetime-showcase/`; no visual or final performance acceptance.

Mirror-reset isolation follow-up: the before-fix test proved that Clear overwrote the real
GPU directory while submitted readers retained it (`mirror-clear-before.xml`, failed).
Clear now waits for all submitted owners, rejects new admission/mutation while pending,
and resumes recovery after the last callback. Context demand/reader leases carry world
epochs, so old cleanup cannot remove new-world ownership. Coverage-invalid queued/completed
requests wake the worker for retry. All24 focused checks pass, including real submission
followed by production PrepareFrame world replacement, history invalidation and old-context
cleanup (`mirror-clear-world-replacement.xml`). The48s production module passed8 captures,
edit/handoff/restart and zero fallback/failure counters (`mirror-clear-module/`). Reviewed42s
has intact production geometry but prototype/blockout composition; no visual acceptance.
Full180s Showcase passed12 captures/no exceptions or transaction rejections (`mirror-clear-showcase/`).
Reviewed75s retains castle detail;150s is nearly fully obscured by huge flat surfaces: **unacceptable**.
Exact source hashes/diff and diagnostic summaries accompany both runs. Retired-world pressure
and last-draw retirement remain open; no final performance acceptance.

- [ ] **G11 — Prove GPU-completion-based lifetime.** Keep source mirror/residency leases and lane-local scratch alive until actual consumption completes. Verify the target Metal fence/capability/`passed` behavior and safe handling when that capability is unavailable. Retire geometry only after draw completion, not a CPU-frame delay. Exercise upload-ring reuse under actual GPU lag, teardown with in-flight work, and repeated renderer/world restart without stale statics, leaks or double ownership.
- [ ] **G12 — Prove bounded pressure and recovery.** Stress page/handle/mirror reuse, directory collisions, negative coordinates, eviction, mixed/uniform/empty publication and tombstones. Force stale generations, full arenas and oversized work; prove reclamation, retry and recovery without deadlock, corruption, permanent holes or CPU takeover. Splitting/backpressure must preserve coverage and budgets rather than silently discarding unsupported work.
- [ ] **G13 — Prove edits and streaming end to end.** Use deterministic real VoxelShowcase traversal and repeated authoritative edits across region/LOD boundaries. Verify current versions converge, stale results never become live, old geometry disappears correctly, and new geometry replaces it without cracks/remnants. Preserve authoritative gameplay and tick behavior; no blocking GPU readback/wait introduced into the frame path.

## 4. Physically remove the obsolete CPU rendering backend

Deletion is a required deliverable, not an optional cleanup after closure. A file name is not a dependency analysis: `CpuTransvoxelChunkCache` currently imports GPU code and owns shared responsibilities too. Move only genuinely shared responsibilities to appropriate existing/common boundaries before removing the obsolete implementation.

- [ ] **G14 — Inventory every CPU-only file and consumer.** Record path, responsibility, callers, assembly/assets references, replacement and delete/retain rationale in a compact removal ledger beside this issue. Start with solid/water CPU caches, CPU Transvoxel mesh jobs/workspaces, CPU topology/mesh upload paths, fallback selection, flags, tests, benchmark helpers and associated `.meta`/assets. Trace production, editor/baking, validation and independent consumers, not just the main scene. Include CPU-only coarse LOD and required water-surface extraction in the migration; do not quietly exclude them.
- [ ] **G15 — Port remaining required behavior before deletion.** Move any still-required surface extraction onto the GPU path and migrate consumers. Preserve authoritative voxel bake/generation, collision and simulation. Preserve genuinely shared material/shape/lookup contracts and the CPU host orchestration needed to submit GPU work. Independent water/vegetation/far-presentation systems used by the GPU scene must stay functional; their legacy CPU-backend dependencies must be removed, not their visible content. No CPU triangle extraction hidden inside a renamed GPU wrapper.
- [ ] **G16 — Delete all files used solely by the CPU rendering path.** Remove their implementations, CPU-only tests/fixtures/assets, `.meta` files, serialized and assembly references, obsolete scripts/configuration and dead branches. Remove CPU-specific sections of mixed files once their shared responsibilities are migrated. Do not keep a disabled backend, compatibility shim, source copy under Tests, archive or alternate scene. Source-control history is the recovery mechanism; independent golden data/canonical semantic checks are not a retained renderer.
- [ ] **G17 — Prove supported coverage and unsupported-device behavior without fallback.** Default production VoxelShowcase and an independent production consumer must use the same GPU backend for all migrated voxel-surface work. Unsupported capabilities fail clearly; a supported material/LOD cannot be relabeled unsupported just to omit it. Preserve supported content/device obligations or record a genuine blocker. No automatic or emergency CPU renderer remains.
- [ ] **G18 — Audit and test the CPU-backend-free build.** Remove CPU-force/experimental compatibility controls and migrate tests that relied on them. Check references/GUIDs/dead code, compile and run affected editor/bake workflows, all repository-derived module players and the real integration consumer. Inspect artifact content, not only counts. Finish the removal ledger with zero unexplained retained CPU-only files and behavioral proof that no render route invokes the deleted backend. A source-string test alone is insufficient.

## 5. Drive actual whole-frame performance toward 1,000 FPS

### Locked benchmark contract

The reference GPU is the **Apple M4 Max / Metal** already identified in the VoxelShowcase player log. Record actual RAM, GPU configuration, macOS, Unity version, power/thermal state and player build settings rather than guessing them. Initial primary resolution is **1920 x 1080, render scale 1.0**, chosen here because the user did not specify resolution; also report the normal/native-resolution result separately. Lock these settings before comparisons, never change them retrospectively to meet the target.

Use a visible standalone non-development player for the primary performance result, same source/content/quality as visual proof. Record the effective frame cap and presentation behavior; request uncapped rendering/VSync off where supported. No minimized window, empty camera, frozen simulation, disabled streaming/effects, lower geometry density, shorter distance or reduced resolution as an unlabelled speedup. Readback, screenshot I/O, verbose probes and deep profiling are excluded from timing windows, not from the required separate visual/correctness runs. Retain lightweight counters and matching before/after captures. Instrumented diagnostic builds are supplementary, with overhead disclosed.

For **each** fixed workload — fully settled stationary full-scene view, deterministic warm traversal, cold/frontier streaming traversal, and repeated edits/recovery — obtain at least **three 60-second measured runs**. Warm the initial scene/shaders for at least 30 seconds and wait for real coverage readiness; report time-to-readiness separately. Frontier/new-work costs inside traversal/edit workloads remain included. Record startup/convergence separately, not as missing data.

Target **>=1,000 rendered frames/second and whole-frame p95 <=1.00 ms** in the measured workloads; publish each workload separately, including p50/p95/p99/max and worst spikes. FPS is frames divided by measured wall-clock duration, not an average of instantaneous FPS and not reciprocal GPU-extraction time. Count real rendered frames and corroborate GPU execution/completion; CPU update ticks or queued empty frames are not rendering throughput. Record presentation/compositor limits explicitly. Uncapped/offscreen diagnostic throughput is not presented as visible-player FPS. Unavailable GPU timing is `unavailable`, never zero or a fabricated success.

Existing device-matrix frame, main-thread streaming, memory, simulation and latency requirements remain hard constraints. The 1.00 ms goal adds an aggressive scene target; it does not authorize changing shared budgets or gameplay. Below-target completion must identify the target as missed and provide the closest repeatable result, bottleneck evidence, tested alternatives and remaining limits; it cannot assert a mathematically proven optimum or declare success after an arbitrary number of attempts.

- [ ] **G19 — Establish trusted timing and GPU attribution starting at G04.** Fix zero-sample timing, identify CPU main/render thread, GPU frame/passes, present wait, extraction, upload/submission and synchronization costs. Verify measurement overhead/caps, all expected visible coverage, zero CPU fallback and no continuing idle rebuild churn. Store source/configuration identity, raw distributions and sample counts with the first GPU baseline.
- [ ] **G20 — Optimize the largest measured whole-frame bottleneck.** Prioritize based on measured critical path, not on a predetermined list of rewrites. Candidates include CPU submission/GC/allocations, repeated preparation/uploads, batching/indirect draws, visibility reuse/culling, URP pass organization, GPU extraction, shader bandwidth/overdraw, shadows, water/vegetation/far presentation and present/driver stalls. Change render-pipeline integration only when measurement or correctness requires it; preserve the production stack and visuals. Re-measure after each material change.
- [ ] **G21 — Measure settled and moving workloads without cheats.** Apply the locked benchmark to stationary, traversal, frontier streaming and edits; preserve p95/p99 budgets, visible detail and responsiveness. Confirm quiescent scenes stop redundant work while real updates remain active. Compare exact-source visual captures around every material optimization and reject speed obtained from missing geometry, stalls moved out of counters, stale output or hidden fallback.
- [ ] **G22 — Bound memory, traffic and long-session behavior.** Report mirror, source pins, scratch lanes, pages, visible handles, upload bytes/calls, allocator peaks and reuse pressure versus the device matrix. Check steady-state growth and the required two-hour memory-flatness criterion (within +/-2%). Use the repository's supported long-run evidence path; do not enlarge a five-minute targeted test timeout or add an ad-hoc workflow. An unavailable long-run path is a recorded evidence blocker, not permission to claim a pass.
- [ ] **G23 — Produce an honest closest-achieved performance result.** Report the complete repeated workload table and improvements versus G19. If below 1,000 FPS, identify the measured limiting stages and remaining gap, test justified in-scope alternatives, retain the best nonregressing implementation, and document why remaining proposals are blocked or trade away required quality/correctness/budgets. Do not invent a lower success target, call 60 FPS equivalent to this goal, or claim no further improvement is possible without evidence.

## 6. Final verification and promotion

- [ ] **G24 — Re-run final regressions and retain all required artifacts.** On the exact CPU-backend-free feature source, run Rendering-owned EditMode tests, only specifically required PlayMode tests, repository-derived affected-module validation and module-local production scenes. Verify unique retained output for FarWorld, Water, GPU validation and every other required scene; zero-match/skipped/missing evidence is failure. Preserve canonical regression coverage without the deleted CPU implementation.
- [ ] **G25 — Prove full application and independent reuse.** Pass canonical standalone `KentridgePlayableSlice` and the independent production GPU consumer without duplicate enabling/render logic, while keeping VoxelShowcase as this issue's visual/performance target. Recheck lifecycle, shader availability and supported capability handling after deletion. No takeover or modification of other SceneIssue records.
- [ ] **G26 — Review final diff, visuals, removal and performance together.** Remove investigation-only controls, bound retained diagnostics, and review actual standalone images, logs, raw timing and memory evidence. Confirm all obligations below are satisfied on compatible exact source revisions. No CPU-force gate, silent fallback, scene-local replacement renderer or unexplained CPU-only file remains. Produce a resolution with both achieved FPS and any shortfall explicit.
- [ ] **G27 — Close and promote only after all pre-closure work passes.** Once G01–G26 and amended issue acceptance are satisfied, move only this issue directly `open/` -> `closed/`, set fixed/resolved metadata with verified evidence, and commit on `fixes/agent-1`. Merge current master as required, open/update the final PR and enable auto-merge immediately. Fix required `affected`-gate failures, preserve exact-source validation after material integration changes, and verify the PR merged and closed issue exists on `origin/master` before checking G27. Never push the feature head directly to master.

## Superseded-task reconciliation

This replaces the old checklist, not its unfulfilled correctness obligations. Historical evidence remains in the existing evidence files and Git history; old checked items are not silently treated as final passes.

| Previous obligation | New destination |
| --- | --- |
| CPU0A/0B/CPU1/CPU3 and 001–018, 020–023, 027B historical proof | Retained evidence above; current reruns G03, G05–G07, G10–G13, G24 |
| CPU2/2A/4/4F draw owner, shared geometry/material and taper | G08/G09; no CPU-first prerequisite |
| CPU4E unique player evidence | G03/G18/G24; preserve queued repair result |
| CPU5 perfect-CPU gate; CPU6 delayed GPU restoration | Gate explicitly withdrawn by user; final visual quality in G09, immediate restoration in G01–G04 |
| 019 and 019A/B/C/D/E/G/H GPU diagnosis/fixtures/parity | G01–G09 |
| 019F, 024–029B allocation, fences, pressure, mixed LOD | G07, G10–G13, G19, G22 |
| 030/031/033/034/035 default cutover, reuse, restart | G02–G04, G11, G17/G18/G25 |
| 032 permanent explicit CPU fallback | Superseded by user-authorized GPU-only capability handling and deletion, G14–G18 |
| 040–043 including 041A scene/traversal/edit/GPU proof | G03/G04/G07–G09/G13/G24/G25 |
| 044–046 performance and memory | G19–G23 and locked 1.00 ms benchmark |
| 050–057 final tests, cleanup, audit, close/merge | G16/G18/G24–G27 |

**This rewrite does not mark GPU restoration, CPU deletion, visual acceptance or 1,000 FPS complete.**
