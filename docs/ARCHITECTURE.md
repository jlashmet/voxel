# Voxel Engine Architecture

This document describes the post-refactor production architecture. The migration history and per-cutover evidence remain in `ARCHITECTURE_IMPLEMENTATION_PLAN.md`; this file is the steady-state contract.

## Dependency law

Every engine subsystem is split into a stable `Api` assembly and, where implementation code exists, a `Runtime` assembly.

- `*.Api` never references `*.Runtime`.
- A subsystem `Runtime` never references another subsystem's `Runtime`.
- Cross-system dependencies use the target subsystem's `Api` assembly.
- `VoxelEngine.Composition` is the only production assembly allowed to reference multiple concrete Runtime assemblies.
- Scene/application assemblies do not reference Runtime assemblies or Runtime namespaces directly.
- `VoxelEngine.Core` no longer exists and must not be reintroduced.

These rules are executable architecture: EditMode boundary tests scan assembly definitions and production source so a forbidden dependency fails CI.

## Subsystems

The engine is organized around these directed subsystem boundaries:

- `Foundation` — deterministic shared primitives such as integer math; no engine ownership.
- `Storage.Api` / `Storage.Runtime` — logical voxel values and read/write/residency/snapshot capabilities versus physical brick pools, region tables, occupancy, catalogues, and mutation mechanics.
- `Terrain.Api` / `Terrain.Runtime` — deterministic terrain queries and generation through Storage API writers.
- `Structures.Api` / `Structures.Runtime` — compiled structure authoring contracts versus feature evaluation, rasterization, castle authoring, and implementation catalogues.
- `Edits.Api` / `Edits.Runtime` — deterministic alteration values versus brush expansion/application.
- `StructuralIntegrity.Api` / `StructuralIntegrity.Runtime` — support/connectivity/collapse algorithms.
- `Tiering.Api` — device-tier budget contracts.
- `Streaming.Api` / `Streaming.Runtime` — region streaming and residency orchestration without networking ownership.
- `Collision.Api` / `Collision.Runtime` — read-only collision queries over Storage API views.
- `Vegetation.Api` / `Vegetation.Runtime` — stable placement/damage/render contracts versus tree state/build/damage implementation.
- `Net.Api` / `Net.Runtime` — replication/protocol/transport over domain APIs; networking does not own voxel, streaming, or structural state.
- `Rendering.Api` / `Rendering.Runtime` — rendering is a sink; it consumes Storage/Tiering/Vegetation APIs and does not expose its implementation as a dependency surface.
- `Composition` — allocation, concrete implementation selection, lifetime wiring, Unity-facing application composition, and Runtime bridging.

## Storage boundary and hot paths

Storage representation is private. `BrickPool`, `BrickRef`, `Region`, `RegionTable`, occupancy structures, and physical catalogue implementations remain in `Storage.Runtime` and may be wired directly only inside Composition.

Ordinary consumers use narrow capabilities from `Storage.Api`:

- readonly region/voxel views;
- mutation and generation stores;
- residency and surface queries;
- semantic snapshot/hash contracts;
- material/surface/coating presentation or authoring views where explicitly required.

Hot jobs do not perform per-voxel virtual dispatch. Interfaces are used to acquire or orchestrate a capability outside the inner loop; jobs receive blittable/native read views and operate directly on those views.

The packed read transfer format is an API-owned representation rather than a promise about the physical brick allocator. That lets Storage.Runtime change its internal representation without changing Rendering, Collision, WorldGen, or other consumers.

## Composition and lifetime ownership

`VoxelEngineBootstrap` is the top-level concrete construction boundary. `StorageRuntimeLifetime` owns the physical region table, brick pool, storage catalogues, adapters, change journal, and their disposal. It exposes only Storage API capabilities to callers.

The Showcase concrete world implementation is physically owned by `Assets/VoxelEngine/Composition/Showcase`. Scene code under `Assets/Scenes` is an API-oriented application/presentation shell. Concrete world construction, physical storage access required by Composition-owned hot paths, structure Runtime bridging, and shutdown stay behind the Composition assembly boundary.

Application shutdown is explicit: in-flight generation is completed/released, application-owned persistent catalogues are disposed, then the Composition-owned storage lifetime is disposed. Storage disposal is idempotent.

## World generation boundary

The MountingForce world-generation package remains semantically independent of the engine:

- `MountingForce.WorldGen.Core` owns high-level settlement/content intent and references no `VoxelEngine` assembly.
- `MountingForce.WorldGen.Architecture` owns architectural detail generation and references no `VoxelEngine` assembly.
- `MountingForce.WorldGen.Voxel` is the engine adapter and references only explicitly approved `VoxelEngine.*.Api` assemblies.

For Kentridge specifically, settlement and massing intent stay under `Runtime/Content/Kentridge`; detailed building and urban-fabric grammars are physically owned by `Runtime/Architecture/Kentridge`. Voxel catalogues and terrain/vegetation realization stay at the Voxel adapter boundary. Migration-marker duplicate grammar files are forbidden by tests.

## No compatibility layer

This refactor is a clean cutover. The architecture does not preserve `VoxelEngine.Core`, forwarding namespaces, legacy Runtime-to-Runtime adapters, or duplicate compatibility types. When ownership moves, callers move to the new API and the old architectural surface is deleted.

## Enforcement

The key steady-state CI guards are:

- `ArchitectureBoundaryGuardTests` — API/Runtime and directed subsystem boundaries.
- `ProductionArchitectureClosureTests` — Composition-only Runtime wiring, scene source isolation, physical Showcase ownership, and no `VoxelEngine.Core` return.
- `ConstitutionGuardTests` — deterministic-source and device-tier invariants.
- `WorldGenAssemblyBoundaryTests` — engine-independent Core/Architecture and API-only Voxel adapter dependencies.
- `KentridgeArchitectureBoundaryTests` and `KentridgeSourceOwnershipTests` — Kentridge semantic/engine split and physical grammar ownership.

A cutover is accepted only when Unity compiles cleanly and the known EditMode baseline remains identical by failed-test name, not merely by failure count. The current refactor baseline is 387 tests: 374 pass and 13 pre-existing failures remain unchanged.
