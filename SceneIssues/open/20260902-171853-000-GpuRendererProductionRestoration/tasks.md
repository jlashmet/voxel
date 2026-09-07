# GPU-only VoxelShowcase — execution checklist

### 2026-09-06 — Fine GPU source readiness and packed source portions (validation ongoing)

Production fine steps1/2/4 no longer call CPU Covers. GPU source resolution fills the existing dense
cache directly from canonical block-reference metadata and current mixed directory payloads;
only missing mixed indices return for uploads. Count submission prepares bounded descriptor views
without overwriting those entries. Coarse keeps material summaries; both use the same source proof.
Fine whole-footprint demand protects accumulated mixed slots; active readers are held only during
GPU source/count submissions so recovery can upload missing data. Context admission retains the
extraction concurrency count without prematurely locking its missing footprint. Epoch/version
checks still reject edits and retired worlds. Obsolete CPU scan fields and misleading readiness
telemetry were removed. Legacy Covers exists only for its remaining direct behavioral tests.

Pre-submission cancellation resets preparation only for the matching lane, then preserves/rebinds
surviving owners. Submitted source portions remain immutable through callback. Existing count-stage
lifetime tests now complete real GPU source preparation before testing count disposal/errors.

`gpu-fine-readiness.xml`:476/482 pass; six failures exposed immediate-count assumptions and
preparation marking queued work immutable before Storage was available.
`gpu-fine-readiness-lifetime.xml`:482/482 pass19s after lifecycle correction.
`gpu-fine-readiness-production.xml`:487/487 pass20s, adding realStorage mixed-geometry publication
at steps1/2/4 plus fine source edit rejection and in-flight retirement.
`gpu-fine-readiness-final.xml`:488/488 pass20s including direct packed dense-entry/offset/pending
regression. `gpu-fine-readiness-module`:28s build,48s/seven captures,exit0; all markers,zero missing/
errors/finalizers,frame p95/p99 .819/.914ms,262.4MB. This is the pre-packing checkpoint.

Inspection found one-Z-plane portions underfilled fine GPU submissions. Pack multiple complete XY
planes while retaining1024 source and64 metadata-row limits. Existing128KiB GPU/16KiB CPU buffers
remain unchanged; relative Y/Z indexing spans up to eight regions. Typical nearest edge10 cube
uses two portions instead of ten. `gpu-fine-readiness-packed.xml`:488/488 pass20s, including real
negative halo/region crossings, fine geometry and coarse4096-mixed-source pressure. Packed-source
`gpu-fine-readiness-packed-module`:28s build,48s/seven captures,exit0; all lifecycle/edit/far
markers,zero missing/errors/finalizers. Frame p95/p99 .817/.918ms;prepare .027/.028ms;262.4MB.
Reviewed42s: fort intact, prototype/blockout composition.
`gpu-fine-readiness-packed-showcase`:18s build,180s/12 captures,exit0; source hashes match.
Stationary60–90s30 samples223.72 FPS,CPU3.89ms,GPU diagnostic5.085ms. Walking120–180s60
samples183.78 FPS,CPU5.245ms,GPU2.17ms (previous222/134 FPS,CPU3.905/7.32ms).
Final CPU coverage polls/rounds0;directory insertion failures0;geometry allocation failures and
evictions0 (was259). Recovery publications8.63M→347,749;host-ready entries894,513→40,270.
Mixed slots still40,270/40,270,307,479 no-slot attempts;oldest coarse21.17s and GPU step8
publications37→13. Missing-visible442→456. This is a source/CPU improvement with remaining
throughput/coverage failures, not400FPS or complete-scene acceptance. Reviewed74.9s castle and
149.9s terrain: castle retained, terrain seams/flat far/vegetation finish remain unacceptable.

Next evidence-backed audit: BeginPersistentStage registers full fine demand BEFORE TryBeginExtraction
can reject admission. Waiting contexts can therefore protect slots despite not being admitted.
Move demand ownership behind successful admission and verify rejected admissions retain nothing;
then determine whether admitted fine footprints need bounded mixed-slot arbitration. Do not solve
pressure by enlarging memory or hiding content. Walking visibility still~1.73ms CPU traversal,
plus occasional hierarchy reconstruction; migrate camera-dependent selection next. Static frame
rate barely changed despite source recovery often0ms, so profile GPU draw cost too: current path
issues128 procedural indirect buckets and fetches paged indices/vertices in the shader. True indexed
draw/vertex reuse is a hypothesis to measure, not a verified cause or selected rewrite yet.


### 2026-09-06 — GPU canonical uniform-block decoding (validation ongoing)

Storage RegionReadView now exposes bounded copy of canonical encoded block references without
per-brick transformation: -1 air, negative uniform(-material-1), nonnegative mixed identifier.
Destination owns its copy; range errors are nonmutating. GPU coarse source resolution decodes
uniform material and excludes water directly, overrides stale directory data with current uniform
metadata, and looks up only mixed payloads. Storage mixed addresses are never GPU addresses.
Host copies only intersected Y rows in one Z slice per region after obtaining a submission slot,
validates borrowed version, and retains buffer/source lifetime. CPU16KiB/GPU128KiB bounded scratch
per lane replaces occupancy scratch. Mirror/arena capacity and submission allowance unchanged.
Storage API is headless data-copy behavior: module-local Storage EditMode coverage is appropriate;
Rendering's existing production validation scene and Showcase exercise its runtime consumer.

`gpu-canonical-blocks.log`: initial test compilation failed because NativeArray using variables
were modified; fixed through mutable aliases. `gpu-canonical-blocks-final.xml`:549/562 passed;
13 failures traced to HLSL reserved identifier `uniform`, renamed `sourceUniform`.
`gpu-canonical-blocks-verified.xml`:562/562 passed20s (482 Rendering,80 Storage),zero skipped.
New tests prove copy encoding/end-of-region/invalid-range isolation; realGPU negative/region
boundaries, uniform override of pending stale material, water exclusion, mixed readiness and
clearing to air. Existing realStorage edit/pressure/retirement tests pass with the new production
source route.
`gpu-canonical-blocks-module`:31s build,48s/seven captures,exit0; all edit/restart/far markers,
zero missing/fallback/errors/finalizers. Frame p95/p99 .818/.892ms;prepare .027/.027ms;261.1MB.
Reviewed42s: fort intact, prototype/blockout composition.
`gpu-canonical-blocks-showcase`:18s build,180s/12 captures,exit0. Source hashes match final source.
Stationary60–90s30 samples222.15 FPS,CPU3.905ms,GPU diagnostic5.075ms. Walking120–180s59
samples133.99 FPS,CPU7.32ms,GPU1.73ms. Final442 missing-visible,259 allocation failures/evictions.
Reviewed74.9s castle and149.9s terrain: castle retained; terrain seams/far/vegetation unacceptable.
No400FPS or coverage acceptance. Directory refusals are zero early but end82,993 (was175,837);
8.63M recovery publications (was11.56M),165,481 mixed-slot refusals,40,270/40,270 mixed slots.
Coarse GPU publications32→37. Fine CPU coverage still causes per-air/uniform handling; final
step4 request has536 polls,4.07s age. Remaining walking visibility traversal~1.9ms plus periodic
~1.3ms CPU hierarchy rebuild. Next migrate fine source readiness into GPU dense-cache preparation,
retaining full fine-source demand to protect mixed slots and ordered readers only while GPU uses
them; do not pin missing blocks against recovery. Preserve version/epoch rejection across portions.
This is a functional checkpoint, not final performance/visual acceptance. Local only; no push.


### 2026-09-06 — GPU occupancy readiness removes per-empty directory records (validation ongoing)

The previous checkpoint's explicit empty keys saturated the1,048,576-entry directory at786,431
ready keys,290,724 refused insertions and12.27M recovery publications. Production now uses
Storage's canonical `TryCopyBlockSummary` bitmap: copy only the source portion's512B Z slice per
intersected region to GPU. GPU recognizes air directly; occupied sources still require current
non-pending uploaded payloads. Optional nonresident halo remains air. No CPU per-brick bitmap
expansion and no geometry/material readback. Production mirror again omits empty keys; fine-step
CPU coverage keeps its existing independent proof until migrated. Queue/frame/arena budgets
unchanged. Lane owns64KiB CPU bitmap scratch plus4KiB GPU slice storage, reused and released
with the submission. Copies occur only after acquiring the single submission slot.

`gpu-occupancy-readiness.xml`:480/480 rendering tests pass23s,zero skipped.
`gpu-occupancy-readiness-final.xml`:480/480 pass19s after slot/lifetime refinement. New realGPU
cases exercise negative coordinates,32-bit word and64-brick region boundaries, stale payloads
over canonical air, occupied missing sources, pending air-to-solid edits and cleared occupancy.
Existing realStorage step8 summary/edit/4096-source pressure tests use a mirror without empty
keys. In-flight world retirement now checks occupancy buffer retention and eventual release.
`gpu-occupancy-readiness-module`:28s build,48s/seven captures,exit0; all edit/lifecycle/far markers,
zero missing/errors/finalizers. Frame p95/p99 .818/.958ms;prepare .027/.040ms;261.1MB allocated.
Reviewed42s fort: production systems intact, prototype/blockout composition.
`gpu-occupancy-readiness-showcase`:18s build,180s/12 captures,exit0; source hashes match.
Stationary60–90s30 samples204.57 FPS,CPU4.095ms,GPU diagnostic5.045ms. Walking120–180s59
samples132.11 FPS,CPU7.58ms,GPU1.98ms. Final376 missing-visible,416 allocation failures/evictions
(up from99 as more sources complete); GPU step8 publications12→32. Reviewed74.8s castle and
149.9s terrain: castle retained, terrain seams/far/vegetation finish remains unacceptable.

Occupancy improves stationary180→205 FPS in single runs, but does not achieve400 FPS or eliminate
source pressure:175,837 refused directory insertions remain (down from290,724),11.56M recovery
publications,824,583 host-ready keys. Empty keys are absent in production; uniform solid terrain
still requires individual directory records. Next expose/copy canonical encoded block metadata
through RegionReadView so GPU decodes uniform material directly and asks mirror only for mixed
payloads. Existing encoding is -1 air, negative uniform(-material-1), nonnegative mixed; CPU must
only copy, never per-brick transform. Do not use fully-solid occupancy alone as a solid-material
substitute: Storage occupancy includes nonempty water. Fine GPU coverage and camera-driven
CPU visibility/band traversal plus hierarchy rebuild remain major migrations after source pressure.
All changes remain local/uncommitted, performance/coverage failures explicitly open.


### 2026-09-06 — GPU source readiness and cross-brick coarse face merging (local, validation ongoing)

Step8 source coordinate expansion, lookup/readiness and summaries now execute on GPU. Retain
explicit known-air directory keys and pending-upload bits; bounded missing-source readback only
requests canonical CPU uploads. Host retains residency/version approval and world truth. Fine
steps1/2/4 still use CPU coverage. Near-ready submission priority with coarse service after eight
near reservations preserves one existing submission slot. No memory/radius/budget increase.

Readiness validation:473/473 rendering tests; unknown/air/solid, negative regions,990-source/six-region
parity, pending edits, cancellation and reduced-capacity pressure. Module48s/seven captures passed
before final priority correction. Startup after correction restored near462 at9.1s after generation.
Full `gpu-source-readiness-showcase`180s/12 captures exited0 but FAILED performance acceptance:
195/129 FPS stationary/walking,566 missing-visible,1,231 allocation failures and evictions. Reviewed
74.9s: castle retained, terrain/far finish unacceptable. Harness success is not performance success.

Proven geometry inefficiency: coarse GPU merges only4x4 subcell slices within a brick; CPU merges
whole planes. Replace with64x64 subcell tiles across brick boundaries,16KiB shared mask per group,
bounded128-plane dispatches and identical count/write merging. Two adjacent bricks now6 instead
of10 quads; solid3x3x3 brick grid6 rather than54. Long17-brick bars across the clipped tile boundary
emit10 quads in all three orientations, preserving exact bounds, material, surface area and winding.

`gpu-hlod-cross-brick.xml`:454/473 passed;19 failures traced to Metal rejecting buffer-dependent
early return before group barrier. Conditional sampling now reaches the barrier uniformly.
`gpu-hlod-cross-brick-fixed.xml`:473/473 passed18s.
`gpu-hlod-cross-brick-final.xml`:477/477 passed18s, zero skipped, including new tile/3D tests.
`gpu-hlod-cross-brick-module`:29s build,48s/seven captures,exit0; all lifecycle/edit/far markers,
zero missing/fallback/errors/finalizers. Frame p95/p99 .822/.901ms;prepare .027/.029ms;261.1MB.
Reviewed42s: fort intact, prototype/blockout composition, not final art acceptance.
`gpu-hlod-cross-brick-showcase`:18s build,180s/12 captures,exit0. Exact source hashes match final
source; standalone74.9s castle retained,149.9s terrain seams/flat far finish remain unacceptable.
Stationary60–90s:179.62 FPS,CPU5.43ms,GPU diagnostic5.085ms. Walking120–180s:127.45 FPS,
CPU7.805ms,GPU1.75ms. Final486 missing-visible,99 allocation failures/evictions. Merging reduces
allocation churn1,231→99 but does not pass performance or coverage gates. Failures returned late
in traversal after initial zero-failure interval. No FPS gain claim and no400FPS acceptance.

Next proven pressure: explicit empty records fill directory at786,431 ready keys/1,048,576 slots,
with290,724 refused insertions and12,267,629 published recovery records. Replace per-empty source
keys with GPU lookup of canonical Storage occupancy bitmaps (`TryCopyBlockSummary`), preserving
version/pending edit proof and existing memory budget. Then migrate fine-step CPU Covers.
Changes remain local/uncommitted until the source-directory regression is corrected. Startup,
terrain/vegetation/far finish, missing geometry, final FPS and full CPU retirement remain open.


### 2026-09-06 — Prioritize initial nearby discovery for startup performance

User steering makes castle/house time-to-visible and FPS the priority. Confirmed startup skipped
AddImmediateCameraDiscoveryRegions because the clipmap had no previous window; background
resident enumeration admitted distant terrain first. Queue the initial camera region then resident
face/edge/corner neighbours before the background sweep. Share existing invalidation/queue logic;
mark those regions already swept to avoid duplicate initial discovery. Preserve all distant work,
world truth, radius, presentation quality, GPU concurrency and frame budgets.

Validation (`Artifacts/LocalGpuShowcase/`):
- `gpu-startup-priority-before.xml`: both positive/negative-location behavioral tests fail,14s.
  First queued region was distant(+12 X) instead of the actual camera region. Tests exercise
  real Storage and scheduler Prepare; pause only the queue consumer to observe admission order.
- `gpu-startup-priority.xml`:468/468 Rendering EditMode pass,20s, zero skips. Tests also verify
  unchanged-camera/repeated resident sweeps do not duplicate demand; existing motion/edit tests pass.
- `gpu-startup-priority-module`:32s build,48s/seven captures, exit0, all lifecycle/edit/far markers,
  zero missing/fallback/blocking/context errors and zero ComputeBuffer-finalizer warnings. Frame
  p95/p99 .895/.994ms, prepare .031/.046ms, allocated261.1MB (focused module, not Showcase FPS).
  Reviewed42s fort: systems intact; diagnostic composition remains prototype/blockout quality.
- `gpu-startup-priority-showcase`:18s build,45s/30 captures at1s cadence, exit0. Exact production
  sources/hashes/diff retained; `summarize-startup.py` writes `startup-timing.json` comparing with
  `gpu-water-exit-showcase`. CPU generation remains14.9s. First logged publication after generation
  ~5.1→2.1s; previous462-near-resident milestone ~11.1→9.1s. These are one-run coarse1s-log
  milestones, not complete-scene readiness or repeated performance acceptance. Different capture
  cadence makes this a startup diagnostic, not a comparable steady-FPS benchmark.
  Reviewed18.3s(partial castle),24.3s(main facade present) and44.4s(retained castle/roof details),
  plus prior29.9s capture. Visible incremental fill-in persists; terrain/far finish unacceptable.

Remaining performance work: quantify and reduce source recovery/coverage and GPU stage/queue
latency; the initial ordering fix does not remove publication throughput limits or the CPU scene
creation stall. Full current-source stationary/walking repetition and canonical Kentridge integration
remain gates. Previous full-run FPS before this scheduling change was229/142. CPU helper/oracle
retirement remains open but is secondary to time-to-visible now. Local only; no push.

### 2026-09-06 — Delete CPU workspace and fix deferred water release at exit

Deleted TransvoxelBuildWorkspace and its metadata after confirming zero production callers;
removed its two container lifetime checks, stale sizing reflection and source-string allocation
check. CPU summary/geometry regressions remain until their GPU migration; no coverage/budget
assertion was weakened. This deletion removes dead code, not additional live frame work.

The19 shutdown ComputeBuffer warnings match water's six buffers plus its13-buffer page arena.
Normal disposal queues a completion callback, but final player teardown cannot depend on another
player-loop iteration. Keep Application.quitting subscribed through physical release and drain
readbacks only in that exit handler. Regular disposal, scene restart and all frame methods remain
asynchronous. The suspected missing batch HLOD releases were falsified by reading full Dispose;
no speculative batch-lifetime change was made.

Validation (`Artifacts/LocalGpuShowcase/`):
- `gpu-workspace-retirement.xml`:466/466 rendering EditMode passed,27s wrapper, no skips.
  Two new real-buffer tests repeat idle/build-started and already-retired water disposal three
  times, checking all19 handles invalid before returning without another player-loop update.
- `gpu-water-exit-before.xml`: both new cases fail with the exit drain removed (buffers still
  valid),14s wrapper/exit2. Restored exact tested production source before player build.
- `gpu-water-exit-module`:36s build,48s/seven captures, exit0; initial/traversal/edit/restart/
  far-handoff/success markers pass, zero missing/fallback/blocking/context errors. **Zero**
  ComputeBuffer finalizer warnings versus19 previously. Frame p95/p99 5.680/6.821ms;
  preparation .034/.039ms; allocated261.6MB. Reviewed42s built-player screenshot: fort,
  materials and terrain retained; diagnostic composition remains prototype/blockout quality.
- `gpu-water-exit-showcase`:18s build,180s/12 captures, exit0 and zero buffer-finalizer
  warnings. Stationary60–90s30 samples:228.57 FPS, CPU4.290ms, GPU diagnostic3.535ms;
  walking120–180s60 samples:141.75 FPS, CPU6.995ms, GPU1.555ms. Prior238/142; no
  performance improvement claimed. Final315 missing-visible, zero allocation failures/evictions.
  Reviewed74.9s and150s: castle/materials retained; terrain holes and far-content finish remain
  unacceptable. Source cleanup and shutdown fix do not resolve appearance latency.

Latest user steering: startup castle/house fill-in is much slower than the CPU renderer; prioritize
actual time-to-visible and FPS now. Existing log shows first GPU request around15s, first solid
publications around19s, near publications462 by26s while step8 still waits. These coarse log
samples are not an exact per-chunk latency measurement. Next experiment must distinguish source
availability/discovery, admission/coverage and GPU submission/publication waits.

G11 long-session/pressure and other solid retirement/visual gates remain open. Exact production
sources, hashes and diff accompany both new player directories. No push.

### 2026-09-06 — Remove CPU upload arena and contiguous draw route

`GpuSolidChunkCache` replaces the old cache name while preserving its asset GUID. Entries now
retain only GPU publication identity/readiness; CPU staging leases, uploads, buffers and draw
methods are removed. Scheduler no longer allocates the335,544,284-byte CPU geometry arena or
scans CPU upload/lease-pressure queues. GPU geometry capacity remains at its previous three-quarter
share of the configured budget; the released share stays unallocated. No distance/content change.
SmoothSurface and VoxelRenderPass now consume only GPU page tables/indirect buckets; removed
CPU draw metadata buffers, staging arrays, shader branch and per-bucket CPU submission route.
Legacy CPU arena metrics remain zero for capture-schema compatibility, explicitly labelled retired.

Removed seven obsolete editor Kentridge capture utilities (including the wrapper and V2 partials)
that rebuilt CPU renderer output into ad-hoc meshes. Repository executable player gates use
`VoxelEngine.Showcase.Editor.ShowcasePlayerBuild.Build`; the canonical standalone scene/harness
is the integration consumer. CI/editor utility deletion adds no separate runtime module scene.

Validation under `Artifacts/LocalGpuShowcase/`:
- `gpu-draw-retirement-final.xml`:468/468 owned rendering EditMode tests passed in26s, no skips.
  Initial compile caught one stale array-clear reference. Intermediate464/469 failures were stale
  architecture expectations; preserved GPU/shared checks and removed the CPU lease-cap source
  test. Real paged draw scale/raster, lifecycle, coverage, allocation and semantic tests all pass.
- `gpu-draw-retirement-tint.xml`: one PlayMode material-distance test passed in13s. Its diagnostic
  triangle now uses paged vertex/index lookup. No colour-drift tolerance change; not visual proof.
- Two retired CPU-specific pressure fixtures were removed from AsyncGeometryStressTests: Entry
  upload/lease replacement and the CPU soft-lease-cap/byte-upload Showcase workload. GPU
  `GpuPagedPublicationTransactionTests` independently exercises exhaustion→retry, failed writes
  preserving live geometry, abort and pending-candidate pressure. Full GPU pressure-player/long
  workload coverage is still required under G11; these unit tests do not stand in for it.
- `gpu-draw-retirement-module`: build34s,48s/seven captures, exit0; all lifecycle/edit/far markers,
  zero missing/fallback/context errors. CPU arena committed/used/uploaded bytes all0. Settled
  frame p95/p99 6.031/6.574ms; preparation .031/.039ms; allocated memory261.6MB. Reviewed42s:
  fort/materials intact, prototype diagnostic composition.19 pre-existing finalizer warnings remain.
- `gpu-draw-retirement-showcase`: build18s,180s/11 captures, exit0.60–90s stationary30 samples:
  238.11 FPS, CPU p50-window median4.060ms.120–180s walking60 samples:141.71 FPS, CPU7.065ms.
  Previous236/140 FPS/4.095/7.00ms; no significant whole-frame improvement established. GPU
  diagnostic medians2.97/1.48ms, not trusted critical-path attribution. Final330 missing-visible,
  zero allocation failures/evictions;2413 publications,1311 in-band candidates (not GPU draw count).
  Final host traversal1.925ms/recovery1.208ms and step8 request20.9s old remain diagnostic leads.
  Exact production sources/hashes/parent/diff and frame-window summary retained for both players.

Reviewed75s castle and150s traversal against preceding screenshots: castle/roof/materials retained;
procedural hills, sparse vegetation, terrain holes and far finish remain **unacceptable**.
No repeated benchmark, full renderer retirement, production-quality or issue closure claim.
Next delete standalone CPU workspace/jobs/oracles and the transitional GPU-to-CPU arena bridge;
add direct retained-profile GPU suppression/backing proof before retiring that CPU predicate.

### 2026-09-06 — Retire solid worker CPU meshing phases

Removed approximately2,600 lines from the mixed solid cache: CPU snapshot/pin assembly,
density/topology/faceted/HLOD jobs, managed polygon/profile/coating emission, result append and
CPU publication phases. Workers no longer allocate `TransvoxelBuildWorkspace` or their own CPU
lookup tables. Admission, residency, dirty queues, slot/catalogue/source checks and GPU handle
publication remain. Source admission backpressure retains GPU demand; context failure cannot
route into CPU meshing. Deleted the environment fallback startup policy. Existing cache class
name, Entry upload helpers, scheduler CPU arena/draw route and standalone CPU job/oracle files
still remain; this is a retirement checkpoint, not full physical removal.

Evidence in `Artifacts/LocalGpuShowcase/`:
- `gpu-solid-retirement/host-tests-fixed.xml`:53 tests passed in17s, no skips. Initial compile
  caught three host visibility fields removed with an adjacent CPU block; restored before tests.
- Full owned assembly audit `module-tests.xml`:463/484 passed,21 failed. Fourteen failures required
  removed CPU methods/workspace/job wiring; removed those obsolete source-string cases, retaining
  independent runtime GPU coverage below. Removed the old step4 CPU-summary/reflection oracle;
  real `GpuBlockHlodMesherTests.StepFourThinVoxelSurvivesProductionGpuCountAndPagedWrite`, reused
  lane thin→ordinary→air, and fallback decision/profile/error cases already cover that policy.
  Preserved shared Storage writer/pin and scheduler completion assertions. Updated the slot guard
  assertion for its existing compound condition. Two pre-existing stale architecture expectations
  were corrected: completion-only readback is one control word at header+10, and lazy scheduler
  creation uses an explicit null guard. No GPU range/count readback was added.
- `module-tests-migrated.xml`: all469 owned tests passed in22s, no skips. Relevant behavior includes
  GPU coverage invalidation, mirror/submission lifetime, pending publication generation approval,
  paged extraction/transactions, semantic face/coating tests, HLOD and host admission/residency.
  Exact retired source-test names saved in `gpu-solid-retirement/retired-source-tests.txt`.
- `gpu-solid-host-module`: build34s,48s/seven captures, exit0; all initial/traversal/edit/settled/
  restart/far-handoff/success markers. Zero missing/fallback/unsupported/context errors.
  Settled frame p95/p99 5.806/6.411ms, preparation .031/.037ms. Reported totalAllocatedMB at
  success261.6 versus687.3 in prior module, a425.7MB reduction; not a repeated memory benchmark.
  Exact production sources/hashes/diff retained. Reviewed42s: fort/materials intact, prototype
  diagnostic composition. Both this and the previous module log contain19 teardown ComputeBuffer
  finalizer warnings. This pre-existing lifetime defect remains an explicit G11 cleanup gate.

Remaining migration: remove legacy Entry upload/contiguous draw and335MB scheduler arena; rename
host ownership; delete standalone CPU workspace/jobs/oracles after required GPU regression
migration. Add direct GPU retained-profile suppression/backing coverage before retiring its last
CPU predicate oracle. Preserve canonical Storage and shared tables. No fresh Showcase/FPS claim
for this checkpoint; previous236/140 FPS remains the last integration measurement.

### 2026-09-06 — GPU faceted face merging

Regular Planar/Sharp/Cubic extraction now merges equal packed face attributes entirely on GPU.
Each workgroup classifies one 64×64 plane tile with 16 KiB shared scratch; its leader uses the
same greedy rectangle walk for count and write. All immediate, recorded and batch dispatch sites
use plane-group dimensions. Larger grids tile within the same scratch bound. Source occupancy,
material/style/coating identity, normals, winding and existing displacement exclusions remain.

Validation: `gpu-faceted-merge-verified.xml` passed all57 focused tests in20s wrapper time,
no skips. Includes exact flat coverage for all three styles, stripes/checkerboard/through-hole
unit-face coverage, negative coordinates, production prepared batch count/write, coatings,
HLOD, queue lifecycle and geometry arena regressions. Initial Metal indexing/barrier compile
failures were repaired. One terminal native crash was in Burst startup compilation; retry
reached tests. New fixtures required payload-offset and NUnit parameter-type corrections;
failed runs are retained and are not counted as successful validation.

Exact working production sources, hashes, parent SHA and diff retained in each player artifact:
- `gpu-faceted-merge-module`: build31s,48s/seven captures, exit0; all initial/traversal/edit/
  settled/restart/far-handoff/success markers, zero missing/fallback/errors. Settled frame
  p95/p99 6.305/7.020ms. Reviewed42s: fort/coating surfaces intact, prototype diagnostic composition.
- `gpu-faceted-merge-showcase`: build18s,180s/12 captures, exit0. FPSLOG60–90s stationary:
  30 samples,235.90 FPS, CPU p50-window median4.095ms (previous198.49 FPS/4.65ms).
  120–180s walking:59 samples,140.24 FPS, CPU7.00ms (previous141.77/6.855ms).
  GPU diagnostic medians3.12/1.61ms; not trusted whole-frame critical-path attribution.
  Final allocation failures/evictions0/0 versus880/880; missing-visible340 versus631.
  GPU publications2446 versus1174; in-band candidates1267 versus97, not actual GPU draw counts.
  Geometry budgets and draw distances unchanged. This supports excess geometry as a real
  pressure cause; it does not establish improved walking performance or complete coverage.

Compared exact74.9s stationary and149.9s walking screenshots against the preceding checkpoint:
castle silhouette/materials retained, adjacent house roof now resolved. Terrain remains overly
procedural with sparse vegetation, unfinished far content and visible holes; the walking right-side
hole is larger in this capture. Visual quality remains **unacceptable**. No production acceptance
or repeated benchmark claim. G01–G27 remain open.

Next: split the GPU host from retired solid CPU meshing/workspace/upload ownership, preserve
source/version/publication invariants, then delete retired implementation and its unused arena.
Moving-camera candidate/source preparation and coarse publication latency still require work.
The final run retains12 in-flight requests and a step8 request about19.5s old; removing geometry
pressure alone has not solved streaming coverage. Keep canonical CPU storage/generation/collision.

### 2026-09-06 — GPU frustum/LOD ownership and persistent candidates

User explicitly resumed the planned CPU-to-GPU migration; alternate renderer research is deferred.
`GpuSurfaceDrawCompact` now classifies padded candidate bounds against the camera frustum,
reduces all-eight-child completion bottom-up, preserves drawable coarse fallback when descendants
are proof-only, selects a non-overlapping physical LOD set, then compacts its own selected-handle
mask into indirect draw buckets. Final solid selection requires no CPU result/count readback and
no per-frame CPU handle upload. CPU source demand, missing/stale urgency, ring ownership and
far-feature publication proof remain. GPU candidate inputs reuse exact camera/planes/scale,
slot membership, demand/readiness and ring settings; publications/empty completion advance
readiness revision, and same-count toroidal replacement advances membership revision.

Focused tests: initial Metal compilation failed on ternary vector indexing; corrected bounded
indices passed9 tests. The first module player failed initial convergence because far handoff
queried the retired CPU selected set. Conservative current-publication proof repaired this;
`gpu-lod-handoff-module` passed48s/seven captures including edits/restart/far restoration.
Camera/projection/frustum tests passed27; final `gpu-persistent-candidates.xml` passed29 in23s,
including real GPU four-level randomized ownership, negative coordinates, partial/completed/edited
children, proof-only fallback, camera and projection changes, indirect raster/prefix/scatter and
same-count slot replacement. No skipped tests or zero-test success claimed.

Final source evidence under `Artifacts/LocalGpuShowcase/` (working diff atop `29703c338`, exact
production hashes, source copies and diffs retained per run):
- `gpu-frustum-lod-showcase`:180s/12 captures/exit0; approximately166/141 FPS stationary/walking,
  CPU6.00/7.005ms, visibility traversal1.334/1.859ms.
- `gpu-persistent-candidates-module`: build32s,48s/seven captures/exit0; all initial/traversal/edit/
  settled/restart/far-handoff/success markers, zero missing/fallback/errors. Settled frame p95/p99
  6.459/7.222ms; preparation p95/p99 .032/.035ms. Reviewed42s; production-rendered diagnostic fort
  remains intact, prototype composition rather than production-art acceptance.
- `gpu-persistent-candidates-showcase`: build18s,180s/12 captures/exit0;60–90s stationary and120–180s
  walking windows contain30/60 one-second samples. Approximate198.49/141.77 FPS; CPU p50-window
  medians4.65/6.855ms. Stationary traversal median0ms, walking1.791ms. Input transport .041/.030ms
  medians, with rebuild spikes still present. GPU reported6.31/1.58ms is diagnostic and not yet
  trustworthy frame-critical attribution. Final631 missing-visible,880 allocation failures and880
  evictions. Source hashes verified against the final working production files.

Reviewed final74.9s stationary and149.9s walking, compared with prior stationary74.9s: castle is
retained; procedural terrain, holes/sparse vegetation and far-proxy/blank-house finish remain
**unacceptable**. The conservative far publication proof can retain proxies longer; this is an
explicit outstanding handoff/overlap risk, not visual acceptance. Candidate counts now describe
pre-GPU in-band inputs, never actual selected draw counts. No full-migration or performance
acceptance, G01–G27 remain open.

At that checkpoint, the next missing port was CPU `FacetedMergeJob` greedily merges compatible planar faces; regular GPU
`VoxelBrickMesher` explicitly emits one quad per exposed cell. A uniform64×64 face illustrates
1 versus4,096 quads, not a measured whole-scene ratio. Port that behavior onto GPU with bounded
scratch and independent surface-area/material/winding/boundary proof; measure page pressure and
raster cost. Step8 already has a bounded4×4 greedy path. Do not weaken semantic assertions merely
to accept different triangulation. Retired solid CPU code/workspaces/arena still await removal.


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

Latest user steering: imperfect water appearance is acceptable for now; prioritize performance.
Do not spend the next work phase polishing water. Preserve full content/distance and the remaining
CPU solid-renderer deletion requirement. Rendering architecture checks after water retirement:
`gpu-water-retired-boundaries.xml`, three passed/exit 0/13s. The requested WorldBuilder boundary test
was not discovered in that run; it is not claimed as executed or passed.

CPU water retirement after `73989d7ac`: physically deleted `CpuWaterSurfaceChunkCache.cs` (934 lines),
`WaterBrickMeshBatchJob.cs` (368 lines), their metadata, obsolete CPU-only configuration/lifetime tests,
and the shader's contiguous CPU-buffer fetch. Source reference audit finds neither retired class in
Assets or active non-SceneIssue repository sources. Required CPU voxel reads and GPU host orchestration
remain in `GpuWaterSurfaceChunkCache`. Solid CPU renderer removal is still open.

Preserved test responsibilities: four former Burst semantic cases now execute the production GPU
mesher through module-local test support; the arbitrary-material vertical-ribbon topology/spray test
also executes GPU extraction. Five broad two-slice fixtures retain vertex/index counts and SHA256
hashes of every sorted quantized vertex/normal/material/active record, captured from the old CPU
oracle only after another full GPU parity pass (`gpu-water-retirement-oracles.xml`: five passed,
exit 0/12s). The CPU extraction algorithm is not retained in fixtures. GPU page/winding checks remain.
Draw regressions now compare bucketed and direct-handle GPU indirect layouts, including both banks,
nonzero handles and body/spray rejection. Narrow synthetic shader fixtures use the production GPU
cache entry/argument kernel/shader and are not visual acceptance or replacement scene content.
Canonical Showcase storage/cascade tests now inspect real GPU-published geometry.

Initial `gpu-water-cpu-cache-removal.xml`: 12/14 passed, exit 2/24s. Failures were an obsolete assertion
requiring positive CPU geometry uploads and `GetInt` observing a `SetInteger` property. The migrated
checks require zero CPU geometry upload plus actual nonempty GPU geometry, and the matching typed
property accessor. `gpu-water-retired-playmode.xml`: all 14 passed, exit 0/14s. Initial retirement
EditMode invocation failed compilation because the new test helper lacked a CoreUtils assembly
reference; replacing that test-only cleanup with immediate Unity object destruction fixed it.
`gpu-water-retired-editmode-fixed.xml`: 21 passed, exit 0/15s (five cache, four semantic, five fixed
mesher-output, six draw/compaction and one nonblocking architecture case). Overlapping preliminary
runs are not additional unique tests.

`gpu-water-retired-module` built without the retired production files and completed 42s/seven captures,
exit 0; required water readiness markers present, no forbidden patterns. Exact source copies/hashes,
diff and deletion manifest are retained. Reviewed 26.3s and 32.3s: river/feeder/receiver water remains,
with comparable cascade sheets after deletion. Quality remains unacceptable: harsh horizontal
waterfall bands, planar layering, missing surroundings and floating vegetation. No visual gate is
closed. This retirement run is not a new Showcase benchmark; the preceding 180s integration and
138/136 FPS diagnostics remain the latest full-scene measurement.

Runtime GPU water migration after `54902c4b8`: scheduler/discovery/render pass now use
`GpuWaterSurfaceChunkCache`; the retired CPU cache remains for unmigrated tests but is not selected
by production orchestration. Snapshots remain authoritative CPU inputs; eight-brick GPU count/write
slices, GPU allocation, status/identity-only feedback, version-checked commit and GPU indirect arguments
replace CPU geometry extraction/upload. Water owns a separate page arena under unchanged geometry
capacities. Completion callbacks retain resources through submitted build work and final water draws;
this does not close the shared G11 page-retirement/error obligations. Same-chunk water/occluder changes
now invalidate the derived surface. Spray culling includes the canonical six-voxel plume extent.

`gpu-water-runtime-compile.xml`: 11 passed/exit 0/20s; initial cache behavioral suite
`gpu-water-runtime-cache.xml`: four passed/exit 0/17s (publication/indirect arguments, stale completion
rejection, same-chunk occluder edit, actual deferred GPU resource disposal). The first standalone
`gpu-water-runtime-module` completed 30s/five captures/exit 0 and emitted liquid-ready. Reviewed 8.3s
and 26.3s show GPU lake/cascade but a dry river; classified unacceptable, with missing surroundings,
floating vegetation and slab-like water/cascade composition also open. Inspection proved inherited
hardcoded 11/16 classification omitted authored RiverWater 22. The GPU cache now uses the installed
presentation catalogue's water mask, captured per transaction. Added material-22 publication regression.
`gpu-water-runtime-catalogue.xml`: 16 passed/exit 0/17s: five cache, five mesher, six draw/compaction cases.
Final `gpu-water-runtime-final.xml`: 17 passed/exit 0/16s, adding the existing fixed draw-staging architecture check updated to the new owner. These test runs overlap; they are not cumulative unique test counts.

The corrected `gpu-water-runtime-catalogue-module` completed 42s/seven captures/exit 0. Exact source
copies and SHA256 records retained. Reviewed 26.3s confirms restored river and waterfall feeder/receiver
water; 32.3s confirms GPU waterfall sheets. H1 (classification omission) is proven and fixed; H2 (river
lost by paged addressing) is not supported. Rendered quality remains unacceptable: waterfall layers
read as planar sheets with harsh horizontal bands, and background terrain/vegetation is incomplete.
No visual gate is closed by the harness exit. `gpu-water-runtime-showcase` completed 180s/11 captures/exit 0 with no forbidden log patterns.
Reviewed 75.1s against prior 75.3s: comparable castle view, flat cyan moat remains, no new full-view
obstruction. Reviewed 150.1s: unfinished green terrain/far hills and sparse vegetation, unacceptable.
Final missing-visible=642, requests=1981, mirror=1970, published=1175, batchArenaWait=124,
step1/2/4/8 publications=553/549/69/4, directory refusals=0, slot refusals=0. This is not full coverage.
`frame-window-summary.json` records approximate frames per one-second diagnostic sample:
60–90s stationary: 30 samples, mean 137.9 FPS, CPU p50 median 7.21ms, GPU 4.70ms;
120–180s walking: 59 samples, mean 135.7 FPS, CPU 7.31ms, GPU 1.35ms, sample range 103–160.
These are instrumented/incomplete-coverage diagnostics, not the repeated acceptance benchmark;
no performance improvement is established by comparison with the earlier run.
CPU deletion, full production-quality coverage, G11 and performance acceptance remain open.

GPU water paged draw preparation after `08236d15e`: both production WaterSurface passes now share
paged vertex/index lookup with GPU compacted bucket metadata, per-instance counts and bank selection.
Padding instances avoid page reads. A dedicated water selector avoids inheriting the solid draw mode.
The existing contiguous fetch remains temporary until runtime cache cutover; no material/fragment
presentation changes. Metal raster regressions exercise three distinct buckets, alternating banks,
body/spray exclusion and exact full-target pixel parity with contiguous input. Original solid coverage
assertions remain intact. `gpu-water-paged-draw.xml`: 9 passed/exit 0/14s; expanded pass separation
`gpu-water-paged-draw-passes.xml`: 11 passed/exit 0/13s; final raster parity
`gpu-water-paged-raster-parity.xml`: 11 passed/exit 0/13s. These overlapping runs comprise five
GPU-water mesher cases plus six draw/compaction cases, not 31 unique tests. Narrow synthetic triangles
only test production shader addressing; they are not art or standalone visual proof. Runtime water
cache/publication migration, WaterDemo/Showcase player evidence, CPU deletion and G01–G27 remain open.

GPU water kernel preparation after `71d70c388`: scheduling inspection found only two step8 workers
versus four count lanes; a complete lane monopoly is not supported. Latest run had no source slot/
directory refusals, so no speculative scheduling change was made. GPU completion remains the priority.
Added count/write kernels and bounded eight-brick dispatches over immutable packed water snapshots.
Preserves greedy faces exposed to air, opaque water masks, signed positions, vertical lip/impact/edge
flags, three impact spray sheets, spray UV flags, winding and paged-arena count/write/finalization.
`gpu-water-mesher.xml`: five failures, including Metal's dynamic-vector-component l-value rejection.
Replaced axis assignment with explicit vectors. `gpu-water-mesher-fixed.xml`: five passed/exit 0/16s,
comparing actual GPU vertex multisets, flags, counts and triangle winding to the existing CPU mesher
across isolated voxels, cascades, full bricks, blocking halos and mixed material patterns, in two
separate source slices. CPU comparison is a temporary migration oracle and must be removed with the
CPU backend. Runtime water still uses CpuWaterSurfaceChunkCache; no GPU-water scene acceptance yet.
Existing module-local WaterDemo will validate the real integration, not a parallel visual fixture.

User reports the earlier CPU renderer reached 400 FPS (2.5 ms/frame). Latest GPU Showcase instrumented
samples average approximately 129 FPS stationary (60–90s), 128 FPS walking (120–180s), with median
one-second CPU p50 around 7.8 ms. Final scheduler prepare is 4.13 ms, visibility 2.11 ms. These are
incomplete-coverage diagnostics, not a comparable accepted benchmark or a proven bottleneck; keep the
CPU regression open and investigate after full GPU migration as requested.


Asynchronous step8 integration after `4df95a68d`: whole-request edit watch replaces full-footprint
source admission. Existing count lanes prepare contiguous bounded row portions directly into their
existing summary buffers. Each portion owns source demand, active brick/region protection and mirror
allocation until completion-only asynchronous feedback on CPU-written request metadata. No summary
payload/geometry readback. Cancellation/stale requests drain submitted portions before lane reset;
world retirement defers buffer disposal to the callback. Prepared step8 count/write skips dense
source lookup and re-summarization; other LOD paths are unchanged.
`gpu-summary-lanes.xml`: 42 existing tests passed/exit 0/21s. New asynchronous fixtures initially
stalled on EditMode's non-advancing frame guards; explicit bounded test slices corrected orchestration.
`gpu-summary-lane-lifetime-fixed.xml`: 14 queue/lifetime tests passed/exit 0/17s.
`gpu-summary-lane-pressure.xml`: 16 passed/exit 0/17s, including real geometry from 4,096 mixed
source bricks through a 1,024-slot production mirror, rejection after editing a completed portion,
and deferred in-flight world disposal. These suites overlap; do not add their totals.
`gpu-summary-lanes-module/`: 60s/six captures/terminal exit 0; both distance-band markers passed.
Reviewed exact 10s/50s: geometry present, lumpy/stepped terrain remains **prototype/blockout quality**.
`gpu-summary-lanes-showcase/`: 180s/11 captures/terminal exit 0. Reviewed exact 75.3s against
`far-material-slots-showcase/` 75.2s: upper castle gaps filled, roof separation preserved. Reviewed
75.3s/150.3s remains **unacceptable**: simplified houses/terrain, sparse frontier and incomplete
coverage. Final 567 missing-visible; 462 step1/564 step2/84 step4/three step8 publications.
Directory refusals zero (prior 4,659,051); mixed 16,250/58,144, ready records 721,677, pending 312,
demand 12, directory probes 15,343,105. No slot refusals. This confirms bounded step8 source reuse,
not full-scene convergence: step8 publication remains low and fine-layer throughput needs checking.
Next discriminate count-lane contention from remaining whole-footprint demand pressure. Exact source
copies/hashes retained with players. Instrumented timings are diagnostic only; no performance acceptance.


Source lifetime preparation after `512a5f746`: separated whole-request edit-watch reference counts
and epochs from source-demand reference counts. Existing full-coverage callers retain both;
summary streaming can watch released source portions without protecting them from eviction.
Rectangular source demands/scans are bounded to 1,024 bricks (one summary dispatch).
`gpu-summary-edit-watches.xml`: 17 passed/exit 0/21s. `gpu-summary-source-ranges.xml`:
24 passed/exit 0/21s. Added actual ready-record eviction proof; initial fixture lacked its mirror
and failed with NullReferenceException. Corrected to acquire the real shared mirror.
`gpu-summary-source-eviction-fixed.xml`: 25 passed/exit 0/17s, including range boundaries/overlap,
released-portion edits, unrelated edits, shared readers, retired-world release, queued cancellation
and coordinator lifetime. `gpu-summary-ranges-module/`: 60s/six captures/terminal exit 0,
step4-ready and step8 success markers passed. Reviewed exact 10s/50s: geometry present,
lumpy/stepped terrain remains **prototype/blockout quality**. Source copies/hashes retained.
These APIs still await asynchronous
count-lane integration; no complete source-streaming or visual acceptance claim.


Bounded HLOD preparation after `7bf5b0f09`: whole-request admission protects 66³ = 287,496
source bricks, exceeding 58,144 mixed mirror slots for sufficiently dense requests. More polling
cannot solve that configuration. Added destination-offset summary dispatch without changing existing
dense production scheduling. Real GPU tests retain seven portions across reuse of a single source
slot, preserve unknown flags/neighbour ranges, reject invalid offsets, and mesh adjacent portions
after releasing their sources. Initial harness stopped on a test-only C# using-variable mutation
compile error; corrected without product changes. `gpu-hlod-portions-fixed.xml`: 34 passed, exit 0,
14s. `gpu-hlod-portions-meshed.xml`: 35 passed, exit 0, 13s, including paged geometry verification.
`gpu-hlod-portions-module/`: 60s/six captures/terminal exit 0; step4-ready and step8 success
markers passed. Reviewed exact 10s/50s: geometry present, lumpy/stepped terrain remains
**prototype/blockout quality**. Exact source copies/hashes retained. This is the summary streaming
prerequisite, not complete source
streaming: pending work includes portion demand leases, ordered asynchronous completion, edit epochs,
cancellation and production count-lane integration. Existing CoarseGpuProductionValidation owns
module visual regression; no new visual path is claimed by these kernel tests.


Far material separation after `b07488645`: adapter retained all additive primitives but assigned
only the first primitive’s presentation to the whole mesh. Wall/roof regression failed with one
submesh versus two required (`far-material-slots-before.xml`, exit 2). Immutable geometry now carries
resolved presentation slots; same material/style/coating primitives share a slot. Both far draw paths
submit each slot; material objects are shared by resolved values across geometry identities.
`far-material-slots-shared.xml`: 31 geometry/presentation/handoff tests passed (17s harness).
`far-material-slots-colours.xml`: explicit grey-wall/red-roof values and cross-geometry material
sharing passed with the presentation suite. `far-material-slots-module/`: 34s/nine captures/exit 0,
required far-world/frusta/12km markers passed. Reviewed 32s: white primitive fixtures, **prototype/blockout
quality**, insufficient for visual acceptance. `far-material-slots-composition/`: 28s/seven captures/exit 0,
modifier readiness/success passed. Reviewed 24s: roof/wall colours separated on production-generated
buildings, but blockout terrain/architecture remains **prototype/blockout quality**.
`far-material-slots-showcase/`: 180s/11 captures/terminal exit 0. Reviewed exact 75.2s:
brown roofs restored, confirming far presentation collapse caused the colour loss. Reviewed
75.2s/150.2s remain **unacceptable**: simplified buildings/terrain and incomplete castle/frontier
coverage. Final 62 step4/three step8 publications, 612 missing-visible, 4,659,051 directory
refusals. No full-view black obstruction in these selected captures. No visual or performance
acceptance; next isolate protected source-footprint capacity and bounded GPU-summary streaming.


Coarse roof follow-up after `daa779e70`: mixed smooth-masonry/planar-roof fixture resolves roof
material in GPU density but initially emits no upward faces (`gpu-step4-roof-mixed-before.xml`,
one failed, exit 2). All-planar preliminary fixture falsified the broader sampling assumption;
that case deliberately returns its centre material. Coarse faceted classification now consumes
the same sampled presentation as continuous extraction, retaining exact centre/neighbor occupancy.
`gpu-step4-roof-fixed.xml`: 23 passed, exit 0 (24s harness). Roof fixture verifies actual paged
vertices/materials. `gpu-step4-roof-semantics.xml`: 12 semantic/coating/negative-shell tests passed,
exit 0 (16s harness). `gpu-roof-coarse-module/`: 60s/six captures/exit 0, both distance-band markers
passed. Reviewed 10s/50s retains prototype terrain quality; geometry is present. Exact-source
`gpu-roof-showcase/` completed 180s/11 captures/exit 0. Reviewed 75.2s/150.2s is still
**unacceptable**: grey distant houses remain; the left mass is grey at 75.2s and frontier is incomplete.
Final 53 step4/three step8 publications, 607 missing-visible and 3,393,137 directory refusals.
The independent GPU roof defect is repaired, but the Showcase symptom is not resolved. Determine
whether missing coverage retains simplified proxies or proxy presentation loses material identity.
Do not mark G09 or full GPU coverage accepted; no performance acceptance claimed.


Step4 migration in progress after `8e5c4ba95`: the existing GPU pipeline now captures a per-descriptor
false-empty decision after ordinary counts and conditionally counts/writes feature-preserving HLOD
into the same paged arena. No CPU count/geometry readback. Real prepared-cache regression verifies a
single voxel missed by the step4 lattice becomes a closed two-voxel box; suppression cases retain
existing geometry, profiles, errors and finer-ring behavior. `gpu-step4-enabled.xml`: 46 passed,
terminal exit 0 (24s harness, 4.7s tests). Step4 production gate enabled for player validation.
Coarse module now views the same production-generated landmark from step4 then step8 distance bands.
`gpu-step4-coarse-module/` build exited 138 in native Burst compiler stack; no player ran. Retry finished exit 1: initial camera was in step2 rather than step4, so required marker correctly
failed. Corrected distance using logged module bands. `gpu-step4-coarse-bands/`: 60s/six captures/exit 0;
three step4 and two step8 publications, both required markers passed. Reviewed exact 10s and 50s:
**prototype/blockout quality**, with lumpy step4 terrain and visibly stepped step8 contouring.
These remain visual defects under G09; module behavior passes, production visual finish does not.
`gpu-step4-showcase/`: 180s/11 captures/exit 0; 79 step4 GPU publications, three step8,
598 missing-visible and 4,376,579 directory refusals at termination. Reviewed exact 75.2s/150.2s:
**unacceptable**. Distant right-side houses have lost brown roof/material separation compared with
`gpu-directory-backshift-showcase/` 75.1s; investigate reconstruction versus far-feature replacement
before accepting step4 fidelity. Frontier remains incomplete; no black full-view obstruction in
these selected captures. This is GPU migration progress, not visual/performance acceptance.
`gpu-step4-reuse.xml`: all 47 targeted tests passed, terminal exit 0 (20s harness, 4.4s tests).
The additional real GPU lane-reuse case proves thin→ordinary→air clears the previous selection/output.
No production code changed after the successful player builds.


`gpu-directory-backshift-module/`: 48s/eight captures/terminal exit 0; publication, edit and
far-handoff markers passed, fallback 0, visible 8/missing 0. Reviewed exact 42s capture: prototype
validation fixture, not full-scene visual acceptance. Exact source copies/hashes retained with both players.

Directory-pressure repair checkpoint (local diff after `bda8c4ef8`): explicit `DirectoryFull`,
bounded cold uniform/mixed reclamation with demanded/active protection, 75% directory live-key
limit within unchanged allocated bytes, conservative GPU lookup bound, backward-shift deletion
and distributed victim selection. Before cold-uniform regression failed; final
`gpu-directory-backshift.xml`: 48 passed in 24s, terminal exit 0, including actual queued GPU reads
across collision relocation/wrap. Intermediate `gpu-directory-reclamation-showcase/` completed
180s/11 captures but failed coverage and retained long searches; not visual-reviewed.

`gpu-directory-backshift-showcase/`: 180s/11 captures/terminal exit 0. Reviewed exact 75.1s and
150.1s screenshots: black obstruction absent in these views; overall **unacceptable** due to
unfinished terrain/presentation and incomplete coverage. Final 2,203 publications include only
three step8, with 1,041,989 directory refusals and oldest step8 request 20.8s. Directory insertion
probes 29,984,389 versus billions previously; late CPU p50 6–8ms/GPU p50 roughly 1ms remains
instrumented diagnostic evidence, not benchmark acceptance. G07/G11/G12/G22 remain open.


Latest coarse-source checkpoint (still incomplete): request-progress/recovery diagnostics falsify
source borrowing, active-reader stalls, scan starvation and change-replay deadline starvation as
sole causes. `gpu-coarse-progress-trace/`: 90s/six captures/terminal exit 0; oldest coarse request
6,732 polls, zero restarts; 9,566 recovery calls, zero deadline skips.

Bounded failed cleanup now retries next frame rather than per incoming brick. Before regression
failed at 1,024 checks versus 64 allowed; after suite 27 passed. A host index of the existing GPU
directory removes exhaustive absent-key CPU scans and permits immediate tombstone reuse.
Before churn regression failed at 16,384 probes for 16 absent keys; GPU collision/reuse/clear
regressions pass. Cleanup-only and index-only 180s Showcase runs completed (12/11 captures)
but remained coverage failures. Index-only traversal exposed directory exhaustion with only
1,949 mixed payload slots occupied out of 65,536.

Shared mirror allocation now reserves 1,048,576 directory entries and 58,144 mixed slots in the
previous 64k-slot GPU-byte ceiling, with metadata included in byte accounting. The old directory
failed at 262,144 uniform keys; the new layout publishes 524,288 keys for two full 64³ coarse cores
and verifies material via the real GPU summary lookup. `gpu-directory-capacity-fixed.xml`:
49 passed, terminal exit 0 in 20s. Smaller shared-budget layout also stays within its old ceiling.

`gpu-directory-capacity-showcase/`: 180s, 11 captures, terminal exit 0, no exceptions or transaction
rejections. Reviewed 75.1s/150.2s is **unacceptable**: terrain gaps/coarse artifacts and a black
obstruction filling most of the walking view. Step8 publications reach 4, but traversal accumulates
11,981 directory refusals with only 39,070/58,144 mixed slots used. Current `PublishBlock` only
reclaims mixed entries on `NoSlot`; cold uniform directory entries need their own safe pressure
recovery. Long GPU directory probes are another unresolved risk (diagnostic GPU windows near 750ms).
Do not accept these timings as a benchmark or this harness completion as coverage/visual success.
Source copies/hashes/diffs are retained with final and intermediate capture directories.
`gpu-directory-capacity-module/`: 48s, eight captures, terminal exit 0; required GPU publication,
edit restoration and far-handoff markers pass. Reviewed 42s retains prototype fixture quality,
with no missing fixture surfaces observed. This does not satisfy Showcase visual acceptance.


Latest source-validity checkpoint: footprint-scoped invalidation replaces global cancellation
for ordinary solid edits. Mirror/history resets still invalidate all demanded footprints; new
occupancy/residency in an absent halo invalidates that footprint even without ready mirror data.
Admission and queued-batch validity use the same stamp. `gpu-coverage-local.xml`: 40 passed,
24s terminal exit 0. `gpu-coverage-queued.xml`: 37 passed, 16s terminal exit 0, including two
new real queued-request validity cases and existing world/history cancellation/lifetime tests.

`gpu-hlod-recovery-reasons/`: 90s, six captures, terminal exit 0. Borrow, block-read and active
skip counters remain zero; these are not the observed recovery blocker. `gpu-hlod-scoped-coverage/`:
180s, 12 captures, terminal exit 0, no exceptions/transaction rejections. Reviewed 74.9s/149.9s
is **unacceptable** with flat coarse terrain and missing traversal coverage. Only one step8
publication: global invalidation is not the sole backlog cause. Source copies/hashes/diff and
validation notes are retained with this run. G07 remains open; investigate coarse scan progress
and recovery scheduling, without weakening budgets or restoring CPU rendering.
`gpu-scoped-coverage-module/`: 48s, eight captures, terminal exit 0, required publication/edit
restoration/far-handoff markers passed. Reviewed 42s: prototype fixture quality, no new missing
fixture surfaces observed. This does not pass Showcase visual acceptance.


Latest local GPU-path checkpoint: step8 now uses GPU dense-cache summaries, bounded greedy
count/write dispatches, the existing paged arena, and versioned asynchronous publication.
Step4 and water remain CPU-backed; G07 is **not complete**. The user explicitly deferred
additional optimization work until the full GPU path works.

`Artifacts/LocalGpuShowcase/gpu-hlod-draw-contract.xml`: **48 passed**, terminal wrapper exit 0
in 16s, including real GPU mesh bounds/area/winding, cross-brick face suppression, unknown
halos, partial-write rejection, integrated extractor, mirror addressability and lifetime tests.
Initial Metal compilation failures were fixed; initial admission and addressability regressions
failed before their fixes. Coarse footprints exposed quadratic scans of pinned readiness records
and an oversized mirror beyond the packed directory’s 65,536-slot address space. Incremental
cleanup and the addressability bound repair those blockers without changing source truth.

`gpu-hlod-coarse-module-draw/`: real Rendering-owned production WorldBuilder scene, 60s,
terminal harness exit 0, six screenshots and required GPU-publication marker. Reviewed 10s
screenshot shows the mountain landmark through step8 GPU geometry. Earlier addressability-only
run passed publication but rendered blank: physical vertex indices violated the consumer's
chunk-local index contract. Corrected indices and test-side page-table resolution repair it.
Visual classification: **prototype/blockout quality**; visible coarse terracing and simple
landmark forms do not meet production acceptance. Full `gpu-hlod-showcase/` integration finished: 180s, 12 captures, terminal exit 0;
no exceptions/transaction rejections. Reviewed 75s/150s: **unacceptable**, with terrain gaps,
flat coarse background and missing traversal coverage. Only one step8 publication completed;
large pending coarse footprints delay near work. G07 source-frontier/admission liveness remains
an integration failure despite successful harness completion. Distinguish unavailable source
regions from recovery starvation before changing admission.
`gpu-hlod-near-regression/`: 48s, eight captures, terminal exit 0; required publication,
edit restoration and far-handoff markers pass. Reviewed 42s screenshot retains prototype fixture
quality. Source copies, hashes and delta are retained with module and Showcase captures.
No performance claim. Core-absent and upload-failure counters remain zero in the stalled
Showcase; next inspect resident-view borrowing, active source protection and recovery fairness.


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
`layout-fixed/` completed180 seconds, player exit 0,11 screenshots. Castle upper walls no longer
fragment into floating strips. Traversal still has missing ground, terrain bands, cyan water and
grey blockout far structures: **unacceptable**. Its `diagnostic-summary.json` retains rounded
per-window timings and caveats; no full-coverage or performance acceptance. Reassess GPU capacity
and live publication with corrected layouts before implementing compression/recovery changes.


Corrected-layout replay `layout-coverage-trace/` completed180s, exit 0,11 screenshots. At60s,
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
`outcome-recovery/` completed180 seconds,exit 0,11 screenshots,no transaction rejection errors.
Visual classification remains **unacceptable**: terrain gaps/bands, grey far masses, cyan water.
Diagnostic window timings/source delta are archived; no benchmark or full coverage acceptance.



Current local publication replacement deletes the automatic pump/kernel, assigns a unique renderer
attempt generation, and requires host source/configuration approval before commit. Cancellation
aborts exact identities, including results arriving after their context was released.
`approval-identity-tests.xml`:25 passed. `approval-module/`:48s,exit 0,8 captures, production
initial/traversal/edit/settled/restart passed with zero fallback/rejections. `approval-showcase/`:
180s,exit 0,11 captures; reviewed60s/165s. **Unacceptable**: terrain banding/seams, coarse grey
far geometry, cyan water and poor terrain integration. These remain G07–G09 defects.

Finalization previously ignored actual write totals. `write-finalization-before.xml`:all5 real-GPU
missing/short/overflow transaction cases failed. GPU-side count comparison now rejects incomplete
candidates, retires their pending pages and preserves prior live geometry; only failure status
crosses to the CPU. `write-finalization-after.xml`:30 passed,zero skipped/failed,18s guarded run.
`write-finalization-module/`:48s,exit 0,8 captures, all module stages passed; final42s screenshot
reviewed. `write-finalization-showcase/`:180s,exit 0,11 captures;165s reviewed, still unacceptable.
This first strict run mapped write mismatches to generic retryable Failed, so quiet logs do not
prove zero write-count failures. Dedicated `WriteFailed` status now remains distinct/nonretryable
and logs rejection. `write-finalization-distinct-tests.xml`:31 passed in18s.
`write-status-module/`:48s,exit 0,8 captures; initial/traversal/edit/settled/restart passed with
zero fallback/missing/transaction rejections,42s screenshot reviewed. `write-status-showcase/`:
180s,exit 0,11 captures,zero transaction rejections/exceptions;60s/150s/165s reviewed.
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
counters. Reviewed 42s shows the real WorldBuilder landmark. Normal180s Showcase completed12
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
Coarse GPU preparation follow-up: `GpuBlockHlodSummary` dispatches at most1024 actual mirrored
bricks and retains64 subcell materials/occupancy plus explicit unknown-source status perbrick.
The initial12-test run failed3 cases: empty bricks have no directory entry and two isolated voxels
were decoded incorrectly. A held complete mirrored-region proof now permits absent-as-air;
CPU region readiness alone is insufficient. Raw GPU payload checks proved source bytes correct.
Simplified column voting did not fix the decode failure. Explicit packed-byte selection passes
all13 cases, including every one of512 voxel positions without phantom neighbours, water masks,
material voting/ties and reset/disposal admission (`gpu-hlod-summary-exhaustive.xml`,14s wrapper,
exit0). Earlier `runtime`, `ready`, `payload`, `columns`, `index` and `byte` attempts remain failed
evidence; `decode` passed12 cases. Temporary per-lane diagnostics are removed. This is GPU data
preparation only: step4/8 CPU summary/mesh jobs remain active and G07 is not complete. Integration
must hold source/version leases through ordered completion, reject unknown coverage, produce
GPU geometry and exercise Rendering-owned player scenes before deleting the old path.
Normal180s Showcase regression passed11 captures, exit 0, no exceptions or transaction rejections
(`gpu-hlod-summary-showcase/`). Reviewed75s/150s: detailed castle intact and phantom wall absent;
terrain banding/seams/gaps and coarse side geometry remain **unacceptable**. This unchanged scene
consumer is regression evidence only; it does not exercise the new summary kernel.

- [ ] **G07 — Eliminate CPU-dependent LOD coverage.** Current `CpuTransvoxelChunkCache.SupportsGpuSurfaceStep` admits steps 1/2/8; step 4 still uses feature-preserving CPU fallback. Step8 GPU module publication and rendering pass locally; mixed-LOD/frontier/edit/pressure proof remains. Inventory every ring/representation actually used by VoxelShowcase and affected consumers, including coarser mip work. Implement the required GPU equivalents before deleting those paths. Test real mixed-LOD batches, logical extent versus physical stride, transition faces, negative-shell ownership and nonresident frontier halos. Do not disable coarse rings or reduce draw distance to claim GPU-only coverage.
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
content, completed180s/12 captures/exit 0, and reviewed150s reproduces the obstruction. Existing
trace identifies mountain CC9F50C170E9C507 at90–110s but truncates the larger150s retained set
to four entries. Exact obstructing primitive remains unproven. Next diagnostic must report nearest
camera-ray/proxy intersections, distinguishing geometry, transform/submission and handoff causes.
These subsystem-isolation runs are excluded from visual/performance acceptance. Harness changes
provide orchestration only; no production geometry or replacement validation scene is introduced.

Phantom modifier-volume fix: `obstruction-ray-trace/` completed180s/12 captures. Exact submitted
mesh ray hits E6C05222E216258F at0.489m at150s, and C3A4E584B0B74831 at0.682m at140s.
Canonical source inspection (`obstruction-canonical.xml`,2 checks; diagnostic source retained)
shows both are single PaintSurface boxes, with no additive occupancy. GeometryFor returned null,
but the adapter still emitted instances that invoked the renderer's fallback box. The adapter now
excludes standalone instances without additive geometry, preserving canonical modifier sources
and their application to real cells. Three generic PaintSurface/PaintSolid/Carve regressions fail
before and pass after; all5 cache/selection checks pass (`modifier-proxy-before/after.xml`).
Normal full-content `modifier-proxy-showcase/` completed180s/12 captures, exit0/no exceptions or
transaction rejections. Reviewed149.8s confirms phantom walls are gone. Terrain seams/aliasing,
near gaps and coarse scenery remain **unacceptable**. A new Composition-owned far-only production
consumer uses the real Showcase catalogue, clipmap, adapter and renderer. First module run lacked
MainCamera tagging, leaving clipmap invisible; that fixture defect was corrected. Final28s run
(`modifier-proxy-module-camera/`) passed7 captures,110 solids and1371 canonical modifier sources.
Reviewed24s confirms production terrain and no modifier boxes; coarse far buildings, weak material
separation and terrain aliasing remain prototype/blockout quality. Improve these shared production
systems before final scene acceptance. The initial module is not accepted visual evidence.
No final visual, CPU-removal or performance acceptance.

Far roof/profile follow-up: seven of eight canonical prism cases fail with the old AABB
approximation (`far-prism-before.xml`). Renderer-neutral profile/axis/direction/cell dimensions
now drive a bounded prism mesh for Gable/Shed/Arch. Eight column-occupancy comparisons pass,
including both axes, reversal, steep profiles and a single-column edge case. A separate box-normal
regression failed before; box faces now own separate shading vertices. Four existing closure
checks initially rejected hard-normal seams by vertex index; they now weld identical positions
and still require every geometric edge twice plus outward/nondegenerate triangles. All27 combined
roof/ramp/material/cache checks pass (`far-roof-verified.xml`). Composition28s player passed7
captures; reviewed24s shows pitched roofs and flat wall shading but remains blockout quality.
Rendering owns `FarFeatureProfileValidation.unity`, sharing the production-catalogue validation
bootstrap without duplicating generation/rendering. Its28s player passed7 captures;24s reviewed
(`far-roof-rendering-module/`). Normal180s Showcase passed12 captures/no exceptions or transaction
rejections (`far-roof-showcase/`); reviewed75s/150s retains detailed castle and no phantom walls.
Terrain seams/aliasing, near gaps, coarse far art, material separation and openings remain unresolved.
Both module views are blockout quality; full Showcase remains unacceptable. No final performance claim.

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
edit/handoff/restart and zero fallback/failure counters (`mirror-clear-module/`). Reviewed 42s
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

### Performance focus after water retirement

- User explicitly accepts imperfect water appearance for now; prioritize CPU performance.
- Local retirement commit `d66763890`; no push. Added visibility substage timing only, with no rendering behavior change.
- `Artifacts/LocalGpuShowcase/gpu-visibility-perf-breakdown`: standalone Showcase build passed (33s), player completed 180s/12 captures/exit 0. Exact scheduler source/hash/diff and `frame-window-summary.json` saved.
- Stationary 60–90s: 29 samples, approximately 133 FPS, median CPU p50 7.59ms; walking 120–180s: 60 samples, approximately 135 FPS, CPU 7.51ms. These are incomplete-coverage diagnostics, not performance acceptance or an improvement claim.
- Stationary median traversal/selection/water/draw preparation: 1.842/0.405/0.001/0.013ms. Walking: 1.722/0.066/0.001/0.010ms. CPU coordinate traversal dominates visibility; GPU submission does not explain that aggregate.
- Reviewed exact 75.0s stationary and 150.0s walking screenshots: castle present, existing terrain gaps/procedural hills/sparse surroundings remain unacceptable. Water art is deferred per user steering; coverage defects remain open.
- Reuse correctness audit: missing-visible guard currently prevents reuse during convergence; do not simply remove it. GPU publication must invalidate cached selection, GPU entries need lifetime refresh, and projection/voxel-scale changes need correct invalidation.

### Stationary visibility geometry cache

- Cache only camera/ring classification, keyed by exact position, six planes, voxel scale, inner/outer distance and suspension. Moving queries bypass insertion; bounded cache misses use original geometry checks. All dynamic known/readiness/empty-generation/demand/promotion/LOD/last-used behavior remains live each frame.
- `visibility-geometry-cache.xml`: 17 focused EditMode tests passed, terminal exit 0/21s. Covers camera/projection/ring/scale invalidation, newly streamed coordinates, empty publication and subsequent edits during stationary reuse, plus existing LOD handoff/fallback cases.
- `gpu-visibility-geometry-cache`: Showcase 180s/11 captures/exit 0, build 34s. Exact production sources, hashes, diff and frame-window summary saved. Stationary traversal median 1.342ms versus 1.842ms (-27%); CPU p50 median 6.905ms versus 7.59ms; approximate FPS 145 versus 133. Walking traversal 1.732ms versus 1.722ms; CPU 7.10ms, approximately 141 FPS. Whole-frame figures remain single-run, incomplete-coverage diagnostics, not final repeatable acceptance.
- Reviewed exact Showcase 75.0s/150.1s: castle retained; existing terrain gaps, broad procedural hills and sparse surroundings remain unacceptable. No visual acceptance closure; imperfect water remains deferred by user steering.
- `visibility-geometry-cache-module`: existing SolidGpuProductionValidation scenario, build 20s; 48s/seven captures/exit 0. Required initial/traversal/edit/settled/restart/far-handoff/success markers present, no forbidden failures. Zero missing/fallback/blocking at checkpoints. Reviewed 18s/42s: intact coated voxel diagnostic structure and terrain, prototype composition; behavioral rendering validation, not production art acceptance.
- Retain this measured stationary improvement. Next: reduce dynamic coordinate bookkeeping and LOD selection without hiding geometry or disrupting publication/eviction. Remaining renderer deletion, coverage and final performance gates stay open.

### Exact LOD selection reuse

- `SurfaceLodVisibilitySelector` owns copies of drawable and current-complete inputs and reuses only an exact sequence match. Same-list, same-count mutations invalidate correctly; publication/removal and physical fallback continue through the original selection algorithm. This does not cache GPU handles or world state.
- `lod-selection-reuse.xml`: 18 focused EditMode tests passed, terminal exit 0/14s. Added mutable-list publication/incompletion/restoration/retirement regression; retained geometry-cache and LOD fallback coverage.
- `gpu-lod-selection-reuse`: Showcase build 32s, player 180s/12 captures/exit 0. Exact sources/hash/diff and frame-window summary saved. Stationary selection 0.064ms versus 0.428ms (-85%), CPU p50 median 6.545ms, approximately 154 FPS (previous 145). Walking selection 0.058ms versus 0.069ms, but CPU 7.41ms/134 FPS versus 7.10ms/141 FPS: no whole-frame walking improvement established. Diagnostics still have incomplete and varying streaming coverage; not final repeatable benchmarks.
- Reviewed exact 74.9s/150.0s Showcase screenshots: castle retained; existing terrain holes, coarse procedural hills and sparse surroundings remain unacceptable. Water art stays deferred per user steering; all coverage/art gates remain open.
- `lod-selection-reuse-module`: existing SolidGpuProductionValidation scenario, build 19s, player 48s/seven captures/exit 0. Initial/traversal/edit/settled/restart/far-handoff/success checks passed; zero missing/fallback and no forbidden failure markers. Reviewed 18s/42s intact coated voxel fixture, prototype composition; behavioral validation only.
- Retain the stationary optimization. Dynamic coordinate bookkeeping still dominates visibility; full CPU renderer retirement and G01–G27 remain incomplete.

### Direct visibility result handoff

- Worker coordinate collection now returns drawable/current-view-complete facts directly. Scheduler no longer infers behavior from before/after diagnostic counter reads. Counters, source generation checks, stale drawable fallback, current-empty handling, off-frustum prefetch and entry last-used updates remain intact.
- `visibility-direct-result.xml`: 28 focused EditMode tests passed, terminal exit 0/17s. Seven direct-result states cover unknown/missing/empty/current/stale/suspended/off-frustum; existing admission, geometry-cache and LOD cases retained.
- `gpu-visibility-direct-result`: build 33s, Showcase 180s/12 captures/exit 0. Exact sources/hash/diff and window summary saved. Traversal medians stationary 1.256ms versus 1.317ms, walking 1.712ms versus 1.770ms: modest savings only. CPU p50 medians 6.395/7.11ms, approximately 155/140 FPS. Single-run, incomplete-coverage diagnostics, not final repeatable acceptance.
- Reviewed exact 74.9s/150.0s screenshots: castle retained; existing terrain gaps/procedural hills/sparse surroundings remain unacceptable. Water appearance stays deferred per user steering; coverage gates remain open.
- `visibility-direct-result-module`: production SolidGpu scenario, build 20s, player 48s/seven captures/exit 0. Required traversal/edit/restart/far-handoff checks passed with zero missing/fallback and no forbidden failure markers. Reviewed 18s/42s intact coated diagnostic fixture, prototype composition; behavioral evidence only.
- Next investigation: correlate GPU allocation failures, off-screen eviction, publication/residency and arena-relief timing. GPU failure relief currently runs independently of the older CPU convergence guard; prefetch churn is a hypothesis, not yet a proven root cause. Preserve generation/visible-geometry correctness before changing policy.

### Dense dirty mirror uploads

- Existing PREPARESECTIONS analysis (121 samples,t≥60s) showed median worst-frame arena-relief0.000ms/max1.102ms. This falsifies steady direct eviction-scan dominance, not downstream rebuild churn. `batchArenaWait` counts extraction-fence waits, not allocation exhaustion.
- Added mirror CPU sync/recovery/flush/batch-advance timings and actual GPU allocation/eviction counters. `gpu-mirror-cpu-breakdown`: build32s, Showcase180s/12 captures/exit0. Flush medians0.5665/0.570ms stationary/walking; final3,800 allocation failures,1,906 evictions,643 missing-visible. Exact sources/hash/diff/window summary saved.
- Proven scan issue: sparse dirty hash indices caused flush to visit almost the entire million-slot directory. Replaced payload/directory min/max scans with fixed arrays of unique dirty indices, keeping existing dirty flags for deduplication. Flush publishes each slot's final state, including repeated writes/deletion relocation. Adds ~4.2MiB bounded host indices at Showcase capacity; GPU allocation/byte budgets unchanged.
- `mirror-dense-dirty.xml`: 15 GPU EditMode tests passed, exit0/18s. New regression verifies three changed slots require three visits in a million-entry directory, repeated writes publish final GPU material, clean flush does no work, and clear/reuse discards stale updates. Existing collision-chain relocation, reader protection, addressability, clear/lifetime and byte-ceiling cases passed.
- `gpu-mirror-dense-dirty`: build27s, Showcase180s/12 captures/exit0. Flush medians0.0055/0.0155ms, CPU p50 medians5.485/6.79ms, approximately180/147 FPS versus baseline156/140. Single-run incomplete-coverage diagnostics, not final repeatable performance acceptance. Final1,645 allocation failures/1,239 evictions and643 missing-visible: coverage/pressure not resolved.
- Reviewed baseline/fixed74.9s and fixed149.9s: castle retained, additional background structure realization visible; existing terrain gaps/procedural hills/sparse surroundings remain unacceptable. Water art stays deferred per user steering.
- `mirror-dense-dirty-module`: existing SolidGpu production scenario, build19s, player48s/seven captures/exit0. Traversal/edit/settled/restart/far-handoff/success assertions passed with zero missing/fallback/no forbidden failures. Reviewed18s/42s intact coated voxel diagnostic fixture, prototype composition; behavioral validation only.
- Next: distinguish GPU page exhaustion/retirement from per-chunk allocation limits and correlate failures with residency. Do not change relief policy merely from its misleading former label. Full renderer retirement, coverage and G01–G27 remain open.

### Admit before retaining GPU source demand

- Proven ownership defect: BeginPersistentStage registered fine source demand/coarse edit watches before extraction admission could refuse the request. Moved registration after successful extraction and chunk-handle admission, preserving GPU readiness and cleanup. No capacity/budget changes.
- `gpu-admission-before.xml`: all four new cases (steps1/2/4/8) fail for retained ownership. `gpu-admission-after.xml`:492/492 Rendering tests pass,zero skipped,wrapper exit0/19s. Regression fills all extraction slots, verifies denied admission retains nothing, then admission/release pair correctly after a slot opens.
- `gpu-admission-module`: build27s,48s/seven captures,exit0. All seven scenario markers pass,zero missing/errors/finalizers; frame p95/p99 .821/.995ms,262.4MB. Exact42s image has intact coated fort; prototype/blockout composition, behavioral evidence only.
- `gpu-admission-showcase`: build18s,180s/12 captures,exit0. Stationary60–90s290FPS,CPU3.105ms; walking120–180s173FPS,CPU5.485ms. Previous224/184FPS. Final519 missing (previous456),coarse publications3 (13),oldest coarse29.3s (21.17s). Allocation failures/evictions0,CPU coverage polls0,directory failures0; mixed no-slot256,528 (307,479),recovery publications296,798 (347,749). Exact source hashes match; source/diff/window summary retained.
- This is NOT an accepted performance improvement: higher stationary FPS accompanies less finished content, walking/coverage regress. Reviewed exact74.9s/149.9s: castle silhouette persists; terrain seams, flat far surroundings and sparse vegetation remain unacceptable. Loading still stalls.
- Keep the corrected admission ownership invariant as an intermediate checkpoint. H1 that waiting demand alone causes loading stalls is falsified as a complete explanation. Next: GPU source-preparation liveness under competing admitted fine footprints, then camera-band classification/persistent GPU hierarchy to remove ~1.7–1.9ms CPU traversal. Full coverage,400FPS,visual acceptance,Kentridge integration and G01–G27 remain open.

### Constant-time rejection of fully pinned mirrors

- Found redundant CPU work in GpuBrickSlotTable.TryTakeSlot: all resident payloads are pinned by the shared coordinator (which reclaims explicitly), but every NoSlot result first walked all40,270 slots. The maintained PinnedCount now proves exhaustion immediately. Ordinary free-list admission, unpinning and LRU eviction remain unchanged. Added slotChecks telemetry to real-player coverage logs.
- `gpu-pin-fast-before.xml`: one behavioral pressure regression fails with1,288,640 visited slots for32 refused uploads. `gpu-pin-fast-after.xml`:493/493 Rendering tests pass,zero skips,wrapper exit0/20s. Test also releases/reuses a slot without scanning, then unpins/reclaims the sole eligible victim with correct pin/eviction counts.
- `gpu-pin-fast-showcase`: build32s,180s/12 captures,exit0. Stationary271FPS/CPU3.6ms,walking186FPS/CPU5.26ms; previous admission checkpoint290/173 andCPU3.105/5.485ms. Final430 missing versus519;1763 geometry publications versus1474. Slot checks0 across307,843 refused admissions,zero directory failures/geometry allocation failures/evictions. More completed geometry, but coarse remains3 publications andoldest49.0s. Single-run improvement in walking/coverage, not400FPS or full readiness acceptance. Exact sources/hash/diff/window summary retained and hashes match.
- Reviewed exact74.9s/149.9s images: castle retains silhouette and additional detailing; terrain seams/patch transitions and flat sparse far scenery remain unacceptable.
- `gpu-pin-fast-module`: build20s,48s/seven captures,exit0. All seven scenario markers pass with zero missing/fallback/errors/finalizers; frame p95/p99 .821/.894ms,262.5MB. Reviewed42s intact coated fort, prototype/blockout composition; behavioral evidence only.
- Next: prove or falsify starvation between same-priority GPU source lanes. AdvanceCountBatches scans fixed lane order and newly full batches call SealCountBatch outside arbitration. Preserve one dispatch/frame, coarse bounded service and pending-generation ownership. CPU camera-band/hierarchy migration remains needed (~1.85ms final traversal). All broader coverage,visual,Kentridge and400FPS gates remain open.

### Fair GPU source service and upload-progress retries

- `gpu-lane-fair-before.xml`: real GPU test proves fixed-order starvation; first near lane takes both eligible turns while second waits. Rotate lane service after actual dispatch, preserving one dispatch/frame and existing eight-near-turn coarse priority. Full batches remain eligible immediately but admission now only queues; PrepareFrame owns all dispatch/completion.
- Initial fairness variant:494 Rendering tests pass (`gpu-lane-fair-after.xml`,19s), Showcase `gpu-lane-fair-showcase` build27s/180s/12 captures/exit0:240/186FPS,2364 publications,412 missing,coarse14,oldest35.3s. Approximate first400 near milestone40.4→22.3s versus prior pin-fast checkpoint;460 at23.3s rather than after walking. Exact74.9s/149.9s screenshots show better castle completion but existing terrain/far finish remains unacceptable. Source/diff/hashes/window summary/startup milestones retained.
- Do NOT accept that intermediate variant: `gpu-lane-fair-module` build20s/48s fails initial14s convergence. `gpu-lane-frame-owned.xml`494 tests pass21s; eliminating admission-time advancement alone still fails `gpu-lane-frame-owned-module` build27s/48s. Both terminal exit1,zero publications. Added production ring diagnostics to validation failure report. Short diagnostic `gpu-lane-diagnostic-module` build26s/18s/exit1 is diagnostic only, not scenario acceptance. It proves216 ready,7 pending,126,520 active-reader skips,zero publications and7 requests at timeout. Capacity is not exhausted (40270slots).
- Cause: overlapping GPU missing-source retries continuously reacquire read protection before host recovery can upload the missing data. `gpu-retry-progress-before.xml` reproduces reacquiring readers without an intervening upload. A missing-feedback lane now waits for existing upload-publication counter progress before GPU retry. It retains demand, releases submitted readers normally, and GPU still decides readiness. No CPU per-block coverage scan, new buffer, capacity increase or relaxed budget. Counter/world reset and lane cancellation clear the wait state.
- `gpu-retry-progress-after.xml`:495/495 Rendering tests pass,zero skips,wrapper exit0/19s. Includes resumed real GPU preparation after upload progress, source/version/edit/retirement behavior and existing near/coarse priority.
- `gpu-retry-progress-module`: build28s/48s/seven captures/exit0,all seven scenario markers pass,zero missing/fallback/errors/finalizers; frame p95/p99 .816/.928ms,262.5MB. Exact42s coated fort intact; prototype/blockout composition, behavioral proof only. Combined Showcase results follow.

- Final `gpu-retry-progress-showcase`: build18s/180s/12 captures/exit0.248FPS stationary (CPU3.9ms),188 walking (CPU5.22ms). Final2947 publications versus1763 at pin-fast checkpoint;413 missing versus430;coarse15 versus3,oldest24.6s versus49.0s. First400 near chunks~23.3s versus40.4s;460~24.3s versus not reached before90s walking. Counter milestone timestamps use preceding one-second FPS samples and are approximate. Source hashes match; exact source/diff/window summary/startup milestones retained.
- Faster throughput exposes arena pressure:386 allocation failures/evictions (previous0). Do not call this complete coverage or400FPS acceptance. CPU coverage polls0,slot scans0,directory failures0 remain; recovery active skips9198 versus95830,host publications384846. Reviewed exact74.9s/149.9s: completed castle detail retained, terrain seams/patch changes/flat sparse far presentation remain unacceptable.
- Next substantial migration: GPU camera-band classification and persistent candidate/hierarchy input, preserving bounds (33*step voxels), Chebyshev near<=max/far>min, current-empty ownership, parent fallback, and eviction age. CPU CollectVisibleCoordinate currently gates both render candidates and missing-source urgency; separate those responsibilities before removing camera from candidate-cache invalidation. Keep all module and integration gates, and resolve renewed arena pressure without increasing budgets or hiding content.

### GPU detail bands and camera-independent candidate metadata

- GPU CSClassifyLodCandidates now applies per-step padded Chebyshev bands (nearest<=max,farthest>min), suspension and frustum before parent reduction. Out-of-band owned children do not become false completion proof. CPU supplies every known active clipmap candidate; band filtering no longer determines the render candidate set.
- Candidate lists no longer invalidate on camera/projection changes. Source readiness/demand, band configuration and slot membership still invalidate; SurfaceChunkSlotGrid.MembershipVersion includes clipmap centre/radius changes. CPU pending metadata caches coordinate/desired version/ready presence, bounded by active clipmap coordinates. Cached camera changes refresh unsettled build urgency and resident ages without repeating discovery/readiness dictionaries and hierarchy list assembly. Unchanged queries only refresh previously in-band ages; projection planes always reach GPU. CPU build urgency and age bookkeeping remain, not a claim of full CPU retirement.
- `gpu-bands.xml`:500 tests pass23s; `gpu-bands-cached.xml`:500 pass21s; `gpu-bands-final.xml`:501 pass21s; `gpu-bands-candidate-cache.xml`:501 pass22s. Final band tests cover all four steps, negative coordinates, outer/inner equality, suspension, GPU metadata reuse, parent fallback and restored child handoff. Host behavioral test proves out-of-band candidates persist, camera movement activates/parks missing work, preserves desired version, does not duplicate lists, and ages only eligible entries.
- `gpu-bands-module`: build32s/48s/seven captures/exit0. All seven markers pass,zero missing/fallback/errors/finalizers; frame p95/p99 .817/.898ms,262.5MB. Exact42s image intact coated fort, prototype/blockout composition, behavioral proof only.
- `gpu-bands-showcase`: build18s/180s/12 captures/exit0.245FPS stationary/CPU3.9ms,195 walking/CPU4.6ms (59 samples), versus248/188 andCPU3.9/5.22ms.451 final missing,140 allocation failures/evictions. Walking traversal sample~0.9ms versus1.7ms, metadata now includes all6784 known candidates versus2473 in-band hierarchy nodes. Repeated full hierarchy assembly on readiness changes is the next cost. Exact74.9s/149.9s screenshots retain castle; terrain seams/patch transitions, flat far surroundings and sparse vegetation remain unacceptable. Exact source/diff/hashes and window summary saved.
- GpuSurfaceLodInputs now preserves topology when owned membership and referenced keys are unchanged, clears old drawable/completion/handle state and updates only those fields. New/reordered membership or new references still rebuild. `gpu-stable-topology.xml`:502/502 Rendering tests pass,zero skips,23s. Real GPU regression proves parent→children→parent selection across completion/edit changes with one topology build, then a membership change creates a second build and valid selection. Final standalone evidence follows.

- `gpu-stable-topology-module`: build28s/48s/seven captures/exit0. All seven production scenario markers pass,zero missing/fallback/errors/finalizers; frame p95/p99 .820/.916ms,262.5MB. Reviewed42s intact coated fort, prototype/blockout composition, behavioral evidence only.
- `gpu-stable-topology-showcase`: build18s/180s/12 captures/exit0.231FPS stationary/CPU4.01ms,201 walking/CPU4.585ms,60 samples per walking window. Relative to pre-band248/188, walking improves but stationary regresses. Final414 missing,55 allocation failures/evictions,2670 publications,coarse22,oldest28.3s. No claim of complete coverage or400FPS. Slot scans/CPU coverage polls/directory failures remain0.
- Walking RINGS sample medians: traversal1.664→.977ms; metadata input .045→.092ms. Observed input maximum1.410→2.924ms: genuine membership changes still rebuild larger all-candidate topology. Topology reuse is proven behaviorally, but these samples do not isolate its speedup from other scheduling effects. Candidate refresh/reuse totals4138/31984 versus17918/17589 before bands. Exact source hashes match; source/diff/window summary and visibility-comparison.json retained.
- Reviewed exact74.9s/149.9s final captures: castle silhouette/detail retained; terrain seams/material patch transitions and flat sparse far presentation remain unacceptable. All broader visual/coverage/lifetime/Kentridge gates remain open.
- Next: remove remaining per-camera CPU missing-build band/frustum scans using bounded GPU presentation-demand feedback, with generation/membership guards and preserved urgency/aging. Independently profile stationary render cost before selecting an indexed-draw/material rewrite; static frame pipeline remains~4ms despite low scheduler work. Do not hide content or raise budgets to claim400FPS.


### Far replacement traversal checkpoint (2026-09-07)

- Native stationary sample completed under the existing standalone harness: `gpu-stationary-native-sampled`, 90s, six captures. 2549/3498 main-thread samples were inside the scripted render loop; managed symbols unresolved. Scheduler ~0.34ms versus main ~3.8ms rules out scheduler-only attribution. Sampling is diagnostic, not FPS acceptance.
- `gpu-far-prepare-profile`: opt-in temporary timing around `PrepareSurfaceConsumers`, 90s/six captures, harness exit0. Median of 30 one-second mean timings at60–90s: **1.642ms**. Cost grows with replacement readiness. Diagnostic source/patch preserved alongside player evidence; logging removed from production after capture. Reviewed74.9s castle baseline; terrain/far finish still unacceptable.
- Selected fix: coarse-first bounded coverage traversal, descend only where no current proof exists. Same fine-cell union rule, exclusive seams/negative floor arithmetic, no allocation, recursion bounded to four levels. Fully coarse-covered largest admitted bounds:8 proofs instead of16,384.
- Added256 seeded differential cases against original fine-cell oracle with mixed readiness levels and positive/negative bounds. Initial `gpu-far-hierarchy.xml`502/503: only architecture rejection of temporary Debug.Log failed. After removing diagnostic, `gpu-far-hierarchy-final.xml`**503/503 pass**, zero skipped, wrapper exit0/22s.
- `gpu-far-hierarchy-module`: wrapper build and48s/seven captures exit0; all seven markers including far-handoff/edit restoration. Zero missing/fallback/errors/finalizers. Framep95/p99 .821/.910ms, prepare .030/.041ms,262.5MB. Reviewed exact42s fort: prototype/blockout, behavioral evidence only.
- `gpu-far-hierarchy-showcase`:180s/12 captures, exit0, no forbidden errors/finalizers. Stationary60–90s30 samples **350.1567FPS**, CPU p50 median2.71ms, GPU diagnostic5.34ms; walking120–180s60 samples **218.7483FPS**, CPU4.10ms,GPU diagnostic1.505ms. Previous231/201FPS andCPU4.01/4.585ms. No direct post-fix far-only timing claimed; final run excludes temporary instrumentation.
- Final363 missing,12 arena failures/evictions,2571 publications,coarse20,oldest30.265s. Candidate2104/node5735,source40270/40270,slotChecks0,dirRefused0. Loading/coarse completion still incomplete; no400FPS claim.
- Source hashes/patch/parentHEAD preserved alongside module and Showcase; hashes match final code. Reviewed exact74.8s stationary and149.9s walking: castle retained; terrain gaps/seams, flat far landscape/sparse vegetation remain unacceptable. Remaining GPU demand/far handoff migration and400FPS goal unchanged.


### GPU build demand and page reclamation checkpoint (2026-09-07)

- GPU LOD classification preserves band/frustum/ownership in existing state-buffer high bits. One bounded async readback, topology/settings rejection, main-thread live source-generation checks, callback lifetime drain only at disposal. No extra GPU buffers. Removed CPU camera demand refresh and candidate band/frustum work from production.
- First implementation used all-node host application and visible/background FIFO. `gpu-demand-feedback.xml`510 pass23s; after telemetry/count retention, `gpu-demand-feedback-final.xml`510 pass19s. Module48s/seven captures/exit0, all seven markers,262.5MB, framep95/p99 .816/2.843ms; exact42s fort reviewed, prototype quality.
- `gpu-demand-feedback-showcase`180s/12 captures/exit0: stationary280.1867FPS CPU3.59ms; walking252.35FPS CPU3.745ms.500missing,1589allocations failures/evictions,3234publications but only661resident candidates and zero retained coarse candidates at end. This is a rejected performance/coverage checkpoint. Host feedback application~1ms; all-node application and loss of near-first background order require correction. Original sources/hashes/diff preserved with artifacts.
- Revised transport carries 25-bit GPU squared-distance rank (1/16m², saturation tested). Background admission compares bounded GPU ranks; visible FIFO stays prioritized. Unchanged stationary camera/projection/readiness/settings reuse last feedback; unchanged inactive nodes skip callbacks. Host age stamps reuse GPU in-band tags, no camera tests. MarkDirty promotes cached GPU-visible demand; rejected generations explicitly requeue without a new camera query.
- `gpu-demand-reuse.xml`512 pass23s; added generation-rejection and GPU numeric distance proofs, `gpu-demand-reuse-final.xml`513 pass20s. Tightened float-representable rank saturation plus regression: final `gpu-demand-ranked-final.xml`**513/513 pass**, zero skipped,19s,exit0. Final module/full Showcase validation pending.

- `gpu-demand-ranked-module`:48s/seven captures/exit0,all markers,framep95/p99 .819/.925ms,prepare .029/.031ms,262.5MB. Exact42s fort intact (prototype). `gpu-demand-ranked-showcase`:180s/12captures/exit0,330/265FPS (329.7067/265.275),CPU2.89/3.45ms,523missing/1532evictions. Query reuse improved static cost, but background distance ordering alone did not fix churn; not accepted as complete coverage/performance.
- `gpu-demand-arena-diagnostic`: temporary synchronous arena/live/pending bookkeeping reads during180s/12captures,exit0. Near end: vFree4+vLive7859+vRetired19=7882 versus25197 total (**17315 vertex pages unaccounted**); iFree15982+iLive6052+iRetired14=22048 exact total. GPU live873==CPU resident873,pending0. This isolates allocator reclamation rather than untracked live handles or merely large geometry. Synchronous profiling is diagnostic only; restored scheduler source from saved before-diagnostic copy afterward.
- `gpu-page-conservation-before.xml`:three real GPU release/reclaim cases all fail on first cycle. Four handles×13 vertex pages retire52, but free count rises12→13 instead of64; reversed9-page case gives29 instead of64; one-page case61 instead of64. Retirement accounting itself passed. `gpu-page-conservation-after.xml`:3/3 pass after replacing nested UAV-subscript post-increment with local free counters and final stores in ReclaimVertexPages/ReclaimIndexPages.16 cycles/case verify full free capacity, unique/in-range page IDs. No retirement-delay/budget change.
- Added two multi-page pending supersession cases, also pass; same nested expression in that distinct path was not observed failing, so no speculative rewrite there. `gpu-demand-page-reclaim.xml`:**518/518 Rendering tests pass**, zero skipped,19s,exit0. Final module/clean Showcase validation pending.

- Final `gpu-demand-page-reclaim-module`:48s/seven captures/exit0; all seven markers including edit/restart/far proxy restoration. Zero missing/fallback/errors/finalizers,262.5MB,framep95/p99 .820/.910ms,prepare .028/.030ms. Exact42s module fort reviewed, prototype/blockout quality, behavioral evidence only.
- Final `gpu-demand-page-reclaim-showcase`:180s/12 captures/exit0, all forbidden-error checks clear. Stationary60–90s30 samples **331.5267FPS**, CPU p50 median2.885ms,GPU diagnostic5.86ms; walking120–180s60 samples **255.8383FPS**,CPU3.51ms,GPU diagnostic1.84ms. Last committed checkpoint350.1567/218.7483FPS: stationary regresses, walking improves with greater retained coverage; not400FPS acceptance.
- Final **331 missing,zero allocation failures/pressure evictions**,3913publications,2404resident GPU candidates/5735nodes,step1res1165/pub2586,step2res1103/pub1191,step4res115/pub115,step8res21/pub21. Oldest active4.031s (fine). Source40270/40270,slotChecks0,dirRefused0,recoverypub456626. Feedback10812accepted/1613discarded/0errors,final apply .679ms; stationary query reuses skip application.
- Saved source hashes/patch/parentHEAD alongside exact module/Showcase players; final source/test hashes match. Reviewed74.9s stationary castle and149.9s walking terrain. Castle retained; terrain patch seams/gaps and flat far landscape/sparse vegetation remain unacceptable. Loading gaps, far handoff/eviction GPU migration, helper retirement, long-session/canonical integration gates and400FPS remain open.

### Resident far-instance migration (in progress)

- Replaced per-frame far matrix rebuilding/copying with persistent GPU object/inverse transforms, compute compaction and indirect per-submesh arguments. Source equality retains batches through unchanged producer queries; configurable65536-instance ceiling rejects overflow without hiding existing content. Production URP forward lighting/includes retained in the procedural transform shader. CPU replacement proof remains the next migration boundary.
- `gpu-far-resident-tests.xml`:522/522 Rendering tests pass,0skipped,26s/exit0. Real GPU compaction cases1/65/1025 instances exercise all/none/alternating visibility, unique source indices, submesh args and nonuniform transforms; repeated16 enable/handoff/source-update cycles preserve batches.
- `gpu-far-resident-module`:FarGeneratedLandmarkValidation28s/seven captures/exit0,ready/success markers,2instances/7ramps,no forbidden errors. Reviewed20s standalone: production geometry is present; isolated landmark remains prototype/blockout quality, not final art acceptance.
- Initial `gpu-far-resident-showcase`:180s/12captures/exit0,stationary275.0667FPS/CPU3.5ms,walking244.8167FPS/CPU3.6ms,434missing,zero allocation failures/evictions. Rejected performance checkpoint. Stationary near replacement hides all106far instances but submission still records zero-count indirect draws. Added empty-batch submission suppression; new capacity-preservation and empty-command regressions. Final tests/player rerun pending. Initial source snapshot/hashes preserved separately from this correction.
- Corrected `gpu-far-resident-empty-tests.xml`:523/523 tests pass,0skipped,22s/exit0. Added empty-command submission assertion and configured-capacity rejection preserving existing instances. `gpu-far-resident-empty-module`:48s/seven captures/exit0; all seven production markers including edit, restart and far handoff. Zero missing/fallback/errors/finalizers,262.6MB,framep95/p99 .829/.974ms,prepare .029/.041ms,submission .009/.009ms. Exact42s fort reviewed: intact, prototype/blockout quality, behavioral evidence only.
- Corrected `gpu-far-resident-empty-showcase`:180s/12captures/exit0,forbidden errors/finalizers clear. Stationary60–90s30samples **322.26FPS**,CPU p50 median2.89ms,GPU diagnostic5.47ms; walking120–180s60samples **271.3567FPS**,CPU3.305ms,GPU diagnostic1.85ms. Versus prior332/256: no stationary gain, single-run walking improvement; this does not prove400FPS or repeated-workload acceptance.
- Final272missing,zero allocation failures/evictions,3933publications,2454resident candidates/5731nodes; step1res1217/pub2609,step2res1085/pub1172,step4res123/pub123,step8res29/pub29. Oldest active20.625s(step8), source40270ready. Feedback11309accepted/1606discarded/0errors,final application .607ms. Retaining transforms alone does not remove dominant host work; source-service latency remains open.
- Exact corrected module/Showcase source/test hashes verified matching; source patches/new-file copies/parentHEAD saved. Reviewed74.9s stationary and149.9s walking captures: castle intact; terrain seams/patch gaps, flat far landscape and sparse vegetation remain unacceptable. Module fort/isolated landmark remain prototype quality. CPU conservative far replacement, feedback work, helper retirement,400FPS, loading, memory/endurance and canonical integration gates remain open.

### Incremental GPU demand feedback (in progress)

- Preserves host demand cache/accounting between GPU readbacks. Stable live metadata applies only band/frustum classification deltas and pending distance-rank changes; completed nodes skip unchanged feedback. Topology/settings/readiness changes or a host image newer than the request force full live checks. Removed repeated full application for camera-only rank changes; current generation checks, GPU age tags and retry liveness remain. No new GPU buffers, world-truth or budget changes.
- `gpu-demand-delta-initial.xml`:523/523pass,24s/exit0. `gpu-demand-delta-tests.xml`:526/526pass,0skipped,20s/exit0. New real GPU test checks classification/rank-only callbacks and delayed readiness forcing full checks;80-cycle incremental/full comparison checks missing/band/frustum accounting and dirty demand, and removal/rank isolation cannot revive coordinates. Existing nearest-background admission now verifies a rank-only update changes the selected chunk.
- `gpu-demand-delta-module`:48s/seven captures/exit0; all seven production markers including edit/restart/far restoration,zero missing/fallback/errors/finalizers,262.6MB,framep95/p99 .820/.904ms,prepare .028/.029ms,submission .008/.009ms. Exact42s fort reviewed: intact but prototype/blockout quality, behavioral evidence only. Exact source hashes/patch/parentHEAD saved alongside module/Showcase. Full Showcase still running.
- Initial `gpu-demand-delta-showcase`:180s/12captures/exit0,331.6133/281.5933FPS,CPU2.825/2.915ms,GPU diagnostic5.915/1.615ms. Final318missing versus272 at parent,3670publications/2227resident candidates,zero allocation failures/evictions,oldest4.088s(step8). Feedback11832accepted/1604discarded/0errors,2675full resets,8168228coordinate refreshes,11084011rank-only updates.38 sampled nonzero apply times median.1205ms. All forbidden checks clear; exact source hashes match;74.8s/149.9s reviewed, castle retained but terrain/far scenery defects remain.
- CPU feedback work fell, but coverage worsened in this run; do not call this full-performance acceptance. Reviewed all build rejection/reset/admission paths: rejection/failure explicitly requeues; slot admission refusal is outside the current toroidal window, not in-window capacity exhaustion. Added bounded residency-report diagnostic for in-band pending demand lacking both a current build and durable queue membership. `gpu-demand-delta-liveness.xml`:527/527tests pass,24s/exit0,0skipped. Diagnostic regression distinguishes queued/current-empty/out-of-band/unqueued work. Repeating exact player traversal to resolve the coverage concern.
- Final `gpu-demand-delta-liveness-module`:48s/seven captures/exit0; all seven markers,zero missing/fallback/errors/finalizers,262.6MB,framep95/p99 .889/.914ms,prepare .031/.035ms.42s exact capture reviewed, prototype/blockout fort unchanged.
- Repeated `gpu-demand-delta-liveness-showcase`:180s/12captures/exit0,stationary **331.68FPS**/CPU2.87ms/GPU diagnostic5.83ms; walking **285.4633FPS**/CPU2.90ms/GPU diagnostic1.60ms. All164 residency samples report **unqueued=0**, so no sampled in-band pending demand lacked a current build/durable queue membership. This does not prove full coverage or exclude service-latency problems.
- Final350missing,zero allocation failures/evictions,3380publications/2133resident candidates/5735nodes. Step1res1027/pub2196,step2res919/pub997,step4res167/pub167,step8res20/pub20; oldest21.62s(step8),source40270ready.11894feedbackaccepted/1612discarded/0errors;2338full resets,7378021coordinate refreshes,12126301rank-only updates.39 sampled nonzero apply times median.129ms. CPU feedback cost fell, but fewer resident chunks and worse missing count versus parent272 mean this is not a clean full-workload performance win or400FPS acceptance. Coverage/source-service investigation remains open.
- Exact final source/test hashes match module/Showcase artifacts.74.9s/149.9s captures reviewed: castle retained, terrain/far scenery defects remain unacceptable. Next discriminate128solid indirect submission overhead (known empty-far draw overhead already caused a regression) with fewer GPU size buckets while preserving every handle/index, then continue GPU far replacement. No budget, distance or content reductions authorized.

### GPU draw submission grouping experiment

-32power-of-two groups replace128quarter-power groups without changing selection, handles, indices, banks, materials or budgets. Existing600-handle scatter/prefix regression now also proves padding<2x; LOD fixtures use configured count. `gpu-draw-32-tests.xml`:527/527pass,0skipped,24s/exit0. `gpu-draw-32-module`:48s/seven captures/exit0,all seven markers,zero missing/fallback/errors/finalizers,262.6MB,framep95/p99 .714/.910ms,prepare .028/.029ms,submission .002/.003ms.42s exact fort reviewed, intact prototype/blockout fixture.
- `gpu-draw-32-showcase`:180s/11captures/exit0; required minimum9 captures satisfied. Stationary30samples330.9367FPS/CPU2.8ms/GPU diagnostic7.76ms; walking59samples294.0475FPS/CPU2.88ms/GPU diagnostic1.71ms.353missing,zero allocation failures/evictions,3798publications,2273resident candidates/5735nodes,oldest4.008s(coarse). All163liveness samples unqueued=0. Source hashes match. Stationary does not improve and GPU timing increases; wider padding can offset reduced host submission. Testing64half-power groups (<1.5x padding) next;400FPS/coverage gates remain unmet.

64-group comparison: 527/527 Rendering EditMode tests passed (23s, zero skipped). Production module harness completed 48s/seven captures/exit 0; all seven behavior markers passed, zero missing/fallback/errors, 262.6 MB. Frame p95/p99 0.804/0.992 ms, prepare 0.031/0.033 ms, submission 0.005/0.005 ms. Exact 42s standalone screenshot reviewed: fort remains intact, prototype/blockout quality; no full visual acceptance. 180s VoxelShowcase comparison running; exact production/test hashes and patch preserved in `Artifacts/LocalGpuShowcase/gpu-draw-64-showcase`.

64-group VoxelShowcase completed 180s/12 captures/exit 0. Stationary 60–90s: 30 samples, 338.55 FPS, CPU median p50 2.80 ms, GPU diagnostic 6.185 ms. Walking 120–180s: 60 samples, 288.282 FPS, CPU 2.81 ms, GPU diagnostic 1.73 ms. Final 272 missing, zero allocation failures/evictions, 4074 publications, 2537 candidate residents/5735 nodes, oldest step2 build 8.790s. All 164 demand samples unqueued=0; 12006 accepted/1618 discarded/zero errors. Source/test SHA256 matches preserved patch. Exact 74.9s stationary and 150.0s walking screenshots reviewed: castle intact, terrain gaps/seams, flat far landscape and sparse vegetation remain unacceptable. Keep64 as interim fewer-submission checkpoint; single runs with unequal coverage do not prove a robust FPS gain.400FPS and full GPU migration remain incomplete.

Next indexed-draw experiment: official Unity6000 API supports CommandBuffer.DrawProceduralIndirect(GraphicsBuffer indexBuffer, ...) with five-word arguments and Index|Raw compute-writable storage. Compact only GPU-selected live bank indices to physical vertex IDs, then draw without chunk padding. Single shared output stream requires graphics-queue-ordered compaction/draw; never reuse stale selected output across camera/publication changes. Budget scratch and page descriptors against aggregate tier geometry allocation, preserving existing arena capacity. Existing constructor assigns 75% of geometry budget to primary vertex/index buffers; remaining capacity must be audited against page metadata/water before use. Validate noncontiguous pages, bank swaps/release, exact counts, existing production shader rasterization, module and Showcase. API: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.CommandBuffer.DrawProceduralIndirect.html ; https://docs.unity3d.com/6000.0/Documentation/ScriptReference/GraphicsBuffer.Target.Index.html .

Hardware-indexed experiment (uncommitted): added GPU page-copy dispatcher/shader and production indexed shader variant. Exact selected index ranges use a single GPU-owned Index|Raw stream, recorded before draw on the same graphics queue; no CPU output readback, no padded indices. Two new behavioral checks cover noncontiguous pages, a triangle crossing index-page boundaries, vertex-page remapping, bank changes/removal, and production shader rasterization. Initial529 run had527 pass/two obsolete architecture-string failures; updated the checks for the new path and added a production-module one-draw assertion. Rerun529/529 pass/zero skipped, wrapper19s/exit0. Module all seven markers observed, terminal result pending.

Memory audit gate remains: full scratch costs180,970,016 bytes on PC; primary geometry+scratch+water geometry1,198,776,864 bytes. Including primary arena/LOD metadata gives1,281,024,216 bytes before water metadata; within PC1,342,177,280 budget. Console/mobile full scratch aggregate does not fit; mobile metadata already exceeds its envelope before this change. Do not claim tier-safe or commit the unrestricted allocation as finished. Measure PC performance first, then bounded tiled scratch/metadata design preserving arena capacity, or reject experiment if no benefit. Audit in Artifacts/LocalGpuShowcase/gpu-index-stream-memory-audit.json.

Indexed module terminal exit0,48s/seven captures. All seven markers passed, one solid draw, zero missing/fallback/error. Frame p95/p99 .715/.728ms, prepare .029/.031ms, submission .003/.003ms,262.8MB reported allocation. Exact42s standalone capture reviewed: intact fort, prototype/blockout quality, behavior evidence only.180s Showcase running, source snapshots/hashes including new files preserved under gpu-index-stream-showcase.

Indexed stream (bucket-fed) Showcase terminal exit0,180s/12 captures. Stationary30 samples328.86FPS, CPU2.70ms/GPU diagnostic5.105ms; walking60 samples294.557FPS, CPU2.79ms/GPU diagnostic1.655ms.310missing,zero allocation failures/evictions,4072publications/2498candidate residents,oldest31.318s(step8). All 164 demand samples unqueued=0; errors clear and archived source hashes verified. Exact74.8s/149.9s screenshots reviewed: castle intact; terrain gaps/seams/flat far landscape/sparse vegetation remain unacceptable. No robust overall FPS gain versus64groups339/288; do not claim400FPS.

Current next version bypasses four bucket preparation dispatches once indexed production drawing is active. The GPU-selected handle mask and live bank records feed page compaction directly; explicit-handle metadata regressions retain the old adapter. Added direct-selection case of noncontiguous/bank/removal regression. Suite running in gpu-index-selection-tests; module/Showcase measurement still required. Full-stream lower-tier memory gate remains unresolved, so no production checkpoint commit yet.

Direct-selection suite terminal exit0,25s,530/530 passed,zero skipped. Current production files remain uncommitted pending direct-selection module/Showcase and lower-tier memory resolution. No Unity/player sessions remain live.

Direct GPU selection into indexed drawing: module harness terminal exit0,48s/seven captures; all seven markers passed with one solid draw,zero missing/fallback/error. Frame p95/p99 .715/.721ms,prepare .028/.030ms,submission .003/.003ms,262.8MB reported allocation. Exact42s screenshot reviewed: intact fort,prototype/blockout,behavior evidence only.

Direct-selection Showcase terminal exit0,180s/12 captures. Stationary60–90s30 samples333.493FPS,CPU median p50 2.70ms,GPU diagnostic4.75ms. Walking120–180s59 samples297.176FPS,CPU2.80ms,GPU diagnostic1.54ms.229missing,zero allocation failures/evictions,3777publications/2371candidate residents,oldest13.722s(step8). All 164 demand samples unqueued=0; source/current and archived hashes match, errors clear. Exact74.9s stationary and149.9s walking screenshots reviewed: castle intact; terrain seams/gaps,flat far landscape and sparse vegetation still unacceptable. Evidence in Artifacts/LocalGpuShowcase/gpu-index-selection-showcase. Compared to64buckets339/288, this is no robust overall400FPS result; coverage/workload differs.530tests were already green for this exact source. No production edits this validation turn, no live Unity/player process remains, no commit/push.

Next GPU migration target is far replacement proof, still CPU UpdateReplacement→HasCurrentReplacement per instance. Production SurfaceDiscoveryCoverage stores512 occupancy bits plus completeness per resident region (bounded1024); discovery Begin/Add/Complete/Invalidate/Forget events supply the GPU metadata boundary. Preserve global journal-caught-up guard and region residency, negative/exclusive bounds,4096fine-cell bound, ring ownership and current selected/live-bank checks. Use shared GPU query/visibility storage across batches; avoid one compute setup per instance. Removing CPU known-empty batch suppression can reintroduce106empty draw submissions (previous measured regression), so draw-data consolidation remains part of this migration. No GPU proof implementation yet. Full index-stream lower-tier memory gate remains open before checkpoint.

GPU discovery prerequisite: added versioned snapshots of the existing production SurfaceDiscoveryCoverage, a bounded three-buffer GPU mirror (270,336 bytes), GPU region hashing and known-empty queries for steps1/2/4/8. Unknown, incomplete, invalidated and evicted regions cannot prove empty. In-progress additions stay unpublished until Complete; stable revisions reuse GPU buffers. Source identity prevents reuse across worlds with matching revision numbers. GPU results remain presentation evidence, never authoritative Storage/collision inputs.

Validation: gpu-discovery-map-tests.xml final run532/532 passed,zero skipped,wrapper20s/exit0. Real GPU differential tests cover all512 fine cells at every LOD,negative coordinates,32/64-bit boundaries,completion/invalidation/rediscovery/eviction through every buffered slot,additions while incomplete,matching-version world replacement,full1024-region capacity/hash collisions/rejected admission/slot reuse/clear. New helper is not yet wired to far visibility, so this is a tested GPU-query prerequisite, not completed CPU handoff retirement or player validation. Existing SolidGpu module scene will validate the integrated path when connected; no new FPS claim. CPU far bounds/current-publication checks and global journal/residency gates remain to move/bind. Source hashes preserved in gpu-discovery-map-source-sha256.json.

GPU far handoff integration in progress: shared discovery HLSL lookup now feeds GpuFarCoverageDispatcher, which hashes current production GPU-selected LOD nodes and checks bounded feature AABBs. Completed region evidence required for every cell; empty cells need no geometry, current selected geometry covers occupied visible cells, offscreen cells use GPU frustum proof. Journal/residency gate remains authoritative host metadata; nonresident discovery records are pruned before GPU upload. Rendering pass now supplies GPU coverage rather than calling HasCurrentReplacement per instance. GpuFarDrawSet owns shared query/visible-index/argument/metadata buffers; one GPU compaction dispatch writes all batch submesh instance counts. Persistent per-batch transforms and geometry stay on the production path. Aggregate async readback is diagnostics only, never CPU draw suppression. Empty GPU submissions remain a performance risk to measure.

First integrated535/535 Rendering tests passed, zero skipped,24s/exit0. Coverage tests use production GPU LOD selection and reject incomplete discovery, stale/unselected geometry, closed edit gate, unknown/exclusive-negative boundaries, oversized/distant/invalid bounds. Shared compaction tests check separate3/65-instance batches and all submesh arguments/indices; residency loss is checked without a world edit. Final frustum subcase and callback/lifetime cleanup rerun pending in gpu-far-handoff-tests; module/Showcase not yet run for integrated source. No400FPS or visual-acceptance claim.

Full GPU far handoff baseline: final535/535 tests passed/zero skipped,21s/exit0. Production module48s/seven captures/exit0; all markers including initial proxy→near replacement→edit proxy restoration passed; zero missing/fallback/errors. Frame p95/p99 .707/.718ms,prepare .026/.027ms,submission .005/.006ms,one solid draw,263.3MB allocation. Exact42s screenshot reviewed,intact fort,prototype/blockout quality.

Showcase gpu-far-handoff-showcase completed180s/11captures/exit0. Stationary29samples296.452FPS,CPU3.17ms/GPU diagnostic8.54ms; walking60samples240.703FPS,CPU3.40ms/GPU diagnostic2.405ms. Render-thread median p50 rose .32→.92ms stationary and .32→.96ms walking versus direct indexed selection, consistent with now-submitted empty far MeshInstancedIndirect draws.359missing,zero allocation failures/evictions,3657publications/2269candidate residents,oldest21.857s(step8);163demand samples unqueued=0. Archived source hashes verified,errors clear. Exact74.9s and150.1s screenshots reviewed: castle remains intact; additional flat gray rock/far proxy surfaces and a larger flat green foreground patch appear alongside existing terrain gaps/seams/sparse vegetation. Unacceptable visual finish; do not claim fidelity or performance acceptance.

Next version (not yet player validated): each coverage query uses a cooperative64-thread group with bounded cell reduction;1024-wide dispatch rows also support65536queries/batches without exceeding per-axis dispatch limits. Added a1025-query row-boundary regression. Found missing current-empty extraction proof: conservative discovery may report surface where current extraction completed empty. GPU coverage now accepts only current owned/in-band empty nodes using production LOD state; tests include rejection when that ring is disabled. Source geometry capacity,draw distances and budgets unchanged. Parallel/current-empty suite running in gpu-far-parallel-query-tests; standalone module and Showcase remain required.

Likely next submission experiment: replace far DrawMeshInstancedIndirect encoding with hardware-indexed procedural draws using the existing production Mesh vertex/index buffers and GPU transforms; inspect official Mesh buffer APIs and URP vertex layout first. This could avoid mesh submission overhead without duplicating all instance geometry into a large atlas. Preserve production materials/lighting/UVs/normals and verify real raster equivalence. No such vertex-pull implementation exists yet. Full index-stream lower-tier budget and helper/pressure cleanup still open.

Parallel/current-empty query suite terminal exit0,24s,535/535 passed,zero skipped. No live Unity/player sessions remain. Current source remains uncommitted pending corrected module/Showcase and budget/performance gates.

Parallel/current-empty module gpu-far-parallel-module finished: build29s/exit0, player48s/seven captures/exit0. All seven runtime markers passed, including edit restoration/restart/far handoff; zero missing/fallback/errors. Frame p95/p99 .713/.802ms, prepare .027/.030ms, submission .004/.006ms, one solid draw,263.3MB. Exact42s capture reviewed: intact fort, prototype/blockout quality; behavioral evidence only. Corrected Showcase gpu-far-parallel-showcase is now running; source snapshot, hashes, patch and parent HEAD archived before any further production edits.

Corrected gpu-far-parallel-showcase completed180s/12captures/exit0: stationary30samples300.377FPS,CPU median p50 3.01ms/GPU diagnostic4.60ms; walking60samples274.94FPS,CPU2.89ms/GPU1.695ms. Render-thread median p50 .95/.84ms remains above pre-handoff .32/.32ms.327missing,zero allocation failures/evictions,3894publications/2429candidate residents,oldest21.820s(step8),all demand samples unqueued=0. Archived source hashes match current code. Reviewed exact74.9s/149.9s captures: castle intact, but large left gray proxy, flat foreground patches, terrain gaps/seams and sparse vegetation persist; unacceptable visual finish. Parallel queries improve walking against serial296/241 baseline but do not recover prior333/297 indexed performance or meet400FPS. Current-empty correction did not resolve visible proxy defects. No live player remains. Next discriminating experiment: hardware-indexed procedural submission of existing far Mesh GPU buffers, preserving production Lit shading and geometry; current generated far meshes contain positions/normals with no authored UV/tangent channels. This changes submission overhead before any geometry-atlas consolidation. Unity APIs support raw access to existing Mesh vertex buffers and indexed CommandBuffer.DrawProceduralIndirect; no implementation has been applied yet.

Far procedural submission experiment: GpuFarInstanceBatch retains references to immutable production Mesh position/normal/index GPU buffers and issues hardware-indexed DrawProceduralIndirect; the shader reads those buffers by SV_VertexID and feeds the existing URP Lit vertex/fragment path with the same persistent transforms and GPU visible indices. No geometry atlas, readback admission, content reduction or material change. Buffer targets change only before the first retained reference; Dispose releases wrappers. Explicitly reject unsupported vertex layouts instead of silently dropping attributes. Final gpu-far-procedural-final-tests.xml535/535passed,zero skipped,18s/exit0 (initial535passed23s). gpu-far-procedural-module build30s/player48s/seven captures/exit0; all seven markers passed, zero missing/fallback, frame p95/p99 .709/.718ms,prepare .029/.031ms,submission .004/.004ms,one solid draw,263.3MB. Exact42s screenshot reviewed:intact fort,prototype/blockout; behavior only. Source snapshots/hashes/patch/parentHEAD preserved for module and running gpu-far-procedural-showcase. FPS impact pending.

Far procedural experiment terminal: gpu-far-procedural-showcase180s/12captures/exit0, exact source hashes verified. Stationary30samples303.383FPS,CPU2.995ms/GPU diagnostic5.215ms; walking60samples227.702FPS,CPU3.69ms/GPU2.20ms. Render thread .92/.96ms versus baseline .95/.84ms; no convincing submission benefit.264missing,zero allocation failures/evictions,4023publications/2500candidate residents,oldest23.280s(step8),all demand unqueued=0. Recorded process-load.txt: background VM233%CPU and media/indexing services substantial; workload coverage also differs, so do not claim clean causal walking regression. Exact74.9s/149.9s reviewed: castle intact, gray proxy replaced by dark rock in stationary capture, but flat terrain patches/gaps/seams/sparse vegetation persist and remain unacceptable. Selected action: reject the per-mesh procedural API substitution and restore its two files from the exact parallel/current-empty source archive. All49 archived Rendering source hashes now match the previously535-test/module/Showcase-validated baseline. Experiment source remains archived. No live Unity/player processes remain. Next structural experiment must consolidate far geometry batches across shared materials, with bounded GPU-visible geometry-page descriptors and preserved production source geometry/shading; changing only draw API did not solve overhead.

Material-grouped far submission implemented (uncommitted): one atlas per source set stores each unique production mesh once; GPU copies persistent transforms once and emits bounded192-index geometry pages for each visible instance/submesh. Pages reference source indices and transforms; shared material groups draw once each. Existing URP Lit shading is retained with a vertex-pulling variant; padding produces degenerate triangles. GPU replacement proof remains authoritative for presentation. Legacy explicit CPU-oracle consumer is unchanged. Initial535tests and new536-test suite passed,zero skipped (29s/25s); new real GPU test covers material merging, shared geometry exact indices, transform copying, multi-page boundaries and65-instance dispatch boundary, all/none/alternating proof, exact per-instance triangle sums/no duplicate pages. Added source-build draw/memory diagnostic after tests. Module/Showcase pending.

Material-grouped module completed build33s/player48s/seven captures/exit0. All seven markers passed,including edit proxy restoration and restart,zero missing/fallback/errors. Frame p95/p99 .713/.719ms,prepare .027/.029ms,submission .004/.004ms,one solid draw,264.0MB. Exact42s capture reviewed:intact fort,prototype/blockout,behavioral evidence only. Module source has2batches/2materialdraws,2instances/2uniqueMeshes,9pages,21024additional resident bytes. Full Showcase running in gpu-far-material-showcase;53source files/hashes/patch/parentHEAD archived for both players.

Material grouping Showcase gpu-far-material-showcase finished180s/12captures/exit0; source hashes verified.106batches→11material draws,106instances/106uniqueMeshes,366worst-case pages,964576additional resident bytes. Stationary30samples321.013FPS,CPU median p50 2.76ms/GPU diagnostic10.545ms; walking59samples271.034FPS,CPU2.89ms/GPU1.99ms. Render thread median p50 .40/.40ms versus prior .95/.84ms: draw consolidation materially reduces submission cost, but does not meet400FPS and walking mean is not improved.281missing,zero allocation failures/evictions,3944publications/2361candidate residents,oldest27.095s(step8),all demand unqueued=0. Background VM/media activity remains in process-load.txt; unequal readiness and load prevent broad single-run gain claims. Exact74.8s/149.9s captures reviewed: castle intact, dark left rock visible, persistent terrain patches/gaps/seams and sparse vegetation; unacceptable visual finish. Keep the structural draw reduction. Current source still performs the obsolete per-batch shared index/argument compaction and attachment solely alongside the new material path; remove that duplicated production work, retain only bounded diagnostic count updates at a suitable cadence, then remeasure. New atlas tests also need distinct-mesh offsets and lifetime/input-layout coverage before checkpoint. No live player remains.400FPS and all remaining gates stay open.

Far material cleanup: removed obsolete shared per-batch visible-index/argument/metadata buffers, compaction dispatch and CPU attachment loop from active GpuFarDrawSet. GPU count reduction reads replacement bits only for one4-byte diagnostic snapshot at most10Hz; current material arguments still update every frame.538/538 tests passed,zero skipped,24s/exit0. Added real GPU instance-count reduction checks, distinct production box/cylinder mesh offsets with exact vertices/normals/indices, repeated buffer lifetimes, and unsupported vertex-channel rejection. gpu-far-material-cleanup-module build27s/player48s/seven captures/exit0,all seven markers including edit proxy restoration passed with lower diagnostic cadence. Zero missing/fallback/errors;frame p95/p99 .717/.724ms,prepare .027/.028ms,submission .004/.004ms,one solid draw,264.1MB. Exact42s capture reviewed:intact fort,prototype/blockout.53source files archived for module and running cleanup Showcase.

After cleanup Showcase finished building, separate source edit added reuseInputImage from the scheduler cached-candidate branch to PrepareLod. The scheduler already checks every worker demand/readiness/known/band version and clipmap membership; previously the dispatcher then rescanned all input lists anyway (~.12ms). Reuse now skips only redundant input-image comparison; dynamic camera/band GPU classification and buffered uploads remain. Added count-only-list regression that throws if source items are rescanned while checking GPU selection across nine frames/all slots, current frustum/band changes, and explicit changed membership. This edit is NOT in the running cleanup Showcase binary; testing awaits that player terminal.

Far material cleanup Showcase finished180s/11captures/exit0. Stationary30samples367.127FPS,CPU median p50 2.45ms/GPU diagnostic7.955ms; walking60samples302.687FPS,CPU2.505ms/GPU1.865ms. Render thread .34/.33ms.402missing,zero allocation failures/evictions,3703publications/2127candidate residents,oldest21.057s(step8),all demand unqueued=0. Exact archived source hashes verified; current runtime differs only by later input-reuse edits, plus added LOD test.76.8s/151.8s captures reviewed: castle intact, broad flat foreground patches/terrain gaps/seams/sparse vegetation persist; unacceptable. Higher missing count and unequal workload forbid acceptance from FPS alone.
LOD input reuse test run gpu-lod-input-reuse-tests.xml539/539passed,zero skipped,27s/exit0. New regression proves current GPU frustum/band selection across all buffered slots without source-item reads, and explicit membership changes still update. Current reuse module launched; no Showcase evidence for this additional edit yet.

LOD input reuse module finished build29s/player48s/seven captures/exit0. All seven runtime markers passed;zero missing/fallback/errors. Frame p95/p99 .714/.804ms,prepare .026/.026ms,submission .004/.004ms,one solid draw,264.1MB. Exact42s screenshot reviewed:intact fort,prototype/blockout,behavioral evidence only.54source files and hashes/patch/parentHEAD archived for module and running gpu-lod-input-reuse-showcase.

LOD input reuse Showcase gpu-lod-input-reuse-showcase finished180s/11captures/exit0; all54source hashes match current runtime/tests. Stationary29samples384.941FPS,CPU median p50 2.41ms/GPU diagnostic8.16ms; walking60samples320.033FPS,CPU2.39ms/GPU1.84ms. Render thread .32/.315ms. Stable LOD inputCpu now0.000ms; current candidate-refresh frames still do full host metadata collection.262missing,zero allocation failures/evictions,3860publications/2431candidate residents,oldest6.383s(step8),all demand unqueued=0. Exact76.8s/151.8s captures reviewed: castle intact, broad flat foreground patches/terrain gaps/seams/sparse vegetation remain unacceptable. Coverage improves versus prior402missing but is incomplete;400FPS remains unmet. Background process-load snapshot preserved. Current tests539/module/Showcase all terminal, no live Unity/player remains. Next inspect host metadata refresh/source-service spikes with targeted timing/native sampling; CollectVisibleCoordinate currently computes CurrentBuildCoversDesiredGeneration before the GPU-candidate early return even though that result is only used in the legacy CPU branch. Moving that query below the GPU return is a concrete redundant-work cleanup; broader incremental readiness transport needs a discriminating experiment before editing.

Host profile experiment (unchanged renderer): gpu-host-profile-showcase210s/13captures/exit0. Sampling at logged186.8s,5s/1ms,exit0,after60–90/120–180 FPS windows. Repeat windows379.297/325.712FPS,CPU2.33/2.32ms; full-run final318missing,zero allocation failures/evictions.3466main-thread samples:1852scripted render loop,1014behaviour Update,285presentation wait. Managed frames unresolved, so no individual-method bottleneck claim. Reinspection found CurrentBuildCoversDesiredGeneration is only three field comparisons, not a dictionary walk; defer micro-cleanup as unlikely to explain the gap.
Added opt-in one-shot Mono symbol resolver to ShowcasePlayerHarness, enabled only by -voxel-symbol-request and by default after185s. Reads at most4096sampled addresses, uses exported mono_pmip/mono_free, writes resolved TSV. Library exports confirmed locally and Mono documentation/source consulted. Diagnostic does not alter renderer/world behavior. Managed-profile player and post185s sampling orchestrator running; resolver acceptance pending.

- Resolved managed standalone profile: `gpu-managed-profile-showcase` exited0 after210s/13captures;
  2225 native addresses resolved. Of3447 main-thread samples, timing Snapshot owns521 inclusive
  (~15%), far presentation Query836 (~24%), including150 self in BoundsFor and106 ProjectedPixels.
  Inclusive nested counts cannot be summed. Cheap CurrentBuildCoversDesiredGeneration field guard
  deprioritized. Implemented exact incremental128-sample timing order; behavioral reference and
  standalone performance validation pending. Upstream far GPU selection remains required work.

- Timing-window validation:540/540 Rendering EditMode tests passed,0skips,wrapper exit0/23s.
  `gpu-timing-window-module` build33s/player48s exited0,7captures/all seven markers,
  0missing/fallback/errors, one solid draw,264.1MB, framep95/p99 .312/.322ms versus prior
  .714/.804ms. Exact42s standalone capture reviewed: intact fort, prototype/blockout quality;
  behavioral pass only. Showcase180s comparison running. Far migration requires a versioned spatial
  working set: current IFeaturePresentationSource and IStructureVisualStateSource expose no change
  revision; add a reusable change boundary rather than assuming camera stability means state stability.

- `gpu-timing-window-showcase`: build19s/player180s exited0,11captures,no forbidden errors.
  Standard windows173.153FPS stationary(30samples),367.710walking(59); CPU5.185/1.99ms,
  GPUdiagnostic22.95/2.1ms. Main-thread medianp50 1.995/1.98ms versus prior2.21/2.39;
  stationary presentation wait3.065ms versus.12ms. Background VM224%CPU/media89%/indexing40%
  recorded; cause of excess GPU/presentation wait not isolated, so no overall FPS improvement claim.
  Final307missing,0allocation failures/evictions. All57 archived/current source hashes matched.
  Exact76.8s/151.8s captures reviewed: castle intact, terrain gaps/seams and sparse vegetation remain
  unacceptable. Keep exact bounded timing fix (module improvement and lower main time); isolate
  presentation wait in a repeat workload before concluding total FPS effect. Far selection migration
  remains next substantive GPU work; this timing change itself moves no work to GPU.

- GPU far selection migration: production Showcase now uses revisioned manifest/semantic state and
  a padded resident candidate set; per-camera selection runs in GpuFarSelection before material page
  compaction. Exact active query bounds, existing distance caps and projected-size hysteresis retained.
  Source changes remap prior tier history GPU-to-GPU by stable ID; no admission readback. Geometry
  and authoritative state remain production-derived. Unversioned sources conservatively requery.
  First test attempt hit a validation-script scope error, fixed; final598/598tests passed0skips:
  Rendering544, Composition7, Showcase28, Structures19 (25s wrapper exit0). GPU policy parity covers
  1/65/1025sources across16camera frames, caps, active-query edges, replacement bits and history
  remapping/repeated disposal. Revision tests cover source replacement and removed/ruined/restored
  structures while geometry remains cached.
- `gpu-far-selection-module`: build32s/player48s exit0,7captures/all seven markers,
  0missing/fallback/errors, one solid draw,264.3MB, framep95/p99 .319/.324ms. Exact42s capture
  reviewed: intact fort, prototype/blockout quality; behavioral pass only. Archived77source hashes.
  Showcase180s and Composition far-modifier validation pending; module-local semantic-state owned
  revision test added, execution pending. Full tier memory and long-session cache retirement remain gates.

- `gpu-far-selection-showcase`: build19s/player180s exit0/11captures/no forbidden errors.
  Stationary396.56FPS(30samples), walking455.322FPS(59); CPU2.255/1.68ms, main.87/1.10ms,
  presentation wait1.34/.13ms, GPUdiagnostic9.73/4.86ms. Final240missing,215allocation failures and
  215evictions,4371publications,0unqueued; increased throughput now reaches pressure.77source hashes
  matched. Exact76.8s/151.9s captures: castle intact, house proxies present, persistent terrain gaps,
  seams/sparse vegetation unacceptable.110resident far objects versus106,11draws,979872atlas bytes
  versus964576 (+15296). Stationary target unmet; pressure/coverage equivalent workload unproven.
- `gpu-far-selection-composition`: build22s/player28s exit0/7captures, both required markers,
  no forbidden errors,1371canonical modifiers excluded and110additive sources. Exact24s screenshot
  is close green terrain only: camera/framing fails useful visual evidence. Record as validation defect;
  do not call module visuals accepted. Fix camera using production-derived bounds/terrain before closure.
- Next pressure audit located active CPU `GpuSolidChunkCache.EvictFarthest`, driven by global GPU
  allocation failures in VoxelSurfaceScheduler. It scans offscreen leases and chooses farthest victims;
  current215evictions make this migration relevant. Also investigate repeated full GPU index compaction
  once pressure behavior is understood. Do not lower capacity, distance or coverage requirements.
- WorldBuilder-owned revision test initially discovered0tests because pre-existing
  TemporaryMasterTestDisable.cs shadows [Test]. Qualified the two state tests with NUnit.Framework.Test;
  rerun pending. No unrelated quarantine changes made; broader module validation remains incomplete.

- WorldBuilder state rerun actually executed2/2 tests,0skips,wrapper exit0/13s. All current source files
  passed diff whitespace checks. No live Unity/player sessions remain. GPU far migration is implemented
  and behaviorally validated;400FPS, pressure, coverage and the noted validation framing remain open.

- GPU index reuse experiment: selected live records are compared on GPU; an additional GPU arena
  ownership epoch invalidates on publication/release, including identical handle/count aliases.
  Unchanged streams retain indexed draw arguments and dispatch zero copy groups; legacy explicit-handle
  draws force rebuild and invalidate reuse mode. No CPU admission/readback or reduced geometry capacity.
  Bounded extra memory32bytes/handle+20bytes (262164bytes at8192handles), plus4arena-state bytes.
  545/545Rendering tests pass0skips, including exact noncontiguous physical indices, bank changes,
  removal, unchanged repeats, selection changes, page aliases, and three multi-handle release cases
  verifying every GPU commit/release advances ownership. Final test wrapper exit0/22s.
- `gpu-index-reuse-module`: build31s/player48s exit0/7captures/all seven markers,
  0missing/fallback/errors, one solid draw,264.4MB, framep95/p99 .312/.328ms. Exact42s screenshot
  reviewed: intact fort, prototype/blockout quality, behavioral evidence only.81source files archived.
  Same180s Showcase comparison running. Prior pressure timeline first rises at151.8s/3551publications;
  reaches215failures/evictions by178s/4371publications. Storedpressure-timeline.json. This separates
  late traversal pressure from the stationary GPU/presentation limit; eviction migration remains open.

- `gpu-index-reuse-showcase`: build18s/player180s exit0/11captures/no forbidden errors;
 370.127stationary/454.988walkingFPS, CPU2.36/1.63ms, GPUdiagnostic10.325/4.985ms,
 229missing,314allocation failures/evictions. Exact76.7s/151.7s images reviewed: castle intact,
 terrain gaps/seams persist. No convincing gain against397/455, so six experiment files restored
 from prior source archive or unchanged HEAD originals; all77prior source hashes match. Index reuse
 is NOT active.545test/module successes prove correctness only, not performance benefit.
- Xcode xctrace is installed with Metal System Trace template. Started105s standalone stationary
 diagnostic (autowalk10000s), with5s trace requested atFPSLOG65s. Live harness/session17897 and
 sampler7328 pending; files undergpu-metal-stationary-profile. No profile conclusions yet.

- Metal diagnostic completed:105s player/6captures exit0,5s xctrace attach atFPSLOG65.5s exit0,
 146MB stationary.trace. Exported GPU intervals, encoder list, application intervals and object labels.
 analyze_trace.py resolves XML references and joins encoder IDs.29739voxel GPU intervals; predominant
 Render Command5 fragment3861ms/2038intervals (~1.895ms each), vertex1987ms/1970 (~1.009ms),
 Compute Command0 2027ms/3223 (~.629ms), Compute Command4 359ms/1642 (~.219ms).
 Parallel channels and nested depths overlap; do not sum as frame cost. Encoder5 follows the index
 compute encoder and contains solid/water draws by source command order; no per-shader attribution
 yet. Exact76.6s capture reviewed: castle intact, persistent terrain defects. This diagnostic is not FPS
 benchmark evidence. All profile/export handles terminal.
- Next measured hypothesis targets solid fragment sampling: projection endpoints previously sampled
 both face and triplanar textures, and zero-strength distance-faded normals still fetched normal maps.
 Shader now branches only to skip unused samples, retaining fractional blend path and original weights.
 544/544Rendering tests passed0skips,wrapper exit0/22s; these include GPU geometry/draw behavior,
 not pixel-identical material proof. Standalone module and180s Showcase validation pending.

- `gpu-material-sampling-module`: build20s/player48s exit0/7captures/all seven markers,
 0missing/fallback/errors, one solid draw,264.3MB, framep95/p99 .312/.324ms. Exact42s capture
 reviewed: intact fort and material separation, prototype/blockout quality, no visible new defect;
 not numerical pixel-parity proof. Same180s Showcase comparison running.81source files archived.

- `gpu-material-sampling-showcase`: build19s/player180s exit0/11captures/no forbidden errors.
 Stationary451.53FPS(30samples), walking512.766FPS(59); CPU2.075/1.39ms, main.82/1.06ms,
 presentation wait1.22/0ms, GPUdiagnostic8.4/3.64ms.81archived/current source hashes match.
 Exact76.6s/151.6s captures reviewed: castle and materials intact, no obvious new material defect;
 terrain gaps/seams and sparse vegetation remain unacceptable. This is visual inspection, not
 numerical pixel parity. Final4509publications,266missing,302allocation failures/evictions,0unqueued,
 oldest10.265s step8. Keep the shader optimization: both standard windows exceed400 in this run.
 Full goal remains incomplete because CPU pressure victim selection, pressure/coverage, lower-tier
 memory, helper retirement and validation gates remain. Repeat after remaining migration changes.
 All Unity/player/trace/export sessions terminal; no push. Index reuse remains reverted.


### GPU pressure kernels — integration pending (2026-09-07)

- Added `GpuSurfacePressure.compute` and `GpuSurfacePressureDispatcher`: parallel 64-lane
  groups emit their top16 eligible offscreen handles; bounded global reduction retires at most16.
  GPU eligibility requires live pages, matching desired/live generation and no pending candidate.
  GPU writes exact retired handle/generation acknowledgments and clears live readiness while retaining
  CPU handle ownership. No geometry readback, world mutation or capacity change.
- `Artifacts/LocalGpuShowcase/gpu-pressure-kernels-tests.xml`:547/547 passed,0skips;
  wrapper exit0 in30s, peak4527MB. New real-GPU cases at7/67/130handles exercise cross-group ranking,
  nonzero generation-high identity, visible protection, desired/pending replacement protection,
  a changed current frustum, and no duplicate page retirement. `git diff --check` passed.
- This is a tested component, **not a completed production migration**. Scheduler CPU eviction remains.
  Next wire a resident bounds table from extraction descriptors (64cells/axis, one source-sample halo),
  async outcome lifetime, published render generation on entries, safe stale/new-build acknowledgments,
  and actual live-ready checks in LOD fallback. Then remove CPU GPU-pressure scans and repeat
  module standalone evidence plus identical180sShowcase FPS/coverage windows.
- No new player run or FPS claim for this component. Latest integrated baseline remains452/513FPS;
  previous terrain defects and remaining gates stay open. Water pressure still needs migration.


### Production solid GPU pressure handoff — verification in progress (2026-09-07)

- GPU allocation captures resident bounds from production descriptors in a separate dispatch to
  preserve Metal's eight-UAV allocation limit. Solid scheduler uses GPU pressure selection/retirement;
  reverse handle lookup routes bounded acknowledgments to host entries. Published render generation
  prevents stale acknowledgments from deleting newer publications. Active replacements keep handles.
  Async readback retains arena lifetime, retries the same outcome buffer after errors, and never
  reruns retirement to recover a failed readback. LOD checks actual GPU live readiness.
- Final `gpu-pressure-integrated-final-tests.xml`:556/556 passed,0skips,wrapper21s/exit0.
  Initial547-test integration run had two expected resource-inventory mismatches after adding bounds;
  next556-test run had two new fixture assertions forgetting DirtyCount includes the active build.
  Corrected inventory and fixture accounting; no production invariant or budget weakened.
- `gpu-pressure-integrated-module`:build32s/player48s/exit0,seven captures and all seven required
  markers. Exact42s capture reviewed: fort intact, prototype/blockout quality, no new apparent defect.
- `gpu-pressure-integrated-showcase` currently running.130 source/meta hashes archived before build;
  no FPS or pressure acceptance claimed until terminal output and screenshots are reviewed.
- Remaining migration: water pressure, per-worker capacity selection, dead solid CPU eviction helpers.
  Water needs its real descriptor origin/scale and a six-voxel splash halo (current bounds table is
  unused by water). Desired/live-generation mismatch protection may conservatively retain failed
  replacements under pressure; inspect traversal evidence before changing that invariant.

- Completed `gpu-pressure-integrated-showcase`:build19s/player180s/exit0,11screenshots.
  Stationary60–90s29samples389.524FPS; walking120–180s60samples488.755FPS.
  CPUmedianp502.39/1.425ms; main.87/1.05ms; present1.50/.145ms; GPUdiagnostic9.68/4.08ms.
  Final238missing,252allocationfailures/evictions,4476publications,0unqueued;
  oldest10.545s step8,10resident/269known. All130source/meta hashes match current source.
  Exact76.8s castle and151.8s walking screenshots reviewed: castle intact; terrain gaps/seams,
  smooth far hills and sparse vegetation remain unacceptable. No new transaction/shader errors.
  Stationary400FPS gate fails in this run; walking passes. CPU eviction migration is verified under
  actual pressure, but no speedup claim: stationary main barely changed and present wait rose.
  Next discrimination: capture bounds in existing outcome dispatch rather than adding a dispatch,
  while finishing water/capacity selection migration. Current source and benchmark remain matched.


### Water GPU pressure and fused bounds publication (2026-09-07)

- Moved bounds capture into existing CSPublishBatchPages (six writable resources); deleted the
  extra capture kernel/dispatch. Bounds are available before commit and pressure admission.
  Arena bounds dimensions are explicit: solid64cells+1sample halo; water128cells+6voxel spray halo.
  Water now supplies actual origin, sample step and voxel scale in its production descriptor.
- Water uses the same bounded GPU victim ranking/soft retirement and async lifetime/retry path.
  Handle→entry lookup and published SourceVersion protect acknowledgments, preserve active rebuilds,
  and retain discovered water. Removed unused solid TryEvictOne/EvictFarthest CPU pressure helpers.
  CPU per-worker EnforceCapacity scan remains for migration; do not claim all victim selection GPU.
- `gpu-water-pressure-final-tests.xml`:558/558 passed,0skips,21s/exit0. Includes real production
  water publication→GPU eviction→rebuild from preserved discovery,128cell/6halo GPU bounds and
  disposal during actual pending pressure readback. Initial556test run also passed24s/exit0.
- `gpu-water-pressure-module`:build30s/player30s/exit0,fourcaptures,both required readiness markers.
  Exact26.3s image reviewed: pool/waterfall rendered; prototype/blockout quality, surrounding trees
  visibly float beyond terrain. Water imperfection allowed for this performance task; broader
  visual defects remain open. `git diff --check` passes after EOF whitespace cleanup.
- `gpu-water-pressure-showcase` running with130source/meta hashes archived. No new FPS claim yet.
- Capacity follow-up: GPU needs a resident owner filter (source step + shard ownership) so pressure
  in one bounded worker evicts from that worker, rather than an unrelated ring. Ownership can be
  uploaded with generation commands once per build; host capacity counts remain bookkeeping, while
  all candidate scanning/frustum/ranking stays GPU. Test shard matching and filtered retirement.

- Completed `gpu-water-pressure-showcase`:build18s/player180s/exit0,11captures.
  Stationary60–90s29samples423.248FPS;walking120–180s59samples436.953FPS.
  CPUmedianp502.15/1.70ms;main.85/1.16ms;present1.30/.19ms;GPUdiagnostic8.8/4.64ms.
  Final258missing,119allocationfailures/evictions,4360publications,0unqueued;
  oldest9.02s step8,22resident/269known. All130source/meta hashes match.
  Exact76.7s and151.7s screenshots reviewed: castle intact; terrain gaps/seams and sparse vegetation
  remain unacceptable. Both400FPS windows pass. This does not prove the bounds fusion alone caused
  recovery: walking performance varies and source-service/pressure workload differs between runs.
  Per-worker capacity victim selection still CPU; full migration goal remains active.


### GPU worker-capacity retirement (2026-09-07)

- Generation commands now carry source step and stable CPU shard hash into an8B/handle GPU owner
  table. GPU pressure eligibility filters exact step and hash modulo shard count before distance
  ranking. Coordinator alternates capacity/global allocation pressure when both are pending and
  round-robins full workers; CPU inspects only bounded worker counts, never resident bounds/victims.
  EnforceCapacity now records demand only. Solid/water/global/capacity pressure selection runs GPU.
- `gpu-capacity-pressure-final-tests.xml`:562/562 passed,0skips,21s/exit0. Four130handle real-GPU
  cases cover step/shard filtering, negative-coordinate hashes, cross-workgroup ranking and
  preservation of all unselected live records. Command coalescing verifies final GPU owner metadata.
  Initial558-test run had two old16B command-readback fixtures; transport is now24B. Updated those
  independent layouts and ownership assertions; water disposal inventory includes new owner buffer.
- `gpu-capacity-pressure-module`:build29s/player48s/exit0,seven captures.42s image reviewed:
  fort intact, prototype/blockout quality. Showcase currently running with132source/meta hashes.
- Completion audit still requires the removal ledger: water CollectVisible performs CPU entry
  frustum tests, BeginNearestBuild ranks up to32dirty candidates on CPU, and test-only CPU geometry
  helpers remain. These are distinct from completed pressure migration; do not claim total CPU
  presentation retirement from pressure evidence. Memory-tier/integration gates also remain.

- Completed first `gpu-capacity-pressure-showcase`:build19s/player180s/exit0,11captures;
  stationary175.52FPS/walking424.768FPS;296missing,106allocationfailures/evictions.
  CPUmedianp505.04/1.79ms;GPUdiagnostic21.955/4.82ms. All132hashes matched before follow-up edits.
  Walking151.7s image retains terrain gaps/seams and sparse vegetation. Stationary performance failed.
  At~150s ps showed VM217%CPU,mediaanalysis46%+41%,mds49%,player127%; no process was killed.
  External GPU contention is plausible, not proven. Eviction counters alone cannot establish whether
  capacity dispatch ran with zero eligible victims. Add separate dispatch counters before attribution.
- Follow-up source edits after terminal benchmark: rank only requested K (capacity K=1, global≤16),
  scan only written candidate slots; preserve round-robin cursor on global-pressure turns so alternating
  service cannot repeatedly skip half of persistently full workers. Add dispatch counters to RINGS.
  Eight step/shard tests now exercise K=1 and16. New source is not covered by prior player FPS.

- `gpu-capacity-bounded-tests.xml`:566/566 passed,0skips,26s/exit0. No player validation yet for
  K-sized reduction/fairness/counters follow-up. Exact76.7s prior-player capture also reviewed:
  castle intact, right houses less complete; terrain defects remain unacceptable. Next run must use
  fresh archived source and counters. All tool/player sessions terminal; no pending run to restart.


### User-requested draft PR checkpoint (2026-09-07)

Latest bounded module completed25s build/48s player,7captures/all7markers;42s fort prototype intact.
Latest bounded Showcase reached final verification artifact after successful-player/log/capture checks:
180s,11captures,172.173stationary/429.567walkingFPS,293missing,48failures/evictions,4190publications.
CapacityDispatch0 throughout; AllocationDispatch38 after stationary. Eviction-overhead hypothesis
falsified; separate Metal trace next. VM activity does not itself prove GPU contention.
132source/meta hashes match;566/566Rendering tests,0skips.76.6/151.6s images reviewed: castle intact,
incomplete houses and terrain/vegetation defects remain unacceptable. checkpoint-evidence.json saves
portable result summaries and source provenance; raw local artifacts remain under Artifacts.
User requested committing work and opening a PR against master. Draft leaves issue and unfinished
performance/migration/memory/visual/integration gates open; no auto-merge or remote CI success claim.
Unrelated generated settings/import residue and crash artifacts are excluded from this task commit.

Draft PR#316 opened against master from gpu-rendering-agent-1-resume; code checkpoint47c5f8a29.
GitHub reports conflicting. Read-only git merge-tree identifies tools/run-module-validation.py as
the conflict with origin/master18845c608. No working-tree merge performed; resolve and validate
this integration before promotion.
