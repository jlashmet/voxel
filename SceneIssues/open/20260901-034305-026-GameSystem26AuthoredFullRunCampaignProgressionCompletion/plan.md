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

Exact-SHA run `33941421358` validated feature SHA `c230f44761b757fd83873f5030707d9bcb6fc019`. Compilation succeeded. `CampaignOutcomeIntegrationTests`, both canonical route tests, and `KentridgeSessionPersistenceTests.ResumeRestoresSemanticCampaignStateIntoFreshGraphWithoutReplayingNewGame` all passed. The run's sole non-quarantined failure was unrelated `VoxelEngine.Structures.Tests.PlayMode.TypedStructuralSocketCompositionSceneTests...`, caused by the repository-wide fallback described above. A new exact-SHA run is required for the Story-owned validation tree.

Two external prerequisites remain open on current master: System25 multiplayer E2E validation (T26-043) and Kentridge macro-world physical realization (T26-021/022/044-046). The current Kentridge planner deliberately rejects multi-region campaigns, so the authored Kentridge/Moordell/Rossdam/Logan route must not be forced through it. After green current-SHA CI, re-check those prerequisites; closure/PR promotion occurs only when every acceptance gate can be completed.
