# 24 Production-composed built-player vertical slice — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** Kentridge production composition + shared standalone-player validation. No alternate gameplay authority.
**Execution rule:** prove the real production graph through public player/input seams and read-only diagnostics; no authority mutation, teleport, collision bypass, or substitute runtime.

**Validation history:** Compilation/binding defects and the unrelated Structures fixture were corrected. Runs `34000242054`, `34001710667`, `34004591329`, and `34006180411` passed editor validation but failed canonical `MoveToDestination`. Exact request `08274cda34b900a5bfe22a0a890f0306a42dddbc` / run `34008004416` was terminal **cancelled** and therefore proves no acceptance checkbox. Exact request `3030258dd2cb9095e84995d54c55c018ebcbd414` / run `34016877563` reached the destination but failed interaction; the gamepad interaction correction then produced exact request `7dd2aac6afc1c753742076b0302d862c8e88bb6c` / run `34027162564`, which again reached the destination and repeated `WaitDestinationInteraction` until cancellation. Because the same acceptance symptom survived two materially different fixes, a read-only System24 interaction-edge diagnostic is now required before another gameplay fix. Vitality persistence is implemented but still awaits a successful exact terminal gate.

## Baseline / composition cleanup

- [x] **T24-001 — Inventory Kentridge production/prototype bootstraps.** Audited direct session startup, local input/runtime services, well reflection fallbacks and legacy raw input.
- [x] **T24-002 — Define canonical Kentridge entry composition.** Application owns app/input/navigation lifecycle and delegates run lifecycle to #14/#16. Kentridge supplies world/content/site/NPC/cutscene/placement policy and the production session/content factory.
- [x] **T24-003 — Verify one production Input path.** One composition-owned InputContextService/UnityPlayerInputReader is injected into slice, HUD and forest. The raw KentridgeUnityInputBridge was removed.
- [x] **T24-004 — Verify one production session path.** KentridgeProductionCompositionRoot owns Application and GameSessionOrchestrator; slice consumes the graph without ticking session control. Standalone boot remains at FrontEnd until New Game/Continue.
- [x] **T24-005 — Remove/fail alternate runtime fallbacks.** Missing production bindings throw; no local substitute input/session authority.

## Representative gameplay route

- [ ] **T24-010 — Start through frontend.** Launch built player, reach FrontEnd, request New Game and wait for semantic GameplayReady.
- [ ] **T24-011 — Exercise production movement/world query.** Move the controlled Character through real Characters/Input/world to the authored destination using generated public-circulation facts, not direct-through-geometry steering.
- [ ] **T24-012 — Exercise system 13 interaction.** Interact with a real Kentridge WorldObject/NPC through semantic input and observe authoritative result.
- [ ] **T24-013 — Exercise system 11 progression/story consequence.** Real interaction/site/gameplay fact advances an authored objective/Story rule; no completion setters.
- [ ] **T24-014 — Exercise system 12 encounter realization.** Authored encounter consumes WorldBuilder-realized semantic placement/bindings.
- [ ] **T24-015 — Exercise systems 05/01/02/03 combat chain.** Real Combat with Character/Vitality authority resolves encounter through player actions.
- [ ] **T24-016 — Exercise systems 13/10/09 loot/inventory chain.** Real world pickup/container/drop/transfer proves authoritative inventory integration.
- [ ] **T24-017 — Exercise production presentation.** HUD and relevant inventory/progression/session/audio/VFX presentation consume semantic truth and exact built-player visuals are production-quality.

## Save / continue proof

- [ ] **T24-020 — Capture a real mid-slice save.** Use system 16 through Application/public capability after meaningful world/progression/inventory state has changed.
- [ ] **T24-021 — Perform ordered production teardown.** Leave/return uses systems 23/14, not process-memory shortcuts.
- [ ] **T24-022 — Continue through frontend.** Select save via Application/Persistence and restore a fresh production graph.
- [ ] **T24-023 — Verify restored semantic state.** Character identity/position, vitality, inventory, progression, WorldObject and encounter state match save without one-shot replay.
- [ ] **T24-024 — Continue gameplay after restore.** Perform a further real semantic action proving restored graph is live.

## Validation harness / assertions

- [ ] **T24-030 — Reuse the shared built-player harness.** No Kentridge-specific process runner.
- [ ] **T24-031 — Make scenario milestone-driven.** Semantic readiness/action/outcome milestones with bounded timeouts.
- [ ] **T24-032 — Restrict diagnostics to read-only state.** Stable ids/current truth only; no setters or privileged commands.
- [ ] **T24-033 — Capture failure artifacts.** Process log, semantic milestone history and screenshots through shared conventions.
- [ ] **T24-034 — Run repository-derived module tests/player validations before assembled proof.** No manual target enumeration.
- [ ] **T24-035 — Run canonical standalone scenario on exact built SHA.** Record source identity and require no unhandled exceptions/assertions.
- [ ] **T24-036 — Prove corrected product compilation and full terminal exact-SHA gate.** Intermediate/cancelled artifacts are not success.
- [ ] **T24-037 — Make representative combat player-input-driven.** Physical Primary input advances legal player Combat/Vitality turns; AI advances enemies only; focused Combat regression must pass.
- [ ] **T24-038 — Validate production opening handoff and physical exit.** Owned SceneRuntime validation uses real production world/input/collision and must terminal-pass on exact SHA; diagnostic success inside cancelled run `34008004416` is insufficient.
- [ ] **T24-039 — Preserve vitality across production save/Continue.** Capture/restore current/max/defeated/revision through production persistence; focused regressions and canonical restore must pass.
- [ ] **T24-043 — Isolate repeated destination interaction failure before another fix.** Exact built-player diagnostics must prove whether the production semantic `Interact` edge is observed at the reached destination; if observed, investigate nearby-NPC/session resolution, otherwise isolate Input event delivery. Diagnostic code is read-only and System24-command-line gated.

## Cleanup / close

- [ ] **T24-040 — Audit alternate ownership/private shortcuts.** No production scene-local authority or privileged validation mutation remains.
- [ ] **T24-041 — Verify module-local validation remains distinct.** Assembled Kentridge never substitutes for changed modules' focused tests/scenarios.
- [ ] **T24-042 — Close only when every representative domain runs through its production public boundary.** Every required checkbox and acceptance criterion must be complete.