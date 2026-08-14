# Voxel Engine Architecture Refactor Plan

**Status:** Design / not started  
**Scope:** Architecture and module boundaries only  
**Implementation stance:** Prefer clean subsystem cutovers over compatibility layers  

## 1. Purpose

The voxel engine already contains recognizable subsystems, but the current assembly structure does not enforce ownership strongly enough. In particular, `VoxelEngine.Core` has become a broad shared implementation bucket that contains storage, terrain, edits, structural integrity, feature generation, occupancy, and common utility code. Other assemblies can directly reference implementation types from that shared bucket.

The goal of this refactor is to make each major capability self-contained, expose one deliberate API surface, and mechanically prevent other systems from depending on its implementation.

The target rule is simple:

```text
A system owns its implementation.
A system exposes one explicit API assembly.
Other systems may depend on that API.
Other systems may not depend on its runtime implementation.
```

Because there is not yet a production game that must remain continuously compatible, this migration should optimize for the clean end-state rather than preserving legacy APIs. Large, cohesive subsystem refactors are preferred over temporary adapters, aliases, forwarding shims, or long-lived dual paths.

## 2. Migration philosophy

This plan intentionally favors decisive cutovers.

### Prefer

- moving a complete subsystem in one focused refactor,
- updating all of its consumers in the same change,
- deleting obsolete namespaces and access paths immediately,
- changing public surfaces when the current surface is architecturally wrong,
- accepting larger PRs when they produce a cleaner boundary,
- allowing intermediate commits on a branch to be temporarily incomplete if the final branch state is coherent and reviewable.

### Avoid

- legacy compatibility adapters,
- namespace-forwarding layers,
- duplicate old/new APIs,
- temporary runtime-to-runtime exceptions,
- exposing implementation types merely to reduce migration work,
- preserving an API only because existing prototype code happens to use it.

The default assumption should be:

> If an old dependency is wrong, remove it and update every caller rather than wrapping it.

Compatibility code should only be introduced when there is a concrete external consumer or data-format constraint that genuinely requires it.

## 3. Target directory and assembly model

Each significant subsystem should eventually follow this shape:

```text
Assets/VoxelEngine/
  Storage/
    Api/
      VoxelEngine.Storage.Api.asmdef
    Runtime/
      VoxelEngine.Storage.Runtime.asmdef
    Tests/

  Terrain/
    Api/
      VoxelEngine.Terrain.Api.asmdef
    Runtime/
      VoxelEngine.Terrain.Runtime.asmdef
    Tests/
```

The dependency rule is:

```text
System.Api
    ^
    |
System.Runtime

OtherSystem.Runtime ---> System.Api
OtherSystem.Runtime -X-> System.Runtime
OtherSystem.Api     -X-> System.Runtime
```

The API assembly is the only supported cross-system dependency surface.

## 4. What "API" means

`Api` does not mean interfaces everywhere.

For a high-performance voxel engine, valid API types include:

- blittable structs,
- IDs,
- coordinates,
- handles,
- immutable descriptors,
- commands,
- events,
- query objects,
- query results,
- `NativeArray`-compatible views,
- enums,
- read-only data views,
- interfaces only where runtime polymorphism is genuinely useful.

Avoid needless object-oriented wrappers such as `IVoxel`, `IBrick`, `IRegion`, etc. when a struct or handle is a better representation.

The boundary matters more than the abstraction style.

## 5. Core architectural rules

### 5.1 Every major capability has one owner

Examples:

```text
Foundation
Storage
Terrain
Edits
Structures
StructuralIntegrity
Tiering
Streaming
Collision
Net
Rendering
Vegetation
```

A type should have a clear owning system.

### 5.2 Every externally consumed system exposes one API assembly

Naming convention:

```text
VoxelEngine.<System>.Api
```

Examples:

```text
VoxelEngine.Storage.Api
VoxelEngine.Terrain.Api
VoxelEngine.Collision.Api
```

### 5.3 Runtime implementation remains private

Implementation code lives under:

```text
<System>/Runtime
```

Use `internal` for implementation types wherever Unity requirements do not force them to be public.

### 5.4 Runtime-to-runtime dependencies are forbidden

Invalid:

```text
VoxelEngine.Rendering.Runtime
    -> VoxelEngine.Storage.Runtime
```

Valid:

```text
VoxelEngine.Rendering.Runtime
    -> VoxelEngine.Storage.Api
```

### 5.5 API-to-runtime dependencies are always forbidden

Invalid:

```text
VoxelEngine.Streaming.Api
    -> VoxelEngine.Storage.Runtime
```

### 5.6 The API dependency graph must remain acyclic

Circular API references are architectural defects, not something to solve with assembly exceptions.

### 5.7 Public does not automatically mean API

Unity may require public `MonoBehaviour`, `ScriptableObject`, renderer feature, serialized, or editor-visible types. Those types can remain public inside a Runtime assembly without becoming supported cross-system contracts.

The assembly boundary is authoritative.

## 6. Proposed final top-level organization

```text
Assets/VoxelEngine/
  Foundation/

  Storage/
    Api/
    Runtime/

  Terrain/
    Api/
    Runtime/

  Edits/
    Api/
    Runtime/

  Structures/
    Api/
    Runtime/

  StructuralIntegrity/
    Api/
    Runtime/

  Tiering/
    Api/
    Runtime/

  Streaming/
    Api/
    Runtime/

  Collision/
    Api/
    Runtime/

  Net/
    Api/
    Runtime/

  Rendering/
    Api/
    Runtime/

  Vegetation/
    Api/
    Runtime/

  Composition/
```

`VoxelEngine.Core` should eventually disappear completely.

## 7. Foundation

Foundation replaces only the small subset of `Core` that is genuinely universal.

Possible contents:

```text
Coordinates/
Math/
Identity/
```

Candidates include:

- `IntMath`,
- voxel/region coordinate value types,
- stable low-level IDs,
- small universal constants.

Foundation must not contain:

- storage services,
- region tables,
- terrain generators,
- material databases,
- edit processors,
- streaming managers,
- shared business logic.

The test for Foundation is:

> Could this type exist without knowing anything about Storage, Terrain, Rendering, Streaming, or another voxel subsystem?

If not, it belongs to a real system.

Do not allow Foundation to become `Core 2.0`.

## 8. Storage

Storage is the most important boundary in the refactor because many other systems consume voxel-world data.

Current concepts appear to include responsibilities such as:

```text
BrickPool
BrickRef
Region
RegionTable
OccupancyMask
MipBuilder
MaterialPalette
MaterialAdjacencyCatalogue
SemanticRegionHasher
```

### 8.1 Target shape

```text
Storage/
  Api/
  Runtime/
```

### 8.2 Do not republish the current implementation

A bad migration would simply move these types into `Storage.Api`:

```text
BrickPool
RegionTable
Region
```

That changes folder names without improving architecture.

Instead, design the API from consumer operations.

Potential API concepts might include:

```text
RegionId
RegionCoord
BrickHandle            // only if "brick" is intentionally public vocabulary
RegionReadView
RegionWriteView
RegionGenerationWriter
MaterialId
OccupancyMask          // if external consumers genuinely need the representation
```

Likely Runtime-only concepts include:

```text
BrickPool
RegionTable
Region allocation
MipBuilder
semantic hashing implementation
mutable palette/catalogue machinery
compaction/allocation details
```

### 8.3 Storage consumer inventory

Before changing Storage code, inventory every external consumer.

For each consumer record:

| Consumer | Current type touched | Capability actually needed | Read/write | Hot path? |
|---|---|---|---|---|
| Streaming | Region/RegionTable/etc. | residency/create/find region | both | yes |
| Rendering | brick/region data | bulk voxel/material/occupancy read | read | yes |
| Collision | voxel/occupancy data | fast spatial queries | read | yes |
| Terrain | mutable region data | bulk generation writes | write | yes |
| Edits | mutable voxels | deterministic runtime mutation | write | yes |
| Net | TBD | probably protocol/state transfer only | TBD | TBD |

The exact API should be derived from this matrix.

### 8.4 Read and write capability separation

Rendering and Collision generally need reads. Terrain and Edits need writes.

Avoid one giant unrestricted world object.

Prefer conceptual capabilities such as:

```text
WorldReadView
RegionReadView
RegionGenerationWriter
RegionMutationAccess
```

These can be structs/handles and need not introduce virtual calls.

### 8.5 Storage owns maintenance work

Consumers should not be responsible for:

```text
brick allocation
deallocation
mip rebuilding
occupancy maintenance
semantic hashing
compaction
pooling
```

For example, Edits should request or submit voxel changes. It should not manually allocate a brick and rebuild Storage's occupancy structures.

### 8.6 Memory and lifetime rules

Any view/handle API must define:

- who owns the native memory,
- who may dispose it,
- how long the view remains valid,
- whether it survives region mutation,
- whether Streaming can invalidate it,
- whether it can be used from Burst/jobs,
- what synchronization is required.

The allocating system owns disposal. Borrowers never dispose Storage-owned memory.

### 8.7 Required Storage design decisions

Before implementation begins, answer:

1. Is `Brick` public domain vocabulary or internal representation?
2. What is the external unit of world access?
3. How does a consumer efficiently read many voxels?
4. How does initial/bulk generation write data?
5. How do runtime edits write data?
6. How is view lifetime represented?
7. Can Streaming invalidate outstanding views?
8. Who owns material metadata?
9. What version/change signal does Rendering consume?
10. What exact hot-path data does Collision require?
11. Who updates occupancy mips?
12. Who owns serialization/persistence of region storage?

Storage should not be migrated until these are answered.

## 9. Terrain

Current terrain concepts include approximately:

```text
TerrainGenerator
TerrainSampler
```

Target:

```text
Terrain/
  Api/
  Runtime/
```

Potential API concepts:

```text
TerrainSample
TerrainQuery
TerrainSeed
TerrainSettings
```

An interface such as `ITerrainSampler` is acceptable only if runtime polymorphism is useful. A struct-oriented API may be more appropriate.

Likely dependency direction:

```text
Terrain.Runtime
    -> Terrain.Api
    -> Storage.Api
    -> Foundation
```

Terrain generates content. Storage owns persistence and storage representation.

Longer-term conceptual boundary:

```text
TerrainGenerator
      -> generated region data / generation writer
      -> Storage
```

## 10. Edits

Voxel mutation deserves its own system.

Current concepts include things such as:

```text
AlterationEvent
BrushExpansion
BrushShapeCodec
BuildBrushes
DeterministicAlterationApplier
DensityCap
AllocationBudget
```

Target:

```text
Edits/
  Api/
  Runtime/
```

Potential API concepts:

```text
AlterationEvent
EditRequest
EditResult
BrushSpec
BrushShape
EditId
AlterationBatch
```

Likely Runtime-only concepts:

```text
BrushExpansion
DeterministicAlterationApplier
BuildBrushes
DensityCap
allocation/budget implementation
```

Serialization codecs should not be promoted into Edits API merely because networking needs them. `Net.Runtime` should serialize domain/API contracts.

Conceptually:

```text
EditRequest
    -> Edits.Runtime
    -> AlterationBatch
    -> Storage.Api
```

## 11. Structures

`Structures` owns procedural built architecture.

Examples:

```text
CastleBuilder
VoxelBrush
MasonryWeathering
StructureMaterials
architectural features
arches
veneers
anchors
feature emitters
```

Target:

```text
Structures/
  Api/
  Runtime/
```

Potential API concepts:

```text
StructureRequest
StructureResult
StructureId
AnchorSpec
StructureMaterialDescriptor
```

Likely Runtime-only:

```text
CastleBuilder
feature emitters
arch generation
weathering
voxel construction algorithms
```

`CastleBuilder` should not become API merely because callers currently instantiate it. The API should describe structure generation capability, allowing future implementations such as village, ruin, fort, or dungeon builders.

## 12. StructuralIntegrity

The existing singular `Core/Structure` responsibility should be separated from procedural Structures.

Concepts such as:

```text
CollapseDetection
Connectivity
SupportField
```

belong to:

```text
StructuralIntegrity/
  Api/
  Runtime/
```

Potential API concepts:

```text
SupportQuery
SupportResult
CollapseQuery
CollapseResult
```

The distinction is intentional:

```text
Structures           = how built structures are generated
StructuralIntegrity  = whether matter remains supported
```

This will become more important as destruction grows.

A desirable mutation flow is:

```text
world edit
    -> StructuralIntegrity evaluates support
    -> CollapseResult / alterations
    -> Edits
    -> Storage
```

StructuralIntegrity should not own Storage allocation internals.

## 13. Core/Features

`Core/Features` appears to contain architectural/procedural concepts such as:

```text
AnchorSpec
ArchFeature
BondedBlockVeneer
FeatureCatalogue
FeatureBudget
Emitters
```

Initial ownership assumption:

```text
Core/Features/*
    -> mostly Structures/Runtime/Features/*
```

Only genuinely cross-system concepts belong in `Structures.Api`.

Do not create a generic `Features` system unless caller analysis demonstrates that it is independently reusable outside structures.

## 14. Occupancy

Do not create a separate system merely because there is an occupancy folder.

Likely classification:

```text
OccupancyMask  -> Storage.Api if consumers need the representation
MipBuilder     -> Storage.Runtime
```

Consumers may see the resulting representation; they should not know how Storage constructs or maintains it.

## 15. Materials

Material ownership is likely to become an architectural pressure point because many future systems care about material properties:

```text
Rendering
Terrain
Structures
Vegetation
Collision
Fire
Water
Gameplay
```

Do not automatically assign all material concerns to Storage.

Distinguish:

### Material identity

```text
MaterialId
```

### Stored material value

The material associated with a voxel. Likely exposed through Storage API.

### Physical/simulation properties

Examples:

```text
solid
flammable
density
friction
```

These may eventually justify a dedicated `Materials` system.

### Rendering properties

Textures, shader data, surface appearance, GPU resources. These belong to Rendering or a rendering-specific material representation.

Before assigning `MaterialPalette` and `MaterialAdjacencyCatalogue`, audit who mutates and consumes them.

A future Materials system is plausible:

```text
Materials/
  Api/
    MaterialId
    PhysicalMaterialProperties
    MaterialTags
  Runtime/
    MaterialCatalogue
```

Do not create it until actual dependency pressure justifies it.

## 16. Tiering

Tiering determines detail/resolution policy.

Target:

```text
Tiering/
  Api/
  Runtime/
```

Potential API concepts:

```text
Tier
TierSettings
TierSelection
TierQuery
```

Likely consumers include Streaming and Rendering.

They should depend on `Tiering.Api`, never `Tiering.Runtime`.

## 17. Streaming

Streaming owns residency and loading coordination.

Target:

```text
Streaming/
  Api/
  Runtime/
```

Potential API concepts:

```text
RegionLoadRequest
RegionUnloadRequest
StreamingState
ResidencyState
StreamingPriority
```

Likely dependencies:

```text
Streaming.Runtime
    -> Streaming.Api
    -> Storage.Api
    -> Terrain.Api
    -> Tiering.Api
```

Streaming decides:

```text
what needs to be loaded
what can be unloaded
priority
residency
```

It should not become the owner of:

```text
voxel storage
terrain algorithms
network session state
render resources
```

A useful architectural test is that Streaming should be fully usable in an offline test with no Net assembly present.

## 18. Networking direction

Networking should be an adapter around domain capabilities.

Preferred direction:

```text
network packet
    -> Net.Runtime
    -> domain command/request
    -> Streaming.Api / Edits.Api / other API
```

Avoid:

```text
Streaming.Runtime
    -> Net.Runtime
```

Streaming should not care whether a load request originated from multiplayer, single player, editor tooling, replay, server simulation, or a benchmark.

Net.Runtime may depend on:

```text
Streaming.Api
Edits.Api
Structures.Api
other domain APIs
```

Domain systems generally should not depend on Net at all.

Networking owns replication, not state ownership.

Never put internal storage identities such as pool indices, table slots, pointers, or implementation handles into protocol contracts unless they are deliberately stable domain identities.

## 19. Collision

Current concepts include:

```text
DdaTraversal
VoxelRaycast
SweptAabb
HullExport
```

Target:

```text
Collision/
  Api/
  Runtime/
```

Potential API concepts:

```text
RaycastQuery
RaycastHit
SweepQuery
SweepResult
CollisionLayer
```

Runtime-only:

```text
DdaTraversal
VoxelRaycast implementation
SweptAabb implementation
HullExport implementation
```

Consumers should request collision results rather than invoke traversal internals directly.

Collision is performance-sensitive, so its Storage-facing API should avoid per-voxel virtual calls. Prefer a Burst-friendly region/world read view or similarly efficient batch representation.

## 20. Vegetation

Vegetation should remain an explicit subsystem as it expands into:

```text
trees
flowers
vines
moss
grass
fungi
roots
```

Target:

```text
Vegetation/
  Api/
  Runtime/
```

Potential API concepts:

```text
VegetationInstance
VegetationTypeId
VegetationPlacement
VegetationDescriptor
VegetationRenderData
```

Runtime may contain:

```text
procedural tree generation
vine generation
moss placement
growth rules
branch generation
placement algorithms
```

Before expanding vegetation significantly, explicitly answer:

1. What data constitutes persistent vegetation?
2. Is a tree represented as voxels, procedural geometry, or both?
3. What is destructible?
4. Who owns terrain attachment?
5. What does Rendering consume?
6. What does Collision consume?
7. What does networking replicate?
8. How are hanging vines and support represented?

Rendering should consume `Vegetation.Api`, never procedural-generation implementation classes.

## 21. Rendering

Rendering should be migrated late because it consumes many lower-level capabilities and will expose weak boundaries.

Target:

```text
Rendering/
  Api/
  Runtime/
```

Runtime contains concepts such as:

```text
SurfaceExtraction
Irradiance
RenderFeature
Shaders
GPU resources
compute pipelines
vegetation rendering integration
```

Likely dependencies:

```text
Rendering.Runtime
    -> Rendering.Api
    -> Storage.Api
    -> Tiering.Api
    -> Vegetation.Api
```

Potentially others as justified by actual data needs.

Rendering should not know about:

```text
BrickPool
RegionTable
TerrainGenerator
MipBuilder
Tiering implementation
Vegetation generation implementation
```

Rendering should mostly be a dependency sink: simulation systems provide data toward rendering; simulation should not depend back on rendering.

## 22. Composition root

Some assembly must instantiate concrete implementations and wire systems together.

Create an explicit privileged assembly:

```text
Composition/
  VoxelEngine.Composition.asmdef
  VoxelEngineBootstrap.cs
```

This assembly may reference Runtime assemblies.

It should contain wiring/bootstrap logic, not domain behavior.

Controlled Runtime-reference exceptions may include:

```text
Composition
System.Tests
System.Editor when genuinely required
```

Production domain/runtime systems are not exceptions.

## 23. Target runtime dependency graph

Approximate final graph:

```text
Foundation

Storage.Runtime
  -> Storage.Api
  -> Foundation

Terrain.Runtime
  -> Terrain.Api
  -> Storage.Api
  -> Foundation

Edits.Runtime
  -> Edits.Api
  -> Storage.Api

Structures.Runtime
  -> Structures.Api
  -> Storage.Api
  -> Terrain.Api
  -> Edits.Api as needed

StructuralIntegrity.Runtime
  -> StructuralIntegrity.Api
  -> Storage.Api
  -> Edits.Api if needed

Tiering.Runtime
  -> Tiering.Api

Streaming.Runtime
  -> Streaming.Api
  -> Storage.Api
  -> Terrain.Api
  -> Tiering.Api

Collision.Runtime
  -> Collision.Api
  -> Storage.Api

Vegetation.Runtime
  -> Vegetation.Api
  -> Storage.Api
  -> Terrain.Api as needed

Net.Runtime
  -> Net.Api
  -> Streaming.Api
  -> Edits.Api
  -> other domain APIs

Rendering.Runtime
  -> Rendering.Api
  -> Storage.Api
  -> Tiering.Api
  -> Vegetation.Api

Composition
  -> required Runtime assemblies
```

Rule:

```text
Runtime -> other systems' APIs only
```

## 24. Cross-system mutation

Longer term, systems should not arbitrarily mutate another system's internal state.

For example, StructuralIntegrity may determine that matter should collapse, but it should not manage Storage allocation directly.

Prefer:

```text
StructuralIntegrity
    -> collapse result / alteration intent
    -> Edits
    -> Storage
```

Likewise, Structures may produce a placement plan or generation output rather than directly manipulating Storage internals.

There may legitimately be two mutation paths:

### Runtime gameplay mutation

```text
Edits
```

### Initial/bulk generation

```text
RegionGenerationWriter or equivalent Storage API
```

Do not force bulk world generation through a gameplay edit path if it damages performance.

## 25. Commands, queries, and events

A formal CQRS framework is unnecessary, but API interactions should be easy to classify.

### Queries

Read without changing subsystem state.

Examples:

```text
TerrainQuery
RaycastQuery
RegionReadView
TierQuery
```

### Commands

Request state changes.

Examples:

```text
EditRequest
RegionLoadRequest
StructureRequest
```

### Events

Only expose cross-system events when another system genuinely cares about the occurrence.

Good candidates may include:

```text
RegionLoaded
RegionUnloaded
VoxelEditApplied
StructureCreated
VegetationRemoved
```

Avoid leaking implementation events such as brick allocation or mip-node updates.

## 26. Namespace rules

Target namespaces:

```text
VoxelEngine.Storage.Api
VoxelEngine.Storage.Runtime
VoxelEngine.Terrain.Api
VoxelEngine.Terrain.Runtime
```

Sub-namespaces are fine:

```text
VoxelEngine.Storage.Api.Materials
VoxelEngine.Rendering.Runtime.SurfaceExtraction
VoxelEngine.Structures.Runtime.Features
```

A Runtime namespace should never appear in another production system.

## 27. Assembly naming

Use predictable names:

```text
VoxelEngine.Foundation
VoxelEngine.Storage.Api
VoxelEngine.Storage.Runtime
VoxelEngine.Terrain.Api
VoxelEngine.Terrain.Runtime
```

Tests:

```text
VoxelEngine.Storage.Tests
VoxelEngine.Terrain.Tests
```

Avoid names such as:

```text
Core2
Common
Shared
Utils
Helpers
Misc
General
```

These tend to recreate the original problem.

## 28. Folder ownership inside Runtime

The API/Runtime split is the system boundary. Runtime may organize itself internally however is useful.

Example:

```text
Storage/
  Runtime/
    Bricks/
    Regions/
    Materials/
    Mips/
    Allocation/
```

Do not recursively create API assemblies for every small internal folder.

## 29. System sizing rule

A system should correspond to a coherent responsibility that can be stated in one sentence.

Examples:

```text
Storage stores voxel-world data.
Terrain generates terrain.
Streaming manages residency.
Collision answers spatial/physical queries.
Vegetation owns plant generation/state.
Rendering turns world state into visual output.
```

Do not create a separate system merely because two or three related classes currently share a directory.

## 30. Data ownership

Each significant mutable data structure should have one owner.

Examples:

```text
voxel storage state       -> Storage
terrain generation state  -> Terrain
streaming residency state -> Streaming
network session state     -> Net
render resources          -> Rendering
vegetation state          -> Vegetation
```

Other systems observe or request changes through API contracts.

## 31. Native memory ownership

The subsystem that allocates native memory owns disposal.

Borrowing systems receive views/handles and never free the underlying memory.

Cross-system API comments should document:

- ownership,
- mutability,
- lifetime,
- thread safety,
- Burst/job compatibility,
- allocation behavior.

## 32. Performance requirements

Architecture boundaries must not degrade engine hot paths.

Avoid APIs that introduce:

```text
per-voxel virtual calls
boxing
managed allocations
LINQ
whole-region copies
transient managed object graphs
```

Prefer:

```text
NativeArray
NativeSlice
blittable structs
handles
batch operations
read-only native views
Burst-compatible commands/results
```

Encapsulation should hide ownership and implementation, not force slow representations.

## 33. Versioned read views

A useful Storage API direction is a versioned read view, for example conceptually:

```text
RegionReadView
  RegionId
  Version
  voxel/material data view
  occupancy data
```

This may allow Rendering and Collision to cache derived work until the version changes.

The view does not need to copy memory; it may be a versioned read-only handle into Storage-owned native memory.

This is a design direction, not a required initial implementation.

## 34. Determinism

Systems involved in simulation should explicitly document determinism requirements.

Likely candidates:

```text
Terrain
Edits
Structures
StructuralIntegrity
Vegetation generation
future Fire
future Water
```

If identical seed/input/state is expected to yield identical output across server, clients, replays, or platforms, that requirement belongs in the API/design rather than hidden global state.

## 35. Future Fire system compatibility

The architecture should allow Fire to be added without new implementation coupling.

Likely conceptual dependency direction:

```text
Fire.Runtime
  -> Storage.Api          read nearby voxels
  -> Materials.Api        if/when material physics is separated
  -> Edits.Api            request world mutations
```

Rendering may consume `Fire.Api` for visual state.

Fire must not depend on Storage.Runtime or Rendering.Runtime.

## 36. Future Water system compatibility

Likely direction:

```text
Water.Runtime
  -> Storage.Api
  -> Materials.Api if present
  -> Edits.Api where solid-world mutation is needed
```

If water simulation state is distinct from solid voxel storage, Water should own that state rather than forcing Storage to own every spatial system.

Rendering consumes a public water representation, not Water implementation internals.

## 37. Example: combat weapon hits a voxel

Desired conceptual flow:

```text
Combat
  -> Collision.Api.Raycast
  -> RaycastHit
  -> combat/tool effect calculation
  -> Edits.Api.EditRequest
  -> Edits.Runtime
  -> Storage.Api mutation
  -> world version changes
       -> Rendering update
       -> StructuralIntegrity reevaluation
       -> Net replication
```

Combat should never need `BrickPool`, `RegionTable`, DDA internals, or mesh-extraction implementation details.

## 38. Example: vine is cut

Desired conceptual flow:

```text
combat/tool action
  -> Vegetation.Api
  -> Vegetation.Runtime determines affected vine
  -> attachment/support state changes
  -> possible StructuralIntegrity interaction
  -> vegetation removal/drop result
  -> Rendering consumes updated public state
```

This is one reason to establish strong subsystem ownership before vines, moss, and other vegetation features become more complex.

## 39. Refactor sequencing

Because we prefer clean cutovers over compatibility layers, sequencing should be by dependency leverage rather than by minimizing change size.

Recommended order:

```text
1. Document architecture rules
2. Add architecture enforcement
3. Extract Foundation
4. Refactor Storage completely
5. Refactor Terrain and Edits
6. Refactor Structures and StructuralIntegrity
7. Refactor Tiering
8. Correct Net/Streaming dependency direction
9. Refactor Streaming
10. Refactor Collision
11. Refactor Vegetation
12. Refactor Rendering
13. Add explicit Composition root
14. Delete Core
```

Several adjacent items may reasonably be combined into one larger branch/PR if doing so produces a cleaner cutover.

## 40. Phase 0: architecture documentation

Deliverables:

- this migration plan,
- later a shorter permanent architecture guide,
- optionally ADRs for the most important decisions.

No source restructuring occurs in this phase.

## 41. Phase 1: architecture enforcement

Before or alongside the first real refactor, add checks that inspect `.asmdef` references.

Eventually reject:

```text
*.Api -> *.Runtime
SystemA.Runtime -> SystemB.Runtime
```

Allow controlled exceptions only for:

```text
Composition
Tests
Editor when genuinely necessary
```

A source-level namespace check can additionally reject cross-system `using VoxelEngine.*.Runtime` imports.

This should make the architecture enforceable rather than aspirational.

## 42. Phase 2: Foundation extraction

Move only universal primitives.

For every candidate ask whether it has any dependency on a subsystem concept. If so, place it with the owning subsystem instead.

Exit condition: Foundation remains intentionally small.

## 43. Phase 3: Storage cutover

This should be a deliberate, larger refactor.

Sequence within the branch/PR:

1. Inventory all Storage consumers.
2. Decide public Storage vocabulary and view lifetime semantics.
3. Create `Storage.Api` and `Storage.Runtime`.
4. Move Storage implementation to Runtime.
5. Introduce the minimum high-performance API needed by consumers.
6. Update Streaming, Collision, Terrain, Edits, Rendering, and any other consumers directly to the new API.
7. Remove every external reference to the old Core Storage namespace.
8. Tighten implementation visibility.
9. Delete the obsolete old Storage surface.
10. Add architecture checks that prevent regression.

Do not leave old and new Storage APIs active in parallel.

## 44. Phase 4: Terrain and Edits cutover

These are clear capabilities currently trapped in Core and can be extracted after Storage stabilizes.

For Terrain:

- move implementation,
- define minimal sampling/generation contracts,
- update every caller,
- delete old namespace immediately.

For Edits:

- separate request/event/result types from algorithms,
- move deterministic application and brush machinery to Runtime,
- update callers directly,
- keep serialization in Net,
- remove old Core edit access.

These may be one combined architecture branch if consumer changes overlap heavily.

## 45. Phase 5: Structures and StructuralIntegrity cutover

Perform near each other so naming and ownership become unambiguous in one step.

Move procedural architecture and `Core/Features` into Structures as appropriate.

Move support/connectivity/collapse into StructuralIntegrity.

Update all callers and remove old `Core/Structure` and `Core/Features` access paths in the same refactor.

## 46. Phase 6: Tiering cutover

Create Api/Runtime split and update Streaming/Rendering directly.

Delete direct references to Tiering implementation immediately.

## 47. Phase 7: Net/Streaming direction correction

Refactor the dependency direction rather than adapting the existing relationship.

Desired direction:

```text
Net.Runtime -> Streaming.Api
```

Streaming should become independently runnable without Net.

Any network-specific region messages should be translated to Streaming domain/API requests inside Net.

## 48. Phase 8: Streaming cutover

Once Storage, Terrain, Tiering, and network direction are clean:

- define Streaming API,
- move implementation behind Runtime,
- update consumers,
- remove implementation access,
- ensure offline operation remains possible.

## 49. Phase 9: Collision cutover

Define query/result contracts and keep DDA/traversal algorithms private.

Update every consumer directly.

Do not preserve old traversal APIs for compatibility.

## 50. Phase 10: Vegetation cutover

Formalize Vegetation ownership before expanding vines, moss, flowers, and destructible plant behavior.

Define what data crosses into Rendering, Collision, Net, and gameplay.

Update consumers directly to `Vegetation.Api`.

## 51. Phase 11: Rendering cutover

Perform late, after the lower-level APIs it consumes are established.

If Rendering still appears to require implementation details from Storage, Tiering, or Vegetation, fix those API designs instead of granting Runtime exceptions.

At completion, Rendering should depend only on public contracts from other systems.

## 52. Phase 12: Composition root

Create the explicit place where concrete Runtime implementations are instantiated and wired together.

Move bootstrap knowledge there.

Do not allow Composition to become a business-logic layer.

## 53. Phase 13: delete Core

Before deleting `VoxelEngine.Core`, classify every remaining file.

Initial decomposition worksheet:

| Existing Core area | Future owner |
|---|---|
| `Core/Storage` | Storage |
| `Core/Terrain` | Terrain |
| `Core/Edits` | Edits |
| `Core/Structure` | StructuralIntegrity |
| `Core/Features` | Mostly Structures |
| `Core/Occupancy` | Mostly Storage |
| `IntMath` | Foundation |

Every remaining Core file must have an explicit owner.

Completion condition:

```text
No production source imports VoxelEngine.Core.*
No asmdef references VoxelEngine.Core
Assets/VoxelEngine/Core no longer exists
```

There should be no miscellaneous leftover bucket.

## 54. Refactor branch/PR sizing

Unlike a production migration, PR size should not be artificially minimized at the expense of architecture.

A subsystem cutover should be large enough that the final state has no legacy access path.

Examples of acceptable larger PRs:

```text
refactor/storage-boundary
refactor/terrain-edits-boundaries
refactor/structures-integrity-boundaries
refactor/net-streaming-boundary
refactor/rendering-boundary
```

A Storage refactor may update many files because all consumers must move at once. That is preferable to a sequence of adapter-heavy PRs.

The important review property is conceptual cohesion, not a low line count.

## 55. Commit policy inside large refactors

Commits should still be semantically understandable even if intermediate commits do not represent a supported compatibility state.

Example Storage branch:

```text
1. introduce Storage Api/Runtime assemblies
2. move storage ownership
3. define read/write access contracts
4. migrate Streaming and Terrain consumers
5. migrate Collision and Edits consumers
6. migrate Rendering consumer
7. tighten visibility and remove old Core storage surface
8. add/update architecture checks
```

The final branch state matters more than preserving old APIs between commits.

## 56. Validation strategy

Although there is no finished game to preserve, the refactor should still avoid accidental algorithm changes when possible.

Validate:

- project compiles at the final state of each subsystem refactor,
- existing tests still pass unless intentionally superseded,
- available prototype/test scenes still run,
- deterministic generation tests remain stable where they already exist,
- hot paths do not regress significantly because of API indirection.

This is not a compatibility migration, but it is still an architecture refactor rather than an excuse to rewrite unrelated algorithms simultaneously.

## 57. Performance baselines

Before large Storage/Rendering changes, capture whatever representative measurements are currently available, such as:

```text
world generation time
region load time
surface extraction time
frame time
memory use
raycast cost
edit application cost
```

The purpose is not to freeze prototype performance. It is to catch architectural APIs that accidentally add allocations, copying, or dispatch overhead.

## 58. Architecture review checklist for API types

Before adding a type to an API assembly, answer:

1. Which external system needs it?
2. Which system owns it?
3. Why is an existing contract insufficient?
4. Does it expose implementation details?
5. Is it Burst/job compatible where required?
6. What is its lifetime?
7. Who owns its memory?
8. Can it mutate state?
9. Does exposing it unnecessarily constrain future implementations?
10. Could it remain Runtime-private?

Default to Runtime-private until cross-system use is demonstrated.

## 59. Dependency review checklist

For every new `.asmdef` dependency:

1. Why does system A need system B?
2. Is `B.Api` sufficient?
3. Is the dependency direction correct?
4. Could data be passed instead of adding a dependency?
5. Does it create or contribute to a cycle?
6. Is system A becoming an orchestrator with too many responsibilities?

A production Runtime-to-Runtime reference should fail review.

## 60. Avoid shared utility buckets

Do not create assemblies/folders such as:

```text
Common
Shared
Utils
Helpers
Misc
General
```

as a way to resolve ownership questions.

If multiple systems use a concept, it still needs one semantic owner. Only truly universal primitives belong in Foundation.

## 61. Do not solve dependency cycles with Foundation

If two systems depend on each other, moving their shared concept into Foundation is not automatically valid.

Instead determine which system owns the concept or redesign the interaction around data/commands/results.

Foundation should never become a dumping ground for cyclic domain concepts.

## 62. Headless-server boundary test

A useful architecture test is whether a headless server could include:

```text
Storage
Terrain
Edits
Structures
StructuralIntegrity
Streaming
Collision
Vegetation simulation
Net
```

without requiring:

```text
Rendering
URP
GPU shaders
render features
```

If simulation requires Rendering, the dependency direction is wrong.

## 63. Offline world-generator boundary test

A command-line or editor world-generation tool should be able to use:

```text
Storage
Terrain
Structures
Vegetation
```

without Net, Combat, or Rendering implementation dependencies.

This is another useful measure of subsystem independence.

## 64. Replaceability boundary test

For each subsystem ask:

> Could its implementation be substantially replaced while keeping consumers unchanged behind its API?

Examples:

- Storage changes sparse voxel representation.
- Terrain replaces its generator.
- Collision replaces DDA.
- Vegetation replaces tree generation.
- Rendering replaces surface extraction.

If consumers know enough internals that the answer is no, the API boundary is leaking.

## 65. Architecture enforcement in CI

Eventually encode architecture rules in CI.

At minimum:

```text
No *.Api assembly references *.Runtime
No SystemA.Runtime references SystemB.Runtime
Only Composition/Tests/approved Editor assemblies may reference Runtime implementations
No production source imports another system's Runtime namespace
```

After Core removal, additionally fail if any production code references `VoxelEngine.Core`.

A declarative dependency allowlist is preferable to scattered ad hoc tests.

Conceptually:

```text
Storage.Runtime:
  - Storage.Api
  - Foundation

Terrain.Runtime:
  - Terrain.Api
  - Storage.Api
  - Foundation

Streaming.Runtime:
  - Streaming.Api
  - Storage.Api
  - Terrain.Api
  - Tiering.Api
```

This becomes an executable architecture diagram.

## 66. Architecture decision records

For important choices, add short ADRs under something like:

```text
docs/architecture/decisions/
```

Likely ADRs:

```text
ADR-001 API/Runtime system boundaries
ADR-002 Foundation scope
ADR-003 Storage ownership and access model
ADR-004 Networking as adapter
ADR-005 Structures vs StructuralIntegrity
ADR-006 Composition root exception
ADR-007 Cross-system mutation model
```

These should capture rationale after decisions are made, not prematurely lock unresolved implementation details.

## 67. Deliberately unresolved decisions

This plan fixes architectural direction but intentionally does not yet decide:

- whether Storage API contracts are interface-based or struct-based,
- whether `Brick` is public vocabulary,
- whether Materials becomes its own subsystem,
- whether Persistence becomes its own subsystem,
- whether generation writes pass through Edits,
- exact native-view lifetime mechanics,
- event delivery mechanism,
- exact API type names.

Those decisions require caller and performance analysis.

## 68. Decisions required before code changes

### Before Storage

Resolve the Storage design questions in section 8.7.

### Before Net/Streaming

Answer:

1. Who currently decides client interest?
2. Does Net request region loading directly?
3. Is streaming authority local, server-side, or mixed?
4. Does Net serialize Storage implementation objects?
5. What is the protocol representation of region state?
6. Can Streaming operate completely offline?

The final answer to #6 should be yes.

### Before Rendering

Answer:

1. What exact voxel representation does surface extraction need?
2. How does Rendering learn that region data changed?
3. What material information is rendering-specific?
4. What does Vegetation expose for rendering?
5. What does Tiering expose?
6. What native memory can Rendering borrow and for how long?
7. Which GPU resources are solely Rendering-owned?

## 69. Definition of done

The architecture refactor is complete when all of the following are true:

- Every major subsystem has a clear owner.
- Every externally consumed subsystem has a deliberate `Api` assembly.
- Implementation lives in the owning system's Runtime assembly.
- No production Runtime assembly references another system's Runtime assembly.
- No Api assembly references any Runtime assembly.
- The API dependency graph is acyclic.
- `VoxelEngine.Core` is completely removed.
- Foundation contains only genuinely universal primitives.
- Storage implementation is hidden behind efficient contracts/views.
- Terrain is independent of Storage implementation.
- Edits owns runtime voxel-edit semantics.
- Structures and StructuralIntegrity are distinct.
- Networking is an adapter around domain APIs.
- Streaming does not depend on Net implementation.
- Collision exposes queries/results rather than traversal internals.
- Vegetation generation/state is independent of Rendering implementation.
- Rendering consumes public subsystem representations rather than internals.
- Composition is the explicit Runtime-wiring exception.
- Architecture checks prevent future boundary violations.
- No legacy compatibility layer remains from the refactor.

## 70. Guiding principle

The permanent architectural rule should be:

> **Folders communicate ownership. Assemblies enforce ownership. APIs define collaboration. Runtime implementations remain private.**

For every future cross-system dependency:

```text
1. Identify which system owns the concept.
2. Identify the capability the consumer actually needs.
3. Expose the smallest useful contract from the owner's Api assembly.
4. Keep implementation private.
5. Reject Runtime-to-Runtime coupling.
```

The purpose of this refactor is not to create more folders. It is to make subsystem boundaries real enough that the engine can grow—particularly around destruction, vegetation, fire, water, procedural structures, networking, and rendering—without recreating a new monolithic Core under a different name.
