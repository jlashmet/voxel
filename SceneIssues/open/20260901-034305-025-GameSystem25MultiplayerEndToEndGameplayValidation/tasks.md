# 25 Multiplayer end-to-end gameplay validation — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** shared built-player validation infrastructure + multiplayer scenarios. No gameplay module and no test-only networking runtime.
**Execution rule:** every process runs the same exact production build and enters through systems 23/07; scenarios observe semantic diagnostics and drive public inputs only.

## Harness foundation

- [ ] **T25-001 — Inventory current standalone/multiplayer test runners.** Identify shared built-player harness, UTP loopback tests, process launch/log capture and any feature-specific runner duplication.
- [ ] **T25-002 — Extend shared runner with generic process roles.** Authority/host/client role, arguments/environment, lifecycle and role-tagged stdout/stderr without hardcoding a particular scenario.
- [ ] **T25-003 — Add exact-SHA/build identity assertion.** Every process reports/validates the same build commit/content identity before gameplay assertions proceed.
- [ ] **T25-004 — Isolate writable process state.** Unique save/preferences/log/temp locations per process so clients cannot pass by sharing local files.
- [ ] **T25-005 — Add bounded semantic milestone waiting.** Shared wait primitive consumes read-only diagnostics/readiness and records timeout context; no arbitrary sleep-driven correctness.
- [ ] **T25-006 — Standardize failure artifacts.** Role-tagged logs, process exit status, semantic snapshot/milestone history and screenshots where relevant.
- [ ] **T25-007 — Define read-only multiplayer diagnostic snapshot.** Stable GameSessionId/PartyMemberId/PlayerSlot/CharacterId plus selected current domain truth/revisions, including current combat beat, active/upcoming squad members and event-chain identities where applicable; explicitly no mutation methods.

## Production topology / entry

- [ ] **T25-010 — Launch real authority topology.** Use production UTP/session configuration and system 23/07 startup path, not direct test socket attachment.
- [ ] **T25-011 — Launch client A and client B as separate OS processes.** Both join through the real provider/session formation abstraction and wait for GameplayReady.
- [ ] **T25-012 — Assert durable identity topology.** Unique PartyMemberIds/slots/CharacterIds and consistent roster projections across authority and both clients.
- [ ] **T25-013 — Verify baseline state convergence.** Compare authoritative semantic revision/state against both clients after readiness before scenario mutations.

## Authoritative gameplay cases

- [ ] **T25-020 — Add two-client contention case.** Prefer one Loot/WorldObject claim: both clients issue valid competing player intents; authority accepts exactly one and all processes converge.
- [ ] **T25-021 — Assert conservation/exactly-once result.** World item and inventories reflect one committed transfer and no duplicate/lost quantity.
- [ ] **T25-022 — Add squad-beat combat/vitality convergence case.** Give each participating player a squad, wait for the same authoritative beat/current-active selections, submit at most one deliberate move from each client for its system-selected member, and verify all processes converge on resulting Character Vitality/defeat state.
- [ ] **T25-023 — Add shared progression observation.** A real semantic gameplay fact updates shared Progression and all clients observe the same authoritative objective/quest state.
- [ ] **T25-024 — Prove simultaneous beat resolution.** Both clients' accepted commands belong to the same authoritative beat and resolve through deterministic event ordering rather than serialized player/character turns; client arrival timing must not create a different valid outcome for otherwise equivalent commands.
- [ ] **T25-025 — Add cross-player event-driven combo case.** A move from one player's selected member creates a movement/projectile/impact/spell/world opportunity and a configured character from another squad joins and redirects or transforms/escalates that in-flight event before the final result.
- [ ] **T25-026 — Assert combo-chain convergence and bounds.** Authority and both clients observe the same beat id, active/upcoming sequence, accepted actions, semantic event parent/order, final vitality/world outcome and bounded chain termination with no duplicate effects.
- [ ] **T25-027 — Prove bounded decision count at party scale.** A representative large-party setup keeps deliberate input at one acting-member move per participating player for the beat; additional squad members participate through autonomous/event interactions rather than individual player turns.

## Reconnect / leave

- [ ] **T25-030 — Interrupt one client unexpectedly.** Kill/drop client process/connection without executing Leave Game; authority keeps durable identity under system 08 policy.
- [ ] **T25-031 — Mutate authoritative state while client is absent.** Change at least one character/vitality/inventory/progression fact through normal gameplay from remaining processes; when interruption occurs during combat, also advance authoritative beat/sequence state through normal rules.
- [ ] **T25-032 — Reconnect as a fresh client process/connection.** Use production reconnect/session flow; assert new transport connection but same PartyMemberId/PlayerSlot/CharacterId.
- [ ] **T25-033 — Assert current-state recovery.** Reconnected client receives all absent-period state changes, including current combat beat/active-upcoming sequence when applicable, and reaches GameplayReady without replaying historical audio/VFX/combo one-shots.
- [ ] **T25-034 — Verify explicit Leave Game separately.** A client using system 23 leave path follows membership removal/teardown semantics and is not treated as reconnectable interruption.

## Extended/release scenarios

- [ ] **T25-040 — Add configured-capacity scenario.** Harness derives client count from session configuration; generic runner contains no hardcoded four-player assumption.
- [ ] **T25-041 — Add join-in-progress scenario.** Mutate game state before a new client joins and verify current-state convergence/identity allocation, including current combat beat/sequence if joining during combat is supported.
- [ ] **T25-042 — Add repeated reconnect scenario.** Multiple transport replacements preserve one durable member/character and create no duplicates.
- [ ] **T25-043 — Add persisted rehost scenario.** Save authoritative run, terminate processes, start fresh authority, restore through systems 16/14 and rejoin with preserved gameplay identities/new transport identities.
- [ ] **T25-044 — Classify smoke vs scheduled/release coverage.** Keep normal PR case minimal (authority + two clients); expensive full-capacity/rehost variants run through the repository's existing slower validation tier, not manual registration.

## Cleanup / close

- [ ] **T25-050 — Prove no test-only networking/runtime path.** Repository search scenario/harness code for direct authority mutation, direct socket state injection, alternate transport implementation or client-side combat sequencing/reaction authority.
- [ ] **T25-051 — Verify automatic validation selection.** Module/dependency ownership selects relevant EditMode/PlayMode tests and top-level multiplayer scenario; agents do not enumerate individual tests.
- [ ] **T25-052 — Close with separate-process evidence.** Exact-SHA authority + clients converge through formation, contention, simultaneous squad-beat combat, cross-player event-driven combo, interruption/reconnect and explicit leave with role-tagged artifacts.
