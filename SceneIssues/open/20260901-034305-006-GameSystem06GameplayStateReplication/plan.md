# 06 Gameplay-state replication — implementation plan

**Target module:** `Assets/Game/GameplayReplication/Api` / `Runtime` (`Game.GameplayReplication.Api`, `Game.GameplayReplication.Runtime`). The existing `Assets/VoxelEngine/Net` UTP/server-authoritative transport remains underneath.

## Inventory / ownership

- `Packages/manifest.json` includes `com.unity.transport` 6.5.0 and intentionally does not include NGO.
- Current master contains the production server/client/transport/protocol/interest/convergence stack under `Assets/VoxelEngine/Net`.
- `AuthoritativeServerSession.ProcessAuthoritativeTick` is the authoritative fixed-tick cadence. Gameplay-state publication plugs into that cadence after authoritative simulation state is resolved and before the existing replication/send flush; no second update loop exists.
- Existing Net owns transport, connection identity, packet framing, subscriptions/interest, convergence/repair, reconnect/admission plumbing and client/server receive paths. `Game.GameplayReplication` owns only semantic gameplay publications/revisions and replicated client truth. VoxelEngine.Net remains free of Characters/Combat/Inventory/Sessions authority.
- GameSystem07 provides `Game.Sessions.Api` durable `GameSessionId -> PartyMemberId -> PlayerSlot -> CharacterId` identity. `SessionsGameplayProjectionSource` preserves those identities deterministically.
- Available owning semantic APIs: Characters (`ICharacterQuery`), Encounters (`IEncounterQuery`), Combat (`ICombatService`), Inventory (`IInventoryRuntime.Snapshot`), and Sessions (`IPartySessionQuery`). Current `origin/master` still has no `Game.Vitality`, `Game.Progression`, `Game.Continuity`, or `Game.Outcome` owning API/module; those acceptance slices remain external blockers and are not recreated here.

## API / runtime

One publication barrier advances one monotonic `GameplayRevision`; every projection in that publication shares the revision. Deltas must be exact-next; duplicate/older publications are ignored, gaps/schema incompatibility enter `RepairRequired`, and a newer full snapshot may jump directly to current truth for repair, late join and reconnect convergence.

Subsystem identity/versioning is semantic (`GameplayProjectionId` + schema version). Producers implement `IGameplayProjectionSource` through adapters; owning gameplay modules do not depend on replication Runtime. `GameplayReady` is configuration-driven and true only while synchronized with all configured required compatible projections.

`Game.GameplayReplication.Api` and `Runtime` stay engine-neutral. `Adapters` consumes owning gameplay APIs. `Transport` is the sole gameplay transport bridge on top of the existing Net protocol/send/receive seams. Repair requests travel through the existing client EVENT path; new/reconnected authenticated connections cause coherent current-state snapshots. Sessions durable identity is independent of transient connection IDs.

## Material results / selected fix

The transport-backed fixture covers two existing UTP clients, Characters plus transactional Inventory, a forced semantic revision gap with live repair request/response, a late joiner, and disconnect/reconnect under a new transient connection ID. An earlier request `1b741e0a9e0b6ffe461b938a0a74874e3aea6a8e` for source `257b9b150292f1e2be8562cf844f0d66dddb2516` failed before tests because an obsolete parallel `Game.GameplayReplication.Networking` assembly remained from an earlier transport attempt and no longer implemented the generic repair-handler seam. The demonstrated root cause was removed entirely, leaving the canonical `Game.GameplayReplication.Transport` implementation only.

Validated implementation source `b30991662a8aed7ab2f0d9f7853ccb8db25c0787` used sole CI transport request `540bf22ed49e8bfb0f8b39feadb1386cdee74fbe`. Run `33518314913`, job `99890869505` passed the focused `GameplayReplicationUtpLoopbackTests`, repository-derived automatic module validation, standalone `KentridgePlayableSlice` SceneIssue replay, artifact upload, and final exact-SHA status. This also revalidated deterministic projection/application tests and no-presentation boundaries selected by module ownership.

## Remaining gates / blocker

All currently implementable gameplay-replication transport, convergence, reuse, cleanup and regression work is complete and exact-SHA green. Closure remains blocked only by external prerequisite ownership: Vitality projection, Progression projection, and Continuity/Outcome projections cannot be implemented until their owning semantic APIs/modules land on `origin/master`. Keep the SceneIssue in `open/`; do not weaken or substitute acceptance. When those APIs land, merge current master, add adapters/regressions through the same semantic seams, then run a new exact-head validation before closure.

## Do not build

No second transport, NGO adoption, UI state replication, event-history reconstruction, or subsystem-specific authority inside this module. Do not move reconnect identity into gameplay replication; durable identity remains Sessions-owned and connection identity remains Net-owned.
