# 14 Game session & campaign orchestration — implementation plan

**Target module:** `Assets/Game/SessionOrchestration/Api` / `Runtime` (`Game.SessionOrchestration.Api`, `Game.SessionOrchestration.Runtime`).

## API

Run lifecycle/readiness, startup request/configuration, composed-session handle, semantic start/resume/shutdown results, and minimal high-level control state. It must not become a query facade for every gameplay subsystem.

## Runtime

1. Build one authoritative runtime graph from configured subsystem implementations and campaign/world/session inputs.
2. Make new-game and resume use the same composition path; only initialization/restore inputs differ.
3. Define deterministic cross-system update/event ordering.
4. Route semantic facts between APIs/adapters without moving domain rules into the orchestrator.
5. Establish `GameplayReady` after required world/session/replication bindings.
6. Implement explicit ordered teardown and reject commands during half-composed/shutdown states.
7. Replace scene-local graph construction in Kentridge with this production path.

## Dependencies

Core domain modules 01-13; #15 Outcomes and #16 Persistence integrate through APIs as they land.

## Tests / proof

Small headless new run, same graph resume, interaction/progression/encounter integration, no one-shot replay on resume, deterministic teardown/recreate.

## Do not build

No campaign-specific rules, game outcome policy, serialization, networking stack, or giant GameMode object.
