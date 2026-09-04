# 19 Quest & objective UI / progression presentation — implementation plan

**Target module:** `Assets/Game/ProgressionPresentation/Api`, `Runtime`, `Tests/EditMode`, and module-local `Validation` (`Game.ProgressionPresentation.*`).

## Observed behavior / acceptance

System 11 `Game.Progression` is the sole quest/objective authority and exposes one coherent `ProgressionSnapshot`. Legacy `Game.Quests.Runtime.QuestRuntime` is a compatibility facade over that authority; no production HUD module existed on the implementation baseline, so System19 publishes a compact read-only HUD seam without inventing a parallel HUD implementation. The initial presentation draft projected quests only; System11 standalone objectives also represent unified campaign-objective truth and are projected from the same snapshot.

Repository inventory found no existing ProgressionPresentation store or production completion/debug UI to migrate. Presentation metadata is semantic content keyed by `QuestId`/`ObjectiveId`; local sort/filter/collapse/selection/tracking is not gameplay state.

## Ownership / selected approach

- `Api`: journal/read-model contracts, semantic presentation catalog, local-tracking/HUD projection contracts, and typed replicated current-state payload.
- `Runtime`: one-snapshot journal projector; local preferences; spoiler visibility from authoritative lifecycle plus authored `VisibleWhileInactive`; read-only `ReplicatedProgressionQuery` over GameplayReplication current-state APIs. No `Game.Progression.Runtime` reference.
- Unified projection includes quest objectives and `ProgressionSnapshot.StandaloneObjectives`; no synthetic quest IDs.
- HUD consumers read only `ITrackedObjectiveProjection`; absence of System17 on the implementation baseline is handled by this stable API seam rather than an unmerged dependency.
- Validation: `Assets/Game/ProgressionPresentation/Validation/ProgressionPresentationValidation.unity` + scenario exercises the production presenter and renders journal/tracked projection in a standalone player.

## Hypotheses / material results

1. **H1:** System11 snapshot is sufficient for UI truth. Confirmed: quest and standalone objective state/count/revision are present; no legacy runtime read is required.
2. **H2:** Presentation may need its own mutable progression copy. Falsified: local preferences reconcile against each fresh snapshot while authority remains untouched.
3. Exact-SHA run `33879936541` on source `b40bb145...` failed compile in test/validation catalog stubs: short-circuit `&&` left `out content` unassigned (CS0177). Production API/runtime compiled. Both consumers were corrected with explicit assignment paths.
4. Exact-SHA run `33881215529` on source `a319fbef2ded74848057b122c939ec358d4f2e18` passed the focused EditMode assembly, module-local standalone-player validation, and canonical Kentridge standalone-player integration. Durable module captures show authoritative revision 2, completed/active objective transition, and compact HUD tracking of `objective:open-gate`; player logs contain all required progression assertions and Kentridge completed with 0 assertion failures.

## Blast radius / completed gates

Changes are isolated to the new ProgressionPresentation module and this SceneIssue. No accept/decline, map/minimap, completion command, gameplay mutation, or authority changes were added. Behavioral regressions prove coherent revision projection, spoiler visibility, local-tracking independence across clients, reconnect reconstruction, and standalone-objective projection. The module-local scene exercises the production presenter path, and repository-selected canonical Kentridge validation passed on the same exact source SHA.

Remaining work after this bookkeeping commit is only the repository-required final promotion path: merge any newly advanced `origin/master` into `fixes/agent-8`, open/update the PR to `master`, enable auto-merge, and monitor the required `affected` PR gate until merged.
