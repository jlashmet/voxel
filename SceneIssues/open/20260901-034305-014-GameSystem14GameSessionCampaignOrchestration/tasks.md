# 14 Game session & campaign orchestration — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.SessionOrchestration.Api` / `Game.SessionOrchestration.Runtime`
**Execution rule:** orchestrate one runtime graph and lifecycle; do not absorb subsystem rules or become a universal query/service locator.

## API / lifecycle

- [x] **T14-001 — Inventory current bootstraps/composition roots.** Kentridge owned direct campaign/session construction and local teardown; subsystem Runtime creation was split between Kentridge session and forest encounter composition.
- [x] **T14-002 — Establish asmdefs and dependency direction.** Session API is semantic/engine-neutral; Runtime depends only on Session API + Outcomes API; composition supplies concrete graph factories.
- [x] **T14-003 — Define run lifecycle states.** `Uninitialized -> Composing -> Ready -> Running -> Resolved/ShuttingDown -> Stopped`, with explicit `Failed` state and legal command checks.
- [x] **T14-004 — Define start/new/resume requests and results.** Semantic campaign/world/session/configuration/save-source identity only; no scene or concrete Runtime references.
- [x] **T14-005 — Define composed-session handle/readiness contract.** Exposes identity/generation/lifecycle/readiness/failure, not a subsystem facade.
- [x] **T14-006 — Define semantic startup/shutdown failures.** Missing dependencies, composition/restore/binding/start/capture/shutdown failures are explicit; no fallback runtimes.

## Runtime graph

- [x] **T14-010 — Build one production graph factory/composer.** `ISessionRuntimeGraphFactory` supplies an authoritative graph; Kentridge implements the production factory around existing campaign composition.
- [x] **T14-011 — Make new game and resume share the same graph construction.** `Prepare` always composes the same graph; only initialize-vs-restore input differs.
- [x] **T14-012 — Define deterministic update/event ordering.** Explicit semantic phases and stable `(phase, order, semanticId)` ordering reject ambiguous duplicates.
- [x] **T14-013 — Add semantic fact routing/adapters.** Campaign commands and phase step route through public APIs; headless integration covers Story/progression/Encounter/Combat adapters without Runtime coupling.
- [x] **T14-014 — Implement readiness barrier.** `Ready` is reached only after graph `GameplayBindingsReady` succeeds.
- [x] **T14-015 — Reject commands during partial composition/shutdown.** Lifecycle guards reject premature/reentrant start/tick/capture/shutdown use.
- [x] **T14-016 — Implement ordered teardown.** Stop commands -> settle authoritative state -> detach adapters -> dispose exactly once, continuing cleanup after failures.
- [x] **T14-017 — Integrate Outcomes reaction.** Observe `IGameOutcomeQuery` resolution and move lifecycle to `Resolved` without deciding the outcome.
- [x] **T14-018 — Integrate Persistence restore/capture seam.** Resume/capture use an external persistence bridge; orchestration contains no serializer.
- [x] **T14-019 — Replace Kentridge scene-local graph construction.** `KentridgePlayableSlice` enters the production SessionOrchestration graph and its forest Encounter/Combat slice is a graph extension.

## Verification

- [ ] **T14-020 — Headless new-run test.** Compose -> Ready -> Running with representative core modules through real APIs.
- [ ] **T14-021 — Same-graph resume test.** Fresh graph + restored state follows the identical composition path and reaches Running.
- [ ] **T14-022 — Cross-system integration test.** Semantic interaction advances Progression/Story and can activate Encounter/Combat through adapters without Runtime coupling.
- [ ] **T14-023 — Readiness/failure tests.** Missing dependency, failed world/session binding and premature command all fail deterministically.
- [ ] **T14-024 — Teardown/recreate test.** Ordered shutdown releases graph resources and a second run can start cleanly.
- [ ] **T14-025 — No-one-shot-replay resume regression.** Current state is restored without replaying historical presentation/gameplay events.
- [ ] **T14-026 — Run automatic module and top-level integration tests.**
- [x] **T14-027 — Record module-local validation ownership.** SessionOrchestration is a pure engine-neutral/headless module, so its owned EditMode assembly is the documented module-local validation exception; Kentridge composition is integration-only and remains covered by the SceneIssue built-player replay.

## Cleanup / close

- [x] **T14-030 — Remove alternate composition roots.** Feature-diff audit confirms the playable scene now enters `GameSessionOrchestrator`; authoritative Campaign construction lives in `KentridgeSessionRuntimeGraphFactory`, Encounter/Combat/Input construction lives in its session extension, and scene anchors only hand those factories/registries across composition boundaries. Direct construction remaining in tests is fixture-only.
- [x] **T14-031 — God-object audit.** SessionOrchestration product assemblies own lifecycle/order and semantic ports only; Campaign/Story/Progression rules, serializers, network protocol, subsystem state stores and broad service/query access remain outside the orchestrator.
- [ ] **T14-032 — Close with lifecycle proof.** New, resume, running, resolved reaction and teardown all use one production runtime graph.
