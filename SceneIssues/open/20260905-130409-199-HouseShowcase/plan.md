# Plan — HouseShowcase colors/textures

## Observed behavior / acceptance
The captured HouseShowcase defect is narrowly visual: colors/textures are incorrect. HouseShowcase is a production-path consumer: it authors `GuildHousePrototypeComposition`/furnishings into voxel storage, installs `GameMaterialComposition`, and binds storage material/surface/coating presentation to the shared renderer. Acceptance is a built `Assets/Scenes/HouseShowcase.unity` whose production house materials read correctly at exterior and interior inspection states, with no showcase-only fake material path.

## Competing hypotheses
1. **Production material identity/authoring defect.** Guild-house shell/facade/roof/trim authoring assigns generic material identities whose canonical presentation cannot express the intended house surfaces, so the wrong colors/textures are already encoded before rendering.
2. **HouseShowcase environment defect.** Production material identities/presentation are correct, but HouseShowcase's sky/environment/camera setup makes those shared materials render with incorrect apparent color/texture.

A simple recent material regression is falsified: the earlier visually accepted HouseShowcase merge `af61066de669431a6555e737887bd5d4031525b8` and current master have no changes to `HouseShowcase.cs` or game material rendering/catalogue files.

## Discriminator
Run the current scene through exact-SHA standalone replay and inspect durable exterior/interior screenshots. Cross-check the visible surfaces against the material ids selected by production guild-house authoring and canonical `GameMaterialRuntimeCatalogue` presentation. If the material identities themselves collapse intended facade/roof/trim semantics, fix the shared production authoring boundary and add Structures regression. If identities are correct and only this scene renders them incorrectly, fix HouseShowcase presentation and add SceneRuntime regression/validation.

## Remaining gates
- Capture/inspect current built-player baseline and classify visual quality.
- Implement only the proven owner fix plus module-owned behavioral regression/validation.
- Exact-SHA targeted CI, including built HouseShowcase evidence, must pass.
- Re-review built screenshots as `production-quality` before closure.
- Move open→closed, merge current master, PR + auto-merge, required PR `affected` gate, verify closed issue on master.
