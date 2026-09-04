# Experiment 016 — Gallery regression assembly ownership

## Hypothesis

The restored Gallery regressions are behaviorally valid, but their historical `Assets/Tests/EditMode` location no longer belongs to the current module-owned `VoxelEngine.Tests.EditMode` assembly. Unity therefore compiles them without the WorldBuilder/Cave/Structures assembly references they require.

## Action / source SHA

Exact feature SHA: `aa23d9e42439ed2ca18119051ecedff2a7a6ee1e`.
Targeted run: `33821322632` via request commit `555b93edc3b4eb19ff9a7b0c4ad7d8111945c25a`.

Inspected the uploaded module-validation plan and player build log. The plan correctly selected `Assets/Game/WorldBuilder` and `VoxelEngine.Tests.EditMode`. Unity compilation failed on the three restored files in `Assets/Tests/EditMode` with missing `Game.Composition.CaveWorldBuilder`, `Game.Materials`, `Game.Structures`, `Game.WorldBuilder`, `VoxelEngine.Storage`, `VoxelEngine.Structures`, `VoxelEngine.Terrain`, `IStructureAuthoringSession`, `CaveAuthoringResult`, and `VoxelSurfaceFlags` symbols. The standalone SceneIssue replay then failed from the same compilation break.

The current module test asmdef at `Assets/Game/WorldBuilder/Tests/EditMode/VoxelEngine.Tests.EditMode.asmdef` already references every required production assembly, including `Game.Composition.Showcase` and `Game.Composition.CaveWorldBuilder`.

## Result / verdict

Confirmed. This is a test-assembly ownership regression caused by restoring historical file locations after the repository migrated to module-local test assemblies. It is not evidence of a production secret/runtime defect.

## Next step

Move only `WorldbuildingGallerySecretDiscoveryCompatibilityTests`, `WorldbuildingGallerySecretDiscoveryPhysicalDiscriminatorTests`, and `WorldbuildingGallerySecretDiscoverySurfaceRouteTests` (and their `.meta` files) into `Assets/Game/WorldBuilder/Tests/EditMode`, preserving their contents and GUIDs. Re-run exact-SHA targeted CI; do not change production behavior unless that run reveals a separate product failure.
