# 25 Multiplayer end-to-end gameplay validation — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** shared built-player validation infrastructure + multiplayer scenarios. No gameplay module and no test-only networking runtime.
**Execution rule:** every process runs the same exact production build and enters through systems 23/07; scenarios observe semantic diagnostics and drive public inputs only.

## Harness foundation

- [x] **T25-001 — Inventory current standalone/multiplayer test runners.** `module-validation-plan.py` discovers paired module-local player scenes/scenarios; `run-module-validation.py` delegates player targets to the canonical `player-validation.py`; production multiplayer semantics live in SessionOrchestration/Sessions/GameplayReplication/Continuity rather than `Assets/Scripts/Networking`.
- [x] **T25-002 — Extend shared runner with generic process roles.** `player-validation.py` now dispatches `mode: multiProcess` to the generic build-once orchestrator; configured roles support deterministic launch/wait/terminate/kill/relaunch without scenario-specific gameplay code.
- [ ] **T25-003 — Add exact-SHA/build identity assertion.** Harness side is complete: each launch/relaunch automatically waits for and validates process-reported source SHA + executable SHA-256 before gameplay milestones; `tests-single.yml` passes its authoritative `HEAD^` feature SHA through module player validation. Remaining: the production-composed multiplayer validation process must emit the `build-identity` milestone; no current player bootstrap does so.
- [x] **T25-004 — Isolate writable process state.** HOME/temp/config/cache/state roots are role-specific; relaunch attempts retain only that role's durable state root and use attempt-specific logs. Scenario arguments/environment cannot override harness-owned identity/state/log/run controls, and the environment builder reapplies isolation invariants for programmatic roles.
- [x] **T25-005 — Add bounded semantic milestone waiting.** Shared waits consume `VOXEL_VALIDATION_MILESTONE` JSON with field matching, explicit deadlines, process-exit diagnostics, and no correctness sleeps.
- [x] **T25-006 — Standardize failure artifacts.** Multi-process summary records build identity, ordered lifecycle operations, milestone history, per-role/per-attempt logs/PIDs/exit status and identity-verification state; screenshots remain the responsibility of player-visible scenarios where relevant.
- [ ] **T25-007 — Define read-only multiplayer diagnostic snapshot.** Stable GameSessionId/PartyMemberId/PlayerSlot/CharacterId plus selected current domain truth/revisions; explicitly no mutation methods. Existing pure read components are `IPartySessionQuery`, `IGameplayReplicationReadState`, `GameplaySynchronizationStatus` and typed projection snapshots; final composed player diagnostic must reuse System24 T24-032 rather than introduce a second privileged seam.

## Production topology / entry

> **External prerequisite:** on current `origin/master`, `20260901-034305-024-GameSystem24ProductionComposedBuiltPlayerVerticalSlice` is still open. System25 must not create a substitute composition path. Continue independent harness work; resume the production scene/scenario after System24 lands.

- [ ] **T25-010 — Launch real authority topology.** Use production UTP/session configuration and system 23/07 startup path, not direct test socket attachment.
- [ ] **T25-011 — Launch client A and client B as separate OS processes.** Both join through the real provider/session formation abstraction and wait for GameplayReady.
- [ ] **T25-012 — Assert durable identity topology.** Unique PartyMemberIds/slots/CharacterIds and consistent roster projections across authority and both clients.
- [ ] **T25-013 — Verify baseline state convergence.** Compare authoritative semantic revision/state against both clients after readiness before scenario mutations.

## Authoritative gameplay cases

- [ ] **T25-020 — Add two-client contention case.** Prefer one Loot/WorldObject claim: both clients issue valid competing player intents; authority accepts exactly one and all processes converge.
- [ ] **T25-021 — Assert conservation/exactly-once result.** World item and inventories reflect one committed transfer and no duplicate/lost quantity.
- [ ] **T25-022 — Add combat/vitality convergence case.** Real player/encounter/combat actions change authoritative Character Vitality; both clients converge on current state and defeat transition where applicable.
- [ ] **T25-023 — Add shared progression observation.** A real semantic gameplay fact updates shared Progression and all clients observe the same authoritative objective/quest state.

## Reconnect / leave

- [ ] **T25-030 — Interrupt one client unexpectedly.** Kill/drop client process/connection without executing Leave Game; authority keeps durable identity under system 08 policy. Harness `kill`/`relaunch` lifecycle is implemented; production proof remains blocked on the composed player entry.
- [ ] **T25-031 — Mutate authoritative state while client is absent.** Change at least one character/vitality/inventory/progression fact through normal gameplay from remaining processes.
- [ ] **T25-032 — Reconnect as a fresh client process/connection.** Use production reconnect/session flow; assert new transport connection but same PartyMemberId/PlayerSlot/CharacterId.
- [ ] **T25-033 — Assert current-state recovery.** Reconnected client receives all absent-period state changes and reaches GameplayReady without replaying historical audio/VFX/other one-shots.
- [ ] **T25-034 — Verify explicit Leave Game separately.** A client using system 23 leave path follows membership removal/teardown semantics and is not treated as reconnectable interruption.

## Extended/release scenarios

- [ ] **T25-040 — Add configured-capacity scenario.** Harness derives client count from session configuration; generic runner contains no hardcoded four-player assumption. Generic runner is role-count agnostic; production configured-capacity scenario remains.
- [ ] **T25-041 — Add join-in-progress scenario.** Mutate game state before a new client joins and verify current-state convergence/identity allocation. Harness delayed launch is implemented; production proof remains.
- [ ] **T25-042 — Add repeated reconnect scenario.** Multiple transport replacements preserve one durable member/character and create no duplicates. Harness relaunch attempts are implemented; production proof remains.
- [ ] **T25-043 — Add persisted rehost scenario.** Save authoritative run, terminate processes, start fresh authority, restore through systems 16/14 and rejoin with preserved gameplay identities/new transport identities.
- [ ] **T25-044 — Classify smoke vs scheduled/release coverage.** Keep normal PR case minimal (authority + two clients); expensive full-capacity/rehost variants run through the repository's existing slower validation tier, not manual registration. Current workflow inventory has targeted, PR and post-merge master gates but no scheduled multiplayer player-validation lane; do not misclassify the post-merge all-EditMode master suite. Resolve the repository-owned slower lane when the production scenario is available.

## Cleanup / close

- [ ] **T25-050 — Prove no test-only networking/runtime path.** Repository search scenario/harness code for direct authority mutation, direct socket state injection or alternate transport implementation.
- [ ] **T25-051 — Verify automatic validation selection.** Module/dependency ownership selects relevant EditMode/PlayMode tests and top-level multiplayer scenario; agents do not enumerate individual tests. Discovery path is confirmed and `Assets/Game/Composition/Kentridge/Playable/Validation` is the existing module-owned assembled-game validation root; final multiplayer target still awaits System24 production composition.
- [ ] **T25-052 — Close with separate-process evidence.** Exact-SHA authority + clients converge through formation, contention, combat, interruption/reconnect and explicit leave with role-tagged artifacts.
