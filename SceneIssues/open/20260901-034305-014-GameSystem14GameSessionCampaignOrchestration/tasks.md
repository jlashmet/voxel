# 14 Game session & campaign orchestration — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.SessionOrchestration.Api` / `Game.SessionOrchestration.Runtime`
**Execution rule:** orchestrate one runtime graph and lifecycle; do not absorb subsystem rules or become a universal query/service locator.

## API / lifecycle

- [x] **T14-001 — Inventory current bootstraps/composition roots.** `KentridgePlayableSlice` directly created `ShowcaseWorld`, character/presentation hosts and `KentridgeCampaignSessionBootstrap.CreateSession`, ticked `CampaignRuntime`, and cleared session/world state in scene teardown. `KentridgeCampaignSessionBootstrap` creates Campaign + Inventory runtime state. `CampaignRuntime` remains the Story/Quest/Cutscene domain owner. Outcomes API is landed; Persistence #16 is not yet present.
- [x] **T14-002 — Establish asmdefs and dependency direction.** Added engine-neutral `Game.SessionOrchestration.Api` and `.Runtime`; Runtime depends only on the semantic Session API and `Game.Outcomes.Api`. Kentridge composition supplies the concrete graph factory and continues to own concrete Campaign/Inventory construction.
- [x] **T14-003 — Define run lifecycle states.** API defines `Uninitialized`, `Composing`, `Ready`, `Running`, `Resolved`, `ShuttingDown`, `Stopped`, and `Failed`; orchestrator enforces legal Prepare/Run/Tick/Capture/Shutdown states.
- [x] **T14-004 — Define start/new/resume requests and results.** `GameSessionIdentity`, `GameSessionStartRequest`, semantic restore-source id, and operation results carry campaign/world/session/configuration identity without scene names or Runtime instances.
- [x] **T14-005 — Define composed-session handle/readiness contract.** `ComposedSessionHandle` exposes only semantic identity/generation; `GameSessionSnapshot` exposes lifecycle/readiness/failure without subsystem query forwarding.
- [x] **T14-006 — Define semantic startup/shutdown failures.** `GameSessionFailure` and `SessionCompositionException` distinguish invalid state, missing dependency, failed composition/restore/readiness/start/capture/shutdown with no fallback graph.

## Runtime graph

- [x] **T14-010 — Build one production graph factory/composer.** `ISessionRuntimeGraphFactory` is composition-supplied; `KentridgeSessionRuntimeGraphFactory` reuses `KentridgeCampaignSessionBootstrap.CreateSession` for the real Campaign/Inventory graph.
- [x] **T14-011 — Make new game and resume share the same graph construction.** Both use the same `Compose(identity)` path; new calls graph initialization only when entering Running, while resume restores through `ISessionPersistenceBridge` before readiness and never invokes new-game initialization.
- [x] **T14-012 — Define deterministic update/event ordering.** `SessionUpdatePhase` plus phase/order/semantic-id sorting establishes CommandIntake -> Interaction -> Progression/Story -> Encounter -> Combat -> Replication -> Presentation ordering with duplicate-order rejection.
- [x] **T14-013 — Add semantic fact routing/adapters.** Runtime orders composition-supplied public-API steps; Kentridge supplies a Campaign step and command adapter, while the headless integration fixture routes Encounter/Combat through `IEncounterRegistry` / `IEncounterCombatCoordinator` without SessionOrchestration depending on their Runtime assemblies.
- [x] **T14-014 — Implement readiness barrier.** Prepare reaches `Ready` only when the composed graph reports `GameplayBindingsReady`; Kentridge reports ready only after its existing bootstrap validates/realizes campaign world and actor bindings.
- [x] **T14-015 — Reject commands during partial composition/shutdown.** Orchestrator rejects invalid lifecycle commands and Kentridge graph gameplay commands stay disabled until `EnterRunning()` calls `StartCommands()`.
- [x] **T14-016 — Implement ordered teardown.** Shutdown executes `StopCommands -> SettleAuthoritativeState -> DetachExternalAdapters -> Dispose` exactly once while continuing cleanup after the first failure; Kentridge invokes orchestration shutdown before actor/world disposal.
- [x] **T14-017 — Integrate Outcomes reaction.** Running graph observes optional `IGameOutcomeQuery`; `Resolved` stops commands and transitions lifecycle without choosing disposition/outcome.
- [x] **T14-018 — Integrate Persistence restore/capture seam.** `ISessionPersistenceBridge` receives the already-composed graph for restore/capture; SessionOrchestration owns no serializer or persistence storage.
- [x] **T14-019 — Replace Kentridge scene-local graph construction.** `KentridgePlayableSlice` now supplies semantic content/configuration to the graph factory, prepares through `GameSessionOrchestrator`, enters Running after existing presentation readiness, ticks through ordered graph steps, routes NPC commands through the graph, and shuts it down before existing composition-owned resource disposal.

## Verification

- [ ] **T14-020 — Headless new-run test.** Compose -> Ready -> Running with representative core modules through real APIs.
- [ ] **T14-021 — Same-graph resume test.** Fresh graph + restored state follows the identical composition path and reaches Running.
- [ ] **T14-022 — Cross-system integration test.** Semantic interaction advances Progression/Story and can activate Encounter/Combat through adapters without Runtime coupling.
- [ ] **T14-023 — Readiness/failure tests.** Missing dependency, failed world/session binding and premature command all fail deterministically.
- [ ] **T14-024 — Teardown/recreate test.** Ordered shutdown releases graph resources and a second run can start cleanly.
- [ ] **T14-025 — No-one-shot-replay resume regression.** Current state is restored without replaying historical presentation/gameplay events.
- [ ] **T14-026 — Run automatic module and top-level integration tests.**

## Cleanup / close

- [ ] **T14-030 — Remove alternate composition roots.** Search scenes/tests for direct production construction of Campaign/Combat/Input/etc.; fixtures may compose explicitly only when testing modules.
- [ ] **T14-031 — God-object audit.** No campaign rules, serialization, network protocol, domain state stores or broad subsystem query facade in SessionOrchestration.
- [ ] **T14-032 — Close with lifecycle proof.** New, resume, running, resolved reaction and teardown all use one production runtime graph.
