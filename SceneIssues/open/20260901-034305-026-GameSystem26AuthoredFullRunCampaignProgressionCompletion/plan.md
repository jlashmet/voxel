# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and real milestone-driven built-player full-run evidence. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Current evidence

T26-057 is green at product source `e31528947add430f39588a7d3fda98db40589974`, direct-child request `0498efba7629b09f93cfc00a4c12fcdd8ecfa1ed`, run `34008635270`: `Game.Composition.Kentridge.Tests` 1/1 and `Game.Story.Tests` 2/2 passed. That run's Kentridge integration consumer reaches gameplay readiness but is only layout/autowalk/survey proof; it has no authored terminal milestones and is not System26 full-run acceptance.

Nine tasks remain: T26-021/022/043/044/045/046/053/054/058.

Recovered hierarchy-aware physical planning and semantic site/NPC projection now have exact product proof. Source `31e7b47aefad0f71d4d7ab2b842f4f1dec898ebb`, direct-child request `d1182bc248c54dced2ce5bb86de31056bd5d7b07`, run `34029171252`, job `101475442215` completed successfully. Artifact `9988560566` digest `sha256:7784f88df4e9374e44241375b668ed50e5f8bc506b3c266122087509359ec3e2` records the previously failing `AuthoredFullRunPhysicalWorldPlanTests.FullRunGenerationResolvesAuthoredSitesAndNpcAssignmentsAgainstPhysicalHierarchy` as passed, including source-backed `/rich-generation/starting-pub` identity plus a physical macro anchor. See `ci-evidence-d1182bc.md`.

This exact run does not complete T26-058: the explicit SceneIssue replay step was skipped, the top-level integration consumer remains generic layout/autowalk/survey, and production `KentridgePlayableSlice` still boots `KnownOpeningCampaignContent.Build(...)` rather than the full authored campaign.

## Selected physical-world recovery

The prior macro-world SceneIssue was administratively deferred, but its preserved checkpoint `62533f5c0b1716c70414eb82d0e2b0def9e99f39` contains acceptance-required reusable production work that master intentionally omitted: semantic physical regions/route constraints, deterministic terrain-aware macro planning, generic settlement realization, water/ridge/pass catalogues, and Kentridge macro-selection integration. This System26 assignment recovers only those production pieces required by T26-021/022/044–046; the closed agent-6 SceneIssue and its bookkeeping remain untouched.

Recovered core product source `808c93e8b06a999e37664a021af4eea2382799f7` restored the semantic physical intent/planner, reservation adapter, physical/water voxel catalogues, Kentridge macro intent, and the existing Kentridge/generic catalogue integration points. The follow-on full-run planner compiles the real `WorldHierarchyPlan`, consumes the recovered `TopDownWorldPhysicalPlan`, and now resolves authored rich-settlement roles without weakening generic blockout failure behavior.

## Next implementation gate — T26-058

Production `KentridgePlayableSlice.OnEnable()` still constructs `KnownOpeningCampaignContent.Build(...)`, so the shipped player cannot expose the authored Rorik/Moordell/Rossdam/Logan continuation even though the hierarchy-aware physical plan now resolves. The existing `KentridgeCampaignWorldPlanner` intentionally accepts exactly one region/settlement and rejects outer routes; preserve that opening-only contract.

Implement a distinct production full-campaign realization boundary that boots `AuthoredFullRunCampaignContent`, consumes `AuthoredFullRunCampaignGenerationPlan`, converts resolved site anchors into terrain-relative NPC and cutscene-stage facts through existing production semantics, and feeds the existing Kentridge session/player seams. Reuse `SiteRoleResolver`, `NpcPlacementResolver`, cutscene-stage realization, existing actor host/presentation, and System15 observer/query. Do not invent continuation coordinates, mutate private progression, add parallel world authority, or weaken the opening planner.

Then replace the generic System26 integration proof with bounded semantic milestone waits for Rorik/Moordell/Rossdam/Logan, System15 terminal outcome, and Systems14/23 aftermath. Only exact built-player evidence through the production composition can check T26-058 and T26-021/022/044–046.

T26-043 remains independently owned by production Sessions/System25. Current master still carries System25 open with its production topology/gameplay/reconnect evidence unchecked; System26 will recheck it after independent full-run work and will not duplicate multiplayer authority or transport.

## Cost / validation

Preserve existing residency, streaming, scheduler, renderer and memory budgets. Do not force readiness, widen radius, raise budgets, or substitute storage-only evidence. Update only owning module-local validation required by changed runtime behavior. New exact-SHA CI follows a legitimate production/test change; PR #312 remains draft until every checkbox is genuinely complete.
