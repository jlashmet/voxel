# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect captures/marked regions; none are present.
- [x] Inspect design/source systems and discriminate competing hypotheses with repository evidence.
- [x] Keep work scoped to the assigned SceneIssue; never use `pending` or push the feature head directly to `master`.
- [x] Reassess against module-local ownership: WorldBuilder, CaveWorldBuilder, and Showcase each own focused EditMode coverage and production-path validation.

## Planning / behavior

- [x] Stable secret/route/clue IDs and immutable plan metadata.
- [x] Deterministic plan for same seed/inputs independent of candidate enumeration order.
- [x] Standard/Major clue count/channel rules behaviorally tested.
- [x] Pre-solve observability and circular dependency rejection behaviorally tested.
- [x] Reusable semantic anchors avoid prefab names/capture coordinates.
- [x] Interactable-backed and natural traversal routes both supported without duplicate interaction authority.
- [x] Multiple legal routes resolve to one canonical discovery identity; revisit/reload/repeated activation is idempotent.
- [x] `ProtectedShell`, `AuthoredBreakablesOnly`, and `SystemicBypassAllowed` represented and behaviorally tested.

## Production-path regression / visual validation

- [x] Generated cave secret composition and verified topology exercised through production authoring.
- [x] Deterministic boundary fracture preserves the verified seal before destruction.
- [x] WorldBuilder module-local validation uses production voxel generation, rendering, materials/coatings, vegetation, and destruction.
- [x] Prior accepted exact run `33801222778` proves WorldBuilder EditMode + production SecretDiscovery built player + Kentridge + SceneIssue replay; full-res evidence shows 35 fracture voxels and a 607-voxel breach.
- [x] Restore thin production `WorldbuildingGalleryShowcase` SecretDiscovery composition and compatibility/physical/surface-route regressions.
- [x] Add CaveWorldBuilder and Showcase module-owned EditMode assemblies and focused module-local validation scenes.
- [x] Exact run `33835125556` selected CaveWorldBuilder, Showcase, and WorldBuilder correctly; Cave player passed, Showcase failed before readiness.
- [x] Isolate Showcase readiness failure to validation seed drift; align C# + serialized scene with production Gallery seed `0x5EED1234`.
- [x] Falsify Gallery camera/framing and missing-geometry hypotheses using authoritative occupancy + focused rendered evidence.
- [x] Add Showcase-owned behavioral regression requiring post-bake SecretDiscovery authoring to advance the world change feed.
- [x] Apply smallest proven production fix: publish completed resident state once after post-bake cave/pocket/clue bulk authoring.
- [x] Exact run `33837600536` selected the required modules/players but stopped at compile; isolate the sole error to the new Showcase publication regression's missing `Game.Composition.WorldObjects.Runtime` test-asmdef reference and correct that module-owned dependency.
- [ ] Fresh exact-SHA CI module-validation plan selects CaveWorldBuilder, Showcase, and WorldBuilder EditMode assemblies plus adjacent validation scenes and Kentridge integration.
- [ ] Fresh exact-SHA Showcase publication regression passes.
- [ ] Representative SecretDiscovery examples are visibly understandable in `WorldbuildingGalleryShowcase` at gameplay scale.
- [ ] Gallery visual review is `production-quality`: feature-specific natural + breakable clue language, no stale terrain, placeholder signs, universal glow, floating/intersecting geometry, or invalid framing.

## Cost / integration / closure

- [x] Planner/discovery work is one-shot/event-driven; no per-frame search/polling added.
- [x] Cave composition is bounded by traversal candidates; post-bake publication is one bounded startup publication over resident regions.
- [x] Exact request `13183c5afa4da36d6f22c1f34df0b17aa6351896` / run `33837600536` completed failure and was not replaced while queued/running; its compile cause is isolated and fixed before retry.
- [ ] Fresh exact-SHA targeted gate green against current feature head.
- [ ] All acceptance criteria green from exact tests + built-player evidence.
- [ ] Move assigned SceneIssue `open -> closed`, set `status=fixed` and `resolvedUtc`, and complete supported resolution fields.
- [ ] Integrate then-current `origin/master` into `fixes/agent-5` before final promotion.
- [ ] Open final `fixes/agent-5` -> `master` PR and enable auto-merge immediately.
- [ ] Required PR `affected` gate plus canonical standalone Kentridge full-app test pass.
- [ ] Confirm PR merged and closed SceneIssue visible on `origin/master`.
