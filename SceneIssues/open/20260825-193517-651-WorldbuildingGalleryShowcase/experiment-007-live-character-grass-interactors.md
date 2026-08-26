# Experiment 007 — live character grass interactor publisher

## Date
2026-08-26 UTC

## Input
- Feature branch: `fixes/agent-8`
- Master-merge input: `5f062ddc48a6903d63a89017274c161e9c2c8812`
- Assigned capture only: `20260825-193517-651-WorldbuildingGalleryShowcase`

## Question
The shader and material bridge already implement bounded character-proximity displacement, but does a real production character source actually publish positions into that bridge without making VoxelEngine depend on game code?

## Source inspection
- `CharacterFactoryCharacterPrefabImporter` creates the generated standard character prefab and adds `CharacterEquipmentController` beneath the character root on the `Equipment` child. That child inherits the moving character world transform.
- `Game.Composition.CharacterEquipment.Runtime` is a Unity/game-side assembly and had no assembly references before this experiment.
- `VoxelEngine.Rendering.Runtime` references engine APIs only and has no dependency on `Game.Composition.CharacterEquipment.Runtime`.

## Change under test
- Add renderer-side `GrassInteractorRegistry`, which stores registered `Transform` + influence radius bindings, removes destroyed/disabled bindings, samples them at most once per rendered frame, caps publication at `ProceduralVegetationMaterials.MaxGrassInteractors` (64), and forwards `Vector4(position.xyz, radius)` values to `SetGrassInteractors`.
- Register the standard generated-character `CharacterEquipmentController` transform on enable, publish from `LateUpdate`, and unregister on disable.
- Add the one-way `Game.Composition.CharacterEquipment.Runtime` -> `VoxelEngine.Rendering.Runtime` assembly reference. No engine assembly gains a game dependency.
- Extend `ProceduralVegetationGrassStyleTests.FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract` so the regression now requires the live publisher/registry wiring in addition to the shader contract.

## Expected validation
Run the same focused EditMode regression through `ci-test/fixes/agent-8` after committing these durable source/test changes. The existing experiment-006 real-player replay remains the post-fix visual evidence for the authored grass appearance; this experiment closes the previously unused character-displacement input rather than creating a new capture or temporary replay path.
