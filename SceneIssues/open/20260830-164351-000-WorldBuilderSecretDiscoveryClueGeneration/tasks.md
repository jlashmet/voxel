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
- [ ] Make clue realization explicitly route/mechanism-aware: direct traversal, terrain manipulation, interactable-backed mechanism, or allowed systemic bypass must drive compatible clue intent/presentation.
- [ ] Reuse canonical interactable mechanisms for levers/buttons/plates/pushables/etc.; WorldBuilder may place/connect them but must not own duplicate interaction state.
- [ ] Introduce deterministic anomaly-composition selection across multiple motif families instead of a universal visual marker.
- [ ] Make anomaly realization context-relative: score/alter local normality (for example density, material, silhouette, alignment, repetition, negative space) so the same clue intent can realize differently by environment.
- [ ] Ensure mechanism-specific clue language supports player hypothesis formation (for example blastable wall reads as structurally breakable, diggable route reads as disturbed/soft terrain, lever route exposes mechanical evidence linking control and barrier).
- [ ] Add deterministic variety/repetition control so nearby secrets do not repeatedly use the same motif family when compatible alternatives exist.
- [ ] Behaviorally validate that clue realization is semantically compatible with the selected route mechanism and remains deterministic for identical inputs/seed.

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
- [x] Shared GPU renderer restoration became authoritative on master via PR #230 and was merged into `fixes/agent-5` via PR #266, producing feature head `cf0e95237d1965c99d0f9522e302794ab8a13a4a` before these documentation changes.
- [x] Classify run `33863772871` failure in `derive automatic module validation plan`: required master sync introduced nested tested module roots (`.../Playable` and `.../Playable/SceneRuntime`), and recursive runtime asmdef discovery assigned `Game.Kentridge.PlayableSlice` to both before `plan.json` could be written. This is a deterministic planner regression, not retryable infrastructure and not a SecretDiscovery replay failure.
- [x] Fix nested module ownership narrowly by assigning runtime asmdefs to the nearest discovered module root while retaining duplicate-token fail-closed checks; add a focused Python regression fixture for the nested Kentridge shape.
- [ ] Run fresh exact-SHA targeted validation on the new feature head after confirming then-current master state.
- [ ] Representative SecretDiscovery examples visibly communicate an intentional anomaly and a plausible action hypothesis at gameplay scale.
- [ ] Gallery visual review is `production-quality`: varied feature-specific natural + mechanism-backed clue language, no stale terrain, placeholder signs, universal glow, floating/intersecting geometry, void/underside, or invalid framing.

## Cost / integration / closure

- [x] Planner/discovery work is one-shot/event-driven; no per-frame search/polling added.
- [x] Cave composition is bounded by traversal candidates; post-bake content invalidation is bounded to the same nine preloaded secret-cave regions.
- [x] All named/created exact CI requests were left untouched while queued/running and diagnosed only after completion.
- [x] Fresh exact-SHA targeted gate was green for feature SHA `3e6cd24436fa0a5b3f8f23279697ada624734d16` via run `33852280392`; later branch changes require a new exact validation before closure.
- [ ] All acceptance criteria green from exact tests + built-player evidence.
- [ ] Move assigned SceneIssue `open -> closed`, set `status=fixed` and `resolvedUtc`, and complete supported resolution fields.
- [ ] Integrate then-current `origin/master` into `fixes/agent-5` before final promotion.
- [ ] Open final `fixes/agent-5` -> `master` PR and enable auto-merge immediately.
- [ ] Required PR `affected` gate plus canonical standalone Kentridge full-app test pass.
- [ ] Confirm PR merged and closed SceneIssue visible on `origin/master`.
