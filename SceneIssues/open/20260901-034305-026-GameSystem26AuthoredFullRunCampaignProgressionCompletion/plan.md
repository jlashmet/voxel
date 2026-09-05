# 26 Authored full-run campaign progression & completion — implementation plan

**Ownership:** campaign Story/Progression composition; reuse System11 Progression, System15 Outcomes, System16 persistence, and Systems14/23. **No generic GameLoop/Chapter runtime.**

## Observed behavior / acceptance

The semantic campaign now extends the recovered Kentridge opening through verified Rorik/Moordell/Rossdam/Logan dependencies to one authored System15 terminal condition. Optional content is non-gating. Acceptance still requires a normal production New Game path, meaningful fresh-graph restore, shared multiplayer observation, and built-player full-run proof. Evidence and authored bridges are in `route-evidence.md`.

## Hypotheses / results

1. **Selected:** existing Story + Progression need only owning Encounter-resolution input and an Outcome-condition effect. Implemented; semantic tests route terminal resolution through System15 exactly once.
2. **Rejected:** add chapter/phase runtime. Repository/diff audit found no need or duplicate phase authority.
3. **Rejected:** reconstruct persisted `CutsceneRef` from save IDs. Its constructor is intentionally non-public; restore now resolves IDs against the current authored `CampaignBlueprint` and fails closed for stale content.

## Selected implementation

- Opening and continuation remain plain content helpers; Story observes semantic gameplay facts and System11 owns objective truth.
- `KentridgeSessionPersistenceBridge` delegates capture/validation/restore publication to System16 and restores CampaignRuntime semantic state into the fresh graph composed by System14.
- Module-local Kentridge EditMode regression captures after an opening consequence, shuts the source graph down, composes a distinct graph, restores current progression/completed one-shots, and proves Resume never replays NewGame/history.
- Story effect audit remains narrow: objective/quest start, cutscene request, party/spell progression, and outcome-condition observation only.

## Material validation / remaining gates

Exact-SHA CI exposed and drove fixes for stale Encounter APIs, authored-cutscene reconstruction, and missing direct `Game.Outcomes.Api` assembly references. Latest feature state needs a new exact-SHA targeted run.

Two external prerequisites remain open on current master: System25 multiplayer E2E validation (T26-043) and Kentridge macro-world physical realization (T26-021/022/044-046). The current Kentridge planner deliberately rejects multi-region campaigns, so the authored Kentridge/Moordell/Rossdam/Logan route must not be forced through it. After green current-SHA CI, re-check those prerequisites; closure/PR promotion occurs only when every acceptance gate can be completed.
