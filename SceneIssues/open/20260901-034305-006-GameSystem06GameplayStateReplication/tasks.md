# 06 Gameplay-state replication — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.GameplayReplication.Api` / `Game.GameplayReplication.Runtime`
**Execution rule:** extend the existing UTP/server-authoritative network spine. Replication transports authoritative subsystem projections; it does not become their owner.

## Baseline / contracts

- [x] **T06-001 — Map existing network spine.** UTP 6.5.0 is installed and NGO is absent, but current production source contains no authoritative network tick, serialization/frame codec, snapshot/delta, late-join/catch-up, repair/resync, prediction/reconciliation, connection/session loop, or interest-management implementation to reuse. This missing prerequisite is recorded in `plan.md`; transport-dependent acceptance remains unchanged and blocked rather than replaced.
- [x] **T06-002 — Establish asmdefs and transport boundary.** `Game.GameplayReplication.Api` and `Runtime` are engine-neutral; Runtime references only replication API, and transport/UTP types are absent from public contracts. Subsystem integration lives in the separate adapter assembly.
- [x] **T06-003 — Define global gameplay revision semantics.** One publication advances one monotonic `GameplayRevision`; exact-next delta ordering, stale dedupe, gap detection and snapshot jump/repair semantics are explicit.
- [x] **T06-004 — Define subsystem projection registration seam.** `IGameplayProjectionSource` contributes semantic states through replication API; owning modules do not reference replication Runtime.
- [x] **T06-005 — Define client semantic read state.** `IGameplayReplicationReadState` exposes revision, synchronization/readiness, and semantic projections only; no UI/presentation state or GameObjects are represented.
- [x] **T06-006 — Define snapshot/delta envelope and compatibility rules.** `GameplayPublication` carries snapshot/delta kind, global revision, stable projection identity and schema version while transport encoding remains private.
- [x] **T06-007 — Define `GameplayReady`.** Readiness requires synchronized state plus every configured required compatible projection; connectivity is not represented and cannot satisfy readiness.

## Runtime integration

- [ ] **T06-010 — Reuse server publication tick.** Publish gameplay projections on the existing authoritative network cadence rather than creating another loop. **BLOCKED:** production network cadence/spine is absent on current master.
- [x] **T06-011 — Add Characters projection.** `CharactersGameplayProjectionSource` consumes `ICharacterQuery` and projects stable CharacterId, lifecycle, revision and kinematics without presentation objects.
- [ ] **T06-012 — Add Vitality projection.** Current vitality/defeat truth with deterministic revision application. **BLOCKED:** no `Game.Vitality` owning API/module is present on current master.
- [x] **T06-013 — Add Encounter and Combat projections.** `EncounterGameplayProjectionSource` and `CombatGameplayProjectionSource` consume owning semantic APIs and preserve lifecycle/session/participant truth without moving authority.
- [ ] **T06-014 — Add Inventory and Progression projections.** Inventory projection is implemented from `IInventoryRuntime.Snapshot`; **BLOCKED** on the Progression half because no `Game.Progression` owning API/module is present on current master.
- [ ] **T06-015 — Add Sessions/Continuity/Outcome projections as their APIs land.** **BLOCKED:** those owning APIs/modules are not present on current master; durable ids will remain owned there when available.
- [ ] **T06-016 — Implement client revision application.** Local application is implemented: stale/out-of-order deltas are rejected/deduped, gaps/schema mismatches enter `RepairRequired`, and newer snapshots repair current truth. **BLOCKED:** request/trigger integration into the existing repair transport is impossible until the network spine exists.
- [ ] **T06-017 — Implement late-join full current-state convergence.** Snapshot replacement semantics provide the current-state convergence primitive and do not replay one-shot history. **BLOCKED:** transport-driven late-join integration requires the missing network spine.
- [ ] **T06-018 — Implement reconnect convergence.** Snapshot repair semantics converge directly to current truth after absence. **BLOCKED:** reconnect/repair transport integration requires the missing network spine.

## Verification

- [x] **T06-020 — Projection determinism tests.** `GameplayReplicationRuntimeTests` covers stable source/projection/entry ordering, monotonic publication revisions, and deterministic adapters over Characters/Encounter/Combat/Inventory authorities. Exact-SHA CI request commit `4a431a603c0908c007ae4556a59deb7a815c4f2c`, source `44e7da5284923ab96b382f75a5867434377a36d6`, run `33504339974`, job `99844708050` passed the focused fixture.
- [x] **T06-021 — Stale/gap/dedupe tests.** The same exact-SHA fixture covers stale duplicate, forward gap, repair-required, snapshot repair and schema mismatch and passed in run `33504339974`.
- [ ] **T06-022 — Existing UTP loopback integration.** Two clients converge on character/vitality plus at least one transactional domain. **BLOCKED:** no existing production UTP loopback/server spine is present, and Vitality API is absent.
- [ ] **T06-023 — Late-join test.** Transport-independent snapshot repair semantics are covered; **BLOCKED** for required transport integration because the production network spine is absent.
- [ ] **T06-024 — Reconnect test support.** Transport-independent repair-to-current semantics are covered; **BLOCKED** for reconnect transport integration because the production network spine is absent.
- [x] **T06-025 — Headless/no-presentation test.** Replication API/Runtime/Adapters have no engine references; exact-SHA focused EditMode tests compiled and passed without HUD/audio/VFX references in run `33504339974`.
- [x] **T06-026 — Automatic module/dependent test run.** Repository convention-driven module ownership discovered GameplayReplication from its module-owned test assembly; exact-SHA run `33504339974` passed both `Derive automatic module validation plan` and `Run automatically required module validation`. The obsolete per-module manifest attempt was removed after run `33501651756` demonstrated that `*.module-validation.json` registration is no longer supported.

## Cleanup / close

- [x] **T06-030 — Remove parallel gameplay replication stores/codecs.** T06-001 inventory plus final feature diff found no pre-existing gameplay replication store/codec to migrate; 006 introduces one semantic client read store and no duplicate transport codec.
- [x] **T06-031 — Runtime-boundary audit.** Final source/asmdef diff shows API/Runtime/Adapters only; Runtime depends only on replication API, adapters consume owning gameplay APIs plus replication API, and no raw transport/connection identity or presentation assembly crosses the gameplay contract.
- [ ] **T06-032 — Close with convergence proof.** **BLOCKED:** authority/existing-client/late-joiner/reconnecting-client transport convergence cannot be proven until the required production network spine and missing owning APIs land.
