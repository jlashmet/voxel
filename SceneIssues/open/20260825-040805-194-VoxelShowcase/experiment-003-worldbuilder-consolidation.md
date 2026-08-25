# Experiment 003 — WorldBuilder consolidation

## Hypothesis

If Kentridge is authored exactly once through an opaque `Game.WorldBuilder.Api` plan, and both VoxelShowcase and campaign composition consume that plan through WorldBuilder-owned adapters, then the public game path no longer has parallel Kentridge authoring systems. Relocating the embedded `Packages/com.mountingforce.worldgen` implementation beneath Game/WorldBuilder ownership should also remove the misleading package-level ownership split without changing Unity asset GUIDs or generation semantics.

## What performed + source commit

- Added the focused boundary regression in source commit `04f035153598073a105a947f4d4e5bfc8136151f`.
- Proved it red via `ci-test/fixes/agent-1` request `b50735891bebe7862e51c18ab2fa69158cb66fc8`, GitHub Actions run `32874662064`; Unity compiled and the requested test failed because `Game.WorldBuilder.Runtime.WorldBuilderTownAuthoring` did not yet exist.
- Began the production cut through source commit `d051c6d7e63f42c8c54571cd462e78d176adb133`: introduced the opaque `AuthoredTownPlan`, the `WorldBuilderTownAuthoring` facade, friend-assembly access, and the WorldBuilder voxel adapter assembly boundary.

## Result

In progress. The red regression is established and the facade boundary is partially implemented; production callers and physical backend ownership still need migration before validation.

## What learned

`VoxelWorldGenSettings` already accepts an explicit `SettlementPlan`, and `SettlementVoxelPlan.Resolve` uses that plan before its Kentridge fallback. The WorldBuilder voxel adapter can therefore voxelize the exact plan authored once by WorldBuilder instead of rebuilding Kentridge behind the facade. This is a real single-authoring path rather than a namespace-only wrapper.

## Next

Finish the WorldBuilder voxel adapter, migrate VoxelShowcase and campaign callers off direct `MountingForce.*` APIs, relocate the embedded worldgen runtime beneath `Assets/Game/WorldBuilder` while preserving blobs and `.meta` GUIDs, strengthen the same boundary regression for the physical path, run the assigned targeted CI request to green, then replay the original VoxelShowcase capture and inspect its verification artifact before terminal bookkeeping.
