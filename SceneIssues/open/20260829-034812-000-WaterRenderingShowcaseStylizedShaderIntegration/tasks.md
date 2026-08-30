# Tasks

## Workflow / investigation

- [x] Read `AGENTS.md`, canonical `SceneIssues/README.md`, assignment; record that requested `SceneIssues/feature-readme.md` is absent.
- [x] Inspect assignment metadata and capture list (empty).
- [x] Inspect imported Stylized Water package and `WaterfallReference.shader` semantics.
- [x] Trace canonical water extraction/cache/render pass and one shared material lifecycle.
- [x] Run competing-hypothesis discriminator and maintain `plan.md` separately.
- [x] Resume existing `fixes/agent-9`; prior master refreshes are preserved in branch history.
- [ ] Trace liquid/gameplay consumers: collision, swimming/buoyancy/wading where present, spreading, storage/streaming, discovery/meshing, edits, diagnostics.
- [ ] Trace normal player bootstrap ordering: material presentation installation before water classification/extraction/rendering.
- [ ] Identify `VoxelShowcase`, Kentridge/second production water consumer, and any legacy production fallback.
- [x] Verify active URP renderer-data asset serializes `WaterSurface.shader` and matching URP asset is selected.
- [x] Discover missing `WaterRenderingShowcase` and that build settings currently contain only indices 0/1.
- [ ] Preserve build indices 0/1; register `VoxelShowcase` at 2 and `WaterRenderingShowcase` at required 3.

## Shared implementation

- [x] Add reusable still/lake, river/stream, waterfall/rapid presentation profiles.
- [x] Remove hard-coded game IDs from engine water extraction; classify from installed presentation data.
- [x] Preserve per-vertex water material identity through extraction.
- [x] Keep one renderer-owned `Hidden/VoxelEngine/WaterSurface` material with no scene-local forks.
- [x] Adapt package shallow/deep color, depth fade, contact/surface foam, animated normals/detail, highlights/refraction/wave direction.
- [x] Add distinct directional river motion.
- [x] Add waterfall downward flow, turbulence, aeration, irregular breakup, lip/edge/base foam, mist/spray cues.
- [x] Keep production voxel extraction/cache authoritative for water geometry.
- [ ] Prove no normal built water silently falls back to legacy generic shader/material behavior.
- [ ] Prove player-build shader retention and no editor-only resource dependency.

## Showcase / portability

- [ ] Create `Assets/Scenes/WaterRenderingShowcase.unity` through standard voxel storage/authoring and canonical renderer; no production proof planes/bespoke water meshes.
- [ ] Keep scene/controller limited to terrain/water placement, semantic selection, lighting/camera/inspection controls.
- [ ] Demonstrate still/deep, shoreline, directional river, waterfall/rapid, terrain/rock/structure contacts.
- [ ] Provide readable near, wide, elevated, and time-separated views; waterfall must show downward flow, turbulence, irregular edges, aeration, lip/edge/base foam, mist.
- [ ] Add/verify production portability outside showcase with at least two independently selectable profiles and waterfall semantics.
- [ ] Verify `VoxelShowcase` automatically receives shared presentation.
- [ ] Verify Kentridge or another existing normal water scene automatically receives shared presentation.

## Regression / reliability / cost

- [x] Production installation regression covers still/river/waterfall profiles and excludes non-water material.
- [x] Gameplay regression proves still/river retain spreading-water semantics while cascade remains inert.
- [x] Extraction regression preserves water material identity at negative world coordinates.
- [x] Extraction regression covers reciprocal boundary-neighbor suppression and keeps distinct profiles across seams.
- [x] Extraction regression proves a material outside the installed water mask is not rendered as water.
- [x] Render lifecycle/source tracing confirms one shared material and one `_WaterTime` update path.
- [ ] Add only the missing production renderer-path portability/binding regression (actual shader/profile arrays; no source-string-only assertion).
- [ ] Run player build for shader compile/stripping/pink/missing-resource failures.
- [ ] Measure CPU/GPU/memory/render cost: 3,072-byte installed profile tables plus draw/batching/culling, transparent overdraw, shader ALU/texture/depth work, large bodies, waterfall extras; do not weaken budgets.
- [ ] Review final feature-only diff for unrelated files and `.github/test-request.json` contamination.

## Exact-SHA gates

- [ ] Refresh/merge latest `origin/master` before final feature SHA if advanced.
- [ ] Push final production/test SHA on `fixes/agent-9`.
- [ ] Confirm no queued/running agent-9 targeted CI is being replaced.
- [ ] Submit exactly one smallest final targeted-CI request from `ci-test/fixes/agent-9` for the exact feature SHA.
- [ ] Confirm focused behavioral regressions green on exact SHA.
- [ ] Confirm exact-SHA built `WaterRenderingShowcase` launches without startup/runtime exceptions.
- [ ] Confirm durable near/wide/time-separated showcase evidence and direct waterfall-reference comparison.
- [ ] Confirm exact-SHA `VoxelShowcase` plus second production water scene demonstrate global replacement.
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
- [ ] A14 — Durable exact-built evidence has near/wide/time-separated motion for required cases plus existing production scene evidence.
- [ ] A15 — Direct visual review meets repository quality with stable contacts/depth/foam/flow/waterfall and no placeholder/sorting catastrophe.
- [ ] A16 — Blast radius and CPU/GPU/memory/render costs are measured without weakened budgets.
- [ ] A17 — Durable reference evidence remains and built waterfall is explicitly compared to approved downward-flow/turbulence/aeration/edge/lip/base/mist behaviors.
