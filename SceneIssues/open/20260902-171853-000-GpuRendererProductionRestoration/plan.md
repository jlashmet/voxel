# GPU renderer production restoration — implementation plan

**Acceptance scene:** `Assets/Scenes/VoxelShowcase.unity` only. Kentridge, mountain, castle, terrain, structures, vegetation, and far-world content matter only as rendered inside VoxelShowcase.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec`. Draw-distance integration merged by `d6725b4113a54aabe48281f3af7f357ff5975b25`.

## Current state and acceptance

GPU density/semantic/topology parity and the minimal one-chunk GPU fixture were already proven, but full-scene acceptance remains red. CPU is the immediate gate: VoxelShowcase must be production-quality with GPU solid cutover disabled before GPU-specific work resumes.

Exact feature `8ae5d743...`, run `33978398855`, completed the standalone CPU-only VoxelShowcase replay and captured four stationary frames. They show large bright white/gray slab/blob regions. Replay diagnostics kept GPU solid requests at zero and reported roughly `semantic=1477/1481 radius=12000m`, so this is valid defect evidence even though the later repository-derived module step failed.

That module failure was isolated to synthetic Input System devices not being current; production correctly reads `Keyboard.current`/`Mouse.current`. The bounded fixture repair is `057e74c0...`. Its revalidation run `33984671790` never obtained a self-hosted runner and was cancelled while queued, so it is infrastructure, not product evidence.

## First wrong boundary and selected fix

Trace: `FeaturePresentationBake` -> `FarFeaturePresentationAdapter` -> `ShowcaseFarFeatureStateAdapter` -> `ProceduralFarFeatureRenderer`.

The bake preserves canonical material/style/coating identity, but the far renderer previously created a default URP Lit/Standard material for every style. That is the first demonstrated CPU-visible divergence; CPU Transvoxel extraction is not the current cause.

Selected generic repair:
- Composition resolves the already-installed material/coating catalogue into semantic-free coarse `FarFeaturePresentation` values (albedo + roughness). Raw palette indices do not cross the Rendering API.
- `FarFeaturePresentationAdapter` carries those resolved values with the existing opaque style cache key.
- `ShowcaseFarFeatureStateAdapter` preserves the presentation while applying removed/ruined state.
- `ProceduralFarFeatureRenderer` applies the resolved values to its production instanced material instead of shader defaults.
- Focused tests cover installed material/coating projection, renderer material use, and Showcase state preservation.

Do **not** change far geometry yet. A second hypothesis remains that conservative box fallback for unsupported far primitive shapes causes malformed silhouettes; post-material-fix built-player evidence must discriminate that before geometry work.

Exact feature `7ceaa012...`, transport `416400b3...`, run `33986630571` was admitted and failed before tests/player execution because `VoxelEngine.Rendering.Tests.EditMode` did not reference `VoxelEngine.Rendering.Api`; the new parity test therefore could not compile. The bounded owning-assembly dependency fix is `b971001b...`; no product or visual conclusion is taken from that failed run.

## Module ownership

- `VoxelEngine.Rendering`: player-visible; existing `Assets/VoxelEngine/Rendering/Validation/FarWorld/` scene/scenario is the owning focused validation surface.
- `Game/Composition/Showcase/SceneRuntime`: player-visible integration; existing `ShowcaseInputRuntimeValidation.unity` now has its required paired scenario, plus module EditMode coverage.
- `VoxelEngine.Composition`: the changed code is headless value projection/selection with no direct scene behavior; EditMode coverage plus the two production consumers above is the appropriate validation exception.

## Remaining gates

1. Run exact-SHA focused regressions and repository-derived module validation for the current fix.
2. Re-run exact-SHA CPU-only VoxelShowcase captures. White blobs are a hard failure; if material is fixed but silhouettes remain malformed, isolate the first geometry divergence before another fix.
3. Prove CPU VoxelShowcase production-quality from stationary and traversal views.
4. Restore/reconcile the pre-CPU-gate GPU implementation, re-enable normal GPU cutover, and resume deterministic CPU/GPU parity at the first GPU-only mismatch.
5. Finish GPU allocation/publication/lifetime, streaming/edit, performance/memory, no-blocking, and zero-hidden-fallback acceptance.
6. Merge current `master`, run final exact-SHA gates, close `open/` -> `closed/`, then PR + auto-merge and verify the closed issue on `origin/master`.
