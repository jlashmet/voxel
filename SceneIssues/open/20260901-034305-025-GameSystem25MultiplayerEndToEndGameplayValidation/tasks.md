# 25 Multiplayer end-to-end gameplay validation — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** shared built-player validation infrastructure + multiplayer scenarios. No test-only networking runtime.
**Execution rule:** every process runs the same exact production build and enters through production Application/Sessions/provider surfaces; scenarios observe semantic diagnostics and drive public inputs only.

## Harness foundation

- [x] **T25-001 — Inventory current standalone/multiplayer test runners.** `module-validation-plan.py` discovers paired module-local player scenes/scenarios; `run-module-validation.py` delegates player targets to canonical `player-validation.py`; production multiplayer semantics live in SessionOrchestration/Sessions/GameplayReplication/Continuity.
- [x] **T25-002 — Extend shared runner with generic process roles.** `mode: multiProcess` supports deterministic launch/wait/terminate/kill/relaunch without scenario-specific gameplay code.
- [x] **T25-003 — Add exact-SHA/build identity assertion.** Each launch/relaunch validates process-reported source SHA + executable SHA-256 before gameplay milestones. Exact feature `73c3bafd0268fcc80c453d33900f6848e7571153` passed request `71b21322f8d3bb776553e5d915bc10d2c0664695` in run `33937957149`.
- [x] **T25-004 — Isolate writable process state.** Attempts isolate HOME/temp/config/cache while preserving only role durable state. Exact feature `b2d1847ec109eb8be7a631c084bd60230c5932a2` passed request `382b9c33a2f4edabd7bf46c598e1ee1d9eea0291` in run `33986100313`.
- [x] **T25-005 — Add bounded semantic milestone waiting.** Shared waits use semantic milestone JSON, explicit deadlines, and process-exit diagnostics with no correctness sleeps.
- [x] **T25-006 — Standardize failure artifacts.** Multi-process summaries preserve build identity, lifecycle operations, milestone history, per-role/per-attempt logs/PIDs/exit state, and per-target artifact roots.
- [x] **T25-007 — Define read-only multiplayer diagnostic snapshot.** Aggregate copied immutable values from `IPartySessionQuery` durable roster identity, gameplay-replication revision/readiness/current projections, and `IContinuityQuery` recovery state. Public diagnostic types expose no mutation method, socket id, transport handle, private-field reflection, or privileged command seam; semantic-copy and public-surface regressions passed exact feature `5c1256867bcc07278049595d171abf22e2bd1a33`, request `4fdb3400ef72c2095adcba6353f09c70724dd81a`, run `33995470352`.
- [x] **T25-008 — Prevent stale milestone reuse across sequential assertions.** Monotonic per-attempt milestone cursor and stream-shrink failure are exact-SHA proven by run `33986100313`.
- [x] **T25-009 — Preserve harness-owned role/attempt attribution.** Player payloads cannot spoof role/attempt; exact feature `1727641603ba4645a798b3ca246bc8d9130afb95` passed request `06585b896b23c38a7c929dd3294211c68ca494a2` in run `33987833161`.

## Production topology / entry

> **Dependency correction:** the binding System25 source depends on systems 06/07/08/14, representative authoritative gameplay modules, and shared validation architecture. System24 is related work, not a prerequisite. Current Kentridge playable composition directly creates `GameSessionOrchestrator`/local session identity; System25 must provide the narrow production multiplayer composition that enters through Application + Sessions/provider/UTP rather than waiting for or copying System24.

- [ ] **T25-010A — Complete Application's production joined-party startup.** Initial implementation `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`; correction `cb54eeec77a7178770e4a4c3260276cd9f26c520`. Wait for Active plus matching connected/gameplay-ready local membership before one local graph startup. Do not send client Start, grant readiness, or retry failed composition every frame; preserve leave/rejoin reset. Real `GameSessionOrchestrator` replaces the earlier substitute fixture; 26 authored cases cover pre/post-start readiness and disconnected/recovery/left states. The owned Application player scene runs `ApplicationJoinedPartyValidation` through production Application/Orchestration, requiring synchronization-wait, single startup, leave and fresh rejoin. **Execution pending:** preserved request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3` tests only source `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`; run `34002524305`, job `101403824713` remains queued. Newer source needs later exact validation. Keep unchecked; these boundary checks do not replace separate-process topology evidence.
- [ ] **T25-010B — Carry session admission through production UTP.** Required by T25-010/011. Implemented in `e3ea06d7589c0a2d13a53962a6ebccbf67489e5b`: explicit client request/server reply framing, maximum 1,200-byte EVENT packet, exact length/direction validation, transport-owned sender identity and optional queue handlers. Unhandled/malformed admission fails closed; Net never grants membership or gameplay readiness. Existing client/server runtimes own delivery. Added 27 authored `Assets/VoxelEngine/Net/Tests/EditMode/SessionAdmissionTransportTests.cs` cases and owned `Validation/SessionAdmissionTransportValidation.unity` plus paired scenario using the same bounded production-runtime probe. It covers two-client isolation, maximum payload, unchanged alteration traffic and a replacement connection. **Not executed:** preserve the queued original request; no new CI request was created. Keep unchecked until exact-SHA module tests/player validation pass. This is transport-only proof, not Sessions/provider admission or full multiplayer acceptance.
- [ ] **T25-010 — Launch real authority topology.** Route Kentridge multiplayer host through production `ApplicationFlowCoordinator.RequestHost` -> `ISessionFormationService.Host` -> Sessions/provider/UTP admission. Do not attach test sockets or construct a second gameplay authority.
- [ ] **T25-011 — Launch client A and client B as separate OS processes.** Both join through production `ApplicationFlowCoordinator.RequestJoin` / `ISessionFormationService.Join` and wait for GameplayReady.
- [ ] **T25-012 — Assert durable identity topology.** Unique PartyMemberIds/slots/CharacterIds and consistent roster projections across authority and both clients.
- [ ] **T25-013 — Verify baseline state convergence.** Compare authoritative semantic revision/state against both clients after readiness before scenario mutations.

## Authoritative gameplay cases

- [ ] **T25-020 — Add two-client contention case.** Prefer one Loot/WorldObject claim: both clients issue valid competing player intents; authority accepts exactly one and all processes converge.
- [ ] **T25-021 — Assert conservation/exactly-once result.** World item and inventories reflect one committed transfer and no duplicate/lost quantity.
- [ ] **T25-022 — Add combat/vitality convergence case.** Real player/encounter/combat actions change authoritative Character Vitality; both clients converge on current state and defeat transition where applicable.
- [ ] **T25-023 — Add shared progression observation.** A real semantic gameplay fact updates shared Progression and all clients observe the same authoritative objective/quest state.

## Reconnect / leave

- [ ] **T25-030 — Interrupt one client unexpectedly.** Kill/drop the client process/connection without executing Leave Game; authority keeps durable identity under Continuity policy.
- [ ] **T25-031 — Mutate authoritative state while client is absent.** Change at least one character/vitality/inventory/progression fact through normal gameplay from remaining processes.
- [ ] **T25-032 — Reconnect as a fresh client process/connection.** Use production reconnect/session flow; assert new transient connection with same PartyMemberId/PlayerSlot/CharacterId.
- [ ] **T25-033 — Assert current-state recovery.** Reconnected client receives absent-period state changes and reaches GameplayReady without replaying historical audio/VFX/other one-shots.
- [ ] **T25-034 — Verify explicit Leave Game separately.** Application/session leave follows membership removal/teardown and is not treated as reconnectable interruption.

## Extended/release scenarios

- [ ] **T25-040 — Add configured-capacity scenario.** Derive client count from production session configuration; no hardcoded four-player assumption.
- [ ] **T25-041 — Add join-in-progress scenario.** Mutate game state before a new client joins and verify current-state convergence/identity allocation.
- [ ] **T25-042 — Add repeated reconnect scenario.** Multiple transport replacements preserve one durable member/character and create no duplicates.
- [ ] **T25-043 — Add persisted rehost scenario.** Save authoritative run, terminate processes, start fresh authority, restore through systems 16/14 and rejoin with preserved gameplay identities/new transient transport identities.
- [x] **T25-044 — Classify smoke vs scheduled/release coverage.** Ordinary `<Module>/Validation/` is smoke; expensive scenarios live under structural `<Module>/Validation/Release/` and are repository-discovered.

## Cleanup / close

- [x] **T25-050 — Prove no test-only networking/runtime path.** System25 harness only controls production processes/lifecycle/state isolation/semantic waits/logs; no direct socket injection, alternate transport, gameplay authority mutation, or privileged mutation command exists.
- [ ] **T25-051 — Verify automatic validation selection.** Current harness/tooling selection is compatibility-green in feature `94ab660260d9b066f030f702500aba092b321a98`, request `4625c82ab1710411723aa4afdecc646826f8fe51`, run `33992072202`. Keep open until final multiplayer smoke/release targets exist and are automatically selected.
- [ ] **T25-052 — Close with separate-process evidence.** Exact-SHA authority + clients converge through formation, contention, combat, interruption/reconnect and explicit leave with role-tagged artifacts.
