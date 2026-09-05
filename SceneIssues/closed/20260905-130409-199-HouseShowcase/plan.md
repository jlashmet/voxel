# Plan — HouseShowcase colors/textures

## Observed behavior / acceptance
The captured HouseShowcase defect is narrowly visual: colors/textures are incorrect. HouseShowcase is a production-path consumer: it authors `GuildHousePrototypeComposition`/furnishings into voxel storage, installs `GameMaterialComposition`, and binds storage material/surface/coating presentation to the shared renderer. Acceptance is a built `Assets/Scenes/HouseShowcase.unity` whose visible production house surfaces use real material shading rather than diagnostic normal colors, with no showcase-only fake material path.

## Competing hypotheses and discriminator
1. **Production material identity/authoring defect.** Falsified. The built baseline uses the expected Kentridge Wood/MasonrySmall/Cloth/LitWindow identities and canonical material presentation; those values are not the cyan/magenta output seen in the capture.
2. **HouseShowcase environment/presentation defect.** Confirmed. Exact-SHA baseline run `33994794734` reproduced flat cyan/teal and magenta faces with no shader/material errors. HouseShowcase passed a non-white `surfaceDebugTint`; `VoxelRenderPass` therefore enabled `_DebugCoverage`, and `SmoothSurface.shader` returned encoded normals before material texture evaluation.

The ordinary VoxelShowcase runtime does not call `ConfigureEnvironment`, so this is a HouseShowcase-only scene-composition misuse rather than a same-owner shared-consumer break.

## Selected fix
Use `Color.white`, the renderer's documented production surface-debug value, from HouseShowcase presentation setup. Keep the fix scene-owned; do not alter shared renderer/material semantics. Pin the production tint policy with `HouseShowcasePresentationTests` in the Showcase EditMode assembly.

## Acceptance evidence
- Baseline exact run `33994794734`: reproduced cyan/magenta normal-debug shading.
- Fixed feature SHA `ac8262bc48d4a0069856fb2afc41e06bf679b076` via exact transport `e14333a4005a4d959bd91b88ab5d0253c2b87ac2`, run `33995946540`: success.
- Repository-derived Showcase EditMode/PlayMode validation, requested Structures regression, module-local player validation, Kentridge integration player, and built HouseShowcase replay all passed.
- Fixed screenshots show production wood/stone/cloth shading instead of encoded normals.
- Sparse/floating geometry is identical in corresponding baseline frames, so it predates this fix and is not part of the reported colors/textures defect.

## Closure
Acceptance evidence is complete. Move `open` directly to `closed`, record resolution fields, merge current `master` into `fixes/agent-3`, open PR, enable auto-merge, wait for required `affected`, and verify the closed issue on `master`.