# 06 Gameplay-state replication — implementation plan

**Target module:** `Assets/Game/GameplayReplication/Api` / `Runtime` (`Game.GameplayReplication.Api`, `Game.GameplayReplication.Runtime`). The existing `Assets/VoxelEngine/Net` UTP/server-authoritative transport remains underneath.

## Inventory / ownership

- `Packages/manifest.json` includes `com.unity.transport` 6.5.0 and intentionally does not include NGO.
- The earlier pre-GameSystem07 inventory that found no production network spine is now obsolete. Current master contains the production server/client/transport/protocol/interest/convergence stack under `Assets/VoxelEngine/Net`.
- `AuthoritativeServerSession.ProcessAuthoritativeTick` is the authoritative fixed-tick cadence. It begins the network tick, processes authoritative simulation input, samples player state after game-owned movement has resolved, flushes replication, emits convergence hashes, processes repair/bulk state, and flushes sends. Gameplay-state publication must plug into this cadence rather than create another loop.
- Existing Net owns transport, connection identity, packet framing, subscriptions/interest, convergence/repair, reconnect/admission plumbing and client/server receive paths. `Game.GameplayReplication` owns only semantic gameplay publications/revisions and replicated client truth. VoxelEngine.Net must not become dependent on Characters/Combat/Inventory/etc.; any shared hook added there must remain generic.
- GameSystem07 landed `Game.Sessions.Api` with durable `GameSessionId -> PartyMemberId -> PlayerSlot -> CharacterId` identity and transport-neutral readiness/presence/leadership. `SessionsGameplayProjectionSource` now consumes `IPartySessionQuery.Snapshot` and preserves those durable identities deterministically.
- Current owning semantic APIs available to replication: Characters (`ICharacterQuery`), Encounters (`IEncounterQuery`), Combat (`ICombatService`), Inventory (`IInventoryRuntime.Snapshot`), and Sessions (`IPartySessionQuery`). `Game.Vitality`, `Game.Progression`, Continuity and Outcome owning APIs are still absent; those acceptance slices remain blocked rather than recreated here.

## API

Authoritative gameplay revision, typed snapshot/delta contracts for registered subsystem projections, synchronization/readiness state, and read-only client semantic state. Transport packet types remain private and are not gameplay contracts.

A coherent authoritative revision is one publication barrier: all projections captured by a publication share the same monotonic gameplay revision. Delta revisions must be exactly current+1; duplicate/older publications are ignored, and forward gaps transition client state to `RepairRequired`. A full snapshot may jump directly to a newer revision and replaces current replicated projection truth, which is the repair/late-join/reconnect convergence primitive.

Subsystem identity/versioning is semantic and stable. `GameplayProjectionId` plus schema version define compatibility; projection producers implement `IGameplayProjectionSource` through adapters/composition and owning gameplay modules never depend on replication Runtime.

`GameplayReady` is configuration-driven through required projection descriptors. It is true only while synchronized and every required descriptor has a compatible current projection; socket connectivity alone cannot satisfy readiness.

## Runtime / transport integration

1. `Game.GameplayReplication.Api` and `Runtime` remain engine-neutral. Runtime depends only on replication API.
2. `GameplayPublicationBuilder` captures registered sources in stable projection-id order and advances one global gameplay revision per coherent publication.
3. `GameplayReplicationReadState` applies exact-next deltas, dedupes stale/repeated revisions, detects gaps/schema incompatibility, and accepts newer full snapshots as repair/current-state convergence.
4. `Game.GameplayReplication.Adapters` contains authority adapters for Characters, Encounters, Combat, Inventory and Sessions. It references those owning APIs plus replication API, not replication Runtime.
5. Reuse `AuthoritativeServerSession.ProcessAuthoritativeTick` for publication cadence. Add only generic Net extension seams if required; do not make VoxelEngine.Net depend on game-domain assemblies and do not create another transport/update loop.
6. Reuse existing Net protocol/send/receive and convergence/repair infrastructure for snapshot/delta delivery and repair requests. Late join and reconnect converge from current-state snapshots; one-shot event history is not replayed.
7. Sessions durable member/slot/character identity remains independent of transient connection IDs and is included semantically through the Sessions projection.

## Tests / proof

`Game.GameplayReplication.Tests.GameplayReplicationRuntimeTests` covers deterministic projection/publication ordering, monotonic revisions, duplicate/stale handling, gap detection, snapshot repair, schema incompatibility, configuration-driven `GameplayReady`, existing authority adapters, and Sessions durable-identity ordering without presentation assemblies.

Exact-SHA validation for source `44e7da5284923ab96b382f75a5867434377a36d6` used the sole transport `ci-test/fixes/agent-2` with request commit `4a431a603c0908c007ae4556a59deb7a815c4f2c`; run `33504339974`, job `99844708050` passed focused EditMode, automatic module validation, standalone SceneIssue replay, artifacts, and final status. An earlier run `33501651756` exposed obsolete `*.module-validation.json` registration; that 006-owned manifest was removed without altering planner infrastructure.

After current master landed GameSystem07, the branch was merged to current master and the transport blockers were reclassified as actionable. New exact-head validation is required after transport integration. Vitality/Progression/Continuity/Outcome remain external prerequisite blockers.

## Do not build

No second transport, NGO adoption, UI state replication, event-history reconstruction, or subsystem-specific authority inside this module. Do not move reconnect identity into gameplay replication; durable identity remains Sessions-owned and connection identity remains Net-owned.
