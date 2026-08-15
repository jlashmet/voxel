# Architecture Dependency Closure Report

**Status:** Complete  
**Implementation branch:** `refactor/system-boundaries-foundation-storage`  
**Accepted source:** `0d7ec234b89da707db3ccda8649d4ed900ac3f61`  
**Acceptance run:** `31912507821`

## Final dependency rules

- Api assemblies do not reference Runtime assemblies.
- Runtime assemblies do not reference another subsystem's Runtime assembly.
- `VoxelEngine.Composition` is the only production assembly allowed to wire concrete Runtime implementations.
- `VoxelEngine.Showcase` and scene/application source consume subsystem APIs or Composition entry points only.
- Storage physical representation remains inside `Storage.Runtime`; Composition may borrow it only as the application wiring/ownership root.
- Streaming has no Net dependency; Net consumes `Streaming.Api`.
- Rendering consumes `Storage.Api`, `Tiering.Api`, and `Vegetation.Api`, not foreign Runtime assemblies.
- `MountingForce.WorldGen.Core` and `MountingForce.WorldGen.Architecture` remain engine-independent; `MountingForce.WorldGen.Voxel` consumes engine Api assemblies only.
- `VoxelEngine.Core` assembly, folder, and production namespace references are deleted.
- No compatibility facade was retained to preserve the old architecture.

## Mechanical enforcement

The final EditMode suite passes the architecture closure tests, including:

- `ArchitectureBoundaryGuardTests.ApiAssembliesDoNotReferenceRuntimeAssemblies`
- `ArchitectureBoundaryGuardTests.RuntimeAssembliesDoNotReferenceForeignRuntimeAssemblies`
- `ProductionArchitectureClosureTests.CompositionIsTheOnlyProductionAssemblyThatReferencesRuntimeImplementations`
- `ProductionArchitectureClosureTests.SceneSourceDoesNotReferenceRuntimeImplementationNamespaces`
- `ProductionArchitectureClosureTests.ShowcaseConcreteWorldOwnershipLivesInComposition`
- `ProductionArchitectureClosureTests.ShowcaseWorldDoesNotOwnStructuresRuntimeDetails`
- `ProductionArchitectureClosureTests.ShowcaseWorldDoesNotAllocateOrDisposePhysicalStorage`
- `ProductionArchitectureClosureTests.DeletedCoreNamespaceDoesNotReappearInProductionSourceOrAsmdefs`

## Acceptance result

Run `31912507821` at `0d7ec234b89da707db3ccda8649d4ed900ac3f61` executed 403 tests: 390 passed and exactly the same 13 pre-existing known baseline tests failed. There were zero C# compiler errors. Under the implementation plan's acceptance policy, this is an accepted architecture gate because the failed-test-name set is unchanged and no new regression was introduced.

The branch also removes the orphaned `Assets/VoxelEngine/Net/Runtime/Server/Storage.meta` left after the Net storage cleanup; the accepted run confirms that cleanup does not alter the baseline.
