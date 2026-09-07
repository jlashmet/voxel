# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and real milestone-driven built-player full-run evidence. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Current evidence

T26-057 is green at product source `e31528947add430f39588a7d3fda98db40589974`, direct-child request `0498efba7629b09f93cfc00a4c12fcdd8ecfa1ed`, run `34008635270`: `Game.Composition.Kentridge.Tests` 1/1 and `Game.Story.Tests` 2/2 passed. That historical run's Kentridge integration consumer reaches gameplay readiness but is only layout/autowalk/survey proof; it has no authored terminal milestones and is not System26 full-run acceptance.

Recovered hierarchy-aware physical planning and semantic site/NPC projection have exact product proof at source `31e7b47aefad0f71d4d7ab2b842f4f1dec898ebb`, direct-child request `d1182bc248c54dced2ce5bb86de31056bd5d7b07`, run `34029171252`, job `101475442215`. Artifact `9988560566` digest `sha256:7784f88df4e9374e44241375b668ed50e5f8bc506b3c266122087509359ec3e2` records the previously failing hierarchy/site realization regression as passed. See `ci-evidence-d1182bc.md`.

The preserved exact request `dcc9f36e64a4edaebe250560a89c2754aece2931` targets product source `5702064ce9feb070f8cb893342af106b3df957a4` and remains queued in run `34158903057`. It must not be replaced while queued/running. Since the feature branch has advanced with the dedicated full-run player proof, this request is historical evidence only; final acceptance requires a new direct-child exact-SHA request for the stabilized final feature source after `dcc9f36e...` completes.

## T26-058 production full-run path — implemented, awaiting exact proof

The shipped `KentridgePlayableSlice` now enters through `KentridgePlayableFullRunBootstrap`. The bootstrap intentionally retains the opening-only Kentridge planner for exact rich pub/hidden-space geometry, then overlays those semantic opening identities onto the hierarchy-aware `AuthoredFullRunKentridgeComposition`. The session factory therefore boots `AuthoredFullRunCampaignContent` against the real multi-settlement physical realization without weakening the single-settlement opening invariant or inventing continuation coordinates.

`KentridgePlayableSlice` now asserts that its session is full-run and emits `KENTRIDGE_AUTHORED_FULL_RUN_READY` with settlement/NPC counts. The canonical top-level Kentridge player scenario requires that marker in addition to its existing world-layout evidence, so the actual shipped scene cannot silently regress to the opening-only graph.

Focused hierarchy/opening-overlay regressions remain module-owned. Final exact CI must compile and exercise these paths before T26-058 is checked.

## T26-021/022/044-046 built-player terminal proof — implemented, awaiting exact proof

`Assets/Game/Composition/Kentridge/Playable/Validation/KentridgeFullRunCampaignValidation.unity` is a paired module-local standalone player validation. It enters through the exact shipped `KentridgePlayableFullRunBootstrap`, production `GameSessionOrchestrator`, and production `ApplicationFlowCoordinator`; no alternate campaign/session/outcome authority is constructed.

The driver advances the authored route using public semantic gameplay facts and the production Encounter registry: opening -> Awon -> Medrare -> church/Angel -> Rorik -> Moordell -> Rossdam -> mayor -> Logan -> lower castle. Cutscenes use bounded SessionOrchestration ticks, encounters resolve through their owning runtime, and semantic milestones are logged for the standalone scenario. The proof asserts the hierarchy-backed physical realization includes continuation NPC/stage facts, System15 resolves `main-campaign-complete` with success at revision `1`, Application projects `ApplicationScreen.Outcome`, and `ReturnFromOutcome` reaches the frontend through normal teardown.

The paired `.player-scenario.json` requires milestone logs through Rorik, Moordell, Rossdam and Logan plus System15/frontend completion, with bounded 30-second smoke classification and failure-pattern rejection. This is ordinary module-local player validation, while the existing production Kentridge integration scenario separately proves the shipped scene boots the full-run graph.

T26-021/022/044/045/046 and the final T26-053 gate stay unchecked until the final exact-SHA run proves these assets in CI.

## External prerequisite — T26-043

T26-043 remains independently owned by production Sessions/System25. `fixes/agent-7` still records core authority/client topology, convergence, gameplay contention/shared progression, reconnect/leave and release acceptance as incomplete. System26 will not duplicate multiplayer transport, process harness, or campaign authority. Closure is therefore prohibited until authoritative System25 evidence satisfies the shared multiplayer progression/outcome requirement.

## Closure sequence

1. Preserve request `dcc9f36e...` until it reaches a terminal result; do not replace its transport commit while queued/running.
2. After that request completes, stabilize any CI-discovered defects and issue a new direct-child exact-SHA request for the current final System26 source.
3. Require automatic domain/module validation, the new full-run standalone player scenario, and the production Kentridge integration scenario to pass with retained artifacts/milestones.
4. Recheck System25 and check T26-043 only from authoritative separate-process evidence.
5. When every checkbox and acceptance criterion is genuinely complete, record final CI evidence, set `acceptanceComplete: true`, move the issue atomically from `SceneIssues/open/...` to `SceneIssues/closed/...`, merge current `origin/master` into `fixes/agent-8`, then promote only by PR + auto-merge and required `affected` gate.

## Cost / validation constraints

Preserve existing residency, streaming, scheduler, renderer and memory budgets. Do not force readiness, widen radius, raise budgets, substitute storage-only evidence, or create a second network/runtime path. New validation remains module-local and uses existing structural discovery. PR #312 is historical/closed; final promotion must use a fresh PR only after all acceptance is complete.
