# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and production-quality built-player proof. There are no original captures/marked regions. `issue.json` also requires representative SecretDiscovery examples in `WorldbuildingGalleryShowcase`.

## Hypotheses / material results

- The missing planning layer is implemented: stable IDs, deterministic scoring/tie-breaking, semantic anchors, route identity, discovery idempotence, clue-channel/count rules, and explicit bypass semantics have behavioral coverage.
- Focused `Assets/Game/WorldBuilder/Validation/SecretDiscovery` already proved production cave authoring/rendering/destruction; exact run `33801222778` was green and showed 35 sparse fracture voxels before a 607-voxel production breach.
- Current module ownership is repository-compliant: CaveWorldBuilder, Showcase, and WorldBuilder each own their focused EditMode surface and production-path validation scene.
- Exact run `33835125556` on source `46251d2499aa5f39d69c20cd76a3685a2b4bce77` selected the correct three modules. Cave validation passed; Showcase failed before its readiness log. The validation scene was using a non-production seed. Current head aligns both C# and serialized scene with production Gallery seed `0x5EED1234`.
- Production Gallery visual evidence then isolated a second defect. Camera/framing and missing-geometry hypotheses are falsified: authoritative storage proves the acceptance eye is carved air and focused SecretDiscovery renders the authored cave. The production Gallery restores a bake first, then bulk-authors the secret without publishing new resident state. The repository’s existing `IVoxelStorageRuntime.PublishAllResidentRegions()` contract is specifically for completed bulk authoring, and baked castle repairs already use it after post-bake mutation.
- A Showcase-owned regression was committed before the fix: it preloads Gallery + secret regions, samples `Changes.CurrentVersion`, composes the production secret, and requires a post-authoring change-feed publication. Production commit `55901ba493987c18477808d358d762504106e340` now publishes resident state once after all cave/pocket/clue writes succeed.

## Selected fix / remaining gates

Keep reusable secret/cave/clue behavior in production modules and Gallery-specific placement/presentation in Showcase composition. The post-bake publication is bounded startup work and adds no per-frame search or renderer-specific policy.

Current `master` (`abe9602e1025b5e02f11a7aa6b8a5965aa1c7fe3`) is two disjoint commits beyond the branch’s last merged base; its changes are Campaign/Progression/Quests/WorldObjects only. The connector cannot perform a supported base-into-feature merge, so final master integration remains required before promotion but does not block issue-specific exact validation.

Run fresh exact-SHA targeted CI from the current feature head. Require CaveWorldBuilder, Showcase, WorldBuilder module tests/scenes, Kentridge integration, and exact SceneIssue replay. Inspect full-resolution built-player captures; `WorldbuildingGalleryShowcase` must visibly show understandable natural and breakable clue language at production quality. Only then complete closure bookkeeping, integrate then-current master, open the final PR, enable auto-merge, and monitor the required `affected` gate through merge.
