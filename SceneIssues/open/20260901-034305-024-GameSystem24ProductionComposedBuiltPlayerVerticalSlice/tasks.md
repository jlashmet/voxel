# 24 Production-composed built-player vertical slice — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** Kentridge production composition + shared standalone-player validation architecture. No new generic gameplay Api/Runtime module.
**Execution rule:** prove the real production graph through public player/input seams and read-only diagnostics; no authority mutations or simplified substitute runtimes.

**Validation history:** Application and current master are integrated. Compilation/binding defects were corrected; master merge `36ecce581846fe6b0e1c021f9980890c279ad3e4` supplied the upstream Structures fixture fix. Runs `34000242054`, `34001710667`, `34004591329`, and `34006180411` passed editor validation but failed built-player MoveToDestination. The `_openingGameplayReleased` readiness gate is not a sufficient movement fix. Logs do not identify blocked geometry or pub-exit state; captures are not production-quality traversal proof. T24-038 isolates the invariant before another route fix.

**Latest completed request:** `1b08b40d1bad21e866a2d2095edf75887fdd8863`, run `34006180411`, product `37416dd7b1a35b8f9a181f8d3005cf35b103906b`, failed; artifact `9981502669`. Its derived plan omitted the new exit probe because SceneRuntime did not own its Tests root. That ownership is now corrected, and vitality preservation is implemented, but both require a new exact-head request. Use only ci-test/fixes/agent-2 and preserve the next request while queued/running. All remaining boxes require actual validation evidence.

## Baseline / composition cleanup

- [x] **T24-001 — Inventory Kentridge production/prototype bootstraps.** Audited direct session startup, local input/runtime services, well reflection fallbacks and legacy raw input.
- [x] **T24-002 — Define canonical Kentridge entry composition.** Application owns app/input/navigation lifecycle and delegates run lifecycle to #14/#16. Kentridge supplies world/content/site/NPC/cutscene/placement policy and the production session/content factory; adapters receive public capabilities rather than competing authority.
- [x] **T24-003 — Verify one production Input path.** One composition-owned InputContextService/UnityPlayerInputReader is injected into slice, HUD and forest. The raw KentridgeUnityInputBridge was removed.
- [x] **T24-004 — Verify one production session path.** KentridgeProductionCompositionRoot owns Application and GameSessionOrchestrator; slice consumes the graph without ticking session control. Serialized auto-start is off; standalone boot stays at FrontEnd until New Game/Continue.
- [x] **T24-005 — Remove/fail alternate runtime fallbacks.** Missing production bindings throw. No local substitute input/session authority; unsupported multiplayer formation explicitly rejects.

## Representative gameplay route

- [ ] **T24-010 — Start through frontend.** Launch built player, reach FrontEnd, request New Game and wait for semantic GameplayReady.
- [ ] **T24-011 — Exercise production movement/world query.** Move the controlled Character through real Characters/Input/world to an authored interaction/encounter area.
- [ ] **T24-012 — Exercise system 13 interaction.** Interact with a real Kentridge WorldObject/NPC through semantic input and observe authoritative result.
- [ ] **T24-013 — Exercise system 11 progression/story consequence.** Real interaction/site/gameplay fact advances an authored objective/Story rule; no completion setters.
- [ ] **T24-014 — Exercise system 12 encounter realization.** Authored encounter consumes WorldBuilder-realized semantic placement/bindings.
- [ ] **T24-015 — Exercise systems 05/01/02/03 combat chain.** Real Combat with Character/Vitality authority resolves encounter through player actions.
- [ ] **T24-016 — Exercise systems 13/10/09 loot/inventory chain.** Real world pickup/container/drop/transfer proves authoritative inventory integration.
- [ ] **T24-017 — Exercise production presentation.** HUD and relevant inventory/progression/session/audio/VFX presentation consume semantic truth, never own authority.

## Save / continue proof

- [ ] **T24-020 — Capture a real mid-slice save.** Use system 16 through Application/public capability after meaningful world/progression/inventory changes.
- [ ] **T24-021 — Perform ordered production teardown.** Leave/return or explicit test lifecycle uses systems 23/14, not process-memory shortcuts.
- [ ] **T24-022 — Continue through frontend.** Select save via Application/Persistence and restore a fresh production graph.
- [ ] **T24-023 — Verify restored semantic state.** Character ids/state, inventory, progression, world objects and relevant encounter/outcome state match the save without historical one-shot replay.
- [ ] **T24-024 — Continue gameplay after restore.** At least one further real semantic action proves the restored graph is live, not merely inspectable.

## Validation harness / assertions

- [ ] **T24-030 — Reuse the shared built-player harness.** Add only missing generic capability; no Kentridge-specific process runner.
- [ ] **T24-031 — Make scenario milestone-driven.** Semantic readiness/action/outcome synchronization, bounded timeouts, no arbitrary sleeps as primary synchronization.
- [ ] **T24-032 — Restrict diagnostic access to read-only state.** Stable ids/current truth only; no setters or privileged gameplay commands.
- [ ] **T24-033 — Capture failure artifacts.** Role/process log, semantic milestone history, relevant screenshot/render artifact via shared conventions.
- [ ] **T24-034 — Run editor/module-local tests before built-player proof.** Automatic affected-module discovery plus top-level Kentridge integration tests.
- [ ] **T24-035 — Run canonical standalone scenario on exact built SHA.** Record build/commit identity and require no unhandled exceptions/assertions.
- [ ] **T24-036 — Prove corrected product compilation on exact SHA.** Require a successful terminal module/player gate, not intermediate artifacts.
- [ ] **T24-037 — Make representative combat player-input-driven.** Production PrimaryPressed maps to a legal player attack; deterministic AI advances only enemy turns. Drive physical Input System attacks and prove authoritative Combat/Vitality turn progress with focused regression coverage.
- [ ] **T24-038 — Isolate and validate production opening handoff and physical exit.** Use the real production SceneRuntime/Validation/KentridgeOpeningControlValidation scene/scenario; reject control before camera handoff, observe device/production input and kinematics, require physical public exit. No reflection, teleport, authority setter, collision bypass or alternate runtime. Establish actual module ownership by moving the existing Game.Kentridge.PlayableSlice.Tests.EditMode assembly/test subtree from parent Tests/EditMode/FarWorld into SceneRuntime/Tests/EditMode; the planner only discovers roots from owned Tests. Keep assembly identity and existing test content/metadata; do not register targets manually. Require the derived plan to include the new scene and an exact-SHA player PASS; inspect actual trajectory rather than accepting an exit flag alone. Use failures to discriminate input from access before another route fix.
- [ ] **T24-039 — Preserve vitality across production save/Continue.** Required by T24-023: former snapshot sections omitted vitality while fresh composition created full health. Implementation now captures/restores current/maximum/defeated/revision via IVitalityService and compares live saved/restored health. Nine module-local behavioral cases exercise damaged/defeated round trips and malformed payloads through the production contributor. Require actual tests and canonical restored-state proof; implementation alone is insufficient.

## Cleanup / close

- [ ] **T24-040 — Search Kentridge for alternate service ownership/private test shortcuts.** Remove production scene-local authority and privileged setters.
- [ ] **T24-041 — Verify module-local validation remains distinct.** Kentridge assembled-game proof never substitutes for changed modules' focused tests/scenarios.
- [ ] **T24-042 — Close only when every representative domain runs through its production public boundary.** No fallback/prototype accepted as equivalent; all required criteria and checkboxes complete.
