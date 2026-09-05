# 26 Authored full-run campaign progression & completion — implementation plan

**Ownership:** campaign Story/Progression composition; reuse System11 Progression, System15 Outcomes, System16 persistence, and Systems14/23. **No generic GameLoop/Chapter runtime.**

## Observed behavior / acceptance

The semantic campaign now extends the recovered Kentridge opening through verified Rorik/Moordell/Rossdam/Logan dependencies to one authored System15 terminal condition. Optional content is non-gating. Acceptance still requires a normal production New Game path, meaningful fresh-graph restore, shared multiplayer observation, and built-player full-run proof. Evidence and authored bridges are in `route-evidence.md`.

## Hypotheses / results

1. **Selected:** existing Story + Progression need only owning Encounter-resolution input and an Outcome-condition effect. Implemented; semantic tests route terminal resolution through System15 exactly once.
2. **Rejected:** add chapter/phase runtime. Repository/diff audit found no need or duplicate phase authority.
3. **Rejected:** reconstruct persisted `CutsceneRef` from save IDs. Its constructor is intentionally non-public; restore now resolves IDs against the current authored `CampaignBlueprint` and fails closed for stale content.
4. **Resolved compile root cause:** Unity asmdefs do not inherit transitive public-contract dependencies. The Kentridge persistence fixture and affected consumers now directly reference every assembly exposed by signatures they consume (`Game.Composition.Campaign`, `Game.Persistence.Api`, `Game.Outcomes.Api`).
5. **Resolved validation-ownership root cause:** changed `Assets/Game/Story/*` paths had no module-owned test assembly, so `module-validation-plan.py` correctly treated them as fallback paths and selected every repository module. Exact-SHA run `33941421358` then passed compilation and the affected EditMode campaign/Kentridge regressions but failed only an unrelated Structures PlayMode test because URP's debug updater polled legacy `UnityEngine.Input`. Story now owns a headless `Game.Story.Tests` EditMode assembly that directly covers the System26 encounter-result and outcome-condition semantics, allowing targeted planning to validate Story and its true dependents instead of broad fallback.

## Selected implementation

- Opening and continuation remain plain content helpers; Story observes semantic gameplay facts and System11 owns objective truth.
- `KentridgeSessionPersistenceBridge` delegates capture/validation/restore publication to System16 and restores CampaignRuntime semantic state into the fresh graph composed by System14.
- Module-local Kentridge EditMode regression captures after an opening consequence, shuts the source graph down, composes a distinct graph, restores current progression/completed one-shots, and proves Resume never replays NewGame/history.
- Story effect audit remains narrow: objective/quest start, cutscene request, party/spell progression, and outcome-condition observation only.
- Assembly boundaries declare direct API dependencies instead of relying on transitive runtime references.
- Story now owns module-local headless regression coverage for `EncounterResolved` result matching and `ObserveOutcomeCondition` dispatch.

## Material validation / remaining gates

Exact-SHA run `33941421358` validated the campaign/fresh-graph restore regressions at `c230f44761b757fd83873f5030707d9bcb6fc019`. Story-owned validation then passed run `33942377377`. The latest request commit `57a0b1f00615d0901e2d25ee0d2216296b13f163` is directly parented by feature SHA `86911d9dec109c588310754f28c7a5644aed687a`; exact-SHA run `33944532957` completed successfully, including repository-derived automatic module validation. No production/test change has been made after that verified feature SHA; subsequent branch changes are blocker bookkeeping only.

Blocker review against current `origin/master` `939e9a6f744313d93992b0479d5f6140d774ef42` confirms both required external prerequisites remain open. System25 multiplayer E2E validation (T26-043) is itself waiting for System24's production composed built-player boundary, so System26 must not create an alternate multiplayer harness. Kentridge macro-world physical realization (T26-021/022/044-046) remains blocked on the separately owned GPU renderer publication/restoration path; its current one-region planner still correctly rejects the multi-region authored campaign. No independent System26 task remains. Keep this feature open until those prerequisites land, then synchronize current master, complete production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
