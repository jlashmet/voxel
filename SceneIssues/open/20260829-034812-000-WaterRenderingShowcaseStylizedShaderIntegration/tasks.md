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
- [x] Audit compute copy: `VoxelBrickDensity.hlsl` now consumes the same installed semantic water mask.

## Showcase / visual evidence
- [x] Register `WaterRenderingShowcase` at build index 3 without disturbing existing build indices.
- [x] Author still/deep lake, shoreline, directional river, waterfall/rapid, and terrain/rock/structure contacts through standard voxel authoring.
- [x] Provide near, wide, elevated, and square-on waterfall inspection modes; use existing 10-second real-player screenshot harness.
- [x] Exact run `33323151755` built/launched cleanly but direct review rejected waterfall visibility.
- [x] Exact run `33324084398` validated the first rendered repair but direct 32/42s review again showed only lip/base, no falling sheet.
- [x] After repeated visual failure, stop shader-art speculation and isolate the production boundary per workflow rule.

## Cascade boundary/root-cause isolation
- [x] Add focused exact 62x62x2 Cascade curtain regression through `ShowcaseWorld` storage into `CpuWaterSurfaceChunkCache`.
- [x] Exact run `33336797164` on feature SHA `44947d3b0a4c60c09edbd0433cad389b984067bc` passed 5/5 production water PlayMode regressions and real-player build/capture.
- [x] Use that run to prove authored Cascade survives storage, water-cache extraction, upload/publication, visibility, and non-empty indexed geometry.
- [x] Directly reject run `33336797164` visual closure: 32s/42s square-on frames still show no falling sheet, locating the defect above cache publication.
- [x] Trace shared arena addressing and identify the missing vertex-base defect: local water indices used `IndexStart`, but later aligned `VertexStart` was never applied by the water draw shader.
- [x] Add focused regression before correction: `WaterArenaDrawRegressionTests.SecondArenaLeasePublishesVertexBaseInIndirectDrawRecord` requires a nonzero second arena lease to publish its vertex base.
- [x] Implement the first shared correction attempt: `SurfaceGeometryArena.UploadArgs` stores `VertexStart` in indirect `startInstance`; `WaterSurface.shader` consumes `SV_InstanceID` as the vertex base.
- [x] Exact run `33337560328` passed that regression and 60-second player capture, but direct 32s/42s frames still show no falling sheet; wide frame also lacks later river water while early lake renders. Treat the start-instance correction as falsified for visual acceptance.
- [ ] Run the focused GPU minimal repro `IndirectStartInstanceReachesShaderOnCurrentBackend` on the exact feature SHA before another product fix.
- [ ] Based on that repro, isolate and correct the actual later-lease draw-address transport; remove temporary repro asset if it only served diagnosis.

## Reliability / cost
- [x] Preserve spreading/inert gameplay semantics and storage/streaming/edit/diagnostic contracts; no swim/buoyancy subsystem exists to alter.
- [x] Keep one renderer-owned water material and one `_WaterTime` path.
- [x] Static profile cost remains six 32-entry `Vector4` arrays = 3,072 bytes plus one uint semantic mask.
- [x] Prior player telemetry showed no frame-budget weakening (~1.2–2.8 ms average windows after startup, ~697.8 MiB allocated, ~846–848 MiB reserved); FrameTimingManager CPU/GPU values unavailable and not invented.
- [x] Arena-address correction attempt added no allocation/draw call; it reused the existing fourth indirect argument.
- [ ] Re-measure final corrected built-player frame/memory/draw observations and inspect logs for runtime/shader/stripping errors.

## Exact-SHA gates
- [x] Inspect completed failed run `33332518398`; compile failure was caused by putting runtime-dependent semantic regression in the dependency-pure EditMode assembly, then repaired by moving it to PlayMode.
- [x] Run exact production water suite on `44947d3b...`; automation green but visual acceptance failed, so it is not a closing gate.
- [x] Re-read `origin/master` before corrected validation; master remained `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470`.
- [x] Run `WaterArenaDrawRegressionTests` for the start-instance correction through only `ci-test/fixes/agent-9`: run `33337560328` green with 60-second replay.
- [x] Directly inspect corrected near/wide/time-separated waterfall frames: early lake renders, later river and waterfall do not; visual gate fails and the start-instance hypothesis is falsified.
- [ ] Run the minimal indirect-instance GPU discriminator through only `ci-test/fixes/agent-9`; after completion fix the proven cause or retry only proven infrastructure failure.
- [ ] After the proven correction, rerun `WaterArenaDrawRegressionTests` on the exact corrected feature SHA with the 60-second WaterRenderingShowcase replay.
- [ ] Directly inspect corrected near/wide/time-separated waterfall frames and compare downward flow, turbulence, aeration, irregular edges, lip/edge/base foam, and mist/spray to retained reference semantics.
- [ ] Run `ShowcaseWaterPresentationRegressionTests` on the same corrected exact SHA so portability, arbitrary-ID/remap, solid-classification, and exact Cascade storage→cache coverage are all exact-SHA green.
- [ ] Confirm corrected exact-SHA player build launches without startup/runtime/shader compile/stripping/pink/missing-resource failures.
- [ ] Reconcile final build with `VoxelShowcase` and `WorldbuildingGalleryShowcase` shared-water paths.
- [ ] Complete issue `resolutionSummary`, `regressionTest`, `fixCommit`, `status=fixed`, and `resolvedUtc` only after every acceptance item below is validated.
- [ ] Move assigned issue directly `open/` → `closed/` after all exact-SHA gates pass.
- [ ] Fetch/merge latest master again, then non-force promote the exact closed feature head to `origin/master`; fetch/merge/retry if master advanced.

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
