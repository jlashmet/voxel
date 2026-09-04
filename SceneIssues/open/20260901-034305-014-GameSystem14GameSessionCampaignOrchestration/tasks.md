# 14 Game session & campaign orchestration — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.SessionOrchestration.Api` / `Game.SessionOrchestration.Runtime`
**Execution rule:** orchestrate one runtime graph and lifecycle; do not absorb subsystem rules or become a universal query/service locator.

## API / lifecycle

- [x] **T14-001 — Inventory current bootstraps/composition roots.** `KentridgePlayableSlice` directly creates `ShowcaseWorld`, character/presentation hosts and `KentridgeCampaignSessionBootstrap.CreateSession`, ticks `CampaignRuntime`, and clears session/world state in scene teardown. `KentridgeCampaignSessionBootstrap` creates Campaign + Inventory runtime state. `CampaignRuntime` remains the domain owner for Story/Quest/Cutscene rules. Outcomes API is landed; Persistence #16 is not yet present.
- [ ] **T14-002 — Establish asmdefs and dependency direction.** Runtime composes implementations through APIs/factories supplied by composition; API stays minimal and engine-neutral.
- [ ] **T14-003 — Define run lifecycle states.** `Uninitialized -> Composing -> Ready -> Running -> ShuttingDown` plus failure/resolved handling as approved; enumerate legal commands per state.
- [ ] **T14-004 — Define start/new/resume requests and results.** Inputs identify campaign/world/session/configuration/save source semantically; no scene names or concrete Runtime objects in API.
- [ ] **T14-005 — Define composed-session handle/readiness contract.** Expose only lifecycle/control identity needed by frontend/tests, not a facade for every subsystem.
- [ ] **T14-006 — Define semantic startup/shutdown failures.** Fail missing dependencies/invalid composition explicitly; no silent fallback runtimes.

## Runtime graph

- [ ] **T14-010 — Build one production graph factory/composer.** Instantiate/configure authoritative subsystem Runtime implementations from composition-provided dependencies.
- [ ] **T14-011 — Make new game and resume share the same graph construction.** Only initialization/restore input differs; no separate “load game runtime.”
- [ ] **T14-012 — Define deterministic update/event ordering.** Record cross-system ordering constraints and execute them consistently without moving domain decisions into orchestration.
- [ ] **T14-013 — Add semantic fact routing/adapters.** Wire interactions, progression, story, encounters, combat, outcomes etc. through public APIs/events.
- [ ] **T14-014 — Implement readiness barrier.** `GameplayReady` only after required world/session/replication/player bindings are established.
- [ ] **T14-015 — Reject commands during partial composition/shutdown.** Prevent half-initialized graph use and reentrant starts/stops.
- [ ] **T14-016 — Implement ordered teardown.** Stop input/commands, settle/publish required authoritative state, detach networking/presentation adapters, dispose graph once.
- [ ] **T14-017 — Integrate Outcomes reaction.** Observe system 15 resolution and transition run lifecycle appropriately without deciding the outcome.
- [ ] **T14-018 — Integrate Persistence restore/capture seam.** System 16 requests coherent capture/restore through the normal graph path; Orchestration does not serialize state.
- [ ] **T14-019 — Replace Kentridge scene-local graph construction.** Kentridge supplies content/configuration and enters the production composer.

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
