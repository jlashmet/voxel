# Tasks

## Workflow / investigation

- [x] Read `AGENTS.md`, canonical `SceneIssues/README.md`, assignment, and note requested `feature-readme.md` is absent.
- [x] Inspect assignment metadata/captures (capture list is empty).
- [x] Confirm canonical workflow does not require synthetic `repro.json` / `expected.json` / `replay.json` files.
- [x] Inspect imported Stylized Water Shader material/graph support and identify depth, shallow/deep color, foam, normals, refraction, and wave semantics to preserve.
- [x] Inspect `WaterfallReference.shader` and record the non-lake waterfall semantics.
- [x] Trace canonical voxel-water topology/render path through extraction/cache, render pass, shared material, and per-vertex material selection.
- [x] Run competing-hypothesis discriminator and record results in `plan.md`.
- [x] Preserve the prior feature/master refresh recorded in merge commit `20adc71ba46ac929136c7f95c042fcb62a62a2e0`.
- [x] Re-fetch current `origin/master` on resume and merge it conflict-free into `fixes/agent-9` at `957b798940f008e07fde9ce27225046b2652da81`; current master changes did not overlap the water implementation.
- [x] Correct stale resume-blocker assumptions: the canonical shader is the existing renderer-owned `Assets/VoxelEngine/Rendering/Runtime/Shaders/WaterSurface.shader`; focused tests do not reference a missing `WaterRenderingMaterialBinding`, and the render pass already owns/reuses one water material.
- [ ] Trace all liquid classification/gameplay consumers so presentation changes cannot change swimming, buoyancy, collision, spreading, streaming, discovery, edits, or diagnostics.
- [ ] Identify `VoxelShowcase` and a second normal production water consumer (prefer Kentridge) and any remaining legacy water fallback.
- [x] Verify the active URP renderer-data asset serializes `WaterSurface.shader` and the project GraphicsSettings selects the matching URP asset/renderer.
- [ ] Verify material-presentation installation occurs before renderer water classification/extraction is consumed in normal player bootstrap.
- [x] Discover that the required `Assets/Scenes/WaterRenderingShowcase.unity` is absent and current build settings contain only two enabled scenes, so build index 3 cannot yet exist.

## Shared implementation

- [x] Add semantic-free renderer-owned water presentation/profile data with reusable still/lake, river/stream, and waterfall/rapid motion profiles.
- [x] Remove hard-coded game material IDs from engine liquid extraction; derive liquid classification from installed shared presentation data.
- [x] Keep one canonical shared production water shader/material path and no scene-local material forks.
- [x] Reuse one water material per render-pass lifecycle; `VoxelRenderPass.Setup` creates it once, setup/disposal destroys it, and visible chunks share it with no per-frame material allocation.
- [x] Adapt package shallow/deep color, depth fade, surface/intersection foam, normal/detail breakup, highlights, refraction, and wave direction/speed semantics into the shared `WaterSurface.shader`.
- [x] Keep `CpuWaterSurfaceChunkCache` / voxel meshing authoritative for production water geometry; presentation shading does not introduce an independent scene-local geometry authority.
- [x] Add directional river flow distinct from still-water waves.
- [x] Add reusable shoreline/depth/contact foam inputs.
- [x] Add waterfall downward flow, turbulence, aeration, irregular breakup, lip/edge/base-impact foam, and mist/spray cues from `WaterfallReference.shader`.
- [x] Preserve distinct per-vertex water material indices through extraction so independently authored profiles share the canonical water shader.
- [ ] Ensure no normal water silently falls back to legacy generic shader behavior in a built player.
- [ ] Preserve player-build shader retention / no editor-only dependency or missing shader asset.

## Showcase / portability

- [ ] Create `Assets/Scenes/WaterRenderingShowcase.unity` using the repository's standard voxel storage/authoring and production water renderer; do not add hand-authored production water planes or bespoke water meshes.
- [ ] Keep showcase code limited to water/terrain placement, semantic profile selection, camera/inspection intent, and deterministic review controls; reusable renderer behavior stays in shared engine/game code.
- [ ] Add/verify `WaterRenderingShowcase` at enabled build index 3 without disturbing existing required build scenes or replacing the project URP pipeline.
- [ ] Demonstrate still/deep, shoreline, river, waterfall/rapid, and terrain/rock/structure contact cases with readable near/wide views.
- [ ] Make the showcase traversable/walkable enough to inspect near-player and elevated views under consistent lighting without introducing water-specific gameplay authority.
- [ ] Add/validate a small production-authored portability case with at least two independently selectable water profiles and no `WaterRenderingShowcase` dependency; include waterfall production semantics.
- [ ] Verify `VoxelShowcase` automatically receives the shared presentation.
- [ ] Verify a second existing normal water scene automatically receives the shared presentation.

## Regression / reliability / cost

- [x] Add focused behavioral regression through real production profile installation and liquid classification for still, river, and waterfall profiles.
- [x] Add focused gameplay regression proving still/river preserve spreading-water simulation while authored cascade remains inert rather than becoming spreading water.
- [x] Verify render-loop lifecycle/wiring in production code: one shared water material, `_WaterTime = Time.time`, and one per-frame water-time global update rather than material churn.
- [ ] Add/confirm focused extraction regression proving presentation-mask water classification preserves material identity across multiple independently selectable profiles.
- [ ] Add negative-world-coordinate and chunk/brick-boundary extraction coverage, including reciprocal boundary-neighbor suppression so faces do not appear/disappear at seams.
- [ ] Add a renderer-path portability regression that authors multiple water bodies through production storage/authoring, installs the canonical catalogue, and proves still/river/waterfall all reach the same production renderer without showcase code.
- [ ] Add a low-cost runtime shader/material-binding regression that exercises the actual `Hidden/VoxelEngine/WaterSurface` shader and installed profile arrays rather than only source strings or material names.
- [ ] Check player build for shader compile/stripping/pink/missing-resource failures.
- [ ] Quantify CPU/GPU/memory/render cost: six 32-row `Vector4` profile tables are 3,072 bytes and uploaded once per catalogue install; still quantify water draw calls/culling, transparent overdraw, shader ALU/texture/depth work, large bodies, turbulence/foam/mist work, and confirm budgets are not weakened.
- [ ] Review final feature-only diff for unrelated files and `.github/test-request.json` contamination.

## Visual / exact-SHA gates

- [ ] Run exact-SHA built-player `WaterRenderingShowcase` to a usable rendered state with no startup/runtime exceptions.
- [ ] Capture durable near/wide and time-separated/video-equivalent evidence showing animation plus waterfall downward flow, lip/edge/base foam, turbulence/aeration, irregular breakup, and mist/spray.
- [ ] Directly review visual quality against the imported Stylized Water package behavior and durable `WaterfallReference.shader` reference.
- [ ] Run exact-SHA built-player `VoxelShowcase` and second production water scene to demonstrate global replacement.
- [ ] Record actual built-player CPU/GPU/memory/draw/overdraw/variant/culling observations against existing budgets.
- [ ] Push final production/test SHA on `fixes/agent-9`.
- [ ] Submit exactly one smallest targeted-CI request from `ci-test/fixes/agent-9` for that exact feature SHA; do not replace queued/running CI.
- [ ] Confirm focused behavioral regressions green on exact feature SHA.
- [ ] Confirm exact-SHA built-application scene harness and visual evidence gates green.
- [ ] Complete `resolutionSummary`, `regressionTest`, and `fixCommit`; move open → pending in the workflow-prescribed bookkeeping commit.
- [ ] After every exact-SHA gate is green and every acceptance checkbox below is complete, move pending → closed, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master` into `fixes/agent-9`, push exact feature head, and non-force fast-forward that exact head to `origin/master`; fetch/merge/retry if master advanced.

## Acceptance ledger (must all be validated before close)

- [ ] A1 — Built `WaterRenderingShowcase` exists, is in the normal build/harness path, launches cleanly, and uses standard voxel/WorldBuilder water authoring rather than production proof planes.
- [ ] A2 — Built showcase visibly contains still/deep, shallow shoreline, directional river, convincing waterfall/rapid, and voxel terrain/rock/structure contact cases with all required waterfall visual cues.
- [ ] A3 — Every case uses the same canonical reusable production water renderer with semantic/profile configuration and no scene shader/material fork.
- [ ] A4 — The imported Stylized Water package and approved waterfall reference are materially adapted into the canonical renderer while preserving wave/color/depth/foam/flow/surface-detail intent.
- [ ] A5 — `VoxelShowcase` plus one additional production scene automatically use the replacement with no scene-by-scene reassignment.
- [ ] A6 — No normal game water remains on a legacy production shader/material fallback; retained legacy assets, if any, are diagnostic/reference-only.
- [ ] A7 — Scene code contains only placement/profile/inspection intent; reusable binding, data, batching/culling, flow/depth/foam, waterfall, and presentation behavior remains shared.
- [ ] A8 — Built still, river, and waterfall cases visibly animate differently through reusable semantics; waterfall flows coherently downward and is not a rotated/faster lake.
- [ ] A9 — Shore/depth/contact foam and waterfall lip/edge/base foam are driven by reusable production geometry/depth/profile semantics, not showcase-only masks or coordinates.
- [ ] A10 — Collision/buoyancy/swim/wade where present, spreading, streaming, discovery/meshing, edits, and water visibility diagnostics remain compatible.
- [ ] A11 — Exact player build has no water shader compile/stripping/pink/missing-resource/editor-only dependency failure.
- [ ] A12 — Focused regressions exercise the real production selection/binding/extraction path for independently authored profiles, beyond source-string/material-name assertions.
- [ ] A13 — Portability coverage authors multiple water bodies outside showcase code through the production path and includes waterfall semantics or an equivalent focused production regression.
- [ ] A14 — Durable exact-built visual evidence includes near/wide plus time-separated motion for every required case and evidence from an existing production scene.
- [ ] A15 — Direct visual review meets repository quality: readable large water, stable contacts/depth/foam/flow/waterfall, no obvious sorting catastrophe, floating/disconnected surfaces, or placeholder fallback.
- [ ] A16 — Blast radius and CPU/GPU/memory/render cost are measured for default production use, including overdraw, shader complexity/variants, draw/batching/culling, large bodies, and waterfall extras without weakening budgets.
- [ ] A17 — `WaterfallReference.shader` remains durable ticket evidence and the built waterfall is explicitly compared against its approved downward-flow/turbulence/aeration/edge/lip/base/mist behaviors.
