# Tasks

## Workflow / investigation
- [x] Read `AGENTS.md`, canonical `SceneIssues/README.md`, assignment; record requested `SceneIssues/feature-readme.md` absent.
- [x] Inspect assignment metadata and capture list (empty).
- [x] Inspect imported Stylized Water package and `WaterfallReference.shader` semantics.
- [x] Trace canonical water extraction/cache/render pass and one shared material lifecycle.
- [x] Run competing-hypothesis discriminator and maintain `plan.md` separately.
- [x] Resume existing `fixes/agent-9`; merge current `origin/master` before the final validation attempt.
- [x] Trace liquid/gameplay consumers: no swimming, buoyancy, wading, or generic liquid subsystem exists in the current tree; preserve existing material IDs, spreading/inert semantics, storage/streaming, discovery/meshing, edits, and diagnostics.
- [x] Trace normal player bootstrap ordering: `GameMaterialPresentationBootstrap` installs presentation data `BeforeSceneLoad`, before scene-owned world/extraction/rendering setup.
- [x] Identify normal production bindings: `VoxelShowcase` and Kentridge both construct `ShowcaseWorld` and bind its canonical storage/palette/surface/coating/profile inputs through `RenderingWorldBinding`.
- [x] Identify bounded showcase composition: reuse existing `ShowcaseFeatureContent.HouseOnly` + `ShowcaseStartupSource.Generate` for terrain plus one representative structure without castle authoring or a new world mode.
- [x] Verify second existing water-bearing scene: `WorldbuildingGalleryShowcase` cave authoring supplies `GameMaterialIds.Water` through ordinary structure authoring; Kentridge contains no explicit water authoring and is not claimed as the second water consumer.
- [x] Verify active URP renderer-data asset serializes `WaterSurface.shader` and matching URP asset is selected.
- [x] Preserve build indices 0/1; register `VoxelShowcase` at 2 and `WaterRenderingShowcase` at required 3.

## Shared implementation
- [x] Add reusable still/lake, river/stream, waterfall/rapid presentation profiles.
- [x] Remove hard-coded game IDs from engine water extraction; classify water cache from installed presentation data.
- [x] Preserve per-vertex water material identity through extraction.
- [x] Keep one renderer-owned `Hidden/VoxelEngine/WaterSurface` material with no scene-local forks.
- [x] Adapt package shallow/deep color, depth fade, contact/surface foam, animated normals/detail, highlights/refraction/wave direction.
- [x] Add distinct directional river motion.
- [x] Add waterfall downward flow, turbulence, aeration, irregular breakup, lip/edge/base foam, mist/spray cues.
- [x] Keep production voxel extraction/cache authoritative for water geometry.
- [x] Source/asset audit shows no alternate normal water shader/material path; final player replay remains the stripping/fallback discriminator.
- [x] Verify player-build retention has a production asset dependency: active `VoxelUniversalRenderer.asset` directly serializes `WaterSurface.shader`; do not add a global always-included exception unless the player build falsifies this.
- [x] Exact run `33323151755` proved the player build retained and launched `WaterSurface.shader` without pink/missing-resource failure on candidate `d3729aa...`; visual quality still failed and requires a new exact repaired candidate.
- [x] Remove stale solid-extractor hard-coded water IDs 11/16 from CPU/Burst/GPU-equivalent classification paths; use the installed water presentation mask without changing gameplay semantics.

## Showcase / portability
- [x] Add one bounded `ShowcaseWorld.AuthorVoxelBox` seam over its existing Storage.Api mutation/change path; validate positive bounds/size and cap authored volume; add no renderer/material knowledge.
- [x] Create `Assets/Scenes/WaterRenderingShowcase.unity` through standard voxel storage/authoring and canonical renderer; no production proof planes/bespoke water meshes.
- [x] Add thin `WaterRenderingShowcase` scene controller using `HouseOnly + Generate`, canonical material definitions/IDs, `RenderingWorldBinding`, and production surface extraction.
- [x] Keep scene/controller limited to terrain/water placement, semantic selection, lighting/camera/inspection controls.
- [x] Author still/deep lake, shoreline, directional river, waterfall/rapid, terrain/rock/structure contacts.
- [x] Provide near, wide, elevated, and waterfall inspection view modes plus moving inspection controls.
- [x] Reuse existing screenshot/SceneIssue replay/standalone-player harness contracts; add no parallel capture stack or CI transport.
- [x] Make unattended real-player capture deterministically expose near, wide, then repeated waterfall views using the existing 10-second screenshot cadence.
- [x] Add production portability outside the scene: independent Water/RiverWater/Cascade authoring through `ShowcaseWorld`, canonical read storage, normal presentation installation, and `RenderingComposition` world binding.
- [x] Verify `VoxelShowcase` automatically receives shared presentation and restores water after a leaked diagnostic disable.
- [x] Verify `WorldbuildingGalleryShowcase` automatically reaches the same globally installed water presentation for its cave-authored water.

## Reusability review
- [x] Audit water extraction/shader code: shared water cache/shader behavior is selected from semantic presentation/profile tables and `WaterMaterialMask`; no `GameMaterialIds` or showcase IDs control water-cache behavior.
- [x] Keep reusable flow/foam/depth/turbulence/aeration parameters in `WaterPresentationDefinition`/`VoxelPresentationCatalogue`; `WaterRenderingShowcase` chooses material/profile placement and inspection only.
- [x] Add regression `WaterProfiles_UseOpaqueMaterialIdsAndCanBeRemappedByPresentationData`: arbitrary IDs can share Still and one can remap to Waterfall through presentation data only; final exact CI still required.
- [x] Confirm capture-only telemetry runs only under unattended screenshot evidence capture and is not a production renderer lifecycle dependency.
- [x] Audit solid extractor classification boundary: `CpuTransvoxelChunkCache`, `ExactSnapshotMetadataJobs`, and `TransvoxelDensityJob` still hard-code IDs 11/16 as non-solid; `MipDensityJob` delegates to the same predicate. This is a confirmed reusable RiverWater defect, not the missing Cascade root cause because Cascade is ID16 and already excluded.
- [x] Review `FlowerBlue = Cascade` transitional alias blast radius: only the alias declaration was found; no live usages reference `FlowerBlue`. Preserve ID16 Cascade semantics and do not change gameplay behavior.
- [x] Search compute/GPU solid-classification copies for the same stale ID assumptions before patching; `VoxelBrickDensity.hlsl` contained the equivalent 11/16 predicate and now consumes the installed semantic mask.
- [x] Add regression proving an arbitrary presentation-water ID is excluded from solid classification without relying on gameplay class or hard-coded IDs.

## Regression / reliability / cost
- [x] Production installation regression covers still/river/waterfall profiles and excludes non-water material.
- [x] Gameplay regression proves still/river retain spreading-water semantics while cascade remains inert.
- [x] Extraction regression preserves water material identity at negative world coordinates.
- [x] Extraction regression covers reciprocal boundary-neighbor suppression and keeps distinct profiles across seams.
- [x] Extraction regression proves a material outside the installed water mask is not rendered as water.
- [x] Render lifecycle/source tracing confirms one shared material and one `_WaterTime` update path.
- [x] Production renderer-path portability coverage authors Water/RiverWater/Cascade through `ShowcaseWorld`, binds the ordinary `RenderingWorldBinding`, verifies installed profile arrays, and combines with focused extraction identity/seam regressions; no source-string-only assertion.
- [x] Exact run `33323151755` built/launched the real player without shader compile/stripping/pink/missing-resource failures for the pre-repair candidate.
- [x] Static memory cost: six 32-entry `Vector4` water tables = 3,072 bytes; no per-water-voxel GameObjects/material instances were added.
- [x] Add capture-only frame timing and managed/native memory telemetry because the existing player harness records FPS but has no reusable GPU/memory sampler; keep it scene-local and inactive outside unattended evidence capture.
- [x] Record pre-repair exact-player baseline: Apple M4 Max, ~1.5–2.1 ms 10-second average frame windows after startup, ~697.8 MiB allocated, ~861–864 MiB reserved, ~9 MiB mono used, 191 resident draw leases, zero lease failures. GPU/CPU FrameTimingManager values were unavailable (`-1`), so do not invent them.
- [x] Re-measure repaired two-sided pass from run `33324084398`: ~1.2–2.8 ms average frame windows after startup, ~697.8 MiB allocated, ~846–848 MiB reserved; no frame-budget weakening. CPU/GPU FrameTimingManager values remained unavailable.
- [x] Review feature-only diff against current master before resumed investigation: only assignment water code/tests/scene/build registration/docs are changed; `.github/test-request.json` is absent from the feature diff.
- [x] Falsify water-cache starvation/vertical-bounds hypotheses: arena supports 2,048 entries, dirty work is camera-prioritized, and `CollectVisible` culls full brick AABBs rather than zero-thickness generated geometry.
- [x] Add focused regression proving the exact authored Cascade curtain survives canonical `ShowcaseWorld` storage readback and production water-cache geometry/material encoding; exact CI remains the execution discriminator.
- [ ] Add focused regression for the confirmed missing-sheet root cause before the corrective implementation change.

## Rendered gate from exact run 33323151755
- [x] Bake succeeded on exact feature parent `d3729aa0c971aa4973286fe61d024f500f6f308a`.
- [x] Focused PlayMode regression passed 3/3.
- [x] Real-player `WaterRenderingShowcase` build, launch and 60-second capture completed successfully.
- [x] Download and inspect all six exact-built screenshots, player/build logs, FPS and telemetry.
- [x] Reject visual closure: first near frame at ~2.3s is only clear sky before cold-view convergence; useful wide frame exists at ~12.3s.
- [x] Reject waterfall evidence: 22/32/42/52s frames show a dark cliff with horizontal cyan lip/base slabs but no readable falling sheet, coherent downward motion, turbulent streaks, aeration, localized edge/lip/base foam or mist; waterfall-region image change is negligible.

## Discovered rendered-gate repair
- [x] Trace canonical `WaterBrickMeshBatchJob`: it emits all six exposed faces, so the fix must preserve canonical voxel geometry rather than add a bespoke scene mesh.
- [x] Strengthen shared waterfall raster/pixel response: two-sided water pass, vertical-coordinate multi-scale animated breakup, bright aerated threads, stronger vertical waterfall opacity/foam/mist response.
- [x] Keep exhibit repair as semantic authoring/inspection intent: use thinner exposed Cascade sheet/fingers and a nearly square-on waterfall camera; no plane/material fork.
- [x] Hold unattended near/wide phases long enough for the existing 10-second capture cadence to record converged near and wide evidence before repeated waterfall frames.
- [x] Add focused regression proving a vertical Cascade column emits canonical vertical sheet faces with Cascade material identity.
- [x] Repaired exact run `33324084398` on source `3b3a55c...` completed green for automated tests/build/player capture.
- [x] Directly inspect repaired frames and reject closure again: converged 32/42s square-on views still show no visible vertical waterfall sheet; only top lip/lower pool read. Framing hypothesis is falsified because authored sheet is directly in front of cliff and camera target.
- [x] Trace shared shader/render pass: no vertical-face discard, `Cull Off`, waterfall profile forces high vertical alpha, and every visible water entry uses the same procedural draw path. If vertical Cascade reaches the water shader, it should be visible.
- [x] Falsify initial-discovery/admission and visibility-cap hypotheses: partial curtain bricks are discoverable; cache admission is mask-based; full brick bounds are used for frustum culling.
- [ ] Determine whether exact authored Cascade cells survive canonical storage and become production water-cache vertices/material IDs.
- [ ] If geometry survives the cache, continue discriminator at shared upload/material encoding/draw/depth boundary; do not change shader art speculatively.
- [ ] Implement only the smallest shared production-path correction proven by the discriminator.

## Exact-SHA gates
- [x] Refresh/merge latest `origin/master` before prior feature SHA attempt; compare was `behind_by=0` after merge commit `ab8b3bc3efa3eac933ce861748664fb246dc1ea2`.
- [x] Inspect failed exact run `33320921998`: Unity stopped before tests/player evidence because `ShowcaseWaterPresentationRegressionTests` lacked the `VoxelEngine.Showcase` namespace import; repaired on feature branch.
- [x] Submit canonical request for candidate `d3729aa...` through `ci-test/fixes/agent-9`; run `33323151755` completed green but failed direct visual acceptance and therefore was not used for closure.
- [x] Inspect already-queued repaired request: run `33324084398` completed green on source `3b3a55c...` but failed direct visual acceptance; do not replace/reuse it as final CI.
- [ ] Re-read current `origin/master` and final `fixes/agent-9`; merge before final CI if master advanced.
- [ ] Submit exactly one canonical final targeted-CI request from `ci-test/fixes/agent-9` whose parent is the final feature SHA; never replace a queued request or create another transport branch.
- [ ] Confirm focused behavioral regressions green on final exact SHA, including arbitrary-ID/remap, solid-classification, and missing-sheet root-cause coverage.
- [ ] Confirm final exact-SHA built `WaterRenderingShowcase` launches without startup/runtime exceptions.
- [ ] Download and inspect durable final real-player artifact, build/player logs, and converged near/wide/time-separated showcase frames.
- [ ] Directly compare final built waterfall evidence with retained reference semantics: downward flow, turbulence, aeration, irregular edges, lip/edge/base foam, mist/spray.
- [ ] Reconcile exact final build with registered `VoxelShowcase` and verified `WorldbuildingGalleryShowcase` shared-water paths; do not create an extra CI transport.
- [ ] Record final built-player CPU/GPU/memory/draw/overdraw/variant/culling observations.
- [ ] Complete `resolutionSummary`, `regressionTest`, `fixCommit`; move open → pending in prescribed bookkeeping commit.
- [ ] After every gate and A1–A17 item is complete, move pending → closed; set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest master again, push feature exact head, then non-force promote exact head to `origin/master`; fetch/merge/retry if advanced.

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
- [ ] A10 — Collision/buoyancy/swim/wade where present, spreading, streaming, discovery/meshing, edits, diagnostics remain compatible.
- [ ] A11 — Exact player build has no shader compile/stripping/pink/missing-resource/editor-only failure.
- [ ] A12 — Focused regressions exercise production selection/binding/extraction for independently authored profiles.
- [ ] A13 — Portability coverage outside showcase authors multiple bodies through production path and includes waterfall semantics.
- [ ] A14 — Durable exact-built evidence has near/wide/time-separated motion for required cases plus existing production-scene portability evidence.
- [ ] A15 — Direct visual review meets repository quality with stable contacts/depth/foam/flow/waterfall and no placeholder/sorting catastrophe.
- [ ] A16 — Blast radius and CPU/GPU/memory/render costs are measured without weakened budgets.
- [ ] A17 — Durable reference evidence remains and built waterfall explicitly compared to approved downward-flow/turbulence/aeration/edge/lip/base/mist behaviors.
