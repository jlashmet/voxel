# Tasks

## Workflow / architecture
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, `SceneIssues/feature-readme.md`; maintain separate plan/tasks.
- [x] Keep water authoring in canonical `ShowcaseWorld`/Storage and rendering in the shared renderer; no bespoke proof mesh/material path.
- [x] Keep material IDs opaque in shared code; scene/game IDs remain composition policy.
- [x] Prove portability with independently authored Water/RiverWater/Cascade through ordinary production-path fixtures; `VoxelShowcase` is a canonical consumer. Do not count `WorldbuildingGalleryShowcase` as water portability evidence; it uses bounded gallery meshes. Kentridge uses the canonical `ShowcaseWorld` + `RenderingWorldBinding`, but visible-water content still must be proven before A5.
- [x] Merge current master before validation; branch initially contained master `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` via merge `84fecff091649390e7ee8a67228a636219191e21`, then merged advanced master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via `87000073f2ca648922a18ae0788ed9008a55dd18` before spray validation.

## Shared implementation / reuse
- [x] Add reusable still, flowing/river, and waterfall presentation profiles.
- [x] Adapt shallow/deep color, depth/contact/surface foam, animated normals/detail, highlights/refraction/wave direction into canonical `Hidden/VoxelEngine/WaterSurface`.
- [x] Add distinct river motion and waterfall downward flow/turbulence/aeration/breakup/lip-edge-base foam/mist cues.
- [x] Preserve per-vertex water material identity and canonical voxel extraction/cache authority.
- [x] Remove hard-coded water IDs 11/16 from CPU/Burst/GPU solid classification; publish the installed semantic water mask through `SharedStatic` and `_SolidWaterMaterialMask`.
- [x] Add arbitrary opaque water-ID regression proving solid exclusion/remap is presentation-driven.
- [x] Audit compute copy: `VoxelBrickDensity.hlsl` consumes the same installed semantic water mask.

## Cascade boundary / addressing root cause
- [x] Prove the authored Cascade curtain survives standard storage → `CpuWaterSurfaceChunkCache` extraction/upload/publication/visibility (`33336797164`).
- [x] After repeated missing-curtain visuals, isolate the production draw boundary instead of continuing shader speculation.
- [x] Identify missing arena vertex-base addressing for later water leases.
- [x] Reject indirect `startInstance` as the transport after exact run `33337560328` remained visually broken.
- [x] Run minimal GPU discriminator `33339119323`; prove Metal reports `SV_InstanceID=0` for this procedural path even with `startInstance=256`.
- [x] Bind `_SurfaceVertexBase` explicitly per water draw, matching the solid renderer contract; restore indirect `startInstance=0` and remove temporary repro.
- [x] Replace obsolete buffer-only test with `SecondWaterEntryBindsExplicitArenaOffsets` exercising actual water draw property state.
- [x] Re-read master before corrected validation; master remained `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470`.
- [x] Exact run `33339706799` passes `WaterArenaDrawRegressionTests` plus 60-second built-player replay and visibly restores later river/waterfall geometry.

## Demonstrated waterfall visual defect
- [x] Directly inspect `33339706799` time-separated frames; reject visual closure because waterfall is a bright rectangular cross-hatched wall with weak downward-flow readability/breakup and no convincing visible mist/spray.
- [x] Confirm reusable API already supplies generic turbulence/edge-foam/impact-foam/mist controls; no scene-ID renderer API is needed.
- [x] Identify shader cause: crossed high-frequency bands plus forced ~0.84 minimum alpha on every vertical waterfall fragment.
- [x] Replace waterfall lattice with anisotropic descending strand/noise fields and coverage-driven vertical alpha breakup while leaving still/river branches unchanged.
- [x] Validate updated shader on exact run `33343405166`; 32s/42s built-player frames materially improve downward strand motion and remove the lattice without losing the waterfall curtain.
- [x] Reject final visual closure on `33343405166` because the outer curtain remains a large rectangular slab with obvious stepped side columns and mist/spray is still visually weak.
- [x] Reshape only the showcase's ordinary Cascade voxel placement into overlapping ribbons with varied lip/foot heights and depth; no bespoke render path.
- [x] Validate irregular ribbons on exact run `33345745137`; CI is green and direct replay review confirms a less rectangular silhouette with moving strands and intact lake/river.
- [x] Reject `33345745137` visual closure: the same acceptance symptom remains after two materially different fixes—layered flat sheets, weak lip/base foam, and no convincing mist/spray—so stop visual tweaking and isolate a minimal root cause.
- [x] Compare production shader with durable `WaterfallReference.shader`: reference localizes lip/edge/base/mist by sheet-local UV, while production lacks top/base/side topology and can only apply mist on existing sheet fragments.
- [x] Trace canonical extraction boundary: `WaterBrickMeshBatchJob` has voxel-neighborhood data during quad emission; `SmoothSurfaceVertex.Material` reserves bits 24..31 for flags, water currently writes only base ID, and no conflicting water flag ownership was found.
- [x] Add an independent vertical-water extraction regression proving reusable lip/base/edge topology semantics without showcase-specific IDs or placement.
- [x] Encode generic water topology in the reserved material flag byte and interpolate/decode it in the shared shader with no vertex-stride or draw-path increase.
- [x] Use topology semantics to localize existing waterfall lip/edge/impact/mist controls.
- [x] Exact topology run `33346565021` passes both `WaterArenaDrawRegressionTests`, including the independent material-7 fixture, plus the 60-second real-player replay on feature head `8494d0f517fc01199df43c572a4543d71a900f72`.
- [x] Directly reject `33346565021`: coherent downward strands and irregular ribbons remain, but mist still exists only on sheet fragments and creates no free spray volume. This demonstrates a geometric limitation, not another shader scalar-tuning hypothesis.
- [x] Add one reusable `WaterSprayFlag` semantic and canonical impact-spray quad at true lower vertical-water boundaries in `WaterBrickMeshBatchJob`; keep the same water mesh/buffer/material/draw and clip spray geometry for non-waterfall profiles.
- [x] Extend the independent vertical-water fixture to require spray quad emission while preserving arbitrary opaque material identity.
- [x] Add a production-path spray regression that exercises ordinary Cascade authoring through Storage → `CpuWaterSurfaceChunkCache` → shared GPU arena and checks `WaterSprayFlag` survives publication.
- [x] Exact corrected spray run `33355120310` passes `WaterArenaDrawRegressionTests`, automatic module validation and a 60-second standalone showcase replay.
- [x] Directly reject `33355120310` 32.2s/42.2s/52.2s frames: coherent strands and irregular ribbons remain, but impact spray is not visibly free of the bright sheet bottoms.
- [x] Run production-path discriminator `33356900725`; it fails before the spray assertion because `CompletedBuildCount` stays zero for 120 frames, so it is inconclusive about flag survival rather than evidence of a production spray loss.
- [x] Add cache-state diagnostics to the discriminator without increasing the 120-frame bound or changing production rendering.
- [x] Diagnostic run `33357865312` isolates the non-completion state: `dirty=1 runningJobs=1 pendingUploads=0 pendingBytes=0 meshOverflow=0 arenaFailures=0 blockingCompletionViolations=0 staleBuilds=0 residents=0 uploadedBytes=0 residentGpuBytes=0`; the 120-yield test finishes in ~0.46 s, so the worker batch is not becoming dispatch-ready in this test harness.
- [x] Fix only the discriminator harness by calling nonblocking `JobHandle.ScheduleBatchedJobs()` after `cache.Prepare`; retain the 120-frame bound and never call `Complete()`.
- [ ] Re-run the production-path discriminator with explicit job-batch flush and prove or deny `WaterSprayFlag` survival through Storage → production cache → GPU arena before another visual correction.

## Reliability / cost
- [x] Preserve spreading/inert gameplay semantics and storage/streaming/edit/diagnostic contracts; no swim/buoyancy subsystem exists to alter.
- [x] Keep one renderer-owned water material and one `_WaterTime` path.
- [x] Static profile cost remains six 32-entry `Vector4` arrays = 3,072 bytes plus one uint semantic mask.
- [x] Arena correction adds one scalar to existing per-water-draw properties; no geometry allocation or draw call.
- [x] `33339706799` player logs show no shader/pink/missing-resource/runtime failure; post-start telemetry remains sub-~1.3 ms p95 windows with ~698 MiB allocated and ~854–882 MiB reserved. FrameTimingManager GPU values are unavailable and not invented.
- [x] `33346565021` topology head reports `avgFrameMs=1.090`, `allocatedMiB=697.9`, `reservedMiB=847.6`, `monoUsedMiB=9.1` at 30s; CPU/GPU FrameTimingManager values are `-1` and not inferred.
- [x] `33355120310` spray-head replay keeps the shared arena at `1,886,976 / 34,408,080` vertices, `2,841,088 / 60,214,140` indices and `191 / 16,384` draws with `leaseFail=0`; allocated memory stays 698.4 MiB, reserved 861.6–863.6 MiB, and 10–50s average frame samples remain ~0.89–1.40 ms. The 157 MB player build has no C# compile, shader compile, build-failure, pink/missing-shader, or runtime-exception signature; its three generic error counts are licensing-handshake messages. FrameTimingManager GPU data remains unavailable and is not inferred.
- [ ] Complete final accepted-head CPU/GPU/memory/render-cost statement after visual acceptance; do not weaken budgets or invent unavailable GPU timing.

## Exact-SHA gates
- [x] `33337560328`: start-instance attempt test-green but visually rejected.
- [x] `33339119323`: minimal Metal start-instance discriminator failed exactly at the expected assertion, proving product root cause rather than infrastructure.
- [x] Correct request-schema-only failure `33339677889` after completion by resubmitting same exact feature parent with integer `replay_seconds`; no code failure/retry substitution.
- [x] `33339706799`: explicit arena-base regression + 60-second player capture green; addressing visual defect fixed but art acceptance still open.
- [x] `33343405166`: `WaterArenaDrawRegressionTests` + 60-second player replay green; shader quality improved but silhouette rejected.
- [x] `33345745137`: exact irregular-ribbon head passes `WaterArenaDrawRegressionTests` + 60-second player replay; direct quality review still rejects lip/base/mist closure.
- [x] `33346565021`: focused topology regression plus 60-second WaterRenderingShowcase replay passes; direct review rejects free-spray acceptance.
- [x] Re-read current `origin/master` immediately before spray exact request; master advanced to `2ea5f5c95f89fbf0403dbefb50b782829583d304` and was merged into the feature branch.
- [x] `33354768733` completed failure before tests/player capture because the new production-path regression lacked `using VoxelEngine.Rendering.Runtime;` after the master merge; fixed the compile cause on feature branch.
- [x] `33355120310`: `WaterArenaDrawRegressionTests` + automatic module validation + 60-second WaterRenderingShowcase replay green; direct visual review still rejects free spray/mist.
- [x] `33356900725`: production-path spray discriminator fails before flag assertion because no cache publication occurs.
- [x] `33357559750`: accidental stale-head CI request was allowed to complete untouched; it is non-authoritative and not used as evidence.
- [x] `33357865312`: authoritative diagnostic discriminator reports one still-running mesh job and zero downstream failure counters, isolating test-runner job dispatch as the harness blocker.
- [ ] Run corrected production-path discriminator on the exact harness-flush head.
- [ ] After downstream root cause is proven/corrected, run `WaterArenaDrawRegressionTests` plus 60-second WaterRenderingShowcase replay on exact candidate head.
- [ ] Directly accept/reject near/wide/time-separated waterfall frames against downward-flow, turbulence/aeration, irregular breakup, lip/edge/base foam, free mist/spray and overall visual-quality requirements.
- [ ] Run `ShowcaseWaterPresentationRegressionTests` on the same visually accepted feature head.
- [ ] Run `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the same accepted head.
- [ ] Confirm exact player build has no startup/runtime/shader compile/stripping/pink/missing-resource failure.
- [ ] Reconcile accepted build with `VoxelShowcase` and one actual production scene containing water; prefer Kentridge only if visible water content is proven there.
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
