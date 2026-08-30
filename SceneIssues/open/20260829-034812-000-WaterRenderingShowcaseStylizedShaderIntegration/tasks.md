# Tasks

## Workflow / investigation

- [x] Read `AGENTS.md`, canonical `SceneIssues/README.md`, assignment, and note requested `feature-readme.md` is absent.
- [x] Inspect assignment metadata/captures (capture list is empty).
- [x] Confirm canonical workflow does not require synthetic `repro.json` / `expected.json` / `replay.json` files.
- [x] Inspect imported Stylized Water Shader material/graph support and identify depth, shallow/deep color, foam, normals, refraction, and wave semantics to preserve.
- [x] Inspect `WaterfallReference.shader` and record the non-lake waterfall semantics.
- [x] Trace canonical voxel-water topology/render path through extraction/cache, render pass, shared material, and per-vertex material selection.
- [x] Run competing-hypothesis discriminator and record results in `plan.md`.
- [x] Refresh `fixes/agent-9` with current `origin/master` in merge commit `20adc71ba46ac929136c7f95c042fcb62a62a2e0`; no water-path conflicts were present.
- [ ] Trace all liquid classification/gameplay consumers so presentation changes cannot change swimming, buoyancy, collision, spreading, streaming, discovery, edits, or diagnostics.
- [ ] Identify `WaterRenderingShowcase`, `VoxelShowcase`, and a second normal production water consumer (prefer Kentridge) and any remaining legacy water fallback.
- [ ] Resolve resumed-branch compile blockers: the focused tests currently reference missing `WaterRenderingMaterialBinding`, and `Assets/Game/Materials/Shaders/StylizedWater.shader` is absent.

## Shared implementation

- [ ] Add semantic-free renderer-owned water presentation/profile data with reusable still/lake, river/stream, and waterfall/rapid motion profiles.
- [ ] Remove hard-coded game material IDs from engine liquid extraction; derive liquid classification from installed shared presentation data.
- [ ] Keep one canonical shared production water shader/material path and no scene-local material forks.
- [ ] Add `WaterRenderingMaterialBinding` (or the smallest equivalent shared binding) that creates/reuses one production material per renderer lifecycle, applies deterministic profile properties, and releases owned resources without per-frame material churn.
- [ ] Add/promote the canonical player-build shader asset at `Assets/Game/Materials/Shaders/StylizedWater.shader` (plus Unity metadata as required) with the exact production shader name used by shared composition.
- [ ] Adapt package shallow/deep color, depth fade, surface/intersection foam, normal/detail breakup, highlights, refraction, and wave direction/speed semantics into the shared shader.
- [ ] Keep `CpuWaterSurfaceChunkCache` / voxel meshing authoritative for production water geometry/deformation; presentation shading must not introduce an independent scene-local geometry authority.
- [ ] Add directional river flow distinct from still-water waves.
- [ ] Add reusable shoreline/depth/contact foam inputs.
- [ ] Add waterfall downward flow, turbulence, aeration, irregular breakup, lip/edge/base-impact foam, and mist/spray cues from `WaterfallReference.shader`.
- [ ] Ensure no normal water silently falls back to the legacy generic shader behavior.
- [ ] Preserve player-build shader retention / no editor-only dependency or missing shader asset.

## Showcase / portability

- [ ] Validate `WaterRenderingShowcase` build index 3 uses standard WorldBuilder/voxel water authoring with no hand-authored production water planes.
- [ ] Demonstrate still/deep, shoreline, river, waterfall/rapid, and terrain/rock/structure contact cases with readable near/wide views.
- [ ] Add/validate a small production-authored portability case with at least two independently selectable water profiles; include waterfall where practical.
- [ ] Verify `VoxelShowcase` automatically receives the shared presentation.
- [ ] Verify a second existing normal water scene automatically receives the shared presentation.

## Regression / reliability / cost

- [ ] Add focused behavioral regression through real production profile/material installation and liquid classification for multiple independent bodies/profiles.
- [ ] Add focused material-binding lifecycle regression proving deterministic property application, reuse, and release with no frame-by-frame material allocation.
- [ ] Add focused `CpuWaterSurfaceChunkCache` regression proving stable topology/allocation identity while configured deformable regions animate deterministically.
- [ ] Add focused waterfall semantic regression (not source-string-only).
- [ ] Check player build for shader compile/stripping/pink/missing-resource failures.
- [ ] Quantify CPU/GPU/memory/render cost: constant/profile uploads, water draw calls/batching/culling, transparent overdraw, shader variants, large bodies, turbulence/foam/mist work; do not weaken budgets.
- [ ] Review final feature-only diff for unrelated files and `.github/test-request.json` contamination.

## Visual / exact-SHA gates

- [ ] Run exact-SHA built-player `WaterRenderingShowcase` to a usable rendered state with no startup/runtime exceptions.
- [ ] Capture durable near/wide and time-separated/video-equivalent evidence showing motion plus waterfall lip/edge/base foam/turbulence/mist.
- [ ] Directly review visual quality against package behavior and `WaterfallReference.shader`.
- [ ] Run exact-SHA built-player `VoxelShowcase` and second production water scene to demonstrate global replacement.
- [ ] Push final production/test SHA on `fixes/agent-9`.
- [ ] Submit exactly one smallest targeted-CI request from `ci-test/fixes/agent-9` for that exact feature SHA; do not replace queued/running CI.
- [ ] Confirm focused behavioral regression green on exact feature SHA.
- [ ] Confirm exact-SHA built-application scene harness green.
- [ ] Complete `resolutionSummary`, `regressionTest`, and `fixCommit`; move open → pending in a separate bookkeeping commit.
- [ ] After all exact-SHA gates are green, move pending → closed, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master` into `fixes/agent-9`, push exact feature head, and non-force fast-forward that exact head to `origin/master`; fetch/merge/retry if master advanced.
