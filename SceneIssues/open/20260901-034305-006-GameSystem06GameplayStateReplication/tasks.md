# 06 Gameplay-state replication — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.GameplayReplication.Api` / `Game.GameplayReplication.Runtime`
**Execution rule:** extend the existing UTP/server-authoritative network spine. Replication transports authoritative subsystem projections; it does not become their owner.

## Baseline / contracts

- [x] **T06-001 — Map existing network spine.** UTP 6.5.0 is installed and NGO is absent, but current production source contains no authoritative network tick, serialization/frame codec, snapshot/delta, late-join/catch-up, repair/resync, prediction/reconciliation, connection/session loop, or interest-management implementation to reuse. This missing prerequisite is recorded in `plan.md`; transport-dependent acceptance remains unchanged and blocked rather than replaced.
- [ ] **T06-002 — Establish asmdefs and transport boundary.** GameplayReplication.Runtime may adapt existing network APIs; GameplayReplication.Api must not expose UTP packet/connection types.
- [ ] **T06-003 — Define global gameplay revision semantics.** Specify monotonic revision/order rules and what constitutes one coherent published authoritative revision.
- [ ] **T06-004 — Define subsystem projection registration seam.** Owning modules contribute typed snapshots/deltas through their APIs/adapters without referencing replication Runtime.
- [ ] **T06-005 — Define client semantic read state.** Expose current replicated truth and synchronization/readiness status for presentation consumers; prohibit UI/presentation state in the schema.
- [ ] **T06-006 — Define snapshot/delta envelope and compatibility rules.** Preserve typed subsystem identity/versioning while keeping transport encoding private.
- [ ] **T06-007 — Define `GameplayReady`.** List required projections/barriers and ensure socket-connected alone can never satisfy readiness.

## Runtime integration

- [ ] **T06-010 — Reuse server publication tick.** Publish gameplay projections on the existing authoritative network cadence rather than creating another loop. **BLOCKED:** production network cadence/spine is absent on current master.
- [ ] **T06-011 — Add Characters projection.** Current CharacterId/lifecycle/kinematic truth; no presentation GameObjects.
- [ ] **T06-012 — Add Vitality projection.** Current vitality/defeat truth with deterministic revision application. **BLOCKED if Vitality API is not yet present on current master.**
- [ ] **T06-013 — Add Encounter and Combat projections.** Replicate current lifecycle/session truth without moving authority into the network layer.
- [ ] **T06-014 — Add Inventory and Progression projections.** Preserve coherent inventories/objectives and avoid reconstructing truth from event history.
- [ ] **T06-015 — Add Sessions/Continuity/Outcome projections as their APIs land.** Durable ids remain owned by their modules.
- [ ] **T06-016 — Implement client revision application.** Reject stale/out-of-order deltas, dedupe repeats, detect gaps and request/trigger existing repair semantics. **Repair transport hook blocked by missing network spine; local gap semantics remain independently implementable.**
- [ ] **T06-017 — Implement late-join full current-state convergence.** New clients reach the latest coherent authoritative state before GameplayReady. **Transport integration blocked by missing network spine.**
- [ ] **T06-018 — Implement reconnect convergence.** Recovery may use fast repair or full snapshot but must expose the same resulting current truth and no historical one-shot replay. **Transport integration blocked by missing network spine.**

## Verification

- [ ] **T06-020 — Projection determinism tests.** Same authoritative subsystem snapshots produce stable serialized semantic projections/order.
- [ ] **T06-021 — Stale/gap/dedupe tests.** Exercise old revision, duplicate delta, missing revision and repair path.
- [ ] **T06-022 — Existing UTP loopback integration.** Two clients converge on character/vitality plus at least one transactional domain. **BLOCKED:** no existing production UTP loopback/server spine is present.
- [ ] **T06-023 — Late-join test.** Mutate state before join and prove the joiner receives current state without event replay. **Transport integration blocked by missing network spine.**
- [ ] **T06-024 — Reconnect test support.** Verify state changes while absent appear after resync; identity continuity is asserted in system 08. **Transport integration blocked by missing network spine.**
- [ ] **T06-025 — Headless/no-presentation test.** Replication operates without HUD/audio/VFX assemblies.
- [ ] **T06-026 — Automatic module/dependent test run.** No manual per-test CI registration.

## Cleanup / close

- [ ] **T06-030 — Remove parallel gameplay replication stores/codecs.** Consolidate duplicated gameplay snapshot state onto this projection path while retaining existing voxel/network foundations.
- [ ] **T06-031 — Runtime-boundary audit.** Replication must consume subsystem APIs/adapters only and expose no raw transport identity as gameplay identity.
- [ ] **T06-032 — Close with convergence proof.** Authority, existing client, late joiner and reconnecting client converge on the same semantic current state.
