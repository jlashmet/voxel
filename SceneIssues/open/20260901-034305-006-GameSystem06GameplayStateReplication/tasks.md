# 06 Gameplay-state replication — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.GameplayReplication.Api` / `Game.GameplayReplication.Runtime`
**Execution rule:** extend the existing UTP/server-authoritative network spine. Replication transports authoritative subsystem projections; it does not become their owner.

## Baseline / contracts

- [x] **T06-001 — Map existing network spine.** Current master now contains the production UTP/server-authoritative spine under `Assets/VoxelEngine/Net`: `AuthoritativeServerSession.ProcessAuthoritativeTick` owns the fixed authoritative cadence; `ServerNetworkRuntime` owns transport; existing replication, convergence, repair/resync, client/server protocol, interest/subscription, and reconnect/admission seams are present. This supersedes the earlier pre-GameSystem07 inventory that found the spine absent.
- [x] **T06-002 — Establish asmdefs and transport boundary.** `Game.GameplayReplication.Api` and `Runtime` are engine-neutral; Runtime references only replication API, and transport/UTP types are absent from public contracts. Subsystem integration lives in the separate adapter assembly.
- [x] **T06-003 — Define global gameplay revision semantics.** One publication advances one monotonic `GameplayRevision`; exact-next delta ordering, stale dedupe, gap detection and snapshot jump/repair semantics are explicit.
- [x] **T06-004 — Define subsystem projection registration seam.** `IGameplayProjectionSource` contributes semantic states through replication API; owning modules do not reference replication Runtime.
- [x] **T06-005 — Define client semantic read state.** `IGameplayReplicationReadState` exposes revision, synchronization/readiness, and semantic projections only; no UI/presentation state or GameObjects are represented.
- [x] **T06-006 — Define snapshot/delta envelope and compatibility rules.** `GameplayPublication` carries snapshot/delta kind, global revision, stable projection identity and schema version while transport encoding remains private.
- [x] **T06-007 — Define `GameplayReady`.** Readiness requires synchronized state plus every configured required compatible projection; connectivity is not represented and cannot satisfy readiness.

## Runtime integration

- [x] **T06-010 — Reuse server publication tick.** Gameplay publication is integrated into the existing `AuthoritativeServerSession.ProcessAuthoritativeTick` cadence after authoritative simulation state is resolved and before the existing replication/send flush; no second update loop was added. Exact-head CI proof remains under T06-022/023/024.
- [x] **T06-011 — Add Characters projection.** `CharactersGameplayProjectionSource` consumes `ICharacterQuery` and projects stable CharacterId, lifecycle, revision and kinematics without presentation objects.
- [ ] **T06-012 — Add Vitality projection.** Current vitality/defeat truth with deterministic revision application. **BLOCKED:** no `Game.Vitality` owning API/module is present on current master.
- [x] **T06-013 — Add Encounter and Combat projections.** `EncounterGameplayProjectionSource` and `CombatGameplayProjectionSource` consume owning semantic APIs and preserve lifecycle/session/participant truth without moving authority.
- [ ] **T06-014 — Add Inventory and Progression projections.** Inventory projection is implemented from `IInventoryRuntime.Snapshot`; **BLOCKED** on the Progression half because no `Game.Progression` owning API/module is present on current master.
- [ ] **T06-015 — Add Sessions/Continuity/Outcome projections as their APIs land.** Sessions is now implemented from `IPartySessionQuery.Snapshot`, preserving durable `GameSessionId`, `PartyMemberId`, `PlayerSlot`, readiness/presence/leadership and bound `CharacterId`, with deterministic slot ordering and regression coverage. **BLOCKED** only on Continuity/Outcome because those owning APIs/modules are not present on current master.
- [x] **T06-016 — Implement client revision application.** Local stale/out-of-order handling, gap/schema repair state, snapshot replacement, and the live Net EVENT repair request/response path are implemented through generic Net seams. Exact-head CI proof remains under T06-022.
- [x] **T06-017 — Implement late-join full current-state convergence.** A newly authenticated connection causes a coherent full current-state snapshot through the existing connection/admission path, without replaying one-shot history. Exact-head CI proof remains under T06-023.
- [x] **T06-018 — Implement reconnect convergence.** Reconnected authenticated connections converge directly to current truth through the same snapshot path while durable Sessions identity remains transport-neutral. Exact-head CI proof remains under T06-024.

## Verification

- [x] **T06-020 — Projection determinism tests.** `GameplayReplicationRuntimeTests` covers stable source/projection/entry ordering, monotonic publication revisions, deterministic Characters/Encounter/Combat/Inventory adapters, and durable Sessions slot/member projection. Exact-SHA CI request commit `4a431a603c0908c007ae4556a59deb7a815c4f2c`, source `44e7da5284923ab96b382f75a5867434377a36d6`, run `33504339974`, job `99844708050` passed the focused fixture before the latest transport integration; a new exact-head gate is required.
- [x] **T06-021 — Stale/gap/dedupe tests.** The same fixture covers stale duplicate, forward gap, repair-required, snapshot repair and schema mismatch and passed in run `33504339974`.
- [ ] **T06-022 — Existing UTP loopback integration.** `GameplayReplicationUtpLoopbackTests` now exercises two authenticated live UTP clients converging for Characters plus transactional Inventory and forces a semantic gap whose repair request travels over the live EVENT path. Exact-SHA request `1b741e0a9e0b6ffe461b938a0a74874e3aea6a8e` for source `257b9b150292f1e2be8562cf844f0d66dddb2516` did not reach the test because an obsolete parallel Networking assembly caused a compiler error; that duplicate is removed. Re-run exact-head CI.
- [ ] **T06-023 — Late-join test.** The same UTP fixture authenticates a third client after current truth advances and requires immediate current-state snapshot convergence without historical replay. Re-run exact-head CI after the compiler-root-cause fix.
- [ ] **T06-024 — Reconnect test support.** The same UTP fixture disconnects a participant, verifies server removal, reconnects under a new transient connection ID with the same player identity, advances Inventory truth, and requires current-state convergence. Re-run exact-head CI after the compiler-root-cause fix.
- [x] **T06-025 — Headless/no-presentation test.** Replication API/Runtime/Adapters have no engine references; exact-SHA focused EditMode tests compiled and passed without HUD/audio/VFX references in run `33504339974`.
- [x] **T06-026 — Automatic module/dependent test run.** Repository convention-driven module ownership discovered GameplayReplication from its module-owned test assembly; exact-SHA run `33504339974` passed both automatic planning and required module validation. The obsolete per-module manifest attempt was removed after run `33501651756` demonstrated that `*.module-validation.json` registration is no longer supported. Re-run exact-head validation after transport integration.

## Cleanup / close

- [x] **T06-030 — Remove parallel gameplay replication stores/codecs.** T06-001 found no pre-existing gameplay-state store/codec. A first transport attempt left a parallel `Game.GameplayReplication.Networking` assembly beside the canonical `Game.GameplayReplication.Transport`; run `33513817861` exposed it as a compile-time stale implementation after the generic repair seam changed. The entire obsolete Networking assembly/folder was removed, leaving one semantic read store and one transport bridge on the existing Net spine.
- [x] **T06-031 — Runtime-boundary audit.** API/Runtime remain engine-neutral and transport-neutral; adapters consume owning gameplay APIs plus replication API. Net integration keeps VoxelEngine.Net free of game-domain authority and uses generic cadence/protocol seams only.
- [ ] **T06-032 — Close with convergence proof.** Not closeable yet: exact-head transport-backed convergence validation still must pass, and Vitality/Progression/Continuity/Outcome owning APIs remain external prerequisites for their acceptance slices.
