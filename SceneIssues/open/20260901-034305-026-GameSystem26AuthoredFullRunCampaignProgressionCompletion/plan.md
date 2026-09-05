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

Exact-SHA run `33941421358` validated campaign/fresh-graph restore; Story-owned validation passed `33942377377`. Request `57a0b1f00615d0901e2d25ee0d2216296b13f163` is directly parented by feature SHA `86911d9dec109c588310754f28c7a5644aed687a`; run `33944532957` passed repository-derived automatic module validation. No production/test change has been made after that verified feature SHA; later System26 commits are blocker bookkeeping only.

Current `origin/master` remains `939e9a6f744313d93992b0479d5f6140d774ef42`. Prerequisites have advanced but have not landed: System24 `fixes/agent-2` `f6b3ace316f7122b48135ea04c0f04078049d9a5` now authors the canonical production vertical-slice scenario, but exact request `64296df9f805d2690942d5f302e07a32f3a8b823` run `33984774287` completed `cancelled`. System25 `fixes/agent-7` `b2d1847ec109eb8be7a631c084bd60230c5932a2` contains independent harness correctness fixes, but request `fa5bfd1c902d7832298690e874621a80a2271b01` run `33984593462` also completed `cancelled` and System25 still depends on System24. Macro-world `fixes/agent-6` `7142ae97de62486fd932fbdc4c9f323b9eade24e` remains blocked on renderer ownership; renderer branch `fixes/agent-1` has advanced to `1429ce8b1e9064ab76717ccbe8b74dfe88509482`, while run `33984671790` completed `cancelled` and no validated correction is on master. No independent System26 task remains. Keep open until prerequisites land, then sync master, complete production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
