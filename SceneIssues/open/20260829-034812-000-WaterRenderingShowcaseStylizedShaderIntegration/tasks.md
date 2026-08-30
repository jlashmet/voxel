# Tasks

## Workflow / architecture
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, `SceneIssues/feature-readme.md`; maintain separate plan/tasks.
- [x] Keep water authoring in canonical `ShowcaseWorld`/Storage and rendering in the shared renderer; no bespoke proof mesh/material path.
- [x] Keep material IDs opaque in shared code; scene/game IDs remain composition policy.
- [x] Prove portability with independently authored Water/RiverWater/Cascade through ordinary `RenderingWorldBinding`, plus existing `VoxelShowcase` and `WorldbuildingGalleryShowcase` consumers.
- [x] Merge current master before validation; branch contains master `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` via merge `84fecff091649390e7ee8a67228a636219191e21`.

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
- [ ] Validate the updated waterfall shader in exact built-player near/wide/time-separated evidence; retain only if downward motion, aeration/breakup, foam and mist materially improve without regressing lake/river.
- [ ] If shader quality passes but silhouette remains too rectangular, reshape only the showcase's ordinary Cascade voxel placement into an irregular stepped/fingered curtain; no bespoke render path.

## Reliability / cost
- [x] Preserve spreading/inert gameplay semantics and storage/streaming/edit/diagnostic contracts; no swim/buoyancy subsystem exists to alter.
- [x] Keep one renderer-owned water material and one `_WaterTime` path.
- [x] Static profile cost remains six 32-entry `Vector4` arrays = 3,072 bytes plus one uint semantic mask.
- [x] Arena correction adds one scalar to existing per-water-draw properties; no geometry allocation or draw call.
- [x] `33339706799` player logs show no shader/pink/missing-resource/runtime failure; post-start telemetry remains sub-~1.3 ms p95 windows with ~698 MiB allocated and ~854–882 MiB reserved. FrameTimingManager GPU values are unavailable and not invented.
- [ ] Re-measure final accepted built-player frame/memory/render observations and inspect logs.

## Exact-SHA gates
- [x] `33337560328`: start-instance attempt test-green but visually rejected.
- [x] `33339119323`: minimal Metal start-instance discriminator failed exactly at the expected assertion, proving product root cause rather than infrastructure.
- [x] Correct request-schema-only failure `33339677889` after completion by resubmitting same exact feature parent with integer `replay_seconds`; no code failure/retry substitution.
- [x] `33339706799`: explicit arena-base regression + 60-second player capture green; addressing visual defect fixed but art acceptance still open.
- [ ] Re-read current `origin/master`; merge if needed before next exact visual request.
- [ ] Run `WaterArenaDrawRegressionTests` on exact updated waterfall-shader feature SHA with 60-second WaterRenderingShowcase replay.
- [ ] Directly accept/reject updated near/wide/time-separated waterfall frames against downward-flow, turbulence/aeration, irregular breakup, lip/edge/base foam, mist/spray and overall visual-quality requirements.
- [ ] Run `ShowcaseWaterPresentationRegressionTests` on the same visually accepted feature head.
- [ ] Confirm exact player build has no startup/runtime/shader compile/stripping/pink/missing-resource failure.
- [ ] Reconcile accepted build with `VoxelShowcase` and `WorldbuildingGalleryShowcase` shared-water paths.
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
