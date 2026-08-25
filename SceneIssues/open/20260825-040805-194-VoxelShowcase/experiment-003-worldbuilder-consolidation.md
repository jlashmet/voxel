# Experiment 003 — WorldBuilder consolidation

## Hypothesis

If Kentridge is authored exactly once through an opaque `Game.WorldBuilder.Api` plan, and both VoxelShowcase and campaign composition consume that plan through WorldBuilder-owned adapters, then the public game path no longer has parallel Kentridge authoring systems. Relocating the embedded `Packages/com.mountingforce.worldgen` implementation beneath Game/WorldBuilder ownership should also remove the misleading package-level ownership split without changing Unity asset GUIDs or generation semantics.

## What performed + source commit

- Added the focused boundary regression in source commit `04f035153598073a105a947f4d4e5bfc8136151f`.
- Proved it red via `ci-test/fixes/agent-1` request `b50735891bebe7862e51c18ab2fa69158cb66fc8`, GitHub Actions run `32874662064`; Unity compiled and the requested test failed because `Game.WorldBuilder.Runtime.WorldBuilderTownAuthoring` did not yet exist.
- Began the production cut through source commit `d051c6d7e63f42c8c54571cd462e78d176adb133`: introduced the opaque `AuthoredTownPlan`, the `WorldBuilderTownAuthoring` facade, friend-assembly access, and the WorldBuilder voxel adapter assembly boundary.
- Migrated the VoxelShowcase and Kentridge campaign public boundaries through source commit `44939954fced69f7a7d04895f80da0727a42cda6` and strengthened the regression to require WorldBuilder-owned physical placement.
- Relocated the unchanged legacy runtime tree from `Packages/com.mountingforce.worldgen/Runtime` to `Assets/Game/WorldBuilder/Generation` in source commit `19b9a3d5952a3f6e2365516c985ba984247e7cf9`, preserving the runtime tree blobs and the former `Runtime.meta` GUID as `Generation.meta`.
- Requested `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` through CI commit `ce1c7fd7b98f36602780686a28a5f611b8b6e9ca`, Actions run `32878171548`.

## Result

Failed at Unity compilation before the requested regression executed. The relocated assemblies themselves were discovered, but `Assets/Game/Composition/WorldBuilderWorldGen/Runtime/KentridgeCampaignWorldRealization.cs` could no longer resolve five WorldBuilder runtime services: `BlueprintCompiler` (line 99), `SiteRoleResolver` (113), `NpcPlacementResolver` (118), `SecretPlanner` (128), and `CutsceneStageRealizer` (253). `ci/single-test` correctly reported failure; this is not a green validation.

## What learned

`VoxelWorldGenSettings` already accepts an explicit `SettlementPlan`, and `SettlementVoxelPlan.Resolve` uses that plan before its Kentridge fallback. The WorldBuilder voxel adapter can therefore voxelize the exact plan authored once by WorldBuilder instead of rebuilding Kentridge behind the facade. This is a real single-authoring path rather than a namespace-only wrapper.

The physical relocation also exposed a real assembly-boundary dependency that the old package placement had masked: `Game.Composition.WorldBuilderWorldGen` consumes WorldBuilder runtime realization services as well as the legacy backend assemblies. The next change must make that dependency explicit rather than restoring the package path or leaking backend ownership back into Game composition.

## Next

Inspect `Game.Composition.WorldBuilderWorldGen.asmdef` and the five unresolved service definitions, add the smallest explicit assembly reference needed for those services (or move the integration code only if the dependency direction proves invalid), then repin `ci-test/fixes/agent-1` to the new source commit and rerun this exact regression. After green CI, replay the original VoxelShowcase capture and inspect its verification artifact before terminal bookkeeping.
