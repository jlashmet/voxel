# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and built-player proof. There are no original captures/marked regions. `issue.json` requires representative generated SecretDiscovery examples in `WorldbuildingGalleryShowcase` as well as focused production validation.

## Hypotheses / material results

- Canonical hidden-secret selection existed; deterministic route/readability/clue planning was the missing layer. Stable IDs, semantic anchors, deterministic scoring/tie-breaking, diagnostics, route identity, discovery idempotence, and explicit bypass semantics are implemented and behaviorally covered.
- Dedicated `Assets/Game/WorldBuilder/Validation/SecretDiscovery` validation uses production voxel generation/rendering/destruction. Exact run `33801222778` passed WorldBuilder EditMode, dedicated built player, Kentridge integration, and SceneIssue replay after renderer lifecycle repair.
- The prior Gallery-integration instruction conflict is resolved by the current assignment directive: repository acceptance is authoritative. The thin production Gallery consumer and its compatibility/physical/surface-route regressions were restored at feature SHA `aa23d9e42439ed2ca18119051ecedff2a7a6ee1e`.
- Exact run `33821322632` failed before gameplay validation because the restored regressions were returned to legacy `Assets/Tests/EditMode`. The three regressions were moved unchanged into `Assets/Game/WorldBuilder/Tests/EditMode`; exact targeted run `33822800307` then succeeded against feature SHA `5ce4b97bc4bd3b69556b2c41ce8c995319f4278d`.
- Current master `13b3c6a752deb030effba0f6e430863d0c1fd115` was merged into feature commit `43c2afc083bdf2d25a101bcc609f361fe18819d0`. That master revision makes module-local validation scenes mandatory for every player-visible/runtime owner touched by an assignment. This issue changes `WorldBuilder`, `Composition/CaveWorldBuilder`, and `Composition/Showcase`; only `WorldBuilder` currently owns a focused module-local validation scene.

## Selected direction / remaining gates

Keep reusable secret/cave/clue behavior in production modules and Gallery-specific placement/presentation in Showcase composition. Add only the two newly required focused validation surfaces:

1. `Assets/Game/Composition/CaveWorldBuilder/Validation/...` exercises production cave authoring, secret-pocket composition, boundary clue presentation, and authored breakable breach through the production voxel/rendering path.
2. `Assets/Game/Composition/Showcase/Validation/...` exercises the production Worldbuilding Gallery bootstrap and its SecretDiscovery consumer, then frames both the natural environmental clue and authored breakable clue at gameplay scale. It must reuse production Gallery/SecretDiscovery methods rather than duplicate secret state or acceptance-only geometry.

Do not alter production behavior unless these focused validation consumers reveal a demonstrated defect. After the two module-local scenes exist, run a fresh exact-SHA targeted gate on `ci-test/fixes/agent-5`. Acceptance requires affected module tests, all required module-local built-player scenes, Kentridge integration, and exact `WorldbuildingGalleryShowcase` SceneIssue replay to pass. Review full-resolution Gallery evidence for understandable feature-specific geometry/material/environmental language with no placeholder sign or universal glow. Then complete closure bookkeeping, merge then-current master, open the feature PR, enable auto-merge, and require the PR `affected`/standalone Kentridge gate before completion.
