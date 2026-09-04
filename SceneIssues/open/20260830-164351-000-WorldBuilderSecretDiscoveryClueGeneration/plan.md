# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and built-player proof. There are no original captures/marked regions. `issue.json` requires representative generated SecretDiscovery examples in `WorldbuildingGalleryShowcase` as well as the dedicated production validation scene.

## Hypotheses / material results

- Canonical hidden-secret selection existed; deterministic route/readability/clue planning was the missing layer. Stable IDs, semantic anchors, deterministic scoring/tie-breaking, diagnostics, route identity, discovery idempotence, and explicit bypass semantics are implemented and behaviorally covered.
- Dedicated `SecretDiscovery` validation uses production voxel generation/rendering/destruction. Exact run `33801222778` passed WorldBuilder EditMode, dedicated built player, Kentridge integration, and SceneIssue replay after renderer lifecycle repair.
- The prior Gallery-integration instruction conflict is resolved by the current assignment directive: repository acceptance is authoritative. The thin production Gallery consumer and its compatibility/physical/surface-route regressions were restored at feature SHA `aa23d9e42439ed2ca18119051ecedff2a7a6ee1e`.
- Exact run `33821322632` failed before gameplay validation because the restored regressions were returned to legacy `Assets/Tests/EditMode`. That folder is outside the current module-owned `VoxelEngine.Tests.EditMode` asmdef, so Unity reported missing `Game.*`/`VoxelEngine.*` namespaces and types. The standalone SceneIssue replay failed from the same compile break; no runtime/visual verdict is valid from that run.
- Current WorldBuilder test ownership is `Assets/Game/WorldBuilder/Tests/EditMode/VoxelEngine.Tests.EditMode.asmdef`, which already references `Game.Composition.Showcase`, `Game.Composition.CaveWorldBuilder`, Structures, Storage, Terrain, Materials, and WorldBuilder assemblies. Moving the three Gallery regressions under that existing assembly is the minimal harness-compatible fix.

## Selected direction / remaining gates

Keep reusable secret/cave/clue behavior in production modules and Gallery-specific placement/presentation in Showcase composition. Move only the three restored Gallery regressions into the current WorldBuilder-owned test assembly; do not alter production behavior to address a test-assembly ownership failure.

Then run a fresh exact-SHA targeted gate on `ci-test/fixes/agent-5`. Acceptance requires the module tests plus built-player SecretDiscovery/Kentridge and exact Gallery SceneIssue replay to pass, with gameplay-scale Gallery evidence showing understandable feature-specific clue language and no placeholder/universal-glow treatment. After green exact-SHA evidence, complete closure bookkeeping, merge current master, open/update the feature PR, enable auto-merge, and require the `affected` PR/full-app gate before completion.
