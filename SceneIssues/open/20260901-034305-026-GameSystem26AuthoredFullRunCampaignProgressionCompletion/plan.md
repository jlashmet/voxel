# 26 Authored full-run campaign progression & completion — implementation plan

**Ownership:** campaign Story/Progression composition; reuse System11 Progression, System15 Outcomes, System16 persistence, and Systems14/23. **No generic GameLoop/Chapter runtime.**

## Observed behavior / acceptance

The semantic campaign now extends the recovered Kentridge opening through verified Rorik/Moordell/Rossdam/Logan dependencies to one authored System15 terminal condition. Optional content is non-gating. Acceptance still requires a normal production New Game path, meaningful fresh-graph restore, shared multiplayer observation, and built-player full-run proof. Evidence and authored bridges are in `route-evidence.md`.

## Hypotheses / results

1. **Selected:** existing Story + Progression need only owning Encounter-resolution input and an Outcome-condition effect. Implemented; semantic tests route terminal resolution through System15 exactly once.
2. **Rejected:** add chapter/phase runtime. Repository/diff audit found no need or duplicate phase authority.
3. **Rejected:** reconstruct persisted `CutsceneRef` from save IDs. Its constructor is intentionally non-public; restore now resolves IDs against the current authored `CampaignBlueprint` and fails closed for stale content.
4. **Resolved compile root cause:** Unity asmdefs do not inherit transitive public-contract dependencies. The Kentridge persistence fixture and affected consumers now directly reference every assembly exposed by signatures they consume (`Game.Composition.Campaign`, `Game.Persistence.Api`, `Game.Outcomes.Api`).
5. **Resolved validation-ownership root cause:** changed `Assets/Game/Story/*` paths had no module-owned test assembly, so `module-validation-plan.py` correctly treated them as fallback paths and selected every repository module. Exact-SHA run `33941421358` passed compilation and affected campaign/Kentridge regressions but exposed an unrelated Structures PlayMode fallback failure. Story now owns a headless `Game.Story.Tests` EditMode assembly covering System26 encounter-result and outcome-condition semantics.

## Selected implementation

- Opening and continuation remain plain content helpers; Story observes semantic gameplay facts and System11 owns objective truth.
- `KentridgeSessionPersistenceBridge` delegates capture/validation/restore publication to System16 and restores CampaignRuntime semantic state into the fresh graph composed by System14.
- Module-local Kentridge EditMode regression captures after an opening consequence, shuts the source graph down, composes a distinct graph, restores current progression/completed one-shots, and proves Resume never replays NewGame/history.
- Story effect audit remains narrow: objective/quest start, cutscene request, party/spell progression, and outcome-condition observation only.
- Assembly boundaries declare direct API dependencies instead of relying on transitive runtime references.
- Story owns module-local headless regression coverage for `EncounterResolved` result matching and `ObserveOutcomeCondition` dispatch.

## Material validation / remaining gates

Exact-SHA run `33941421358` validated campaign/fresh-graph restore; Story-owned validation passed `33942377377`. Request `57a0b1f00615d0901e2d25ee0d2216296b13f163` is directly parented by feature SHA `86911d9dec109c588310754f28c7a5644aed687a`; run `33944532957` passed repository-derived automatic module validation. No production/test change has been made after that verified feature SHA; later System26 commits are blocker bookkeeping only.

Current `origin/master` is `a180749ed7c00d28bed6661fc9a3da4c9a9b61fc`; that advance is unrelated Astra-manager tooling. All three required prerequisite lanes are now actively validating exact feature state and must be left untouched. System24 remains at feature `f6b3ace316f7122b48135ea04c0f04078049d9a5`; exact request `64296df9f805d2690942d5f302e07a32f3a8b823` run `33984774287` is retry attempt 2 and queued. System25 remains at `b2d1847ec109eb8be7a631c084bd60230c5932a2`; exact-current harness request `382b9c33a2f4edabd7bf46c598e1ee1d9eea0291` run `33986100313` is queued and System25 still depends on System24. Renderer owner `fixes/agent-1` is now `7ceaa0120a4e30c260b1f383e7fc973c3c205309`; exact-current transport `416400b334fbac3561a6e790723d99328954e12e` is directly parented by that feature SHA and run `33986630571` is queued. Macro-world `fixes/agent-6` remains blocked until that renderer correction validates and lands. No independent System26 task remains. Keep open until prerequisites land; then sync master, complete production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
