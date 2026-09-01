# 06 Gameplay-state replication — implementation plan

**Target module:** `Assets/Game/GameplayReplication/Api` / `Runtime` (`Game.GameplayReplication.Api`, `Game.GameplayReplication.Runtime`). Existing voxel/network transport remains underneath when its production spine is available.

## Inventory / ownership

- `Packages/manifest.json` includes `com.unity.transport` 6.5.0 and intentionally does not include NGO, matching the approved transport direction.
- Repository/source inventory on the current `fixes/agent-2` head found no production UTP/server-authoritative spine to reuse: no `NetworkDriver`, `DataStreamWriter`/`DataStreamReader`, connection/session loop, authoritative network tick, gameplay serialization/frame codec, snapshot/delta publication, late-join/catch-up, repair/resync, prediction/reconciliation, or interest-management implementation is present under `Assets/`.
- This is an external prerequisite blocker for transport-dependent acceptance (`T06-010`, `T06-016` repair integration, `T06-017`, `T06-018`, `T06-022`–`T06-024`). Acceptance is unchanged. Agent-2 will not create a second transport or silently invent the missing network spine.
- Independent work remains valid: define engine-neutral replication contracts/revision semantics/readiness, deterministic semantic projection state, subsystem adapters over existing gameplay APIs, and headless regressions. Runtime transport adaptation stays behind an explicit boundary so the missing UTP spine can plug in without changing gameplay contracts.
- Existing gameplay authority remains in its owning modules. Current master exposes semantic read seams for Characters (`ICharacterQuery`), Encounters (`IEncounterQuery`), Combat (`ICombatService`) and Inventory (`IInventoryRuntime.Snapshot`). No `Game.Vitality`, `Game.Progression`, Sessions/Continuity, or Outcome module is present on current master, so those projection tasks remain blocked on their owning APIs rather than being recreated here.

## API

Authoritative gameplay revision, typed snapshot/delta contracts for registered subsystem projections, synchronization/readiness state, and read-only client semantic state. Do not expose transport packet types as gameplay contracts.

A coherent authoritative revision is one publication barrier: all projections captured by a publication share the same monotonic gameplay revision. Delta revisions must be exactly current+1; duplicate/older publications are ignored, and forward gaps transition client state to `RepairRequired`. A full snapshot may jump directly to a newer revision and replaces current replicated projection truth, providing the transport-independent repair/late-join/reconnect convergence primitive.

Subsystem identity/versioning is semantic and stable. `GameplayProjectionId` plus schema version define compatibility; transport encoding is private. Projection producers implement `IGameplayProjectionSource` through adapters/composition; owning gameplay modules never depend on replication Runtime.

`GameplayReady` is configuration-driven through required projection descriptors. It is true only while synchronized and every required descriptor has a compatible current projection. Transport/socket connectivity is not part of the API and therefore cannot satisfy readiness by itself.

## Runtime

1. `Game.GameplayReplication.Api` and `Runtime` are engine-neutral asmdefs. Runtime depends only on replication API; transport types remain outside both public contracts and the current implementation.
2. `GameplayPublicationBuilder` captures registered sources in stable projection-id order and advances one global gameplay revision per coherent publication.
3. `GameplayReplicationReadState` owns only replicated client truth. It applies exact-next deltas, dedupes stale/repeated revisions, detects gaps/schema incompatibility, and accepts newer full snapshots as repair/current-state convergence.
4. `Game.GameplayReplication.Adapters` contains current-authority adapters for Characters, Encounters, Combat and Inventory. It references those owning APIs plus replication API, but not replication Runtime; this preserves authority and reuse boundaries.
5. Reuse the existing authoritative UTP/network tick, serialization, ordering, late-join, catch-up, and repair foundations **when that production spine lands**; this assignment will not create a replacement transport.
6. Late-join/reconnect semantics are current-state snapshot convergence only; historical one-shot event replay is not required or used by the replication store.

## Tests / proof

`Game.GameplayReplication.Tests.GameplayReplicationRuntimeTests` exercises deterministic projection/publication ordering, monotonic revisions, duplicate/stale handling, gap detection, snapshot repair, schema incompatibility, and configuration-driven `GameplayReady` without presentation assemblies. Focused exact-SHA CI request `agent-2-20260901-gameplay-replication-1` targets this fixture; transport-driven loopback/two-client/late-join/reconnect gates remain blocked until the prerequisite production network spine exists.

## Do not build

No second transport, NGO adoption, UI state replication, event-history reconstruction, or subsystem-specific authority inside this module. Do not weaken or reinterpret transport-dependent acceptance because prerequisites are missing.
