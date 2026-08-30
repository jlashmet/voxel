# Tasks — Water Rendering Showcase Stylized Shader Integration

## Workflow / investigation
- [x] Read `AGENTS.md`, canonical `SceneIssues/README.md`, assignment, and confirm requested `feature-readme.md` is absent.
- [x] Confirm `captures=[]`; no original screenshot/annotation poses exist.
- [ ] Restore the SceneIssue contract files required by the repository workflow (`repro.json`, `expected.json`, and `replay.json`) before capture/CI work; keep them scoped to this feature.
- [ ] Inspect supplied stylized-water Shader Graph, support HLSL/subgraphs/material/textures/scripts.
- [x] Inspect `WaterfallReference.shader` and record required reusable waterfall behaviors.
- [ ] Trace canonical voxel-water authoring, meshing/discovery, renderer/material/shader selection, streaming, visibility diagnostics, and gameplay consumers.
- [ ] Identify all normal production scenes/consumers currently using water and locate any legacy production shader/material fallback.
- [ ] Run discriminator and record at least two hypotheses/results in plan/experiment evidence.

## Shared production implementation
- [ ] Define reusable semantic water profiles/body inputs for still/lake, river/stream, waterfall/rapid behavior.
- [ ] Route all standard voxel-engine water through one canonical stylized production renderer/material path.
- [ ] Preserve stylized wave/normal breakup, deep-mid-shallow color/depth, localized foam, highlights/refraction where supported, and surface displacement.
- [ ] Add reusable directional flow semantics; still, river, and waterfall must have materially different motion.
- [ ] Add reusable shoreline/depth/contact foam semantics from production water/terrain/render data, not showcase-painted masks.
- [ ] Add waterfall orientation/speed, turbulent breakup/aeration, irregular edge/lip foam, base-impact churn/foam, and mist/spray semantics derived from shared profile/body data.
- [ ] Ensure no normal game water can silently select the legacy production water shader/material.
- [ ] Preserve collision/buoyancy/swimming/wading, edits, discovery/meshing, streaming/culling, and render-disable diagnostics.
- [ ] Ensure player builds retain required shader graph dependencies/resources/variants without editor-only lookup.

## Showcase / portability
- [ ] Add `Assets/Scenes/WaterRenderingShowcase.unity` at build index 3 through normal scene/build harness.
- [ ] Author showcase through WorldBuilder/voxel water systems—no scene-local production water planes/material forks.
- [ ] Include broad still/deep water, shallow shoreline, directional river/stream, waterfall/rapid, terrain/cliff/rock/structure contacts, near and elevated/wide views.
- [ ] Add production-authored portability coverage with independently authored bodies/profiles outside showcase code, including waterfall semantics.
- [ ] Verify standard water in `VoxelShowcase` automatically uses the new renderer.
- [ ] Verify a second existing production scene containing water automatically uses the new renderer (prefer Kentridge if applicable).

## Regression / reliability / cost
- [ ] Add focused behavioral regression through real production material/profile selection for multiple independently authored bodies.
- [ ] Add waterfall semantic regression proving it is more than rotated/faster lake water.
- [ ] Validate no pink/missing material, shader compile errors, missing variants, or accidental stripping in player build.
- [ ] Measure/record blast radius and CPU/GPU/memory, transparent overdraw/sorting, draw-call/batching, shader-variant, culling/large-body, and waterfall turbulence/foam/mist costs without weakening budgets.

## Visual/build acceptance
- [ ] Built `WaterRenderingShowcase` launches without startup/runtime exceptions.
- [ ] Inspect motion/time-separated evidence for still, shoreline, river, and waterfall cases; waterfall must visibly show downward coherent flow, turbulent streaks, irregular edges, aeration, lip/edge/base foam and mist/spray consistent with `WaterfallReference.shader`.
- [ ] Inspect near-player and wide/elevated showcase views for depth/color separation, shoreline stability, contacts, sorting, support and no placeholder fallback.
- [ ] Build/launch `VoxelShowcase` and visually prove global replacement outside showcase.
- [ ] Build/launch one additional existing production water scene and visually prove global replacement.
- [ ] Review final feature-only diff for no unrelated SceneIssues, workflows, packages, generated expansion, or feature-branch `.github/test-request.json` edit.
- [ ] Run one final exact-SHA targeted CI request on `ci-test/fixes/agent-9`; inspect logs/artifacts and require focused regression + exact built-scene evidence green.
- [ ] Complete pending metadata with validated source SHA and durable evidence.
- [ ] Move open -> pending -> closed, set `status=fixed` and `resolvedUtc`, merge current master, and promote exact feature head non-force.
