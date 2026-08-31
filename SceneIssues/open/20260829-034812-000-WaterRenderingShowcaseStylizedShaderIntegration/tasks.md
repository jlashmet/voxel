# Tasks

## Workflow / architecture
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, `SceneIssues/feature-readme.md`; maintain separate plan/tasks.
- [x] Keep water authoring in canonical `ShowcaseWorld`/Storage and rendering in the shared renderer; no bespoke proof mesh/material path.
- [x] Keep material IDs opaque in shared code; scene/game IDs remain composition policy.
- [x] Prove reusable still/river/Cascade semantics with independent production-path fixtures; do not count `WorldbuildingGalleryShowcase` as production-water evidence.
- [x] Merge current master before validation; feature includes master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via `87000073f2ca648922a18ae0788ed9008a55dd18`.

## Shared implementation / reuse
- [x] Add reusable still, flowing/river, and waterfall presentation profiles.
- [x] Adapt shallow/deep color, depth/contact/surface foam, animated normals/detail, highlights/refraction/wave direction into canonical `Hidden/VoxelEngine/WaterSurface`.
- [x] Add distinct river motion and waterfall downward flow/turbulence/aeration/breakup/lip-edge-base foam/mist cues.
- [x] Preserve per-vertex water material identity and canonical voxel extraction/cache authority.
- [x] Remove hard-coded water IDs from CPU/Burst/GPU solid classification; publish installed semantic water mask through shared runtime/GPU state.
- [x] Add arbitrary opaque water-ID regression proving solid exclusion/remap is presentation-driven.
- [x] Audit compute copy: `VoxelBrickDensity.hlsl` consumes the same installed semantic water mask.

## Addressing / waterfall root cause history
- [x] Prove authored Cascade survives standard storage → `CpuWaterSurfaceChunkCache` extraction/upload/publication/visibility (`33336797164`).
- [x] Isolate Metal procedural-indirect arena addressing and fix with explicit `_SurfaceVertexBase`; `33339706799` restores later water geometry.
- [x] Replace cross-hatched waterfall lattice with anisotropic descending strand/noise fields; `33343405166` improves motion but remains visually incomplete.
- [x] Reshape only showcase Cascade placement into overlapping ordinary voxel ribbons; `33345745137` improves silhouette but repeats weak lip/base/mist symptom.
- [x] After two materially different visual fixes, stop tuning and isolate reusable root cause from `WaterfallReference.shader` versus production.
- [x] Add generic lip/base/edge topology in reserved `SmoothSurfaceVertex.Material` flag byte with independent arbitrary-material regression.
- [x] `33346565021` passes topology regression/player replay but proves shader-local mist cannot create free spray volume.
- [x] Add reusable `WaterSprayFlag` plus canonical impact-spray quad at true lower vertical-water boundaries; same water mesh/buffer/material/draw, clipped for non-waterfall profiles.
- [x] Extend independent extraction fixture to require spray quad emission and opaque material preservation.
- [x] Add production-path spray regression through Storage → `CpuWaterSurfaceChunkCache` → shared GPU arena.
- [x] `33355120310` passes extraction/arena regressions, module validation, and 60-second WaterRenderingShowcase replay; direct review still rejects visible free spray.

## Production-path discriminator root cause
- [x] `33356900725` fails before spray assertion because no cache build publishes within 120 coroutine yields; result is inconclusive about flag survival.
- [x] `33357865312` adds diagnostics and isolates `dirty=1 runningJobs=1` with zero pending/upload/overflow/arena/blocking/stale failures after ~0.46 s.
- [x] Add nonblocking `JobHandle.ScheduleBatchedJobs()` without `Complete()`; `33358290720` reproduces the same pre-publication state, triggering mandatory minimal root-cause isolation.
- [x] Add temporary wall-clock worker-time probe. Exact run `33361014521` passes the same production cache path with a two-second bound and also passes automatic `kentridge-integration` built-player validation. Root cause: fixed 120-yield bound was too short in wall-clock time; cache job is not stuck.
- [x] Replace the original discriminator's yield-count bound with the proven two-second realtime bound; retain nonblocking batch flush and remove the temporary probe. No production rendering code changed.
- [ ] Run `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the exact wall-clock-harness feature head and prove or deny `WaterSprayFlag` survival through Storage → production cache → GPU arena before another visual correction.

## Reliability / cost
- [x] Preserve spreading/inert gameplay semantics and storage/streaming/edit/diagnostic contracts; no swim/buoyancy subsystem exists to alter.
- [x] Keep one renderer-owned water material and one `_WaterTime` path.
- [x] Static profile cost remains six 32-entry `Vector4` arrays = 3,072 bytes plus one uint semantic mask.
- [x] Arena correction adds one scalar to existing per-water-draw properties; no geometry allocation or draw call.
- [x] Topology/spray reuse the existing 32-byte vertex stride; spray adds one ordinary quad only at exposed vertical lower boundaries.
- [x] `33355120310` spray replay: arena `1,886,976/34,408,080` vertices, `2,841,088/60,214,140` indices, `191/16,384` draws, `leaseFail=0`; allocated ~698.4 MiB, reserved ~861.6–863.6 MiB, average-frame samples ~0.89–1.40 ms. GPU FrameTimingManager data unavailable and not inferred.
- [ ] Complete final accepted-head CPU/GPU/memory/render-cost statement after visual acceptance; do not weaken budgets or invent unavailable GPU timing.

## Exact-SHA gates
- [x] `33339706799`: explicit arena-base regression + 60-second player capture green.
- [x] `33343405166`: strand shader regression + 60-second replay green; visual closure rejected.
- [x] `33345745137`: irregular-ribbon replay green; visual closure rejected.
- [x] `33346565021`: topology regression + replay green; free-spray acceptance rejected.
- [x] `33355120310`: spray extraction/arena regression + automatic module validation + 60-second showcase replay green; visible spray rejected.
- [x] `33356900725`: production-path discriminator blocked before publication.
- [x] `33357865312`: diagnostics isolate one running job and zero downstream failures.
- [x] `33358290720`: explicit batch flush still hits same short-bound state.
- [x] `33361014521`: two-second wall-clock root-cause probe passes; automatic `kentridge-integration` builds/runs `KentridgePlayableSlice` for 60 seconds and passes.
- [ ] Run corrected original production-path discriminator on exact wall-clock-harness head.
- [ ] If spray survives GPU publication, isolate downstream renderer visibility/presentation cause before another visual change; otherwise fix only the proven canonical extraction/upload defect.
- [ ] Run `WaterArenaDrawRegressionTests` plus 60-second WaterRenderingShowcase replay on exact corrected candidate head.
- [ ] Directly accept/reject near/wide/time-separated waterfall frames against downward-flow, turbulence/aeration, irregular breakup, lip/edge/base foam, free mist/spray and overall visual-quality requirements.
- [ ] Run `ShowcaseWaterPresentationRegressionTests` on the same visually accepted feature head.
- [ ] Run `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the same accepted head.
- [ ] Confirm exact player build has no startup/runtime/shader compile/stripping/pink/missing-resource failure.
- [ ] Reconcile accepted build with `VoxelShowcase` and one actual production scene containing visible water; Kentridge integration alone does not satisfy this until visible water content is proven.
- [ ] Complete issue `resolutionSummary`, `regressionTest`, `fixCommit`, `status=fixed`, `resolvedUtc` only after every acceptance item below is validated.
- [ ] Move assigned issue directly `open/` → `closed/` after all exact-SHA gates pass.
- [ ] Fetch/merge latest master again, then non-force promote exact closed feature head to `origin/master`; fetch/merge/retry if master advanced.

## Acceptance ledger
- [ ] A1 — Built `WaterRenderingShowcase` is in normal build/harness path, launches cleanly, and uses standard voxel/WorldBuilder water authoring.
- [ ] A2 — Built showcase visibly contains still/deep, shallow shoreline, river, waterfall/rapid, terrain/rock/structure contacts and all required waterfall cues.
- [ ] A3 — All cases use canonical reusable renderer/profile configuration; no scene shader/material fork.
- [ ] A4 — Stylized Water package and `WaterfallReference.shader` are materially adapted into canonical renderer.
- [ ] A5 — `VoxelShowcase` plus one additional production scene automatically use replacement.
- [ ] A6 — No normal game water remains on legacy production shader/material fallback.
- [ ] A7 — Scene code contains only placement/profile/inspection intent; reusable behavior stays shared.
- [ ] A8 — Built still/river/waterfall animate distinctly; waterfall flows coherently downward.
- [ ] A9 — Shore/depth/contact and waterfall lip/edge/base foam come from reusable production semantics.
- [ ] A10 — Existing spreading, streaming, discovery/meshing, edits, diagnostics and any present gameplay compatibility remain intact.
- [ ] A11 — Exact player build has no shader compile/stripping/pink/missing-resource/editor-only failure.
- [ ] A12 — Focused regressions exercise production selection/binding/extraction for independently authored profiles.
- [ ] A13 — Portability coverage outside showcase authors multiple bodies through production path and includes waterfall semantics.
- [ ] A14 — Durable exact-built evidence has near/wide/time-separated motion for required cases plus production-scene portability evidence.
- [ ] A15 — Direct visual review meets repository quality with stable contacts/depth/foam/flow/waterfall and no placeholder/sorting catastrophe.
- [ ] A16 — Blast radius and CPU/GPU/memory/render costs are measured without weakened budgets.
- [ ] A17 — Durable reference evidence remains and built waterfall is explicitly compared to downward-flow/turbulence/aeration/edge/lip/base/mist behavior.
