# 06 Gameplay-state replication — implementation plan

**Target module:** `Assets/Game/GameplayReplication/Api` / `Runtime` (`Game.GameplayReplication.Api`, `Game.GameplayReplication.Runtime`). The existing `Assets/VoxelEngine/Net` UTP/server-authoritative transport remains underneath.

## Inventory / ownership

- `Packages/manifest.json` includes `com.unity.transport` 6.5.0 and intentionally does not include NGO.
- The production server/client/transport/protocol/interest/convergence stack remains under `Assets/VoxelEngine/Net`.
- `AuthoritativeServerSession.ProcessAuthoritativeTick` is the authoritative fixed-tick cadence. Gameplay-state publication plugs into that cadence after authoritative simulation state is resolved and before the existing replication/send flush; no second update loop exists.
- Net owns transport, connection identity, packet framing, subscriptions/interest, convergence/repair, reconnect/admission plumbing and client/server receive paths. `Game.GameplayReplication` owns semantic gameplay publications/revisions and replicated client truth only.
- Existing owning semantic APIs consumed directly: Characters, Encounters, Combat, Inventory, Sessions, and now canonical Continuity from current master.

## Minimal contracts and later canonical adoption

- `Game.Vitality.Api`: immutable `VitalitySnapshot` keyed by `CharacterId` with current/max/defeated/revision and `IVitalityQuery`. No damage/heal/defeat runtime.
- `Game.Progression.Api`: stable quest/objective identities, lifecycle state, revisions, coherent `ProgressionSnapshot`, and `IProgressionQuery`. No observation evaluation, completion mutation, Story integration, or runtime.
- `Game.Outcomes.Api`: Running/Resolved lifecycle, disposition, semantic `OutcomeRef`, current snapshot/query. No resolution policy, orchestration, or runtime.
- System 06 initially supplied a minimal Continuity read seam because no owner implementation existed. Current master later landed canonical `Game.Continuity.Api` / `Runtime` / tests. The feature adopts that canonical implementation unchanged, removes the provisional Continuity contract/test shape, and adapts replication to `IContinuityQuery.TryGetRecovery` plus Sessions-owned durable member enumeration.
- Current master also introduced the small `IGameplayReplicationClientState` semantic contract needed by Continuity. The richer system-06 replication API preserves that seam together with its publication/projection/revision contracts. `GameplayRevision` uses the canonical unsigned monotonic value and keeps nonnegative signed constructors only as convenience.
- Master-established GameplayReplication folder/API/test `.meta` GUIDs are preserved so the full system-06 implementation extends the existing Unity assets instead of replacing their identities.

## Replication API / runtime

One publication barrier advances one monotonic `GameplayRevision`; every projection in that publication shares the revision. Deltas must be exact-next; duplicate/older publications are ignored, gaps/schema incompatibility enter `RepairRequired`, and a newer full snapshot may jump directly to current truth for repair, late join and reconnect convergence.

Subsystem identity/versioning is semantic (`GameplayProjectionId` + schema version). Producers implement `IGameplayProjectionSource` through adapters; owning gameplay modules never depend on replication Runtime. `GameplayReady` is configuration-driven and true only while synchronized with all configured required compatible projections.

`Game.GameplayReplication.Api` and `Runtime` stay engine-neutral. `Adapters` consumes owning gameplay APIs. `Transport` is the sole gameplay transport bridge on top of the existing Net protocol/send/receive seams. Repair requests travel through the existing client EVENT path; new/reconnected authenticated connections cause coherent current-state snapshots. Sessions durable identity is independent of transient connection IDs.

## Validation and demonstrated fixes

The transport-backed fixture covers two authenticated UTP clients with Characters + Vitality + transactional Inventory, a forced semantic revision gap with live repair request/response, a late joiner, and disconnect/reconnect under a new transient connection ID. `GameplayReplicationProjectionContractTests` is an independent consumer fixture for Vitality, Progression, Outcomes and canonical Continuity/Sessions semantic reads.

Run `33513817861` exposed an obsolete parallel `Game.GameplayReplication.Networking` assembly as a compile-time duplicate; it was removed, leaving `Game.GameplayReplication.Transport` as the sole bridge. Run `33521707518` then proved the strengthened focused UTP test and standalone player but broadened module validation because new contract-only API folders had no convention-owned tests; unrelated `Game.Materials.Tests` failures demonstrated that validation-ownership defect. Tiny module-local contract tests fixed the ownership boundary without modifying Materials or the validation planner.

Exact source `5432ef305138c2948d182342df52af626da154f0` passed acceptance validation in run `33522951566`, job `99906521904`: focused UTP, repository-derived automatic module/dependent validation, standalone `KentridgePlayableSlice` replay, screenshot/artifact evidence, and final `ci/single-test=success` all passed.

After closure, master advanced materially with GameSystem08 Continuity. The feature merged master, retained the canonical Continuity runtime/tests and master GameplayReplication asset GUIDs, and resolved only the overlapping semantic API/adapter boundary described above. One final exact merged-head gate is required before promotion to master; no queued/running CI request is replaced to achieve it.

## Non-goals preserved

No second transport, NGO adoption, UI state replication, event-history reconstruction, or subsystem-specific runtime authority was added. Vitality damage, Progression evaluation, Continuity policy, and Outcome resolution remain outside system 06. Reconnect identity remains Sessions-owned and connection identity remains Net-owned.
