# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and built-player proof. There are no original captures/marked regions. `issue.json` requires representative generated SecretDiscovery examples in `WorldbuildingGalleryShowcase` as well as focused production validation.

## Hypotheses / material results

- Canonical hidden-secret selection existed; deterministic route/readability/clue planning was the missing layer. Stable IDs, semantic anchors, deterministic scoring/tie-breaking, diagnostics, route identity, discovery idempotence, and explicit bypass semantics are implemented and behaviorally covered.
- Dedicated `Assets/Game/WorldBuilder/Validation/SecretDiscovery` validation uses production voxel generation/rendering/destruction. Exact run `33801222778` passed WorldBuilder EditMode, dedicated built player, Kentridge integration, and SceneIssue replay after renderer lifecycle repair.
- The prior Gallery-integration instruction conflict is resolved by the current assignment directive: repository acceptance is authoritative. The thin production Gallery consumer and its compatibility/physical/surface-route regressions were restored at feature SHA `aa23d9e42439ed2ca18119051ecedff2a7a6ee1e`.
- Exact run `33821322632` failed before gameplay validation because the restored regressions were returned to legacy `Assets/Tests/EditMode`. The three regressions were moved unchanged into `Assets/Game/WorldBuilder/Tests/EditMode`; exact targeted run `33822800307` then succeeded against feature SHA `5ce4b97bc4bd3b69556b2c41ce8c995319f4278d`.
- Current master `13b3c6a752deb030effba0f6e430863d0c1fd115` was merged into feature commit `43c2afc083bdf2d25a101bcc609f361fe18819d0`. That master revision requires every affected runtime module to own its own EditMode and built-player validation surfaces; a shared WorldBuilder suite or Gallery alone does not satisfy module ownership.
- `tools/module-validation-plan.py` discovers runtime modules from module-owned `Tests/**/*.asmdef` assemblies before selecting adjacent authored validation scenes. Static inspection therefore showed that adding only `Composition/CaveWorldBuilder/Validation` and `Composition/Showcase/Validation` would leave those scenes undiscoverable because neither module owned a `Tests` asmdef.
- Feature commit `1fac049fd45198b4d6096f67f5346f100fdff82e` corrects that structural gap without changing regression behavior: the existing physical discriminator test blob/GUID now belongs to CaveWorldBuilder, the existing Gallery compatibility/surface-route blobs/GUIDs belong to Showcase, and both modules own focused EditMode asmdefs. Their validation scenes remain thin production consumers.

## Selected direction / remaining gates

Keep reusable secret/cave/clue behavior in production modules and Gallery-specific placement/presentation in Showcase composition. The required module-local surfaces are:

1. `Assets/Game/Composition/CaveWorldBuilder/Tests/EditMode` owns the physical cave-secret discriminator regression; `Validation/CaveWorldBuilderSecretPocketValidation.unity` exercises production cave authoring, secret-pocket composition, boundary clue presentation, and authored breakable breach through the production voxel/rendering/destruction path.
2. `Assets/Game/Composition/Showcase/Tests/EditMode` owns Gallery replay compatibility and natural surface-route regressions; `Validation/ShowcaseSecretDiscoveryValidation.unity` exercises the production Worldbuilding Gallery bootstrap and its SecretDiscovery consumer, then frames both the natural environmental clue and authored breakable clue at gameplay scale.
3. `Assets/Game/WorldBuilder` retains planner/core regressions and its focused production SecretDiscovery validation.

Do not alter production behavior unless these focused module surfaces reveal a demonstrated defect. The already queued run `33830888621` remains untouched because repo policy forbids replacing queued/running CI, but it targets intermediate feature SHA `b13c04565f60b34c43fcc57be07e80e8725c6521` and cannot close the later ownership commit. After that request completes, run a fresh exact-SHA targeted gate parented to the final feature head. Acceptance requires all affected module EditMode assemblies, all required module-local built-player scenes, Kentridge integration, and exact `WorldbuildingGalleryShowcase` SceneIssue replay to pass. Review full-resolution module and Gallery evidence for understandable feature-specific geometry/material/environmental language with no placeholder sign or universal glow. Then complete closure bookkeeping, merge then-current master, open a fresh feature PR, enable auto-merge, and require the PR `affected`/standalone Kentridge gate before completion.
