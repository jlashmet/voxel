# 06 Gameplay-state replication — implementation plan

**Target module:** `Assets/Game/GameplayReplication/Api` / `Runtime` (`Game.GameplayReplication.Api`, `Game.GameplayReplication.Runtime`). The existing `Assets/VoxelEngine/Net` UTP/server-authoritative transport remains underneath.

## Inventory / ownership

- `Packages/manifest.json` includes `com.unity.transport` 6.5.0 and intentionally does not include NGO.
- Current master contains the production server/client/transport/protocol/interest/convergence stack under `Assets/VoxelEngine/Net`.
- `AuthoritativeServerSession.ProcessAuthoritativeTick` is the authoritative fixed-tick cadence. Gameplay-state publication plugs into that cadence after authoritative simulation state is resolved and before the existing replication/send flush; no second update loop exists.
- Existing Net owns transport, connection identity, packet framing, subscriptions/interest, convergence/repair, reconnect/admission plumbing and client/server receive paths. `Game.GameplayReplication` owns semantic gameplay publications/revisions and replicated client truth only.
- Existing owning semantic APIs consumed directly: Characters, Encounters, Combat, Inventory, and Sessions.
- The binding system designs specify replication-facing read seams for Vitality, Progression, Continuity, and Outcomes. Their runtimes need not exist for system 06, so system 06 supplies only the minimum engine-neutral owning API contracts needed for this consumer; future owning implementations can implement those contracts without changing replication.

## Minimal missing API contracts

- `Game.Vitality.Api`: immutable `VitalitySnapshot` keyed by `CharacterId` with current/max/defeated/revision and `IVitalityQuery`. No damage/heal/defeat runtime.
- `Game.Progression.Api`: stable quest/objective identities, lifecycle state, revisions, coherent `ProgressionSnapshot`, and `IProgressionQuery`. No observation evaluation, completion mutation, Story integration, or runtime.
- `Game.Continuity.Api`: semantic recovery state snapshots keyed by durable Sessions-owned `PartyMemberId`, coherent snapshot/query. No grace policy, reconnect authentication, input gating, or runtime.
- `Game.Outcomes.Api`: Running/Resolved lifecycle, disposition, semantic `OutcomeRef`, current snapshot/query. No resolution request policy, event emission, orchestration, or runtime.

These are semantic/configuration-neutral contracts only. They contain no Unity, transport, presentation, scene, or named-content policy. Each contract-only module now owns a minimal module-local EditMode contract assembly so repository convention can validate that API without broad fallback.

## Replication API / runtime

One publication barrier advances one monotonic `GameplayRevision`; every projection in that publication shares the revision. Deltas must be exact-next; duplicate/older publications are ignored, gaps/schema incompatibility enter `RepairRequired`, and a newer full snapshot may jump directly to current truth for repair, late join and reconnect convergence.

Subsystem identity/versioning is semantic (`GameplayProjectionId` + schema version). Producers implement `IGameplayProjectionSource` through adapters; owning gameplay modules never depend on replication Runtime. `GameplayReady` is configuration-driven and true only while synchronized with all configured required compatible projections.

`Game.GameplayReplication.Api` and `Runtime` stay engine-neutral. `Adapters` consumes owning gameplay APIs. `Transport` is the sole gameplay transport bridge on top of the existing Net protocol/send/receive seams. Repair requests travel through the existing client EVENT path; new/reconnected authenticated connections cause coherent current-state snapshots. Sessions durable identity is independent of transient connection IDs.

## Tests / material results

The transport-backed fixture covers two existing UTP clients with Characters + Vitality + transactional Inventory, a forced semantic revision gap with live repair request/response, a late joiner, and disconnect/reconnect under a new transient connection ID. This directly satisfies the original character/vitality-plus-transactional convergence shape rather than using character lifecycle as a vitality substitute.

`GameplayReplicationProjectionContractTests` is an independent consumer fixture for the four minimal APIs. It provides API-only query fixtures and verifies deterministic Continuity, Outcomes, Progression, and Vitality semantic projections without any owning runtime implementation.

An earlier request `1b741e0a9e0b6ffe461b938a0a74874e3aea6a8e` for source `257b9b150292f1e2be8562cf844f0d66dddb2516` failed before tests because an obsolete parallel `Game.GameplayReplication.Networking` assembly remained; that duplicate was removed. Source `b30991662a8aed7ab2f0d9f7853ccb8db25c0787` then passed focused UTP, automatic module validation, standalone player replay and final status in run `33518314913`.

After adding the minimal owning APIs, exact source `4c88588547e9842091a43c9706ebfc003090bf73` ran as request `11462838d0125ff344cf9102e9697b62e20eb71f` in run `33521707518`, job `99902340999`. The strengthened UTP test passed and standalone `KentridgePlayableSlice` replay passed. Automatic module validation failed because the four new API directories were convention-unowned, so the planner correctly used broad safe fallback. Artifact inspection showed the selected unrelated `Game.Materials.Tests` assembly executed 29 tests and failed three existing material ownership/boundary assertions; there was no GameplayReplication compile failure. Selected fix: give each new API its own minimal module-local contract test assembly so validation ownership is explicit by repository convention, rather than modifying the planner or unrelated Materials tests.

## Remaining gates

No external prerequisite remains for system 06. Re-run exact-head validation after the module-ownership fix. If targeted, module and standalone-player gates pass, complete closure bookkeeping, move only this SceneIssue open -> closed, merge current master, revalidate only if that merge affects this work, and promote the exact feature head non-force.

## Do not build

No second transport, NGO adoption, UI state replication, event-history reconstruction, or subsystem-specific runtime authority inside this module. Do not implement Vitality damage, Progression evaluation, Continuity policy, or Outcome resolution here. Do not move reconnect identity into gameplay replication; durable identity remains Sessions-owned and connection identity remains Net-owned.
