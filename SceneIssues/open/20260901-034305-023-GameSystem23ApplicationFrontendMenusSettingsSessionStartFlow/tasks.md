# 23 Application frontend, menus, settings & session start flow — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Application.Api` / `Game.Application.Runtime`
**Execution rule:** menus express local player intent and coordinate owning APIs; Application never constructs or mutates gameplay domains directly.

## API / application model

- [ ] **T23-001 — Inventory current app/menu bootstrap.** Find startup scenes, new/load buttons, host/join flows, pause menus, PlayerPrefs/settings, raw input and direct network/gameplay construction.
- [ ] **T23-002 — Establish asmdefs.** Application.Api remains engine-neutral; Runtime/client view layer may use Unity UI/Input while consuming Sessions/Orchestration/Persistence APIs.
- [ ] **T23-003 — Define application lifecycle.** `Boot -> FrontEnd -> StartingSession -> InGame -> ReturningToFrontEnd -> FrontEnd`, plus explicit Exiting paths and legal transitions.
- [ ] **T23-004 — Define semantic user intents/results.** NewGame, Continue, Host, Join, LeaveGame, QuitApplication and startup failure reasons; no scene-name or raw socket contracts.
- [ ] **T23-005 — Define screen/navigation model.** Local navigation state distinct from gameplay state and in-game menu distinct from authoritative pause.
- [ ] **T23-006 — Define loading/readiness model.** StartingSession waits for system 14/06 semantic readiness; connected or scene-loaded alone is insufficient.
- [ ] **T23-007 — Define `IUserPreferencesStore` and supported preference contracts.** Include only approved settings; Input binding overrides flow through Input System adapter.

## Runtime / frontend

- [ ] **T23-010 — Implement ApplicationFlowCoordinator.** Enforce lifecycle transitions, serialize conflicting user intents and surface semantic startup failures.
- [ ] **T23-011 — Implement New Game.** Delegate to system 14 production start path and enter InGame only after GameplayReady.
- [ ] **T23-012 — Implement Continue.** List/select save through Persistence API, then system 16 + 14 normal restore/composition path.
- [ ] **T23-013 — Implement Host/Join.** Delegate party/session formation to system 07/provider seam; do not invoke UTP sockets directly from UI.
- [ ] **T23-014 — Implement Leave Game.** Route explicit leave through systems 07/08 as applicable, request ordered system 14 teardown, then return to FrontEnd.
- [ ] **T23-015 — Implement Quit Application.** Perform semantic teardown when InGame, then exit; do not treat quit as gameplay outcome.
- [ ] **T23-016 — Implement front-end/in-game screen navigation.** Nested `Ui` InputContext push/pop with deterministic unwind; opening menu does not globally `Time.timeScale=0` unless separately approved policy says so.
- [ ] **T23-017 — Implement settings/preferences.** Persist/apply supported local preferences, audio settings and Input System binding overrides through owning APIs/adapters.
- [ ] **T23-018 — Integrate SessionPresentation.** Party/readiness/continuity screens consume system 20 semantic models rather than transport state.
- [ ] **T23-019 — Integrate Outcomes aftermath.** On system 15 resolution, present configured outcome/front-end flow after orchestration semantics; Application does not decide outcome.
- [ ] **T23-020 — Remove scene-name-driven lifecycle/direct domain construction.** Scene loading becomes a view/composition detail under the semantic app flow.

## Verification

- [ ] **T23-030 — Lifecycle transition unit tests.** Valid/invalid transitions, duplicate clicks/intents and startup failure recovery.
- [ ] **T23-031 — New Game built-player flow.** Boot -> FrontEnd -> NewGame -> StartingSession -> GameplayReady -> InGame through production seams.
- [ ] **T23-032 — Continue built-player flow.** Saved session restores via systems 16/14 and reaches InGame without alternate runtime.
- [ ] **T23-033 — Host/join flow test.** Frontend uses Sessions provider/semantic readiness and never direct sockets.
- [ ] **T23-034 — Nested menu/InputContext test.** Close order restores prior context; no legacy key polling.
- [ ] **T23-035 — Settings persistence test.** Supported preferences and rebinding overrides survive application restart and update presentation.
- [ ] **T23-036 — Leave/return vs Quit test.** Leave returns FrontEnd after semantic teardown; Quit exits; neither fabricates GameOutcome.
- [ ] **T23-037 — Failed startup test.** Missing/incompatible save/session returns useful FrontEnd error with no half-running graph.
- [ ] **T23-038 — Module-local built-player frontend validation using shared harness.**

## Cleanup / close

- [ ] **T23-040 — Remove raw network/gameplay construction from UI.** Repository search frontend/menu code for direct Runtime/socket calls.
- [ ] **T23-041 — Remove raw physical input/legacy polling from production menu/gameplay flow.** Unity Input System -> Input.Runtime -> Input.Api remains the path.
- [ ] **T23-042 — Boundary audit.** No gameplay authority, hardcoded save policy, generic matchmaking or scene-name-owned lifecycle.
- [ ] **T23-043 — Close with end-to-end app-flow proof.** New, continue, multiplayer formation, leave and outcome return all traverse semantic owning APIs.
