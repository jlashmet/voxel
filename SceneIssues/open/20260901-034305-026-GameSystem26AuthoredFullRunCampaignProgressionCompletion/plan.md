# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and real milestone-driven built-player full-run evidence. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Current evidence

T26-057 is green at product source `e31528947add430f39588a7d3fda98db40589974`, direct-child request `0498efba7629b09f93cfc00a4c12fcdd8ecfa1ed`, run `34008635270`: `Game.Composition.Kentridge.Tests` 1/1 and `Game.Story.Tests` 2/2 passed. The same 80-second Kentridge integration consumer reaches `GAMEPLAY READY` but is only layout/autowalk/survey proof; it has no authored terminal milestones and still ends with `coverage=False`, so it is not System26 full-run acceptance.

Nine tasks remain after the demonstrated production-player integration defect was split into T26-058: T26-021/022/043/044/045/046/053/054/058.

Exact request `3d3d7de4b1a0ea954e23e80e6c6bd5e2be34ef7e`, run `34023048689`, source `d27b0faeedf98a3be2538372d517ae5523e302d7` compiled successfully but failed `Game.Composition.Kentridge.Tests.AuthoredFullRunPhysicalWorldPlanTests.FullRunGenerationResolvesAuthoredSitesAndNpcAssignmentsAgainstPhysicalHierarchy`: the site-facts adapter incorrectly required macro building blockouts for Kentridge. The physical intent explicitly marks Kentridge/Hightown as `ExistingRichGeneration` with zero macro blockouts, so this was an adapter defect, not missing geometry. Fix `68e7d4aeb14d86298c44a971325e523fb666b23d` uses the source-backed settlement centre only for `ExistingRichGeneration`; generic blockout settlements still fail closed when buildings are absent. Next discriminator is exact-SHA CI of that fix on the existing agent-8 transport.

## Selected physical-world recovery

The prior macro-world SceneIssue was administratively deferred, but its preserved checkpoint `62533f5c0b1716c70414eb82d0e2b0def9e99f39` contains acceptance-required reusable production work that master intentionally omitted: semantic physical regions/route constraints, deterministic terrain-aware macro planning, generic settlement realization, water/ridge/pass catalogues, and Kentridge macro-selection integration. This System26 assignment will recover only those production pieces required by T26-021/022/044–046; the closed agent-6 SceneIssue and its bookkeeping remain untouched.

Recovered core product source `808c93e8b06a999e37664a021af4eea2382799f7` restores the semantic physical intent/planner, reservation adapter, generic physical/water catalogues, Kentridge macro intent, and the existing Kentridge/generic catalogue integration points. Its direct-child exact request is `14970ac2095b50a35b8397ffec22d83034f86808`, run `34014321563`.

The recovered slice is limited to `TopDownWorldPhysicalIntent`, `TopDownWorldPhysicalPlanner`, reservation adapter, physical/water voxel catalogues, `KentridgeTopDownWorldPhysicalIntent`, and the two existing integration points (`KentridgeCombinedVoxelCatalogue`, `WorldBuilderVoxelCatalogue`). No renderer-owner files or old agent-6 SceneIssue files are imported.

## Demonstrated full-player integration defect

Current production `KentridgePlayableSlice.Start()` still constructs `KnownOpeningCampaignContent.Build(...)`, so the built player cannot expose the authored Rorik/Moordell/Rossdam/Logan continuation even if macro streaming succeeds. The existing `KentridgeCampaignWorldPlanner` also intentionally accepts exactly one region/settlement and rejects outer routes; that guard is correct for its opening-only consumer and must not be weakened.

T26-058 therefore owns a distinct hierarchy-aware full-campaign realization path: boot `AuthoredFullRunCampaignContent`, consume the real `WorldHierarchyPlan` plus `TopDownWorldPhysicalPlan`, reuse `SiteRoleResolver`, `NpcPlacementResolver`, cutscene-stage resolution and existing world facts, and feed the existing session/player seams. No invented progression coordinates, private completion, parallel world authority, or removal of the opening planner guard. The production built-player route must prove the new path.

T26-043 remains independently owned by production Sessions/System25. Current agent-7 work is not yet accepted; System26 will recheck it after independent physical/full-run work and will not duplicate multiplayer authority or transport.

## Cost / validation

Preserve existing residency, streaming, scheduler, renderer and memory budgets. Do not force readiness, widen radius, raise budgets, or substitute storage-only evidence. If the core compiles, add/update only the owning module-local validation surface required by recovered runtime behavior, then implement T26-058 and the milestone-driven production full-run scenario. PR #312 remains draft until every checkbox is genuinely complete.
