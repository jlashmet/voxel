# Voxel Engine Architecture Implementation Plan

**Status:** In progress — live checklist maintained on the implementation branch  
**Companion:** `docs/ARCHITECTURE_MIGRATION_PLAN.md`  
**Baseline:** `master` at `cd76b3579ae99bdd196303a96bc73b91baf61152`  
**Baseline date:** 2026-08-14  
**Planning branch:** `architecture-system-boundaries-plan`  
**Implementation branch:** `refactor/system-boundaries-foundation-storage`  
**Current focus:** Cutover 13 Composition/Core deletion — final wiring and Core removal
**Implementation stance:** clean subsystem cutovers; no compatibility layer phase


## Live implementation status

The implementation is intentionally doing dependency-boundary extraction before some final physical
Api/Runtime file moves. A subsystem is **not** marked complete merely because its Storage boundary is
green; final namespace/file/asmdef moves still have to satisfy that cutover's gate.

| Cutover | Status | Accepted work | Remaining before cutover completion |
|---|---|---|---|
| 0 — Guardrails | **Complete** | asmdef boundary guard, split-safe determinism roots, WorldGen boundary guards | final no-Core assertions tighten automatically as final assemblies land |
| 1 — Foundation | **Complete** | `IntMath` clean-moved to `VoxelEngine.Foundation`; consumers and Core bridge reference migrated | none |
| 2 — Storage | **In progress** | `Storage.Api` contracts/read views/generation/mutation/snapshot/hash boundaries complete; physical representation and occupancy now live under `Storage.Runtime`; Core storage ownership and Core assembly are deleted; full 384/371/13 baseline accepted | remove remaining direct Storage.Runtime consumers outside Composition/tests/tooling and finish final architecture gate |
| 3 — Terrain | **Complete** | `Terrain.Api` owns deterministic query/generation contracts; `TerrainGenerator` lives in `Terrain.Runtime`; Runtime references only Terrain.Api/Storage.Api/Foundation; WorldGen/Structures remain Api-only; exact 384/371/13 baseline accepted | none |
| 4 — Structures | **Complete** | `Structures.Api` owns canonical authoring/material/layout contracts; all implementation (feature VM/generation/rasterizer/emitters, retained-profile store, CastleBuilder, VoxelBrush, MasonryWeathering) lives under `Structures/Runtime` with Runtime namespaces and preserved Unity GUIDs; Storage dependencies route through Storage.Api; Rendering uses the retained-profile read boundary; WorldGen Voxel is Api-only; broad Structures assembly, legacy `VoxelEngine.Core.Features` namespace, and Kentridge compatibility seam are gone | none |
| 5 — Edits | **Complete** | Edits.Api owns canonical vocabulary and `IAlterationApplier`; all edit implementation lives under `Edits/Runtime` with Runtime namespace and preserved Unity GUIDs; Net protocol/client/server/validation consume Api only; dead `DensityCap`, redundant Net wrapper, and `VoxelEngine.Core.Edits` are gone; Storage boundaries/parity accepted | none |
| 6 — StructuralIntegrity | **Complete** | dead Net `StructuralGraph` removed; StructuralIntegrity.Api/Runtime assemblies created; `SupportField`, `CollapseDetection`, and `Connectivity` all live in Runtime with preserved Unity GUIDs and Storage.Api-only reads; `Core/Structure` is gone; final inventory found no production/network structural consumer, so Api remains intentionally empty rather than inventing DTOs; parity accepted | none |
| 7 — Tiering | **Complete** | `DeviceTier`/`DeviceTierBudget` live under `Tiering/Api` with preserved Unity GUIDs; broad Tiering assembly replaced by dependency-free `VoxelEngine.Tiering.Api`; Streaming, Rendering, Showcase and tests consume Api; no Tiering.Runtime exists; parity accepted | none |
| 8 — Streaming | **Complete** | `Streaming.Api` exposes the real `RegionLoadRequest`/`IRegionStreaming` orchestration contract; all four implementation files live under `Streaming/Runtime` with preserved Unity GUIDs; `RegionStreamingService` hides Storage residency behind the Api; Runtime depends only on Streaming.Api, Storage.Api and Tiering.Api; broad Streaming/Net coupling is gone | none |
| 9 — Collision | **Complete** | `Collision.Api`/`Collision.Runtime` replace the broad assembly; DDA/raycast/sweep/hull implementation lives in Runtime with preserved Unity GUIDs; Runtime consumes Storage.Api only; final caller inventory found no production subsystem consumer, so Api remains intentionally empty instead of inventing DTOs | none |
| 10 — Vegetation | **Complete** | `Vegetation.Api` owns stable placement/profile plus immutable presentation/damage/topology contracts; mutable tree state, skeleton generation and damage implementation live in `Vegetation.Runtime`; WorldGen Voxel and Rendering consume Vegetation.Api only; Kentridge surface/terrain boundaries remain Storage.Api/Terrain.Api | none |
| 11 — Net | **Complete** | Net.Api/Runtime physical decomposition and Runtime namespaces are complete; Runtime references only approved domain APIs; residency delegates Streaming.Api; semantic repair/snapshots use Storage.Api logical capabilities; structural graph and duplicate edit wrapper are gone; final 384/371/13 behavioral baseline accepted | none |
| 12 — Rendering | **Complete** | Rendering Api/Runtime physical + namespace + asmdef cutover complete; Runtime consumes Storage.Api/Tiering.Api/Vegetation.Api only; presentation catalogues/change feed use Storage.Api read views; retained profiles use Storage.Api; tree presentation uses Vegetation.Api; dead physical leaks removed; static and 384/371/13 behavioral parity accepted | none |
| 13 — Composition/Core deletion | **In progress — current** | final inventory complete; WorldGen direction accepted; Terrain/Storage ownership extracted; Core deleted; functional Composition Storage bootstrap accepted; far-field, CharacterMotor, network presentation, GpuDebris, VoxelShowcase and ShowcaseMultiplayerSession storage seams are API-only; ShowcaseCatalogue consumes Structures.Api only; mixed-brick capacity and lookdev renderer configuration are Composition-owned; CompactFps consumes Rendering.Api diagnostics; Structures authoring routes through an Api session; VoxelFarTerrain and TerrainLookdev are Runtime-free through subsystem-specific RenderingComposition; TerrainLookdev dead profile-store ceremony removed; empty Tools shells deleted; `ShowcaseTreePopulation` routes world/structure/vegetation access through Composition; `ArchLookdev` routes presentation catalogue, build-budget, sky, and surface-status access through `RenderingComposition`; current Showcase Runtime-coupled source inventory reduced to two files; 387/374/13 baseline preserved | migrate two remaining Showcase concrete Runtime consumers, enable the final production-wide Runtime dependency guard, remove residual Core documentation literals, final dependency report |

### Checklist discipline

- Check a task off only after its code is committed **and** the relevant CI acceptance gate passes.
- Update this document immediately after an accepted slice, before starting the next slice.
- Do not check off final cutover gates for boundary-only work when file/namespace/asmdef moves remain.
- CI acceptance means no new compiler/test regression and the failed-test-name set matches the currently documented known baseline. The baseline may shrink only when an intended cutover change directly fixes an existing failure; that reduction must be investigated and documented here before accepting the slice.
- Latest accepted code gate: `8780cffe66a0e3e4ba75b524957d00b481ace971`, run `31907252082` — 387 tests, 374 passed, exactly the same 13 known baseline failures with zero C# compiler errors. This accepts `ArchLookdev` routing rendering presentation/build configuration and surface-status access through `RenderingComposition` and reduces the remaining Runtime-coupled Showcase source inventory to two files.
- Prior integrated Cutover 13 gate: `a0528159ada889c59190888840523e6fb8c05a10`, run `31899261112` — 387/374/13, exact same known failure set.
- Latest accepted Rendering static gate: source `f5e0b646102a50305424850a0508d190bae3e44d`, run `31894268246` — physical Api/Runtime layout, Runtime namespaces, dependency direction, reverse simulation dependency, and explicit/manual lookdev status all passed. Behavioral parity is still pending and is not implied by this static gate.

This document turns the architecture specification into a repository-specific execution plan. The architecture document explains the rules and desired boundaries; this document says what to move, what to create, what to delete, which consumers change in the same cutover, and what must pass before moving to the next cutover.

Where this document is more specific than speculative wording in `ARCHITECTURE_MIGRATION_PLAN.md`, this post-merge implementation plan wins.

---

## 1. Baseline and scope

This plan was refreshed after the Kentridge/world-generation merge. It is pinned to master commit:

```text
cd76b3579ae99bdd196303a96bc73b91baf61152
```

If implementation starts from a different master SHA, the first task is to re-run the file/asmdef dependency inventory and update this document before moving files. Do not assume GitHub code search is complete for this private repository; use repository trees plus local/compiler reference errors or a local textual reference scan when implementing.

### In scope

- dissolve `Assets/VoxelEngine/Core`;
- establish one deliberate `Api/` assembly for each exposed engine subsystem;
- move implementations under their owning subsystem `Runtime/` folder;
- prohibit foreign Runtime assembly references;
- separate storage representation from storage capabilities;
- make semantic world generation a client of engine APIs rather than engine internals;
- remove the current Streaming -> Net dependency;
- move structural simulation out of Net;
- make Rendering consume read-only engine APIs;
- update architecture/determinism guard tests so the rules are mechanically enforced.

### Explicitly out of scope

- LayerProcGen adoption or a new procedural-generation orchestration framework;
- changing the terrain algorithm itself;
- replacing the current surface extraction/rendering algorithm;
- fire/water simulation implementation;
- a full material-domain redesign unless a compile-time dependency forces it;
- gameplay/combat redesign;
- preserving old namespaces or APIs for compatibility.

---

## 2. Non-negotiable cutover policy

Each subsystem cutover is decisive.

For a cutover:

1. Create the target Api/Runtime assemblies.
2. Move the subsystem files.
3. Change namespaces.
4. Change every known consumer in the repository in the same cutover.
5. Change asmdef references in the same cutover.
6. Delete obsolete files, namespaces and assembly references immediately.
7. Compile and run the cutover-specific checks before starting the next subsystem.

Do **not** add:

- namespace-forwarding wrappers;
- deprecated aliases;
- `Legacy*` adapters;
- duplicate old/new APIs;
- temporary foreign Runtime references;
- a `Common`, `Shared`, `Utils`, or `Core2` assembly to break cycles;
- public `BrickPool`, `RegionTable`, `Region`, or other representation types merely to get the compiler green.

A working branch may have intermediate local commits that do not compile while a single subsystem is being moved. The branch state at each completed cutover must compile and satisfy its acceptance gates.

---

## 3. Target repository layout

```text
Assets/VoxelEngine/
  Foundation/
    VoxelEngine.Foundation.asmdef

  Storage/
    Api/
      VoxelEngine.Storage.Api.asmdef
    Runtime/
      VoxelEngine.Storage.Runtime.asmdef

  Terrain/
    Api/
      VoxelEngine.Terrain.Api.asmdef
    Runtime/
      VoxelEngine.Terrain.Runtime.asmdef

  Edits/
    Api/
      VoxelEngine.Edits.Api.asmdef
    Runtime/
      VoxelEngine.Edits.Runtime.asmdef

  Structures/
    Api/
      VoxelEngine.Structures.Api.asmdef
    Runtime/
      VoxelEngine.Structures.Runtime.asmdef

  StructuralIntegrity/
    Api/
      VoxelEngine.StructuralIntegrity.Api.asmdef
    Runtime/
      VoxelEngine.StructuralIntegrity.Runtime.asmdef

  Tiering/
    Api/
      VoxelEngine.Tiering.Api.asmdef

  Streaming/
    Api/
      VoxelEngine.Streaming.Api.asmdef
    Runtime/
      VoxelEngine.Streaming.Runtime.asmdef

  Collision/
    Api/
      VoxelEngine.Collision.Api.asmdef
    Runtime/
      VoxelEngine.Collision.Runtime.asmdef

  Vegetation/
    Api/
      VoxelEngine.Vegetation.Api.asmdef
    Runtime/
      VoxelEngine.Vegetation.Runtime.asmdef

  Net/
    Api/
      VoxelEngine.Net.Api.asmdef
    Runtime/
      VoxelEngine.Net.Runtime.asmdef

  Rendering/
    Api/
      VoxelEngine.Rendering.Api.asmdef
    Runtime/
      VoxelEngine.Rendering.Runtime.asmdef

  Composition/
    VoxelEngine.Composition.asmdef
```

`Foundation` is intentionally a single assembly because everything in it is public/value-level infrastructure. `Tiering` initially has only an Api assembly because there is no meaningful runtime implementation today. Do not create empty Runtime assemblies to make the folder pattern look symmetrical.

---

## 4. Final assembly dependency allowlist

The final allowed engine dependency graph is:

| Assembly | Allowed engine references |
|---|---|
| `VoxelEngine.Foundation` | none |
| `VoxelEngine.Storage.Api` | Foundation |
| `VoxelEngine.Storage.Runtime` | Storage.Api, Foundation |
| `VoxelEngine.Terrain.Api` | Foundation, Storage.Api only if its public contracts require voxel values |
| `VoxelEngine.Terrain.Runtime` | Terrain.Api, Storage.Api, Foundation |
| `VoxelEngine.Structures.Api` | Foundation, Storage.Api only where compiled feature format requires voxel/surface value types |
| `VoxelEngine.Structures.Runtime` | Structures.Api, Storage.Api, Terrain.Api, Foundation |
| `VoxelEngine.Edits.Api` | Foundation, Storage.Api only for canonical voxel value types |
| `VoxelEngine.Edits.Runtime` | Edits.Api, Storage.Api, Foundation |
| `VoxelEngine.StructuralIntegrity.Api` | Foundation |
| `VoxelEngine.StructuralIntegrity.Runtime` | StructuralIntegrity.Api, Storage.Api, Edits.Api, Foundation |
| `VoxelEngine.Tiering.Api` | Foundation only if needed |
| `VoxelEngine.Streaming.Api` | Foundation |
| `VoxelEngine.Streaming.Runtime` | Streaming.Api, Storage.Api, Terrain.Api, Structures.Api, Tiering.Api, Foundation |
| `VoxelEngine.Collision.Api` | Foundation |
| `VoxelEngine.Collision.Runtime` | Collision.Api, Storage.Api, Foundation |
| `VoxelEngine.Vegetation.Api` | Foundation |
| `VoxelEngine.Vegetation.Runtime` | Vegetation.Api, Storage.Api, Terrain.Api if needed, Foundation |
| `VoxelEngine.Net.Api` | Foundation plus stable domain APIs only if required |
| `VoxelEngine.Net.Runtime` | Net.Api, Streaming.Api, Edits.Api, Storage.Api, StructuralIntegrity.Api, Foundation |
| `VoxelEngine.Rendering.Api` | Foundation only if needed |
| `VoxelEngine.Rendering.Runtime` | Rendering.Api, Storage.Api, Tiering.Api, Vegetation.Api, Foundation |
| `VoxelEngine.Composition` | any Runtime or Api assembly required to instantiate/wire the application |

External package policy after the refactor:

```text
MountingForce.WorldGen.Core          -> no VoxelEngine references
MountingForce.WorldGen.Architecture  -> no VoxelEngine references
MountingForce.WorldGen.Voxel         -> VoxelEngine *Api assemblies only
```

No engine Api assembly may reference an engine Runtime assembly. No engine Runtime assembly may reference another subsystem's Runtime assembly. `Composition` is the sole production exception for runtime-to-runtime wiring.

---

# CUTOVER 0 — Architecture guardrails before moves

## 5. Purpose

Put mechanical checks in place before the large moves so accidental runtime coupling cannot creep in while the compiler is being repaired.

## 5.1 Update `ConstitutionGuardTests`

Current determinism checks scan the physical `Assets/VoxelEngine/Core` directory. That becomes invalid as soon as Core is dissolved.

Replace the Core-directory assumption with an explicit deterministic-path/assembly allowlist. The exact allowlist evolves as files move but must include deterministic simulation code and exclude presentation-only code.

Initial target categories:

```text
Foundation
Storage/Api
Storage/Runtime
Terrain/Api
Terrain/Runtime
Edits/Api
Edits/Runtime
Structures/Api
Structures/Runtime deterministic generation code
StructuralIntegrity/Api
StructuralIntegrity/Runtime
Net deterministic protocol/application code
```

Do not blanket-apply deterministic/no-float rules to Rendering or all Vegetation code; some of those systems legitimately use floating-point presentation data.

## 5.2 Add asmdef dependency test

Add a new EditMode architecture test, preferably:

```text
Assets/Tests/EditMode/VoxelEngineAssemblyBoundaryTests.cs
```

It must parse `.asmdef` JSON and fail when any of these become true:

- an assembly named `*.Api` references any `*.Runtime`;
- an assembly named `*.Runtime` references another subsystem's `*.Runtime`;
- a production assembly references `VoxelEngine.Core` after the final cutover;
- `VoxelEngine.Streaming.Runtime` references Net;
- a simulation/runtime assembly references `VoxelEngine.Rendering.Runtime`;
- a non-Composition production assembly references multiple foreign Runtime assemblies;
- `MountingForce.WorldGen.Core` references any `VoxelEngine.*` assembly;
- `MountingForce.WorldGen.Architecture` references any `VoxelEngine.*` assembly;
- `MountingForce.WorldGen.Voxel` references a `VoxelEngine.*.Runtime` assembly.

Allowed exceptions must be declared in one explicit test allowlist, with a comment explaining why. Do not encode exceptions as broad string patterns.

## 5.3 Keep the existing Kentridge boundary test

Expand `KentridgeArchitectureBoundaryTests.cs` so it verifies both source namespace restrictions and asmdef restrictions.

### Cutover 0 gate

- [x] EditMode architecture tests pass on current layout with temporary explicit current-layout exceptions.
- [x] Every temporary exception has the cutover number that removes it. (No broad Runtime-reference exceptions are currently carried.)
- [x] No permanent exception allows foreign Runtime references.

---

# CUTOVER 1 — Foundation

## 6. Ownership

Foundation contains only types that have no domain owner and are cheap/stable enough to be used everywhere.

### Exact initial file move

| Current | Target | Target namespace |
|---|---|---|
| `Assets/VoxelEngine/Core/IntMath.cs` | `Assets/VoxelEngine/Foundation/IntMath.cs` | `VoxelEngine.Foundation` |

Create `VoxelEngine.Foundation.asmdef` with `autoReferenced: false` and only the Unity Mathematics reference if `IntMath` requires it.

### Do not move into Foundation

- `BrickRef`
- `Region`
- `VoxelCell`
- material/surface catalogues
- terrain settings
- feature definitions
- edit events
- networking packet types

If a later cutover discovers a genuinely universal coordinate/ID value, add it deliberately; do not move a type merely because multiple systems use it.

### Consumer action

Replace `VoxelEngine.Core.IntMath` references with `VoxelEngine.Foundation.IntMath` and add Foundation asmdef references only where needed.

### Gate

- [x] No source references old `VoxelEngine.Core.IntMath`.
- [x] Foundation references no engine assembly.
- [x] Foundation contains no mutable state/service.

---

# CUTOVER 2 — Storage, Occupancy and authoritative voxel values

## 7. Ownership

Storage owns authoritative voxel memory and its physical representation. It exposes capabilities and stable voxel values, not its allocator/table implementation.

This is the hinge cutover. Do not begin Terrain/Structures/Rendering rewiring until the Storage API is capable of serving generation, read-only hot paths, surface queries and semantic snapshot/hash use cases without exposing `BrickPool`/`RegionTable`.

## 7.1 Exact current file disposition

### Move to `Storage.Api`

| Current | Target | Notes |
|---|---|---|
| `Core/Storage/VoxelCell.cs` | `Storage/Api/VoxelCell.cs` | canonical logical voxel/surface value types |
| selected public world-grid constants from `Core/Storage/VoxelDimensions.cs` | `Storage/Api/VoxelGrid.cs` | region/world coordinate facts only |

`VoxelCell.cs` currently contains `VoxelBoundarySample`, `SurfaceStyles`, `Coatings`, `VoxelSurfaceFlags`, `VoxelSurfaceSemantics`, and `VoxelCell`. Keep these together for the first cutover because they form the authoritative logical voxel value and compiled feature semantics.

### Move to `Storage.Runtime`

| Current | Target |
|---|---|
| `Core/Storage/BrickPool.cs` | `Storage/Runtime/BrickPool.cs` |
| `Core/Storage/BrickRef.cs` | `Storage/Runtime/BrickRef.cs` |
| `Core/Storage/Region.cs` | `Storage/Runtime/Region.cs` |
| `Core/Storage/RegionTable.cs` | `Storage/Runtime/RegionTable.cs` |
| `Core/Storage/VoxelAccess.cs` | `Storage/Runtime/VoxelAccess.cs` |
| `Core/Storage/VoxelChangeJournal.cs` | `Storage/Runtime/VoxelChangeJournal.cs` |
| `Core/Storage/MaterialPalette.cs` | `Storage/Runtime/MaterialPalette.cs` |
| `Core/Storage/MaterialAdjacencyCatalogue.cs` | `Storage/Runtime/MaterialAdjacencyCatalogue.cs` |
| `Core/Storage/SurfaceCatalogue.cs` | `Storage/Runtime/SurfaceCatalogue.cs` |
| `Core/Storage/SemanticRegionHasher.cs` | `Storage/Runtime/SemanticRegionHasher.cs` |
| `Core/Storage/SemanticRegionSnapshotCodec.cs` | `Storage/Runtime/SemanticRegionSnapshotCodec.cs` |
| `Core/Occupancy/MipBuilder.cs` | `Storage/Runtime/Occupancy/MipBuilder.cs` |
| `Core/Occupancy/OccupancyMask.cs` | `Storage/Runtime/Occupancy/OccupancyMask.cs` initially |

Split `VoxelDimensions.cs` rather than moving it wholesale:

```text
Storage.Api/VoxelGrid.cs
    RegionVoxelEdge
    RegionVoxelEdgeLog2
    MaterialEmpty (or keep with VoxelCell if that produces a cleaner contract)

Storage.Runtime/StorageLayout.cs
    BrickEdge
    BrickEdgeLog2
    BrickEdgeMask
    VoxelsPerBrick
    OccupancyWordsPerBrick
    RegionEdge
    RegionEdgeLog2
    RegionEdgeMask
    BricksPerRegion
    BytesPerMixedBrick
```

The public API must not contain `BrickEdge`, `BrickRef`, pool slot concepts, mixed-brick byte sizes, or allocator indices.

If `OccupancyMask` is required by Rendering/Collision for zero-copy performance, do **not** simply make the current runtime type public. Introduce a read-only Api value/view that exposes only the occupancy operations those consumers require; keep construction/mip maintenance in Runtime.

## 7.2 New Storage API capabilities

Create the following contracts. Names are fixed by this plan; field layout may be tuned during implementation to preserve Burst/native performance.

```text
Storage/Api/RegionCoord.cs
Storage/Api/RegionVersion.cs
Storage/Api/VoxelReadView.cs
Storage/Api/RegionReadView.cs
Storage/Api/RegionGenerationWriter.cs
Storage/Api/VoxelMutationWriter.cs
Storage/Api/VoxelSurfaceQuery.cs
Storage/Api/RegionSnapshot.cs
Storage/Api/IWorldStorage.cs
```

Responsibilities:

### `RegionCoord`

Stable region identity/coordinate. Must not contain pool indices or `BrickRef`.

### `RegionVersion`

Monotonic semantic content version used to invalidate cached render/collision/network reads after mutation or replacement.

### `RegionReadView`

A Burst/native-friendly read-only view used by Rendering and Collision. It must permit hot voxel/occupancy reads without virtual/interface dispatch in inner loops.

It may contain `NativeArray`/native container views, but its public fields must describe logical voxel data rather than expose the `BrickPool` owner object. If zero-copy requires physical lookup metadata, define dedicated readonly descriptor structs in Storage.Api rather than publishing `BrickRef` or `Region`.

Lifetime contract:

- view is owned by Storage;
- borrower never disposes backing storage;
- view carries a version;
- view is valid only until the documented mutation/unload/publish boundary unless explicitly pinned by Storage;
- use-after-invalidation must be detectable in Development/Editor builds where practical.

### `VoxelReadView`

The inner-loop read primitive used by jobs. It returns canonical `VoxelCell`/occupancy information and may be embedded inside `RegionReadView`.

### `RegionGenerationWriter`

Bulk generation-only write capability. Terrain/structure generation can populate a not-yet-published region efficiently. It is not the gameplay edit API and must not emit network/gameplay edit events.

### `VoxelMutationWriter`

Mutation primitive owned/issued by Storage and consumed by Edits.Runtime. It must preserve current `VoxelAccess` behavior: uniform -> mixed materialization, allocation accounting, occupancy/mip updates, change journal/version changes, and mixed -> uniform collapse.

Do not expose it to arbitrary gameplay systems.

### `VoxelSurfaceQuery`

Focused world-space query used by worldgen/vegetation and similar placement consumers. It must support at minimum:

```text
TryRead(worldVoxel, out VoxelCell)
TryFindTopSolid(x, z, minY, maxY, out y, out VoxelCell)
```

The implementation may internally traverse RegionTable/BrickPool. The consumer must not.

### `RegionSnapshot`

Stable semantic snapshot/hash input for Net convergence/repair. Snapshot semantics are logical voxel state; network code must not serialize `Region`, `BrickRef`, or pool slots.

### `IWorldStorage`

This is a coarse orchestration capability, not the hot-path voxel interface. It owns operations such as:

```text
create unpublished region
aquire generation writer
publish region
try acquire read view
release/unload region
query region version
capture/apply semantic snapshot
```

Spelling/API naming can follow project conventions, but do not turn this into a per-voxel virtual `GetVoxel()` hot path.

## 7.3 Storage Runtime responsibilities

`VoxelAccess` remains implementation-only. Its current responsibilities are important and must not be accidentally split across callers:

- materialize uniform bricks when first mixed write occurs;
- allocate/release brick storage;
- update voxel material/surface/boundary state;
- update occupancy and all mip levels;
- append change journal/version state;
- collapse mixed bricks back to uniform when possible.

`RegionTable` remains the internal owner of region map/index state.

`SemanticRegionHasher` and `SemanticRegionSnapshotCodec` remain Storage implementations. Net receives only Api snapshots/hashes/capabilities.

## 7.4 Consumers changed in this same cutover

At minimum repair every direct storage consumer in:

- `Assets/VoxelEngine/Streaming/*`
- `Assets/VoxelEngine/Collision/*`
- `Assets/VoxelEngine/Rendering/SurfaceExtraction/*`
- `Assets/VoxelEngine/Core/Terrain/*` until Terrain moves in Cutover 3
- `Assets/VoxelEngine/Core/Edits/*` until Edits moves
- `Assets/VoxelEngine/Core/Features/*` until Structures moves
- `Assets/VoxelEngine/Net/*`
- `Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeVegetationPlanner.cs`
- tests and showcase/bootstrap code.

For consumers not yet moved to their final assembly, it is acceptable during this cutover for their existing assembly to reference `VoxelEngine.Storage.Api`. It is not acceptable for them to reference `VoxelEngine.Storage.Runtime` unless they are temporary Composition/bootstrap wiring that is removed by the final cutover.

## 7.5 Kentridge vegetation requirement

Replace the current `KentridgeVegetationPlanner` parameters/use of:

```text
RegionTable
BrickPool
VoxelAccess
```

with `VoxelSurfaceQuery` (or the equivalent concrete Api value created above). Its algorithm should ask for top solid/surface information; it must not know how voxels are physically stored.

## 7.6 Storage acceptance gates

- [x] `BrickPool`, `BrickRef`, `Region`, `RegionTable`, `VoxelAccess`, `MipBuilder` are owned by `Storage.Runtime`; physical move + Core deletion accepted by static gate `31894801235` and exact behavioral gate `31895124610`.
- [ ] No source outside `Storage/Runtime` imports their namespaces.
- [x] Rendering and Collision use readonly native views, not virtual per-voxel services.
- [x] Kentridge vegetation no longer takes `RegionTable` or `BrickPool`; it consumes `IVoxelSurfaceQuery` and preserves water/cascade exclusion via caller-owned material IDs.
- [x] Kentridge vegetation surface-query slice accepted by CI at `a47c3b8abff99e27e5c5cbeda0451ad8b963c314`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Net semantic hash/snapshot paths do not depend on physical brick layout.
- [x] Existing storage/read/mutation parity tests pass against the established CI baseline; snapshot/hash final ownership remains tracked by the unchecked item above.
- [ ] Architecture guard has no Storage.Runtime foreign-reference exception.

---

# CUTOVER 3 — Terrain

## 8. Exact file moves

| Current | Target | Target namespace |
|---|---|---|
| `Core/Terrain/TerrainGenerator.cs` | `Terrain/Runtime/TerrainGenerator.cs` | `VoxelEngine.Terrain` |
| `Core/Terrain/TerrainSampler.cs` | split/move as described below | Api + Runtime |

Create:

```text
Terrain/Api/TerrainSeed.cs
Terrain/Api/TerrainSample.cs
Terrain/Api/TerrainQuery.cs
Terrain/Api/TerrainGenerationRequest.cs
Terrain/Runtime/TerrainGenerator.cs
Terrain/Runtime/TerrainSampler.cs
```

## 8.1 Public contract

Terrain.Api owns deterministic terrain-query vocabulary needed by Structures/worldgen without exposing the implementation class.

Provide a Burst-friendly query value or static deterministic query contract for height sampling. Do not make foreign systems reference `Terrain.Runtime.TerrainSampler`.

Required operations:

```text
sample terrain height at world X/Z for seed/settings
sample terrain attributes needed for placement/slope decisions
request bulk generation into a Storage.Api RegionGenerationWriter
```

`TerrainGenerator` must no longer allocate/manipulate `BrickPool`/`Region` directly. It generates through `RegionGenerationWriter`.

## 8.2 Consumers

Update:

- `Streaming/RegionLoader.cs`
- Structures shape/placement code that calls `TerrainSampler.HeightAt`
- `MountingForce.WorldGen.Voxel` vertical/plot placement adapters
- terrain tests.

The Voxel worldgen package references `VoxelEngine.Terrain.Api`, never Terrain.Runtime.

### Implementation progress

- [x] Deterministic height/slope/mountain-mask sampling extracted to `Terrain.Api/TerrainQuery.cs`; the old `Core/Terrain/TerrainSampler.cs` compatibility surface is deleted.
- [x] Structures, WorldGen Voxel, Showcase, CI/editor capture, and terrain tests consume `Terrain.Api` for direct terrain sampling.
- [x] Terrain query extraction accepted by CI at `1233cc3d29a56f7a37cc979d0bd897f4f716db8a`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `TerrainGenerator` no longer receives or writes `BrickPool`; generation goes through Storage.Api bulk generation views.
- [x] Table-backed and standalone generation writers have parity coverage.
- [x] Terrain.Api/Runtime physical move and namespace cutover complete at `4a46df0f39299535f6d036fd0641964eefd3a516`; static Terrain gate `31894703756` passed and final post-Core exact baseline is `31895124610`.
- [x] Final Terrain Cutover 3 behavioral acceptance: source `c6089e18e1774dcedb6145f0b6977147ffba3d0d`, isolated run `31895124610`, 384 total / 371 passed / exact same 13 failed-test names as the accepted baseline.

### Gate

- [x] no `VoxelEngine.Core.Terrain` references remain; static Terrain gate `31894703756` passed;
- [x] Terrain.Runtime references only Terrain.Api + Storage.Api + Foundation; static Terrain gate `31894703756` passed;
- [x] Structures/worldgen cannot call a Terrain.Runtime type; static Terrain gate `31894703756` verifies WorldGen remains Terrain.Api-only and no Structures.Runtime reference exists;
- [x] deterministic terrain parity tests remain byte/value identical unless a deliberate behavior change is separately approved.

---

# CUTOVER 4 — Structures and compiled feature authoring

## 9. Ownership

Structures owns reusable structure/feature authoring format and its runtime realization. The merged Kentridge Voxel package is an important external author of compiled feature catalogues, so Structures.Api must expose the **authoring contract** it genuinely needs. It cannot be reduced to a single high-level `SpawnStructure()` interface.

## 9.1 Exact Core/Features disposition

### Structures.Api

Move/split these as public authoring-format contracts:

| Current | Target |
|---|---|
| `Core/Features/AnchorSpec.cs` | `Structures/Api/AnchorSpec.cs` |
| `Core/Features/FeatureBudget.cs` | `Structures/Api/FeatureBudget.cs` |
| `Core/Features/FeatureCatalogue.cs` | `Structures/Api/FeatureCatalogue.cs` |
| `Core/Features/FeatureDefinition.cs` | `Structures/Api/FeatureDefinition.cs` |
| `Core/Features/ParameterSpec.cs` | `Structures/Api/ParameterSpec.cs` |
| `Core/Features/PlacementRule.cs` | `Structures/Api/PlacementRule.cs` |
| `Core/Features/Primitive.cs` | `Structures/Api/Primitive.cs` if referenced by external catalogue authoring; otherwise Runtime |
| `Core/Features/ShapeOps.cs` | `Structures/Api/ShapeOps.cs` |
| `Core/Features/CatalogueLoader.cs` | `Structures/Api/FeatureCatalogueBuilder.cs` |
| `Core/Features/FeatureHash.cs` | Api/internal helper if required by builder validation; otherwise Runtime |

`FeatureCatalogueBuilder` is the clean name for the allocation/finalization functionality currently in `CatalogueLoader`. Do the rename during this cutover and update all consumers; do not leave `CatalogueLoader` as a deprecated forwarding type.

### Structures.Runtime

| Current | Target |
|---|---|
| `Core/Features/FeatureGeneration.cs` | `Structures/Runtime/FeatureGeneration.cs` |
| `Core/Features/ShapeProgram.cs` | `Structures/Runtime/ShapeProgram.cs` |
| `Core/Features/PrimitiveRasteriser.cs` | `Structures/Runtime/PrimitiveRasteriser.cs` |
| `Core/Features/ProfileBlockStore.cs` | `Structures/Runtime/ProfileBlockStore.cs` |
| `Core/Features/ArchFeature.cs` | `Structures/Runtime/Features/ArchFeature.cs` |
| `Core/Features/BondedBlockVeneer.cs` | `Structures/Runtime/Features/BondedBlockVeneer.cs` |
| `Core/Features/Emitters/BoxEmitter.cs` | `Structures/Runtime/Emitters/BoxEmitter.cs` |
| `Core/Features/Emitters/CapsuleChainEmitter.cs` | `Structures/Runtime/Emitters/CapsuleChainEmitter.cs` |
| `Core/Features/Emitters/CurvedPrimitiveEmitter.cs` | `Structures/Runtime/Emitters/CurvedPrimitiveEmitter.cs` |
| `Core/Features/Emitters/CylinderEmitter.cs` | `Structures/Runtime/Emitters/CylinderEmitter.cs` |
| `Core/Features/Emitters/PrismEmitter.cs` | `Structures/Runtime/Emitters/PrismEmitter.cs` |

Move the existing top-level structure files into Runtime:

| Current | Target |
|---|---|
| `Structures/CastleBuilder.cs` | `Structures/Runtime/CastleBuilder.cs` |
| `Structures/MasonryWeathering.cs` | `Structures/Runtime/MasonryWeathering.cs` |
| `Structures/StructureMaterials.cs` | `Structures/Runtime/StructureMaterials.cs` |
| `Structures/VoxelBrush.cs` | `Structures/Runtime/VoxelBrush.cs` |

Delete the old `VoxelEngine.Structures.asmdef` after Api/Runtime asmdefs replace it.

## 9.2 Shape bytecode is an API contract; evaluator is not

Kentridge compiles shape programs. Therefore the canonical opcode/encoding constants belong in Structures.Api (`ShapeOps` and any small encoding value types).

The interpreter/evaluator belongs in Structures.Runtime (`ShapeProgram`).

Delete:

```text
Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeShapeProgramCompatibility.cs
```

It is an explicit compatibility seam for old/mismatched encoding. In this pre-game clean cutover, update every Kentridge catalogue/program builder to emit the canonical Structures.Api encoding directly.

Update `KentridgeShapeProgramEncodingTests.cs` to assert the canonical encoding, not compatibility translation.

## 9.3 Runtime dependencies

Structures.Runtime may use:

- Structures.Api
- Storage.Api generation writer/value contracts
- Terrain.Api query contracts
- Foundation

It must not call `Terrain.Runtime.TerrainSampler` or manipulate Storage.Runtime.

## 9.4 Kentridge changes in the same cutover

Update all files under:

```text
Packages/com.mountingforce.worldgen/Runtime/Voxel/
```

that import `VoxelEngine.Core.Features` or the old broad `VoxelEngine.Structures` namespace.

`MountingForce.WorldGen.Voxel.asmdef` changes from broad engine references to `VoxelEngine.Structures.Api` plus only other specific APIs it uses.

Do **not** add any engine reference to `MountingForce.WorldGen.Core` or `MountingForce.WorldGen.Architecture`.

### Implementation progress

- [x] Storage.Api full-cell block mutation matches authoritative `VoxelCell` semantics.
- [x] Storage read views preserve authored boundary samples on empty mixed cells.
- [x] Full-cell mutation/read parity slice accepted by CI: 374 total / 359 passed / exact 15 baseline failures.
- [x] Feature rasterisation/generation Storage.Api boundary accepted by CI: 377 total / 362 passed / exact 15 baseline failures.
- [x] `PrimitiveRasteriser` consumes Storage.Api only and preserves primitive ordering/surface/boundary semantics.
- [x] `FeatureGeneration` consumes the Storage.Api authoring capability rather than `RegionTable`/`BrickPool`.
- [x] Structures.Api extracted with canonical `VoxelEngine.Structures.Api` namespace; authoring contracts moved with Unity GUIDs preserved.
- [x] `CatalogueLoader` clean-renamed to `FeatureCatalogueBuilder`; no compatibility alias remains.
- [x] Structures.Api extraction accepted by CI: 379 total / 364 passed / exact 15 baseline failures.
- [x] Kentridge compatibility encoding seam deleted; catalogue/program builders now emit canonical Structures.Api instruction widths directly.
- [x] Canonical shape encoding slice accepted by CI at `a9d684e612485728320597577680e60a8075f075`: 382 total / 369 passed / exact 13 known baseline failures; all `KentridgeShapeProgramEncodingTests` pass and no new failures were introduced.
- [x] `Structures.Runtime` assembly created with only Structures.Api + Storage.Api + Terrain.Api + Foundation engine references.
- [x] Runtime-candidate Storage dependencies cleaned: `ProfileBlockStore` and `BondedBlockVeneer` use `VoxelGrid.MaterialEmpty`, `CurvedPrimitiveEmitter` has no Core.Storage import, and `ArchFeature` validates through Storage.Api authoring catalogue capabilities rather than concrete mutable Storage types.
- [x] Structures Runtime dependency-cleanup slice accepted by CI at `0c2b0cdd7148e3d14f6ab3a14348a5fc993dfcac`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Canonical ramp direction encoding (`RampAxisMask`/`ReverseRampBit`) is owned by `Structures.Api.ShapeOps`; Runtime and the first directional Kentridge catalogue consumers/tests use the API constants rather than Runtime emitter constants.
- [x] Ramp-encoding / first WorldGen consumer slice accepted by CI at `3c983386b82cbfeb28b8b151c0cf154d65c6995c`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Storage authoring exposes `SetWholeCellBlock`; the implementation preserves compact uniform blocks when possible and mixed authored cell semantics when surface/boundary metadata is present.
- [x] Whole-cell authoring capability accepted by CI at `cc08db71463d21bf23fca8a7459ddde61a3f9d58`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `ArchFeature` emits retained profile blocks through `IProfileBlockWriter`; the concrete `ProfileBlockStore` still owns mutation.
- [x] Profile-block writer seam accepted by CI at `eee4fe264a014a654fd08001d1f6eed69c64b754`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Feature VM/generation/rasteriser/emitters plus Arch/Bonded implementation moved physically under `Structures/Runtime` with existing Unity GUIDs preserved; production subsystems/WorldGen did not gain Structures.Runtime references.
- [x] Physical feature-runtime move accepted by CI at `37241eb3c1e00bd97ea9e22a5682cf17cd9382e7`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Retained profile value/read contract lives in Storage.Api; Rendering consumers and `VoxelWorldView` consume `IProfileBlockReadSource` instead of the concrete Structures store.
- [x] Rendering retained-profile cutover accepted by CI at `e0cdc6109715dd8b93575119550538e0cac36ae6`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `VoxelBrush` consumes only Storage.Api read/mutation/authoring capabilities; no RegionTable/BrickPool/BrickRef/Region/VoxelAccess/physical occupancy types or public Table/Pool escape hatch remain. CastleBuilder, Showcase, parity tests, and editor capture construct Storage capability adapters explicitly.
- [x] VoxelBrush Storage.Api cut accepted by CI at `dd7d5013e00aba68f3b3d75193d10989de3f1537`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `ProfileBlockStore` moved physically under `Structures/Runtime` with its existing Unity GUID preserved; WorldGen Voxel's stale `VoxelEngine.Core.Features` imports were removed across the package so the old namespace no longer has an external-package dependency.
- [x] ProfileBlockStore Runtime move + WorldGen import cleanup accepted by CI at `00f1ddfc5ef505ecf780c4ee49ac13c8e3f2ca14`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `CastleBuilder` construction/orchestration boundary now accepts `IRegionReadSource` + `IRegionMutationStore` + `IMaterialAuthoringCatalogue`; `StepBuild` no longer receives unused `RegionTable`/`BrickPool` parameters and Showcase owns the concrete Storage adapter construction.
- [x] CastleBuilder Storage.Api orchestration seam accepted by CI at `87f5f5d10c537b3641032fd92ac6f0b57bad93a1`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Castle layout/query semantics (`CastlePlan`, gate/waterfall/river/bell-tower geometry) and shared material IDs moved to Structures.Api; WorldGen Voxel consumes these Api contracts and no longer references the broad Structures assembly.
- [x] Castle layout/material Api extraction accepted by CI at `e2945f5e424d73b78acb48b8ba9bf7ecf0b43c1d`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] CastleBuilder, VoxelBrush and MasonryWeathering moved physically under `Structures/Runtime` with existing Unity GUIDs preserved; only implementation PlayMode tests needed the Runtime assembly reference.
- [x] Top-level Structures Runtime move accepted by CI at `b4e6ec28754bfe1b57e82e24ccebd166f81d6fe3`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Empty broad `VoxelEngine.Structures` assembly deleted and stale Showcase/EditMode/PlayMode/CI Editor references removed; Structures now has only explicit Api/Runtime assemblies.
- [x] Broad Structures assembly removal accepted by CI at `6209dfd34cf4da6fe6f294c28a05462d196cd9c4`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Runtime implementation namespaces normalized from `VoxelEngine.Core.Features` / broad `VoxelEngine.Structures` to `VoxelEngine.Structures.Runtime` (plus `.Emitters`); engine-owned Showcase/CI/tests migrated and WorldGen remains Api-only.
- [x] Final Structures Runtime namespace cutover accepted by CI at `2d35d46a067d3860fc4889a6a0d99d5769ab3174`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Structures.Api/Runtime physical move and namespace cutover complete.

### Gate

- [x] no `VoxelEngine.Core.Features` namespace remains;
- [x] Kentridge catalogue builders compile against Structures.Api for extracted authoring contracts;
- [x] compatibility encoding file deleted;
- [x] feature parity/generation tests pass;
- [x] CastleBuilder is Runtime implementation, not public cross-system vocabulary;
- [x] no external package references Structures.Runtime.

---

# CUTOVER 5 — Edits

## 10. Exact file disposition

### Edits.Api

Split the public/canonical event and request vocabulary out of `AlterationEvent.cs` into focused files while preserving serialized numeric meanings:

```text
Edits/Api/AlterationEvent.cs
Edits/Api/CanonicalBrushType.cs
Edits/Api/AlterationFlags.cs
Edits/Api/ExpandedVoxelEdit.cs
Edits/Api/EditResult.cs
```

Move any brush descriptors that are part of gameplay/network commands into Api. Preserve enum values and deterministic serialization semantics exactly.

### Edits.Runtime

| Current | Target |
|---|---|
| `Core/Edits/AllocationBudget.cs` | `Edits/Runtime/AllocationBudget.cs` |
| `Core/Edits/BrushExpansion.cs` | `Edits/Runtime/BrushExpansion.cs` |
| `Core/Edits/BrushShapeCodec.cs` | `Edits/Runtime/BrushShapeCodec.cs` unless wire contract requires a small Api codec/value |
| `Core/Edits/BuildBrushes.cs` | `Edits/Runtime/BuildBrushes.cs` |
| `Core/Edits/DensityCap.cs` | `Edits/Runtime/DensityCap.cs` |
| `Core/Edits/DeterministicAlterationApplier.cs` | `Edits/Runtime/DeterministicAlterationApplier.cs` |
| `Core/Edits/DeterministicRandom.cs` | `Edits/Runtime/DeterministicRandom.cs` |
| `Core/Edits/ExplosionExpansion.cs` | `Edits/Runtime/ExplosionExpansion.cs` |
| `Core/Edits/RawBatchExpansion.cs` | `Edits/Runtime/RawBatchExpansion.cs` |

`DeterministicAlterationApplier` applies through Storage.Api `VoxelMutationWriter`; it must not manipulate BrickPool/RegionTable.

## 10.1 Delete redundant Net wrapper

Delete:

```text
Assets/VoxelEngine/Net/Server/ServerDeterministicAlterationApplier.cs
```

It currently delegates directly to the Core implementation and adds no domain value. Net/Composition should invoke the Edits capability directly.

## 10.2 Network contract

Net.Runtime serializes/deserializes Edits.Api events. It does not own the canonical edit domain model.

### Implementation progress

- [x] Edits.Api + Edits.Runtime assemblies created; canonical `AlterationEvent`/`AlterationEventKind` and canonical brush codec moved into Edits.Api with original Unity GUIDs preserved. The Api consumes only Storage.Api logical geometry constants.
- [x] Edits Api/assembly extraction accepted by CI at `2a1ffecf074216f22e8c183647c2509a576dce12`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `DeterministicAlterationApplier` no longer receives physical Storage types.
- [x] Net/client/server/test callers use `IRegionMutationStore` ownership explicitly.
- [x] uniform materialization rollback, mixed-to-uniform collapse, metadata-only and same-material no-op behavior covered by tests.
- [x] Edits.Runtime helper ownership advanced: `AllocationBudget`, `DeterministicRandom`, `BrushExpansion`, `BuildBrushes`, `RawBatchExpansion`, and `ExplosionExpansion` now live under Edits.Runtime with original GUIDs preserved; expansion consumes Storage.Api read views rather than RegionTable/BrickPool.
- [x] Edits Runtime helper/Explosion slice accepted by CI at `3887960c4728bddf5243c30093458f761564d25f`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Net protocol imports canonical Edits.Api only; stale `VoxelEngine.Core.Edits` protocol/server/client imports were removed and dead unused `Core/Edits/DensityCap.cs` was deleted rather than migrated.
- [x] Edits protocol/Core-cleanup slice accepted by CI at `17d7fbcee5007d4e46f7acfd0e7f431693396582`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `IAlterationApplier` lives in Edits.Api; authoritative server/session paths consume that capability by injection and the redundant Net `ServerDeterministicAlterationApplier` wrapper is deleted.
- [x] Server alteration-applier capability/wrapper deletion accepted by CI at `b4fbfb1839419810fe251ae2b12a0ab42877d108`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Net client alteration queue/application/repair paths consume `IAlterationApplier` instead of naming/importing the deterministic implementation; Net source now has no direct `VoxelEngine.Core.Edits` implementation dependency.
- [x] Client alteration capability cut accepted by CI at `62af2841cc933474379373fbe5ef8e7c07e096c0`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `DeterministicAlterationApplier` moved physically under `Edits/Runtime` with its original Unity GUID preserved; implementation namespace/imports and the storage-boundary guard path were updated; Net remains Api-only.
- [x] Final Edits Runtime move accepted by CI at `4477d1cf3285eb96411ca504021fb306993b9da7`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Edits.Api/Runtime physical move and namespace cutover complete.

### Gate

- [x] no `VoxelEngine.Core.Edits` namespace remains;
- [x] Net protocol depends on Edits.Api, not Edits.Runtime;
- [x] server wrapper deleted;
- [x] deterministic edit expansion/application parity tests pass;
- [x] Storage mutation implementation remains encapsulated behind Storage.Api.

---

# CUTOVER 6 — StructuralIntegrity

## 11. Exact file moves

| Current | Target |
|---|---|
| `Core/Structure/CollapseDetection.cs` | `StructuralIntegrity/Runtime/CollapseDetection.cs` |
| `Core/Structure/Connectivity.cs` | `StructuralIntegrity/Runtime/Connectivity.cs` |
| `Core/Structure/SupportField.cs` | `StructuralIntegrity/Runtime/SupportField.cs` |
| `Net/Server/StructuralGraph.cs` | `StructuralIntegrity/Runtime/StructuralGraph.cs` |

Create API types based on actual callers:

```text
StructuralIntegrity/Api/StructuralEvaluationRequest.cs
StructuralIntegrity/Api/CollapseResult.cs
StructuralIntegrity/Api/DetachedComponent.cs
StructuralIntegrity/Api/StructuralChange.cs
```

The exact result fields should carry semantic component/voxel information needed by gameplay/networking, never networking packet types.

## 11.1 Ownership flow

Preferred flow:

```text
voxel edit applied
    -> StructuralIntegrity evaluates affected support/connectivity
    -> returns CollapseResult / structural changes
    -> Edits applies resulting voxel removals/damage
    -> Net serializes authoritative domain results/events
```

StructuralIntegrity does not depend on Net. Net does not own the structural graph.

### Implementation progress

- [x] Unused `Net/Server/StructuralGraph.cs` placeholder deleted instead of moved; repository inventory showed zero callers, and preserving its unbounded region-graph implementation would have created API/Runtime surface with no domain consumer.
- [x] StructuralGraph deletion accepted by CI at `50df9bd2b3a731b6f85208c4272f81283119fd7e`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] StructuralIntegrity.Api/Runtime assemblies created; `SupportField` and `CollapseDetection` moved under Runtime with existing Unity GUIDs preserved and rewritten onto Storage.Api logical read views instead of RegionTable/BrickPool; the old Core copies are gone.
- [x] Support/collapse Runtime cut accepted by CI at `dbaa43648cb47008a55f27bbd692157e752cd8be`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] `Connectivity` moved under StructuralIntegrity.Runtime with its existing Unity GUID preserved and rewritten onto Storage.Api read views; the old `Core/Structure` folder is gone. Inventory found no production/network callers, so StructuralIntegrity.Api remains intentionally empty rather than inventing unused result DTOs.
- [x] Connectivity Runtime cut accepted by CI at `5e0a57023091023437644286fb6954a7853ee253`: 382 total / 369 passed / exact 13 known baseline failures.
- [x] Final StructuralIntegrity inventory/architecture guard accepted by CI at `a4883864756294577cd55870cbcb09038e1337fe`: 382 total / 369 passed / exact 13 known baseline failures.

### Gate

- [x] `StructuralGraph` no longer lives in Net; verified dead and deleted rather than migrated.
- [x] StructuralIntegrity.Runtime has no Net dependency.
- [x] collapse/connectivity/support tests pass against the established CI baseline.
- [x] network structural behavior consumes StructuralIntegrity.Api only; final repository inventory found no current production/network structural consumer, so no cross-boundary dependency exists.

---

# CUTOVER 7 — Tiering

## 12. Exact move

Move:

```text
Assets/VoxelEngine/Tiering/DeviceTierBudget.cs
    -> Assets/VoxelEngine/Tiering/Api/DeviceTierBudget.cs
```

Namespace:

```text
VoxelEngine.Tiering.Api
```

Create `VoxelEngine.Tiering.Api.asmdef`. Delete the existing `VoxelEngine.Tiering.asmdef`.

Do not create Tiering.Runtime until there is actual stateful/runtime policy implementation.

Update Streaming and Rendering to reference Tiering.Api only.

### Implementation progress

- [x] `DeviceTierBudget.cs` moved physically under `Tiering/Api` with its existing Unity GUID preserved; namespace normalized to `VoxelEngine.Tiering.Api` and `DeviceTier` remains with the policy as stable tier vocabulary.
- [x] `VoxelEngine.Tiering.Api` replaces the broad Tiering assembly; Api has no engine references, and the old broad asmdef/meta were deleted.
- [x] Streaming, Rendering, Showcase and test consumers migrated to `VoxelEngine.Tiering.Api`; no `Tiering.Runtime` assembly was created because there is no runtime implementation.
- [x] Tiering Api move accepted by CI at `64e445d6dd98235cd1b232064320121295491368`: 382 total / 369 passed / exact 13 known baseline failures.

### Gate

- [x] only Tiering.Api exists;
- [x] no Tiering assembly references Core;
- [x] device-tier tests compile/pass against the established CI baseline.

---

# CUTOVER 8 — Streaming

## 13. Current files

```text
Streaming/MipRefinement.cs
Streaming/Prefetch.cs
Streaming/RegionLoader.cs
Streaming/ResidencyManager.cs
```

Move all four to `Streaming/Runtime/` and create a deliberately small `Streaming/Api/`.

### Streaming.Api

Create:

```text
Streaming/Api/RegionLoadRequest.cs
Streaming/Api/RegionResidencyRequest.cs
Streaming/Api/RegionResidencyState.cs
Streaming/Api/RegionLoadPriority.cs
Streaming/Api/IRegionStreaming.cs
```

`IRegionStreaming` is orchestration-level and is appropriate here; streaming operations are not inner-loop Burst voxel reads.

## 13.1 `RegionLoader` rewrite

Current RegionLoader reaches directly into Core Features, Occupancy, Storage and Terrain. Rewrite its responsibility to orchestration:

```text
receive RegionLoadRequest
    -> ask Storage.Api for unpublished region/generation writer
    -> run Terrain generation via Terrain.Api/implementation supplied by Composition
    -> run structure/feature generation via Structures capability supplied by Composition
    -> finalize/publish through Storage.Api
    -> expose loaded/residency state through Streaming.Api
```

`RegionLoader` must not know:

- `BrickPool`
- `BrickRef`
- `Region`
- `RegionTable`
- `MipBuilder`
- `TerrainGenerator` concrete Runtime type
- `FeatureGeneration` concrete Runtime type

Runtime implementation instances are injected/wired by Composition through suitable Api-facing capabilities/delegates.

## 13.2 Remove Streaming -> Net

The current Streaming asmdef references Net, but direct inspection of the four Streaming source files found no legitimate networking ownership. Remove that reference completely.

Correct direction:

```text
Net.Runtime -> Streaming.Api
```

Net may translate connection/interest state into streaming residency requests. Streaming must function offline/headless without networking.

## 13.3 ResidencyManager

It owns desired residency/prefetch/fade policy. It releases regions through Storage.Api; it does not dispose internal `Region` structures itself.

### Implementation progress

- [x] Streaming residency/eviction mechanics consume `IRegionResidencyStore`; physical region/pool mechanics remain in Storage.
- [x] dead `BrickRef` completion payload removed and first-completion ring indexing regression covered.
- [x] existing Streaming assembly no longer references `VoxelEngine.Core`.
- [x] `Streaming.Api` exposes the actual orchestration surface used by current consumers: `RegionLoadRequest` plus `IRegionStreaming` enqueue/completion operations. Additional speculative DTOs from the original plan were not created because there are no callers for them.
- [x] `MipRefinement`, `Prefetch`, `RegionLoader`, and `ResidencyManager` moved physically under `Streaming/Runtime` with their existing Unity GUIDs preserved and Runtime namespaces normalized.
- [x] `RegionStreamingService` owns Storage residency behind `Streaming.Api`: `RegionLoader` no longer exposes `IRegionResidencyStore`, and `ResidencyManager`/eviction go through `IRegionStreaming`; Streaming has no Net dependency.
- [x] Streaming Runtime depends only on `Streaming.Api`, `Storage.Api`, and `Tiering.Api`; no Core/Net/Terrain.Runtime/Structures.Runtime engine reference remains.
- [x] Final Streaming Api/Runtime cutover accepted by CI at `3221f33697305658912193053861ee9197873238`: 382 total / 369 passed / exact 13 known baseline failures.

### Gate

- [x] Streaming.Runtime has no Net reference;
- [x] RegionLoader has no Storage.Runtime/Terrain.Runtime/Structures.Runtime compile reference;
- [x] streaming can be instantiated without Net;
- [x] residency/prefetch/mip refinement tests pass against the established CI baseline.

---

# CUTOVER 9 — Collision

## 14. File moves

| Current | Target |
|---|---|
| `Collision/DdaTraversal.cs` | `Collision/Runtime/DdaTraversal.cs` |
| `Collision/HullExport.cs` | `Collision/Runtime/HullExport.cs` |
| `Collision/SweptAabb.cs` | `Collision/Runtime/SweptAabb.cs` |
| `Collision/VoxelRaycast.cs` | `Collision/Runtime/VoxelRaycast.cs` |

Create Api query/result values:

```text
Collision/Api/VoxelRaycastQuery.cs
Collision/Api/VoxelRaycastHit.cs
Collision/Api/VoxelSweepQuery.cs
Collision/Api/VoxelSweepResult.cs
Collision/Api/HullExportRequest.cs   (only if called outside Collision)
Collision/Api/HullExportResult.cs    (only if called outside Collision)
```

Collision.Runtime performs DDA/sweep/hull work against Storage.Api readonly native views. The DDA and storage representation are implementation details.

### Implementation progress

- [x] `VoxelEngine.Collision.Api` + `VoxelEngine.Collision.Runtime` assemblies created; Runtime references only Collision.Api + Storage.Api + Foundation and required Unity native packages.
- [x] `DdaTraversal`, `HullExport`, `SweptAabb`, and `VoxelRaycast` moved physically under `Collision/Runtime` with their existing Unity GUIDs preserved and Runtime namespace normalized.
- [x] Collision implementation continues to consume only Storage.Api read views; no BrickPool/RegionTable/Storage.Runtime dependency was introduced.
- [x] Final consumer inventory found no current production subsystem caller for Collision implementation; Showcase/tests consume Runtime explicitly, while Collision.Api remains intentionally empty instead of inventing unused DTOs.
- [x] Empty broad `VoxelEngine.Collision` assembly deleted and test/Showcase consumers migrated to explicit Collision.Runtime.
- [x] Final Collision cutover accepted by CI at `2157915cae28f51eb159aa5464e847862ad07d1e`: 382 total / 369 passed / exact 13 known baseline failures.

### Gate

- [x] no Collision source references BrickPool/RegionTable/Occupancy Runtime types;
- [x] hot jobs operate on readonly Burst-compatible Storage.Api data views;
- [x] raycast/sweep/hull parity tests pass against the established CI baseline.

---

# CUTOVER 10 — Vegetation

## 15. Current files and namespace correction

Current top-level Vegetation files:

```text
ProceduralTreeDamageService.cs
ProceduralTreeSkeletonBuilder.cs
ProceduralTreeTypes.cs
TreeWorldState.cs
```

`ProceduralTreeTypes.cs` currently declares `VoxelEngine.Core.Vegetation` even though it physically lives in top-level Vegetation. Fix this now; no namespace-forwarding alias.

## 15.1 Vegetation.Api

Split public stable values from `ProceduralTreeTypes.cs` into focused Api files. Exact categories:

```text
Vegetation/Api/TreeId.cs
Vegetation/Api/TreeSpeciesId.cs or existing equivalent stable type
Vegetation/Api/TreePlacement.cs
Vegetation/Api/TreeMorphology.cs
Vegetation/Api/TreeRenderSnapshot.cs
Vegetation/Api/VegetationSpawnRequest.cs
Vegetation/Api/VegetationDamageRequest.cs
Vegetation/Api/VegetationDamageResult.cs
```

Reuse current names instead of inventing parallel names when the existing public type already represents one of these concepts cleanly.

The API needs to support:

- worldgen requesting deterministic vegetation/tree placement;
- gameplay requesting tree/vine damage later;
- Rendering reading immutable presentation/skeleton state;
- stable IDs for network/gameplay references.

Do not expose `TreeWorldState` mutable collections.

## 15.2 Vegetation.Runtime

Move:

```text
ProceduralTreeDamageService.cs
ProceduralTreeSkeletonBuilder.cs
TreeWorldState.cs
```

and any implementation-only types from `ProceduralTreeTypes.cs` to Runtime.

## 15.3 Kentridge integration

`KentridgeVegetationPlanner` becomes a client of:

```text
Storage.Api      -- top-surface/read query
Vegetation.Api   -- placement/spawn contract
```

It must not import `VoxelEngine.Core.Storage`, `VoxelEngine.Core.Vegetation`, or Vegetation.Runtime.

## 15.4 Rendering integration

Rendering currently references the whole Vegetation assembly. Change it to `VoxelEngine.Vegetation.Api` and consume immutable render/skeleton snapshots.

### Implementation progress

- [x] `VoxelEngine.Vegetation.Api` + `VoxelEngine.Vegetation.Runtime` assemblies created; broad Vegetation assembly remains only as a temporary implementation owner until mutable runtime files move.
- [x] Public vegetation vocabulary moved into `Vegetation.Api/ProceduralTreeTypes.cs` with its Unity GUID preserved and namespace normalized to `VoxelEngine.Vegetation.Api`.
- [x] `TreeWorldReadView`, `ITreeWorldReadSource`, and immutable `TreePresentationState` added to Vegetation.Api so Rendering consumes a read-only presentation contract instead of mutable world state.
- [x] `KentridgeVegetationPlanner` now consumes Vegetation.Api tree placement/profile types; no WorldGen Voxel reference to Vegetation.Runtime exists.
- [x] Vegetation.Api extraction/caller migration accepted by CI at `873fa5606b1e69c089e305792780bd652eb836ee`: 384 total / 371 passed / exact 13 known baseline failures.
- [x] `TreeWorldState` moved under `Vegetation/Runtime`; `TreeWorldRuntime` owns mutable tree state and implements `ITreeWorldReadSource`, while Rendering/Showcase receive only the Api read source.
- [x] Tree-world ownership/read boundary accepted by CI at `4fecc557f82b4cf86cf01cdc14560fd679af89df`: 384 total / 371 passed / exact 13 known baseline failures.
- [x] `ProceduralTreeSkeletonBuilder` and `ProceduralTreeDamageService` moved under `Vegetation/Runtime` with original Unity GUIDs preserved; Runtime namespace is `VoxelEngine.Vegetation.Runtime` and mutable topology/world-state logic remains implementation-only.
- [x] Damage results required by callers (`TreeBreakResult`, `TreeDamageResult`) plus immutable segment-removal data (`TreeSegmentRemoval`) moved to Vegetation.Api, while `ApplyBreak` mutates only Runtime state.
- [x] Rendering references Vegetation.Api only; all tree presenters/renderers/mesh builder consume `ITreeWorldReadSource` + Api tree values and have no Vegetation.Runtime/broad Vegetation assembly reference.
- [x] Broad `VoxelEngine.Vegetation` assembly deleted; Showcase/CI/tests use explicit Vegetation.Api/Runtime references as appropriate.
- [x] Final Vegetation Runtime/presentation cutover accepted by CI at `363ea1838c42d9d01e04fd0b74b6aa8f600c35f4`: 384 total / 371 passed / exact 13 known baseline failures.

### Gate

- [x] no `VoxelEngine.Core.Vegetation` namespace remains;
- [x] Kentridge vegetation references Api only;
- [x] Rendering references Vegetation.Api, not Runtime;
- [x] mutable `TreeWorldState` is internal Runtime state;
- [x] tree damage/skeleton/render tests pass against the established CI baseline.

---

# CUTOVER 11 — Net decomposition and ownership correction

## 16. Scope

Net is currently much broader than transport: it contains protocol DTOs, client prediction, server authoritative processing, world history/convergence, interest, region residency, a region store and structural graph logic. This cutover keeps networking/session/replication concerns in Net but removes world ownership that belongs elsewhere.

Create `Net/Api` and move the rest under `Net/Runtime/{Client,Interest,Protocol,Server,Transport}`.

## 16.1 Net.Api

Only expose session-facing application contracts that non-network systems genuinely call. Candidate fixed categories:

```text
Net/Api/NetworkSessionState.cs
Net/Api/ClientConnectionRequest.cs
Net/Api/ServerSessionRequest.cs
Net/Api/NetworkRole.cs
```

Do **not** move packet DTOs into Net.Api merely because they are public today. Wire protocol is a Net.Runtime implementation detail unless an external assembly truly serializes it.

## 16.2 Keep in Net.Runtime

### Client

Keep under `Net/Runtime/Client/`:

```text
AdaptiveFidelity.cs
AlterationBatchReceiver.cs
ClientAuthoritativeEventQueue.cs
ClientNetworkRuntime.cs
ClientPlayerStateTimeline.cs
ClientPredictionReconciler.cs
ClientRegionRepairAssembler.cs
ClientRegionStateAssembler.cs
ClientTickLoop.cs
DestructionInput.cs
EventApplication.cs
EventPacketReceiver.cs
InputBuffer.cs
PlacementCoalescer.cs
Reconciliation.cs
RejectionFeedback.cs
SpeculativeOverlay.cs
```

### Interest

Keep under `Net/Runtime/Interest/`:

```text
InterestFilter.cs
RegionSubscriptionIndex.cs
SimulationInterest.cs
```

These compute network/simulation interest. They may request world residency through Streaming.Api; they do not own Streaming.Runtime.

### Protocol

Keep all current packet/message codecs under `Net/Runtime/Protocol/`, including alteration, player state/input, region state/hash/repair/request/sync and envelope types.

Where protocol code currently duplicates Edits domain values, encode/decode `Edits.Api` values instead of creating a second canonical edit model.

### Server networking/replication

Keep server session, validation, command inbox/processor, rate limiting, replication, convergence, late join, event logs/history, protected-zone policy and transport-facing code under `Net/Runtime/Server/` unless another owner is explicitly identified below.

### Transport

Keep UTP channel/client/server/packet IO/throttle implementation under `Net/Runtime/Transport/`.

## 16.3 Move/delete ownership violations

### Delete

```text
Net/Server/ServerDeterministicAlterationApplier.cs
```

Deleted in Edits cutover; do not recreate it.

### Move

```text
Net/Server/StructuralGraph.cs
    -> StructuralIntegrity/Runtime/StructuralGraph.cs
```

Already moved in StructuralIntegrity cutover.

### Region residency

`Net/Server/RegionResidency.cs` must not own physical world residency. Split its behavior:

- network interest/subscription decisions remain Net;
- actual load/residency requests go through Streaming.Api;
- no RegionLoader/Storage Runtime dependency.

Rename the networking half if needed so it is not confused with Streaming's authoritative residency owner.

### Region store

`Net/Server/Storage/RegionStore.cs` may remain in Net only if it stores **replication/history snapshots** rather than authoritative voxel memory. Rename to:

```text
Net/Runtime/Server/ReplicationRegionStore.cs
```

If inspection during implementation shows it is acting as authoritative voxel storage, delete/replace that responsibility with Storage.Api instead. Do not create a second world store.

### Region hasher/snapshot/repair

Network convergence uses Storage.Api semantic snapshots/hash values. `Net/Server/RegionHasher.cs` may hash network payload framing, but authoritative semantic region hashing belongs to Storage.Runtime behind Storage.Api.

## 16.4 Net.Runtime target references

```text
Net.Api
Foundation
Edits.Api
Storage.Api
Streaming.Api
StructuralIntegrity.Api
Unity.Networking.Transport
Unity.Collections / Mathematics as required
```

No Storage.Runtime, Streaming.Runtime, StructuralIntegrity.Runtime, Edits.Runtime or Rendering reference.

### Implementation progress

- [x] Net.Api/Runtime assemblies exist and Client/Interest/Protocol/Server/Transport are physically under `Net/Runtime`; broad `VoxelEngine.Net` is deleted.
- [x] Server residency delegates to Streaming.Api and semantic convergence/repair uses Storage.Api capabilities; dedicated hosted ownership gate passed against the final Net source.
- [x] Net ownership checkpoint re-earned by the dedicated hosted ownership gate: residency delegates to Streaming.Api, semantic repair applies through Storage.Api, and no physical Storage types remain in Net.Runtime.
- [x] Runtime namespaces are normalized to `VoxelEngine.Net.Runtime.*`; final static architecture gate accepted the physical move, namespace cutover and absence of package Runtime references.
- [x] Final Net static architecture gate passed at `8dafd264dfd3e228e833da23c258d9e21768ad98`; final post-repair behavioral acceptance passed at `ed126903cd18dcd62324fab41942d41fdaa37532` with 384/371 and the identical 13 known failures.

### Gate

- [x] Net.Runtime references only the explicit domain/API allowlist; final static architecture gate passed at `8dafd264dfd3e228e833da23c258d9e21768ad98`;
- [x] structural graph is gone from Net;
- [x] no duplicate deterministic edit applier wrapper;
- [x] network residency calls Streaming.Api; dedicated hosted ownership gate passed;
- [x] semantic repair/snapshot paths use Storage.Api logical data; dedicated hosted ownership gate passed;
- [x] protocol/convergence/late-join/reconciliation tests pass against the accepted 384/371/13 baseline at `871fd663ac9c57d8e001ce2ff11f5ac30df242f0` (isolated run `31892491967`).

---

# CUTOVER 12 — Rendering

## 17. Ownership

Rendering is a sink. It observes world/vegetation/tier state and produces presentation. Simulation systems do not reference Rendering.Runtime.

Move all current rendering implementation under `Rendering/Runtime/`, preserving subfolders:

```text
Irradiance/
RenderFeature/
Shaders/
SurfaceExtraction/
Vegetation/
```

This includes the GPU/CPU surface caches/extractors, Transvoxel jobs/tables, water surface extraction, render pass/feature, sky pass, GPU buffers, timing, tree mesh/presenters/renderers and renderer-specific materials.

## 17.1 Rendering.Api

Keep this intentionally small. Move/create only types required by scene/application clients to register/configure/present the renderer, for example:

```text
Rendering/Api/VoxelPresentationSettings.cs
Rendering/Api/IRenderWorldBridge.cs
```

Do not publish surface extraction caches/jobs/shader data just to avoid changing showcase/bootstrap code. Composition is allowed to instantiate Runtime implementations.

## 17.2 Runtime inputs

Rendering.Runtime consumes:

```text
Storage.Api       -- readonly versioned region/voxel/occupancy views
Tiering.Api       -- device/tier policy values
Vegetation.Api    -- immutable tree/vegetation render snapshots
Foundation
Unity render-pipeline packages
```

It must not consume Terrain, Structures, Edits, Net or Vegetation.Runtime.

## 17.3 Surface scheduler

Rewrite `SurfaceExtraction/VoxelSurfaceScheduler.cs` and related caches/jobs so their input is Storage.Api read views. They may retain zero-copy native performance, but may not take `BrickPool`, `RegionTable`, mutable `Region`, or Storage.Runtime occupancy builders.

### Implementation progress

- [x] `VoxelWorldView` exposes Storage.Api read capability rather than `RegionTable`/`BrickPool`.
- [x] CPU Transvoxel, water extraction and surface discovery consume Storage.Api views.
- [x] Rendering physical-storage boundary guards and parity/equivalence tests accepted.
- [x] Rendering retained-profile readers consume Storage.Api `IProfileBlockReadSource`; no direct `ProfileBlockStore` dependency remains.
- [x] Unused/representation-leaking `ProbeCache` and `VoxelGpuBuffers` implementations removed; Rendering no longer carries those `RegionTable`/`BrickPool`/`BrickRef` paths.
- [x] Rendering dead-leak cleanup accepted at `de24d20ce48e9665f4ab7ab78343e7354601eab0`: 384 total / 371 passed / exact 13 known baseline failures.
- [x] Rendering.Api/Runtime physical move and Runtime namespace cutover complete; broad root assembly/namespace is gone. Static acceptance run `31894268246` passed against source `f5e0b646102a50305424850a0508d190bae3e44d`.
- [x] Rendering catalogue/change-feed consumers and tooling use Storage.Api read-facing views; Rendering.Runtime no longer references `VoxelEngine.Core`.
- [x] Rendering behavioral parity accepted at `f5e0b646102a50305424850a0508d190bae3e44d`, isolated run `31894170304`: 384 total / 371 passed / exact 13 known baseline failures.

### Gate

- [x] Rendering.Runtime has no Storage.Runtime/Vegetation.Runtime ref; static acceptance run `31894268246` verified the Runtime asmdef consumes only Storage.Api, Tiering.Api, Vegetation.Api plus Unity packages;
- [x] surface extraction works from versioned readonly views;
- [x] renderer is not referenced by simulation Runtime assemblies; dedicated reverse-dependency scan passed in static acceptance run `31894268246`;
- [x] targeted GPU/CPU surface and rendering parity tests pass; isolated EditMode acceptance run `31894170304` against source `f5e0b646102a50305424850a0508d190bae3e44d` produced 384 total / 371 passed / exact 13 known baseline failures;
- [x] artifact/lookdev tests remain explicit/manual unless separately changed; static acceptance run `31894268246` verifies the Showcase GPU/lookdev test remains `[Explicit]`.

---

# CUTOVER 13 — Composition, external clients and Core deletion

## 18. Composition

Create:

```text
Assets/VoxelEngine/Composition/VoxelEngine.Composition.asmdef
Assets/VoxelEngine/Composition/VoxelEngineBootstrap.cs
```

Composition is allowed to reference concrete Runtime assemblies for construction/wiring. Keep it nearly logic-free.

Responsibilities:

- allocate/create Storage.Runtime owner;
- create Terrain/Structures/Edits/StructuralIntegrity/Streaming/Collision/Vegetation/Net/Rendering implementations;
- inject Api capabilities into consumers;
- bind scene/showcase/application lifecycle;
- own top-level disposal order.

No domain algorithm belongs here.

Scene/showcase code should either:

1. call a narrow Composition/bootstrap entry point, or
2. consume public Api types for application-level interactions.

Do not keep direct Runtime references scattered through scene code merely because Composition is an exception.

## 18.1 MountingForce WorldGen final asmdef state

Preserve:

```text
MountingForce.WorldGen.Core
    -> no VoxelEngine references

MountingForce.WorldGen.Architecture
    -> MountingForce.WorldGen.Core only (plus required Unity/math packages)
```

Target `MountingForce.WorldGen.Voxel.asmdef` engine references:

```text
VoxelEngine.Storage.Api       # surface/read placement queries if still needed
VoxelEngine.Terrain.Api       # terrain adaptation/placement
VoxelEngine.Structures.Api    # compiled feature catalogue authoring
VoxelEngine.Vegetation.Api    # vegetation placement/spawn contracts
```

Only include an Api reference if current source uses it after refactor. No `VoxelEngine.*.Runtime` reference.

### Implementation progress

- [x] `MountingForce.WorldGen.Core` and `MountingForce.WorldGen.Architecture` remain engine-free.
- [x] `MountingForce.WorldGen.Voxel` broad `VoxelEngine.Core` reference removed at `4599efb83f1ccd95382711be4f229ae2bb344163`; hosted gate `31894519041` verifies its engine references are exactly Storage.Api, Terrain.Api, Structures.Api and Vegetation.Api.
- [x] Physical Core deletion accepted: Storage/Occupancy moved to `Storage.Runtime`, Terrain moved to `Terrain.Runtime`, and no `VoxelEngine.Core` assembly or source namespace remains. Static gate `31894801235`; final exact behavioral gate `31895124610`.
- [x] Functional `VoxelEngine.Composition` Storage bootstrap/lifetime created and accepted at source `6dac3e29d171bf6a8fe7a5538717e4beabdbdaef`: Composition owns `RegionTable`/`BrickPool`/change journal plus Storage.Runtime adapters while its public lifetime surface exposes Storage.Api capabilities only. Static + EditMode gate `31895983180`; 386 total / 373 passed / exact same 13 known failures.
- [x] `FarFieldStructureStore` and both Showcase far-field capture call sites now consume `IRegionReadSource` / `RegionReadView` rather than `RegionTable`/`BrickPool`; accepted by gate `31895983180`.
- [x] Empty `VoxelEngine.Tools` / `VoxelEngine.Tools.Features` assembly shells deleted at `a4f80c5d96397e6de64720c3b3608b6e81fb27a5`; hosted pre-delete inventory found no source and no foreign references, and gate `31896191600` preserved the 386/373/13 baseline.
- [x] `CharacterMotor` no longer imports Storage.Runtime or calls `VoxelAccess`; character overlap reads use `IVoxelSurfaceQuery` exposed by ShowcaseWorld, accepted at source `40007bc9e7ae074223bef71619c1e070a38a7d54`, run `31896455076` (386/373/13, exact known failure set).
- [x] `ShowcaseNetworkWorldBridge` no longer imports Storage.Runtime or reaches `RegionTable`/`Region`; dirty-state/change publication is encapsulated behind API-vocabulary methods on `ShowcaseWorld.StorageBridge`, accepted at source `b7c23caaee6447c2746c101d66f89b4092682c00`, run `31896650921` (386/373/13, exact known failure set).
- [x] `GpuDebrisSystem` no longer imports Storage.Runtime; its coating constants come from Storage.Api. Source `ec5aa7c2950528afb90faab0437cc263cd355759`, run `31897300218` preserved 386/373/13 with the exact known failure set.
- [x] `ShowcaseCatalogue` no longer imports Structures.Runtime; it consumes `Structures.Api.FeatureCatalogue` and the Kentridge catalogue builder only. Source `ab2d1ca4323cc9fd6bdbee814007b716de76c5cd`, run `31897445992` preserved 386/373/13 with the exact known failure set.
- [x] `ShowcaseMultiplayerSession` no longer imports Storage.Runtime; logical block geometry and empty-material constants come from `VoxelReadGrid` / `VoxelGrid`. Source `4a5b486ca0fb238bf59b7b4b8f65e636d57746e7`, run `31900276764` preserved 387/374/13 with the exact known failure set.
- [x] Integrated scene-wiring slice accepted at source `a0528159ada889c59190888840523e6fb8c05a10`, run `31899261112`: mixed-brick capacity sizing is Composition-owned, `TerrainLookdev.Environment` delegates renderer globals through Composition, `CompactFpsOverlay` consumes `Rendering.Api.SurfaceTimingDiagnostics`, `VoxelShowcase` uses Storage.Api surface reads instead of `VoxelAccess`, and repeated TerrainLookdev physical storage adapter construction/publication was consolidated into the owning partial. Result: 387 total / 374 passed / exact same 13 known failures.
- [x] `ShowcaseTreePopulation` no longer imports Rendering.Runtime, Structures.Runtime, or Vegetation.Runtime; current world/seed access, castle planning, and tree-world publication route through narrow Composition entry points returning/accepting Api values only. Source `892b8d5d3fd1fb80d9226ffcb1cb4821be22efb7`, run `31906539037`: 387 total / 374 passed / exact same 13 known baseline failures, zero C# compiler errors.

## 18.2 Delete Core

At this point `Assets/VoxelEngine/Core` must be empty except Unity `.meta` artifacts waiting for cleanup.

Delete:

```text
Assets/VoxelEngine/Core/VoxelEngine.Core.asmdef
Assets/VoxelEngine/Core/
```

Then repository-wide search must return zero source/asmdef references for:

```text
VoxelEngine.Core
VoxelEngine.Core.Storage
VoxelEngine.Core.Occupancy
VoxelEngine.Core.Terrain
VoxelEngine.Core.Edits
VoxelEngine.Core.Features
VoxelEngine.Core.Structure
VoxelEngine.Core.Vegetation
```

Do not retain Core as a forwarding facade.

### Gate

- [x] Core folder and asmdef deleted at `0027e6a64f137763b994d304eac9621071e1ea3d`; static Core/Storage gate `31894801235` and exact behavioral gate `31895124610` accept the deletion with no forwarding facade;
- [x] no source namespace begins `VoxelEngine.Core`; static Core/Storage gate `31894801235` verifies this after physical deletion;
- [ ] all production asmdefs satisfy dependency guard;
- [x] semantic WorldGen assemblies still have no VoxelEngine refs; hosted Cutover 13 WorldGen gate `31894519041` verified Core/Architecture remain engine-free after source `4599efb83f1ccd95382711be4f229ae2bb344163`;
- [ ] Composition is the only runtime-wiring exception.

---

## 19. Kentridge/worldgen integration matrix

The post-merge worldgen package is deliberately **not** folded into VoxelEngine. Its semantic layers already have the correct architectural direction.

| Worldgen area | Engine dependency after refactor |
|---|---|
| semantic settlement/location definitions | none |
| Kentridge town planning / urban skeleton / organization / circulation | none |
| civic/processional/vertical/frontage/gallery/skybridge planning | none |
| semantic vegetation layout | none |
| architecture grammars that remain semantic | none |
| Voxel catalogue compilation | Structures.Api |
| terrain-aware vertical/plot realization | Terrain.Api |
| top-surface lookup for vegetation placement | Storage.Api |
| tree/vegetation spawn descriptors | Vegetation.Api |
| rendering | none; renderer consumes engine/world state independently |

The renderer must never know what Kentridge, an inn, a church, a quest role, or a civic axis is.

Kentridge's stable semantic identities remain independent from generated coordinates. This refactor must not collapse semantic worldgen into the voxel engine merely because the Voxel adapter is being updated.

LayerProcGen evaluation remains a later WorldGen concern and is not introduced by this architecture cutover.

---

## 20. Material ownership decision for this refactor

Do **not** create a new Materials subsystem during the first boundary refactor unless compile-time evidence requires it.

For the clean cutover:

- canonical authoritative voxel material byte and surface semantics stay in Storage.Api;
- mutable palette/catalogue/adjacency storage stays in Storage.Runtime;
- renderer-specific material/presentation mapping stays in Rendering.Runtime;
- semantic `MountingForce.WorldGen.MaterialRole` stays in WorldGen.Core;
- the WorldGen Voxel adapter translates semantic roles to engine material IDs at the boundary.

After Core is gone and dependency guards are active, reassess whether simulation properties shared by future Fire/Water/Collision justify `Materials.Api`. Do not preemptively make a `Materials` junk drawer now.

---

## 21. API design constraints during implementation

### Api does not mean interfaces everywhere

Use:

- readonly/blittable structs;
- IDs/handles without owner internals;
- `NativeArray`-compatible views;
- immutable descriptors;
- request/result values;
- commands/events;
- orchestration interfaces only where virtual dispatch is not an inner-loop cost.

Avoid generic abstractions such as `IVoxel`, `IBrick`, `IRegion` unless they model a real capability.

### Read vs mutation must remain separate

A caller that only renders/collides/places vegetation gets read capability. It must not receive a general storage object that also exposes writes.

### Ownership/lifetime must be explicit

For every Native view/handle, document:

- who allocated backing memory;
- who disposes it;
- whether the caller may retain it;
- what mutation/unload invalidates it;
- how version changes are observed.

### Public does not necessarily mean architectural API

Unity serialization may require public classes in Runtime. The asmdef boundary is authoritative: foreign systems cannot reference Runtime even if a concrete type has to be `public` for Unity/editor reasons.

---

## 22. Validation strategy per cutover

Every completed cutover runs:

1. Unity compile/domain reload.
2. `VoxelEngineAssemblyBoundaryTests`.
3. `ConstitutionGuardTests` deterministic checks relevant to moved code.
4. subsystem EditMode tests.
5. cross-system parity tests affected by that cutover.
6. targeted PlayMode tests for changed runtime paths.

Do not use a full monolithic PlayMode run as the only per-cutover gate on this baseline. Current master documents a native-memory teardown problem: the full run reaches the process memory ceiling while repeatedly loading the showcase/large BrickPool. Fix that teardown separately; until then use targeted PlayMode filters and the deterministic/architecture suites as the cutover gates.

Lookdev/artifact capture tests are explicit by design and should be run deliberately when a rendering/visual-generation cutover could change output.

### High-value parity suites by cutover

| Cutover | Required focus |
|---|---|
| Storage | voxel storage, snapshots/hashes, occupancy/mips, mutation semantics |
| Terrain | deterministic terrain samples/generation |
| Structures | feature VM/catalogue/shape encoding, castle/Kentridge catalogue parity |
| Edits | brush expansion, deterministic alteration application, network event parity |
| StructuralIntegrity | support/connectivity/collapse |
| Streaming | region load/residency/prefetch/mip refinement |
| Collision | DDA/raycast/sweep/hull |
| Vegetation | tree skeleton/damage/state + Kentridge placement |
| Net | protocol, convergence, late join, repair, reconciliation |
| Rendering | surface extraction CPU/GPU parity, vegetation presentation, targeted showcase |

---

## 23. Final dependency review checklist

At the end, generate an asmdef dependency report and verify:

```text
[ ] every exposed subsystem has one Api directory/assembly
[ ] no Api references Runtime
[ ] no Runtime references another subsystem Runtime
[ ] Composition is the only production Runtime-wiring exception
[ ] Streaming has no Net reference
[ ] Net references Streaming.Api, not Streaming.Runtime
[ ] Rendering references Storage.Api, Tiering.Api, Vegetation.Api only
[x] WorldGen.Core references no VoxelEngine assembly
[x] WorldGen.Architecture references no VoxelEngine assembly
[x] WorldGen.Voxel references only VoxelEngine Api assemblies
[x] no VoxelEngine.Core assembly remains
[x] no VoxelEngine.Core namespace remains
[ ] no compatibility/legacy adapter was introduced to preserve the old architecture
```

---

## 24. Execution checklist in order

### 0. Guardrails

- [x] add asmdef boundary guard (`ArchitectureBoundaryGuardTests`); introduced at `fcfea314` and still enforced by the accepted integrated architecture gates
- [x] convert Constitution determinism scan from Core path to explicit deterministic assembly/path policy; introduced at `9223509c` and now scans Foundation/Storage/Terrain/Edits/StructuralIntegrity explicitly
- [x] strengthen Kentridge boundary tests; `WorldGenAssemblyBoundaryTests` now guards semantic source, Voxel adapter Runtime refs, and physical Storage leakage from castle vegetation placement

### 1. Foundation

- [x] create Foundation assembly; `VoxelEngine.Foundation` introduced at `60c3f0abcf8c023a310a473b2e756a2982b1d178`
- [x] move `IntMath`; clean-moved to `VoxelEngine.Foundation` at `6986b7ccadfa983e6ffec81115f5702d29fbca63`
- [x] update Foundation references; all surviving consumers compile against `VoxelEngine.Foundation` and later integrated 387/374/13 gates preserve the accepted boundary

### 2. Storage

- [x] create Storage.Api/Runtime asmdefs
- [x] move `VoxelCell` logical value types to Api
- [x] split public grid constants from private brick layout
- [x] create region/read/generation/mutation/surface-query/snapshot contracts
- [x] move BrickPool/BrickRef/Region/RegionTable/VoxelAccess to Runtime
- [x] move Occupancy implementation to Storage.Runtime
- [x] move semantic hash/snapshot implementation to Runtime
- [ ] update all existing consumers to Storage.Api
- [x] move Kentridge vegetation top-surface reads to Storage.Api
- [ ] remove every foreign Storage.Runtime reference

### 3. Terrain

- [x] create Terrain.Api/Runtime
- [x] move deterministic query contract to Api
- [x] move TerrainGenerator/Sampler implementation to Runtime
- [x] generate through Storage.Api writer
- [x] update Structures/Streaming/WorldGen callers

### 4. Structures

- [x] create Structures.Api/Runtime
- [x] move compiled feature authoring format to Api
- [x] move feature VM/generation/rasterization to Runtime
- [x] move existing CastleBuilder/etc. under Runtime
- [x] rename `CatalogueLoader` to `FeatureCatalogueBuilder`
- [x] update Kentridge Voxel catalogue builders
- [x] delete `KentridgeShapeProgramCompatibility.cs`
- [x] verify canonical shape encoding tests

### 5. Edits

- [x] create Edits.Api/Runtime
- [x] split canonical alteration domain values into Api
- [x] move expansion/apply implementation to Runtime
- [x] route mutations through Storage.Api
- [x] delete `ServerDeterministicAlterationApplier`
- [x] update Net protocol to Edits.Api

### 6. StructuralIntegrity

- [x] create Api/Runtime
- [x] move collapse/connectivity/support algorithms
- [x] delete unused StructuralGraph from Net after zero-caller inventory (no Runtime replacement needed)
- [x] verify no current production structural-result consumer; keep StructuralIntegrity.Api empty instead of inventing unused result DTOs
- [x] verify current Edits/collapse ownership: structural parity exercises Runtime detection while voxel removals remain outside StructuralIntegrity mutation ownership

### 7. Tiering

- [x] create Tiering.Api
- [x] move DeviceTierBudget
- [x] delete old Tiering asmdef

### 8. Streaming

- [x] create Streaming.Api/Runtime
- [x] move four implementation files
- [x] expose RegionLoader orchestration through `IRegionStreaming`; `RegionStreamingService` hides Storage residency/loader internals behind Api
- [x] remove Streaming -> Net
- [x] verify no live Net caller; Streaming.Api remains the available residency boundary for future Net interest integration

### 9. Collision

- [x] create Collision.Api/Runtime
- [x] move DDA/raycast/sweep/hull implementation
- [x] consume Storage.Api readonly views

### 10. Vegetation

- [x] create Vegetation.Api/Runtime
- [x] remove `VoxelEngine.Core.Vegetation` namespace
- [x] isolate tree state/build/damage implementation
- [x] expose stable placement/damage/render contracts
- [x] update Kentridge vegetation to Storage.Api + Vegetation.Api
- [x] update Rendering to Vegetation.Api

### 11. Net

- [x] create Net.Api/Runtime
- [x] move client/interest/protocol/server/transport implementation
- [x] remove structural/storage/residency ownership violations
- [x] verify no second authoritative Net region store remains; dead physical persistence/compaction scaffolding was deleted
- [x] reference only domain APIs

### 12. Rendering

- [x] create Rendering.Api/Runtime
- [x] move all extraction/render/tree presentation implementation to Runtime
- [x] consume Storage/Tiering/Vegetation Api only
- [x] keep Rendering.Api minimal

### 13. Composition and final cleanup

- [x] create Composition assembly/bootstrap
- [x] route `ArchLookdev` rendering/presentation access through `RenderingComposition`; accepted at `8780cffe66a0e3e4ba75b524957d00b481ace971`, run `31907252082` with exact 387/374/13 baseline and zero compiler errors
- [x] route `ArchLookdev` structure authoring through `StructuresComposition`; accepted at `05de54c3`, run `31907948874` with exact 387/374/13 baseline and zero compiler errors
- [x] route `ShowcaseMultiplayerSession` through Composition-owned network facades; accepted at `28a3fda2`, run `31908243915` with exact 387/374/13 baseline and zero compiler errors
- [ ] centralize concrete runtime wiring/disposal
- [ ] remove scattered scene Runtime coupling where practical
- [x] update WorldGen.Voxel asmdef to exact Api refs
- [x] delete Core asmdef/folder
- [ ] remove all architecture-test temporary exceptions
- [ ] repository-wide zero-match check for `VoxelEngine.Core`
- [ ] final targeted/full-available validation

---

## 25. Definition of done

The architecture refactor is done when a new subsystem cannot accidentally reach into another subsystem's implementation simply by adding a `using` statement.

Concretely:

- Storage representation is private.
- Terrain is a producer/query subsystem, not a shared static utility namespace.
- Structures exposes a deliberate compiled-authoring format while hiding its evaluator/runtime.
- Edits owns deterministic mutation semantics.
- Structural integrity is not networking code.
- Streaming is independent of networking.
- Collision reads through Storage Api views.
- Vegetation owns tree state and exposes immutable/stable contracts to worldgen/rendering/gameplay.
- Net translates/replicates domain state but does not own voxel, streaming or structural state.
- Rendering is a sink.
- semantic MountingForce worldgen remains engine-independent until the Voxel adapter boundary.
- Composition is the single place allowed to know concrete runtime implementations.
- `VoxelEngine.Core` is gone rather than renamed.
- tests enforce the dependency graph so the old shape cannot silently grow back.
