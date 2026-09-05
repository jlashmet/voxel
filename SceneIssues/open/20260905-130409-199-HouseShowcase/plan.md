# Plan — HouseShowcase colors/textures

## Observed behavior / acceptance
The captured HouseShowcase defect is narrowly visual: colors/textures are incorrect. HouseShowcase is a production-path consumer: it authors `GuildHousePrototypeComposition`/furnishings into voxel storage, installs `GameMaterialComposition`, and binds storage material/surface/coating presentation to the shared renderer. Acceptance is a built `Assets/Scenes/HouseShowcase.unity` whose production house materials read correctly at exterior and interior inspection states, with no showcase-only fake material path.

## Competing hypotheses and discriminator
1. **Production material identity/authoring defect.** Falsified. The built baseline uses the expected Kentridge Wood/MasonrySmall/Cloth/LitWindow identities and canonical material presentation; those values are not the cyan/magenta output seen in the capture.
2. **HouseShowcase environment/presentation defect.** Confirmed. Exact-SHA baseline run `33994794734` reproduced flat cyan/teal and magenta faces with no shader/material errors. HouseShowcase passed a non-white `surfaceDebugTint`; `VoxelRenderPass` therefore enabled `_DebugCoverage`, and `SmoothSurface.shader` returned encoded normals before material texture evaluation.

The ordinary VoxelShowcase runtime does not call `ConfigureEnvironment`, so this is a HouseShowcase-only scene-composition misuse rather than a same-owner shared-consumer break.

## Selected fix
Use `Color.white`, the renderer's documented production surface-debug value, from HouseShowcase presentation setup. Keep the fix scene-owned; do not alter shared renderer/material semantics. Pin the production tint policy with a Showcase EditMode regression.

## Remaining gates
- Run exact-SHA targeted CI for the fixed feature, including repository-derived Showcase module validation and built HouseShowcase SceneIssue replay.
- Inspect new exterior/interior screenshots and verify real production material shading/textures replace normal-debug colors.
- Re-review final diff and acceptance evidence.
- Move open→closed, merge current master, PR + auto-merge, required PR `affected` gate, verify closed issue on master.
