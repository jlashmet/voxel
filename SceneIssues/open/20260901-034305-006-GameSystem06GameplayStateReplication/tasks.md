# 06 Gameplay-state replication — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.GameplayReplication.Api` / `Game.GameplayReplication.Runtime`
**Execution rule:** extend the existing UTP/server-authoritative network spine. Replication transports authoritative subsystem projections; it does not become their owner. When an owning runtime is not yet implemented, define only the minimum engine-neutral owning API required for this replication consumer; do not invent the runtime.

## Baseline / contracts

- [x] **T06-001 — Map existing network spine.** Current master contains the production UTP/server-authoritative spine under `Assets/VoxelEngine/Net`: `AuthoritativeServerSession.ProcessAuthoritativeTick` owns the fixed authoritative cadence; `ServerNetworkRuntime` owns transport; existing replication, convergence, repair/resync, client/server protocol, interest/subscription, and reconnect/admission seams are present.
- [x] **T06-002 — Establish asmdefs and transport boundary.** `Game.GameplayReplication.Api` and `Runtime` are engine-neutral; Runtime references only replication API, and transport/UTP types are absent from public contracts. Subsystem integration lives in the separate adapter assembly.
- [x] **T06-003 — Define global gameplay revision semantics.** One publication advances one monotonic `GameplayRevision`; exact-next delta ordering, stale dedupe, gap detection and snapshot jump/repair semantics are explicit.
- [x] **T06-004 — Define subsystem projection registration seam.** `IGameplayProjectionSource` contributes semantic states through replication API; owning modules do not reference replication Runtime.
- [x] **T06-005 — Define client semantic read state.** `IGameplayReplicationReadState` exposes revision, synchronization/readiness, and semantic projections only; no UI/presentation state or GameObjects are represented.
- [x] **T06-006 — Define snapshot/delta envelope and compatibility rules.** `GameplayPublication` carries snapshot/delta kind, global revision, stable projection identity and schema version while transport encoding remains private.
- [x] **T06-007 — Define `GameplayReady`.** Readiness requires synchronized state plus every configured required compatible projection; connectivity is not represented and cannot satisfy readiness.

## Runtime integration

- [x] **T06-010 — Reuse server publication tick.** Gameplay publication is integrated into the existing `AuthoritativeServerSession.ProcessAuthoritativeTick` cadence after authoritative simulation state is resolved and before the existing replication/send flush; no second update loop was added.
- [x] **T06-011 — Add Characters projection.** `CharactersGameplayProjectionSource` consumes `ICharacterQuery` and projects stable CharacterId, lifecycle, revision and kinematics without presentation objects.
- [x] **T06-012 — Add Vitality projection.** Added the minimum owning `Game.Vitality.Api` read contract required by replication: immutable `VitalitySnapshot` keyed by `CharacterId` with current/max/defeated/revision plus `IVitalityQuery`. No Vitality runtime/damage implementation was added. `VitalityGameplayProjectionSource` deterministically projects current vitality truth.
- [x] **T06-013 — Add Encounter and Combat projections.** `EncounterGameplayProjectionSource` and `CombatGameplayProjectionSource` consume owning semantic APIs and preserve lifecycle/session/participant truth without moving authority.
- [x] **T06-014 — Add Inventory and Progression projections.** Inventory uses `IInventoryRuntime.Snapshot`. Added the minimum owning `Game.Progression.Api` read contract required by replication: stable quest/objective ids, lifecycle states, deterministic revisions, one coherent `ProgressionSnapshot`, and `IProgressionQuery`; no progression evaluation/runtime was added. `ProgressionGameplayProjectionSource` projects current quest/objective truth without reconstructing event history.
- [x] **T06-015 — Add Sessions/Continuity/Outcome projections as their APIs land.** Sessions uses `IPartySessionQuery.Snapshot`. Added minimum owning `Game.Continuity.Api` snapshot/query keyed only by durable `PartyMemberId`, and minimum owning `Game.Outcomes.Api` Running/Resolved lifecycle, disposition, semantic `OutcomeRef`, current snapshot/query. No reconnect policy or outcome-resolution runtime was added. Continuity and Outcomes projection adapters consume only these semantic read contracts.
- [x] **T06-016 — Implement client revision application.** Local stale/out-of-order handling, gap/schema repair state, snapshot replacement, and the live Net EVENT repair request/response path are implemented through generic Net seams.
- [x] **T06-017 — Implement late-join full current-state convergence.** A newly authenticated connection causes a coherent full current-state snapshot through the existing connection/admission path, without replaying one-shot history.
- [x] **T06-018 — Implement reconnect convergence.** Reconnected authenticated connections converge directly to current truth through the same snapshot path while durable Sessions identity remains transport-neutral.

## Verification

- [ ] **T06-020 — Projection determinism tests.** Existing deterministic source/projection/entry ordering tests remain, and `GameplayReplicationProjectionContractTests` now independently consumes the minimal Vitality/Progression/Continuity/Outcomes APIs with fixtures and verifies deterministic semantic projection. **Pending exact-head CI.**
- [x] **T06-021 — Stale/gap/dedupe tests.** Existing fixture covers stale duplicate, forward gap, repair-required, snapshot repair and schema mismatch; previously green and unchanged semantically. Final exact-head module validation remains required under T06-026.
- [ ] **T06-022 — Existing UTP loopback integration.** `GameplayReplicationUtpLoopbackTests` now explicitly carries Characters + Vitality + transactional Inventory for two authenticated clients and forces a semantic gap whose repair request travels over the live EVENT path. **Pending exact-head CI.**
- [ ] **T06-023 — Late-join test.** The same UTP fixture authenticates a third client after current character/vitality/inventory truth advances and requires current-state snapshot convergence without historical replay. **Pending exact-head CI.**
- [ ] **T06-024 — Reconnect test support.** The same UTP fixture disconnects a participant, verifies server removal, reconnects under a new transient connection ID, and requires current character/vitality/inventory truth. **Pending exact-head CI.**
- [ ] **T06-025 — Headless/no-presentation test.** GameplayReplication API/Runtime/Adapters plus the four minimal owning APIs contain no Unity/presentation types; compile/module proof is pending exact-head CI.
- [ ] **T06-026 — Automatic module/dependent test run.** Previous exact-SHA run `33518314913`, job `99890869505` was green before the minimal owning APIs were added. Re-run repository-selected automatic validation on the new exact feature head.

## Cleanup / close

- [x] **T06-030 — Remove parallel gameplay replication stores/codecs.** A stale parallel `Game.GameplayReplication.Networking` assembly from an earlier transport attempt was removed after run `33513817861` exposed it as a compile-time duplicate. One semantic client read store and one `Game.GameplayReplication.Transport` bridge remain on the existing Net spine.
- [x] **T06-031 — Runtime-boundary audit.** GameplayReplication API/Runtime remain engine-neutral and transport-neutral; adapters consume owning APIs only. The new Vitality/Progression/Continuity/Outcomes additions are contract-only API assemblies, with no runtime authority added by system 06. Net remains free of game-domain authority and uses generic cadence/protocol seams only.
- [ ] **T06-032 — Close with convergence proof.** No external prerequisite remains for system 06: required semantic read APIs now exist minimally without implementations. Close only after the new exact-head targeted/module/player gates are green and closure bookkeeping is populated.
