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
- [x] Falsify Gallery camera/framing and missing-authoritative-geometry hypotheses using authoritative occupancy + focused rendered evidence.
- [x] Correct Showcase publication regression/player fidelity to production Gallery startup radius 4, unload radius 6, and BrickPool 800000.
- [x] Strengthen publication regression to require content-dirty `VoxelChangeKind` and publish the bounded 3x3 secret-cave footprint with `VoxelChangeKind.All`.
- [x] Replace blind capture delay with production-renderer convergence gating through `RenderingComposition` diagnostics; do not import renderer internals from Showcase.
- [x] Exact run `33851419365` proves the Showcase content-dirty publication regression passes and the strict renderer predicate can converge (`visible=487 missing=0`).
- [x] Full-resolution review of `33851419365` falsifies renderer-cold as the remaining explanation; ordinary Gallery frames also show the base void/floating presentation.
- [x] Exact request `698aa3347a3065d1e495ba260cc90913fde71907` / run `33852280392` completed without replacement and passed automatic module validation plus standalone SceneIssue replay on source `3e6cd24436fa0a5b3f8f23279697ada624734d16`.
- [x] Full-resolution review of `33852280392` completed and classified visual evidence `unacceptable`: breakable frame remains below/through terrain with large void; natural frame still does not communicate an understandable cave clue at gameplay scale.
- [x] Apply the issue-guide two-fix rule: stop speculative SecretDiscovery-side visual changes after publication, convergence, and validation-fidelity fixes all leave the same base Gallery renderer symptom.
- [ ] Shared GPU renderer restoration is authoritative on `origin/master` (external prerequisite; current restoration work is not merged to master).
- [ ] After renderer prerequisite lands, merge current `origin/master` into `fixes/agent-5` and rerun exact-SHA targeted validation.
- [ ] Representative SecretDiscovery examples are visibly understandable in `WorldbuildingGalleryShowcase` at gameplay scale.
- [ ] Gallery visual review is `production-quality`: feature-specific natural + breakable clue language, no stale terrain, placeholder signs, universal glow, floating/intersecting geometry, void/underside, or invalid framing.

## Cost / integration / closure

- [x] Planner/discovery work is one-shot/event-driven; no per-frame search/polling added.
- [x] Cave composition is bounded by traversal candidates; post-bake content invalidation is bounded to the same nine preloaded secret-cave regions.
- [x] All named/created exact CI requests were left untouched while queued/running and diagnosed only after completion.
- [x] Fresh exact-SHA targeted gate is green for feature SHA `3e6cd24436fa0a5b3f8f23279697ada624734d16` via run `33852280392`.
- [ ] All acceptance criteria green from exact tests + built-player evidence.
- [ ] Move assigned SceneIssue `open -> closed`, set `status=fixed` and `resolvedUtc`, and complete supported resolution fields.
- [ ] Integrate then-current `origin/master` into `fixes/agent-5` before final promotion.
- [ ] Open final `fixes/agent-5` -> `master` PR and enable auto-merge immediately.
- [ ] Required PR `affected` gate plus canonical standalone Kentridge full-app test pass.
- [ ] Confirm PR merged and closed SceneIssue visible on `origin/master`.
