# 24 Production-composed built-player vertical slice — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** Kentridge production composition + shared standalone-player validation architecture. No new generic gameplay Api/Runtime module.
**Execution rule:** this proves the real production graph; the scenario may drive public player/input seams and observe diagnostics, but it may not mutate authority or substitute simplified runtimes.

**Validation history:** System 23 Application is closed on master and merged into this branch. Run `33984774287` attempt 2 exposed the first compile blockers; run `33988814815` exposed additional Application namespace/loot-id bindings. Run `33995706164` for product SHA `e724343d0f2a2d05631e1305f344ba94f832e94f` exposed six remaining `Application.isPlaying` namespace collisions; those calls are now explicitly `UnityEngine.Application.isPlaying`. Run `33997426174` for product SHA `2e224f76eb80649dc84a82b79090a1142ab8eaa4` compiled successfully but failed an unrelated Structures PlayMode fixture when SRP DebugManager read legacy `UnityEngine.Input`; current master already contained that fixture bootstrap fix and was integrated via merge `36ecce581846fe6b0e1c021f9980890c279ad3e4`. Run `34000242054` for product SHA `b0b1e75ba97696a83bc0d2a5658e3fe28e22022f` then passed the persistent EditMode/PlayMode phase and reached canonical Kentridge built-player validation, proving FrontEnd/New Game/gameplay-ready/control before timing out in `MoveToDestination`. Run `34001710667` for product SHA `14551b3475a0c96d340f647b48d8692022a5749f` reproduced the missing movement milestone after the driver switched to the authored-exit direction: the log showed `gameplay-control` at `(111.5,32.1,82.8)` before the production release subsequently snapped the player near `(101.2,22.0,55.9)`. Root-cause isolation found a lifecycle race: final dialogue dismissal can clear the active-cutscene state late in the slice update, allowing the later System24 driver to see `GameplayControlEnabled` one frame before `ReleasePlayerForGameplay()` runs. Historical doorway regression `c3b3470a4cb1c992d2c95a959347d10e973a3c5d` already proves the generated interior/exterior approach and signed exit corridor. Production `GameplayControlEnabled` now additionally requires `_openingGameplayReleased`; T24-036 and all assembled-route tasks remain open until fresh exact-SHA CI proves the full route.

## Baseline / composition cleanup

- [x] **T24-001 — Inventory Kentridge production/prototype bootstraps.** Audited baseline: playable slice directly owned session startup; forest extension created local input/runtime services; well presentation owned local input/reflection fallbacks; legacy raw input remained.
- [x] **T24-002 — Define canonical Kentridge entry composition.** Application owns app/input/navigation lifecycle and delegates run lifecycle to #14/#16; Kentridge supplies world/content/site/NPC/cutscene/placement policy and a production session/content factory; Unity adapters receive public composed capabilities and never construct competing authority.
- [x] **T24-003 — Verify one production Input path.** One composition-owned `InputContextService` + `UnityPlayerInputReader` is injected into the playable slice, HUD and forest extension. Kentridge gameplay consumers use `Game.Input.Api`; the legacy `KentridgeUnityInputBridge` raw `UnityEngine.Input` owner was removed.
- [x] **T24-004 — Verify one production session path.** `KentridgeProductionCompositionRoot` owns Application + `GameSessionOrchestrator`; the slice consumes the current graph and does not tick session control. The production scene serializes `m_AutoStartNewGame: 0`, so standalone boot remains at Application FrontEnd until New Game/Continue is requested.
- [x] **T24-005 — Remove/fail alternate runtime fallbacks.** Slice/HUD/forest composition requires injected production capabilities and throws on missing bindings; no local substitute input/session authority is created. Multiplayer-unavailable adapters explicitly reject unsupported requests rather than simulating a formed session.

## Representative gameplay route

- [ ] **T24-010 — Start through frontend.** Launch built player, reach FrontEnd, request New Game and wait for semantic `GameplayReady`.
- [ ] **T24-011 — Exercise production movement/world query.** Move the controlled Character through the real Characters/Input/world path to an authored interaction/encounter area.
- [ ] **T24-012 — Exercise system 13 interaction.** Interact with a real Kentridge WorldObject/NPC through semantic input and observe authoritative result.
- [ ] **T24-013 — Exercise system 11 progression/story consequence.** Real interaction/site/gameplay fact advances an authored objective/Story rule; driver cannot call completion setters.
- [ ] **T24-014 — Exercise system 12 encounter realization.** Kentridge authored encounter consumes WorldBuilder-realized semantic placement/bindings.
- [ ] **T24-015 — Exercise systems 05/01/02/03 combat chain.** Encounter activates real Combat with Character/Vitality authority and reaches semantic combat/encounter resolution through player actions.
- [ ] **T24-016 — Exercise systems 13/10/09 loot/inventory chain.** Perform a real world pickup/container/drop/transfer action sufficient to prove authoritative inventory integration.
- [ ] **T24-017 — Exercise production presentation.** HUD plus relevant inventory/progression/session/audio/VFX presentation consume semantic truth; none are required for authority.

## Save / continue proof

- [ ] **T24-020 — Capture a real mid-slice save.** Use system 16 through application/public capability after meaningful world/progression/inventory state has changed.
- [ ] **T24-021 — Perform ordered production teardown.** Leave/return or explicit test lifecycle uses systems 23/14, not process-memory shortcuts.
- [ ] **T24-022 — Continue through frontend.** Select the save via Application/Persistence and restore a fresh production graph.
- [ ] **T24-023 — Verify restored semantic state.** Character ids/state, inventory, progression, world objects and relevant encounter/outcome state match the save with no historical one-shot replay.
- [ ] **T24-024 — Continue gameplay after restore.** Perform at least one additional real semantic action proving the restored graph is live, not merely inspectable.

## Validation harness / assertions

- [ ] **T24-030 — Reuse the shared built-player harness.** Add only generic capability if missing; do not create a Kentridge-specific process runner.
- [ ] **T24-031 — Make scenario milestone-driven.** Wait on semantic readiness/action/outcome milestones with bounded timeouts; eliminate arbitrary sleeps as primary synchronization.
- [ ] **T24-032 — Restrict diagnostic access to read-only state.** Diagnostic snapshot may expose stable ids/current semantic state but no setters or privileged gameplay commands.
- [ ] **T24-033 — Capture failure artifacts.** Role/process log, semantic milestone history and relevant screenshot/render artifact on failure through shared harness conventions.
- [ ] **T24-034 — Run editor/module-local tests before built-player proof.** Automatic affected-module discovery plus top-level Kentridge integration tests.
- [ ] **T24-035 — Run canonical standalone built-player scenario on exact built SHA.** Record build/commit identity and require no unhandled exceptions/assertions.
- [ ] **T24-036 — Prove corrected product compilation on exact SHA.** Runs `33997426174`, `34000242054`, and `34001710667` prove System24 compiles and reaches the canonical assembled player; `34001710667` isolated a production gameplay-readiness race before the authored opening release. Fresh exact-SHA CI must pass repository-owned module/player validation end to end before this task is complete.
- [ ] **T24-037 — Make representative combat player-input-driven.** Map production `PrimaryPressed` input to a legal local-player attack, ensure deterministic combat AI advances only enemy turns, drive the player attack through the normal Input System in the built-player scenario, and add focused Combat regression coverage proving player input causes authoritative Combat/Vitality turn progress.

## Cleanup / close

- [ ] **T24-040 — Search Kentridge for alternate service ownership/private test shortcuts.** Remove production scene-local authority and privileged setters.
- [ ] **T24-041 — Verify module-local validation remains distinct.** Kentridge is assembled-game proof, not a substitute for each changed module's focused tests/scenario.
- [ ] **T24-042 — Close only when every representative domain runs through its production public boundary.** No fallback/prototype path may be accepted as equivalent.
