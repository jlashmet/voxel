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
- Restored the accidentally removed `using Game.WorldBuilder.Runtime;` in source commit `b0566e2e4a88e109b0bc70a7ce2ed63c8f7ee519`; this removed all five first-run compiler errors.
- Retried the same regression through CI commit `e2a796ad3ff44c02d8bc4d6a4fd932f37be1a147`, Actions run `32878499646`.

## Result

Still failing at Unity compilation before the requested regression executes, but the failure advanced. The five `Game.WorldBuilder.Runtime` service errors are gone. Compilation now stops in `Assets/Game/Composition/Kentridge/Runtime/KentridgeCampaignSessionBootstrap.cs`: `ISettlementSiteRealizationFacts` is unresolved at line 60 and `IHiddenSpaceRealizationFacts` is unresolved at line 63. `ci/single-test` correctly reports failure; this is not a green validation.

## What learned

`VoxelWorldGenSettings` already accepts an explicit `SettlementPlan`, and `SettlementVoxelPlan.Resolve` uses that plan before its Kentridge fallback. The WorldBuilder voxel adapter can therefore voxelize the exact plan authored once by WorldBuilder instead of rebuilding Kentridge behind the facade. This is a real single-authoring path rather than a namespace-only wrapper.

The first compile failure was not an assembly-reference defect: `Game.Composition.WorldBuilderWorldGen.Runtime.asmdef` already references `Game.WorldBuilder.Runtime`; the campaign-plan edit had simply removed the required namespace import. The second retry proves that correction. The remaining two unresolved realization-fact interfaces sit at the Kentridge session boundary, so their declarations and method visibility must be inspected before restoring any backend import: if they are public parameters, merely re-importing `MountingForce.WorldGen` would compile but would preserve the architectural leak this capture is meant to remove.

## Next

Inspect `KentridgeCampaignSessionBootstrap.cs`, its asmdef, the two realization-fact interface definitions, and the commit that changed the bootstrap. Keep the public Kentridge composition boundary backend-blind; either restore a purely internal import if no public leak exists, or introduce the smallest WorldBuilder/integration-owned realization boundary if those legacy interfaces are public. Then repin `ci-test/fixes/agent-1` to the new source commit and rerun this exact regression. After green CI, replay the original VoxelShowcase capture and inspect its verification artifact before terminal bookkeeping.
