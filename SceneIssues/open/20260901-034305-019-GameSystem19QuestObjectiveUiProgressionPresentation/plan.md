# 19 Quest & objective UI / progression presentation — implementation plan

**Target module:** `Assets/Game/ProgressionPresentation/Api`, `Runtime`, `Tests/EditMode`, and module-local `Validation` (`Game.ProgressionPresentation.*`).

## Observed behavior / acceptance

System 11 `Game.Progression` is the sole quest/objective authority and exposes one coherent `ProgressionSnapshot`. Legacy `Game.Quests.Runtime.QuestRuntime` is a compatibility facade over that authority; no production HUD module exists on current master, so System19 must publish a compact read-only HUD seam without inventing a parallel HUD implementation. Existing presentation draft initially projected quests only; System11 standalone objectives also represent unified campaign-objective truth and therefore must be presented from the same snapshot.

Repository inventory found no existing ProgressionPresentation store or production completion/debug UI to migrate. Presentation metadata is semantic content keyed by `QuestId`/`ObjectiveId`; local sort/filter/collapse/selection/tracking is not gameplay state.

## Ownership / selected approach

- `Api`: journal/read-model contracts, semantic presentation catalog, local-tracking/HUD projection contracts, and typed replicated current-state payload.
- `Runtime`: one-snapshot journal projector; local preferences; spoiler visibility from authoritative lifecycle plus authored `VisibleWhileInactive`; read-only `ReplicatedProgressionQuery` over GameplayReplication current-state APIs. No `Game.Progression.Runtime` reference.
- Unified projection includes both quest objectives and `ProgressionSnapshot.StandaloneObjectives`; no synthetic quest IDs.
- HUD consumers read only `ITrackedObjectiveProjection`; absence of System17 on master is handled by this stable API seam rather than an unmerged dependency.
- Validation: `Assets/Game/ProgressionPresentation/Validation/ProgressionPresentationValidation.unity` + scenario exercises the production presenter and renders journal/tracked projection in a standalone player.

## Hypotheses / results

1. **H1:** System11 snapshot is sufficient for UI truth. Confirmed: quest and standalone objective state/count/revision are present; no legacy runtime read is required.
2. **H2:** Presentation may need its own mutable progression copy. Falsified: local preferences can be reconciled against each fresh snapshot while authority remains untouched.

## Blast radius / remaining gates

Changes are isolated to the new ProgressionPresentation module and this SceneIssue. No accept/decline, map/minimap, completion command, gameplay mutation, or authority changes. Remaining gates: exact-SHA repository-selected EditMode/module validation, module-local standalone-player evidence, canonical Kentridge integration gate, final checklist/closure, then current-master merge + PR auto-merge.
