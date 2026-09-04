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
- [x] Exact request `56af2443f352fa4ce6561c784143243ecfb0cecc` / run `33839912405` completed without replacement. Cave focused tests passed; standalone Gallery semantic replay passed; Showcase focused tests exposed a fixture-only startup-radius mismatch.
- [x] Full-resolution review of run `33839912405` rejects closure because `02-authored-breakable-boundary.png` still shows underside/void; generic post-bake residency publication is therefore falsified as a complete visual fix.
- [x] Correct the Showcase publication regression fixture to use the production Gallery bake startup radius 4 and unload radius 6.
- [x] Strengthen the regression to require a content-dirty `VoxelChangeKind`, explicitly rejecting residency-only publication.
- [x] Apply the repository's established post-bulk-authoring remesh semantic: publish the bounded 3x3 secret-cave footprint with `VoxelChangeKind.All`, matching the completed-castle path that requires re-mesh/re-upload.
- [x] Exact run `33842982484` selected CaveWorldBuilder, Showcase, and WorldBuilder EditMode assemblies plus their module-local players and Kentridge; automatic validation stopped only because the publication regression's BrickPool tier was smaller than the production Gallery scene.
- [x] Match the publication regression to production Gallery `m_BrickPoolCapacity: 800000` so it can reach the content-dirty assertion.
- [x] Falsify another notification-only fix with run `33842982484`: full-resolution authored-breakable evidence still shows underside/void while player diagnostics show the renderer is cold (`visible=48`, `missingMax=647`) immediately before capture and continues converging afterward.
- [x] Replace the authored-breakable evidence's blind 1.25-second delay with production-renderer convergence gating: require post-pin render diagnostics, nonzero visible solids, and zero missing visible solid chunks for two frames; fail capture on timeout.
- [x] Exact run `33844103873` selected the correct module/player plan but failed compile before tests because Showcase directly imported `VoxelEngine.Rendering.Runtime`.
- [x] Isolate the compile failure to an ownership-boundary mistake rather than a missing asmdef reference; route convergence evidence through existing `RenderingComposition` diagnostics and remove direct `VoxelRenderBridge` access.
- [ ] Fresh exact-SHA Showcase content-dirty publication regression passes.
- [ ] Representative SecretDiscovery examples are visibly understandable in `WorldbuildingGalleryShowcase` at gameplay scale.
- [ ] Gallery visual review is `production-quality`: feature-specific natural + breakable clue language, no stale terrain, placeholder signs, universal glow, floating/intersecting geometry, or invalid framing.

## Cost / integration / closure

- [x] Planner/discovery work is one-shot/event-driven; no per-frame search/polling added.
- [x] Cave composition is bounded by traversal candidates; post-bake content invalidation is bounded to the same nine preloaded secret-cave regions.
- [x] User-named exact request `56af2443f352fa4ce6561c784143243ecfb0cecc` was monitored through completion without replacement.
- [x] Exact request `d003d5ad69f23b47278a8a6157957b7471c716bb` / run `33844103873` was allowed to complete and was not replaced while queued/running; its product compile failure was inspected before another request.
- [ ] Fresh exact-SHA targeted gate green against current feature head.
- [ ] All acceptance criteria green from exact tests + built-player evidence.
- [ ] Move assigned SceneIssue `open -> closed`, set `status=fixed` and `resolvedUtc`, and complete supported resolution fields.
- [ ] Integrate then-current `origin/master` into `fixes/agent-5` before final promotion.
- [ ] Open final `fixes/agent-5` -> `master` PR and enable auto-merge immediately.
- [ ] Required PR `affected` gate plus canonical standalone Kentridge full-app test pass.
- [ ] Confirm PR merged and closed SceneIssue visible on `origin/master`.
