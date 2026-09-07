# 24 Production-composed built-player vertical slice — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** Kentridge production composition + shared standalone-player validation. No alternate gameplay authority.
**Execution rule:** prove the real production graph through public player/input seams and read-only diagnostics; no authority mutation, teleport, collision bypass, or substitute runtime.

**Validation history:** Earlier exact runs isolated route and destination interaction. Exact diagnostics proved semantic Interact delivery and then proved nearby-NPC selection chose `awon` because the driver stopped too far from `destination-npc`; commit `047d8ddf7cf3b0abc31991083b353f3bf9f6756d` corrected the physical approach to 0.50 m. A later exact artifact exposed deterministic enemy-first defeat; Kentridge-owned combat tuning now keeps bandits at 6 vitality and gives the persistent player 40, while module-local validation mirrors real Combat/Input/Vitality/AI and requires player victory. Vitality persistence now captures/restores current/max/defeated/revision. Master reconciliation `20712f7e1b576745ca7620eabd699d31fccfa17e` and validation-tool compatibility work restored current GPU/frame-timing contracts while retaining generic `playerArguments`. Exact request `99ec62f77e341c25ffc9ab6adc2721b234d35959` / run `34158854146` passed every behavioral milestone but failed direct visual review for UI overlap and unreadable combat framing. Those were corrected through production UI policy and ordinary right-stick look input. Exact request `57ae0d0d8aaa75fabd316b6cd3e9c3fb87118e60` / run `34162145763` is terminal success and again proves the full route, but direct combat screenshot review found magenta primitive hood/gear/sword overlays added by `KentridgeForestBanditEncounter.CreateBandit`. Commit `c65c83dbbf225c48bac1701389e837f4a7d66918` removes the primitive adjuncts/fallback and requires the authored character prefab; final exact visual acceptance remains required.

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
- [ ] **T24-037 — Make representative combat player-input-driven.** Physical Primary input advances legal player Combat/Vitality turns; AI advances enemies only; focused Kentridge/Combat validation and canonical player victory must pass.
- [ ] **T24-038 — Validate production opening handoff and physical exit.** Owned SceneRuntime validation uses real production world/input/collision and must terminal-pass on exact SHA; diagnostic success inside cancelled run `34008004416` is insufficient.
- [ ] **T24-039 — Preserve vitality across production save/Continue.** Capture/restore current/max/defeated/revision through production persistence; focused regressions and canonical restore must pass.
- [x] **T24-043 — Isolate repeated destination interaction failure before another fix.** Exact built-player diagnostics proved semantic `Interact` delivery and then proved nearby-NPC selection chose `awon` because the driver stopped too far from the destination. The correction only changes physical approach distance; diagnostic code remains read-only and System24-command-line gated.
- [ ] **T24-044 — Reconcile shared validation metadata with current master contracts.** Preserve generic `playerArguments` support without regressing `gpuCutover` policy, inherited-environment cleanup, or required FRAMEPIPE build timing; repository Python regressions must pass on the exact feature SHA.
- [ ] **T24-045 — Make exact System24 presentation production-quality.** Keep the production HUD, eliminate ordinary-gameplay Application/HUD and scene-evidence/objective overlap, frame a real active combatant using ordinary public look input, and remove the primitive/magenta bandit presentation adjuncts so combat uses the authored character prefab without ad-hoc fallback geometry/materials. Re-run exact built-player validation and inspect durable screenshots directly.

## Cleanup / close

- [ ] **T24-040 — Audit alternate ownership/private shortcuts.** No production scene-local authority or privileged validation mutation remains.
- [ ] **T24-041 — Verify module-local validation remains distinct.** Assembled Kentridge never substitutes for changed modules' focused tests/scenarios.
- [ ] **T24-042 — Close only when every representative domain runs through its production public boundary.** Every required checkbox and acceptance criterion must be complete.
