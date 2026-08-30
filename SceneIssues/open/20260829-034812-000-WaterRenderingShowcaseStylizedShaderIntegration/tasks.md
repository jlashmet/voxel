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
- [x] Remove hard-coded game IDs from engine water extraction; classify from installed presentation data.
- [x] Preserve per-vertex water material identity through extraction.
- [x] Keep one renderer-owned `Hidden/VoxelEngine/WaterSurface` material with no scene-local forks.
- [x] Adapt package shallow/deep color, depth fade, contact/surface foam, animated normals/detail, highlights/refraction/wave direction.
- [x] Add distinct directional river motion.
- [x] Add waterfall downward flow, turbulence, aeration, irregular breakup, lip/edge/base foam, mist/spray cues.
- [x] Keep production voxel extraction/cache authoritative for water geometry.
- [x] Source/asset audit shows no alternate normal water shader/material path; final player replay remains the stripping/fallback discriminator.
- [x] Verify player-build retention has a production asset dependency: active `VoxelUniversalRenderer.asset` directly serializes `WaterSurface.shader`; do not add a global always-included exception unless the player build falsifies this.
- [ ] Prove player-build shader retention and no editor-only resource dependency with the exact player build.

## Showcase / portability
- [x] Add one bounded `ShowcaseWorld.AuthorVoxelBox` seam over its existing Storage.Api mutation/change path; validate positive bounds/size and cap authored volume; add no renderer/material knowledge.
- [x] Create `Assets/Scenes/WaterRenderingShowcase.unity` through standard voxel storage/authoring and canonical renderer; no production proof planes/bespoke water meshes.
- [x] Add thin `WaterRenderingShowcase` scene controller using `HouseOnly + Generate`, canonical material definitions/IDs, `RenderingWorldBinding`, and production surface extraction.
- [x] Keep scene/controller limited to terrain/water placement, semantic selection, lighting/camera/inspection controls.
- [x] Author still/deep lake, shoreline, directional river, waterfall/rapid, terrain/rock/structure contacts.
- [x] Provide near, wide, elevated, and waterfall inspection view modes plus moving inspection controls; exact-built time-separated visual proof remains pending.
- [x] Reuse existing screenshot/SceneIssue replay/standalone-player harness contracts; add no parallel capture stack or CI transport.
- [x] Add production portability outside the scene: independent Water/RiverWater/Cascade authoring through `ShowcaseWorld`, canonical read storage, normal presentation installation, and `RenderingComposition` world binding.
- [x] Verify `VoxelShowcase` automatically receives shared presentation and restores water after a leaked diagnostic disable.
- [x] Verify `WorldbuildingGalleryShowcase` automatically reaches the same globally installed water presentation for its cave-authored water.

## Regression / reliability / cost
- [x] Production installation regression covers still/river/waterfall profiles and excludes non-water material.
- [x] Gameplay regression proves still/river retain spreading-water semantics while cascade remains inert.
- [x] Extraction regression preserves water material identity at negative world coordinates.
- [x] Extraction regression covers reciprocal boundary-neighbor suppression and keeps distinct profiles across seams.
- [x] Extraction regression proves a material outside the installed water mask is not rendered as water.
- [x] Render lifecycle/source tracing confirms one shared material and one `_WaterTime` update path.
- [x] Production renderer-path portability coverage authors Water/RiverWater/Cascade through `ShowcaseWorld`, binds the ordinary `RenderingWorldBinding`, verifies installed profile arrays, and combines with focused extraction identity/seam regressions; no source-string-only assertion.
- [ ] Run exact player build for shader compile/stripping/pink/missing-resource failures.
- [x] Static memory cost: six 32-entry `Vector4` water tables = 3,072 bytes; no per-water-voxel GameObjects/material instances were added.
- [ ] Record exact-player CPU/GPU/memory/render observations: draw/batching/culling, transparent overdraw risk, shader ALU/depth sampling, large bodies, and waterfall-only extras; do not weaken budgets.
- [x] Review feature-only diff against current master: only assignment water code/tests/scene/build registration/docs are changed; `.github/test-request.json` is absent from the feature diff.

## Exact-SHA gates
- [x] Refresh/merge latest `origin/master` before final feature SHA attempt; current compare is `behind_by=0` after merge commit `ab8b3bc3efa3eac933ce861748664fb246dc1ea2`.
- [x] Push current production/test/docs commits on `fixes/agent-9`; final SHA will be re-read after this task synchronization commit.
- [x] Confirm existing `ci-test/fixes/agent-9` workflow history has no queued/running request to replace; latest listed request is completed.
- [ ] Re-read `origin/master` and `fixes/agent-9`; merge again before CI if master advanced.
- [ ] Submit exactly one smallest final targeted-CI request from `ci-test/fixes/agent-9` whose parent is the exact final feature SHA.
- [ ] Confirm focused behavioral regressions green on exact SHA.
- [ ] Confirm exact-SHA built `WaterRenderingShowcase` launches without startup/runtime exceptions.
- [ ] Download and inspect durable real-player artifact, build/player logs, and near/wide/time-separated showcase frames.
- [ ] Directly compare built waterfall evidence with retained reference semantics: downward flow, turbulence, aeration, irregular edges, lip/edge/base foam, mist/spray.
- [ ] Reconcile exact final build with registered `VoxelShowcase` and verified `WorldbuildingGalleryShowcase` shared-water paths; do not create an extra CI transport.
- [ ] Record built-player CPU/GPU/memory/draw/overdraw/variant/culling observations.
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
