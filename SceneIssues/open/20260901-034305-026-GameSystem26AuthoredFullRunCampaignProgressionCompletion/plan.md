# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and real milestone-driven built-player full-run evidence. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Current evidence

T26-057 is green at product source `e31528947add430f39588a7d3fda98db40589974`, direct-child request `0498efba7629b09f93cfc00a4c12fcdd8ecfa1ed`, run `34008635270`: `Game.Composition.Kentridge.Tests` 1/1 and `Game.Story.Tests` 2/2 passed. The same 80-second Kentridge integration consumer reaches `GAMEPLAY READY` but is only layout/autowalk/survey proof; it has no authored terminal milestones and still ends with `coverage=False`, so it is not System26 full-run acceptance.

Eight tasks remain: T26-021/022/043/044/045/046/053/054.

## Selected physical-world recovery

The prior macro-world SceneIssue was administratively deferred, but its preserved checkpoint `62533f5c0b1716c70414eb82d0e2b0def9e99f39` contains acceptance-required reusable production work that master intentionally omitted: semantic physical regions/route constraints, deterministic terrain-aware macro planning, generic settlement realization, water/ridge/pass catalogues, and Kentridge macro-selection integration. This System26 assignment will recover only those production pieces required by T26-021/022/044–046; the closed agent-6 SceneIssue and its bookkeeping remain untouched.

**H1:** the archived core compiles against current System26/master APIs and replaces the marker-only macro catalogue with the real multi-region production realization. **H2:** later API drift or the old strict publication-convergence defect still blocks readiness. **Next discriminator:** restore the narrow core plus its stable Unity metadata, run repository-derived exact-SHA validation, then inspect compile/runtime/player evidence before importing any archived diagnostics or validation extras.

The first recovery slice is limited to `TopDownWorldPhysicalIntent`, `TopDownWorldPhysicalPlanner`, reservation adapter, physical/water voxel catalogues, `KentridgeTopDownWorldPhysicalIntent`, and the two existing integration points (`KentridgeCombinedVoxelCatalogue`, `WorldBuilderVoxelCatalogue`). No renderer-owner files or old agent-6 SceneIssue files are imported.

T26-043 remains independently owned by production Sessions/System25. Current agent-7 work is not yet accepted; System26 will recheck it after independent physical/full-run work and will not duplicate multiplayer authority or transport.

## Cost / validation

Preserve existing residency, streaming, scheduler, renderer and memory budgets. Do not force readiness, widen radius, raise budgets, or substitute storage-only evidence. If the core compiles, add/update only the owning module-local validation surface needed by the recovered runtime behavior, then build the milestone-driven production full-run scenario. PR #312 remains draft until every checkbox is genuinely complete.
