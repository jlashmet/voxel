# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect captures/marked regions; none are present.
- [x] Inspect design/source systems and discriminate competing hypotheses with repository evidence.
- [x] Keep work scoped to the assigned SceneIssue; never use `pending` or push the feature head directly to `master`.
- [x] Reassess against module-local ownership: WorldBuilder, CaveWorldBuilder, and Showcase each own focused EditMode coverage and production-path validation.
- [x] Adopt current module-local validation convention: `Assets/Game/WorldBuilder/Validation/SecretDiscovery/WorldBuilderSecretDiscoveryValidation.unity` is the authoritative built-player visual gate; legacy Gallery captures remain historical regression evidence only.
- [x] Update SceneIssue scene metadata so standalone replay targets `WorldBuilderSecretDiscoveryValidation` instead of `WorldbuildingGalleryShowcase`.

## Planning / behavior

- [x] Stable secret/route/clue IDs and immutable plan metadata.
- [x] Deterministic plan for same seed/inputs independent of candidate enumeration order.
- [x] Standard/Major clue count/channel rules behaviorally tested.
- [x] Pre-solve observability and circular dependency rejection behaviorally tested.
- [x] Reusable semantic anchors avoid prefab names/capture coordinates.
- [x] Interactable-backed and natural traversal routes both supported without duplicate interaction authority.
- [x] Multiple legal routes resolve to one canonical discovery identity; revisit/reload/repeated activation is idempotent.
- [x] `ProtectedShell`, `AuthoredBreakablesOnly`, and `SystemicBypassAllowed` represented and behaviorally tested.
- [x] Make clue realization route/mechanism-aware: breakable, mechanism-backed, and natural/traversal route families choose only compatible anomaly motifs and expose distinct player action intents.
- [x] Reuse canonical interactable mechanisms for levers/buttons/plates/pushables/etc.; WorldBuilder may place/connect them but must not own duplicate interaction state. `SecretRouteWorldObjectIntegrationTests` proves mechanism execution/state restoration through the canonical WorldObject runtime and one canonical SecretDiscovery identity.
- [x] Introduce deterministic anomaly-composition selection across multiple motif families instead of a universal visual marker. Initial families: structural fracture, material seam, surface wear, mechanical trace, debris alignment, vegetation discontinuity, erosion trail, sightline gap, disturbed ground.
- [x] Make anomaly realization context-relative: `SecretClueLocalContext` scores local vegetation density, surface uniformity, structural regularity, occlusion, and recent disturbance so the same route can choose a different compatible anomaly in another environment.
- [x] Ensure mechanism-specific clue language supports player hypothesis formation through explicit `SecretClueActionIntent` (`BreakBarrier`, `OperateMechanism`, `TraverseTerrain`, `Investigate`).
- [x] Add deterministic variety/repetition control: recently used nearby motif families receive a strong deterministic penalty while remaining route-compatible.
- [x] Behaviorally validate route compatibility, identical-input determinism, local-context sensitivity, repetition variation, and mechanism-specific action intent in `SecretClueAnomalyPlannerTests`.

## Production-path regression / visual validation

- [x] Generated cave secret composition and verified topology exercised through production authoring.
- [x] Deterministic boundary fracture preserves the verified seal before destruction.
- [x] WorldBuilder module-local validation uses production voxel generation, rendering, materials/coatings, vegetation, and destruction.
- [x] Prior accepted exact run `33801222778` proves WorldBuilder EditMode + production SecretDiscovery built player + Kentridge + SceneIssue replay; full-res evidence shows 35 fracture voxels and a 607-voxel breach.
- [x] Historical Gallery publication/rendering investigations retained as regression evidence; do not use Gallery as the current visual acceptance scene.
- [x] Classify run `33863772871` planner failure as nested-module ownership regression rather than SecretDiscovery behavior or retryable infrastructure.
- [x] Fix nested module ownership narrowly by assigning runtime asmdefs to the nearest discovered module root while retaining duplicate-token fail-closed checks; add a focused Python regression fixture.
- [x] Exact request `700641da19d648ed7c85d148cf3bb272c6b39ffd` / run `33886411818` completed without replacement and passed plan derivation, automatic module validation, standalone SceneIssue replay, screenshot upload, and final status.
- [x] Merge then-current master `d08612dfe2f4a99aff34897717569744565bc642` into `fixes/agent-5` through PR #273 before validating the new anomaly work.
- [x] Update module-local validation scene to consume `SecretClueAnomalyPlanner`: natural approach uses dense-vegetation discontinuity/negative-space language while the breakable barrier uses structural-fracture language.
- [x] Classify exact request `36d6a7d297ed0a8023993b9482b463e869b0ac14` / run `33898606330`: module planning succeeded, but Unity compilation failed because the local validation assembly imported `Game.WorldBuilder.Runtime` without referencing `Game.WorldBuilder.Runtime`; standalone replay failed for the same compile error. This is a deterministic validation-assembly dependency defect, not renderer/gameplay evidence and not retryable infrastructure.
- [x] Fix the failed-run cause narrowly by adding `Game.WorldBuilder.Runtime` to `Game.WorldBuilder.SecretDiscovery.Validation.asmdef`; anomaly behavior/source remains otherwise unchanged.
- [x] Exact request `c4adea52a154bba78d36741bd32bbbf560ea8f81` / run `33899750246` passed automatic module validation and standalone player replay after the assembly-reference fix.
- [x] Confirm module player validation builds and launches a real standalone `.app` through `tools/player-validation.py` / `tools/showcase-player-capture.sh`.
- [x] Identify remaining harness gap: `WorldBuilderSecretDiscoveryValidation` still manually constructs `ShowcaseWorld`, rendering, cave authoring, and vegetation, so standalone execution is not yet equivalent to the normal gameplay composition lifecycle.
- [x] Identify canonical gameplay lifecycle boundary: production Kentridge uses `KentridgeSessionRuntimeGraphFactory` + `GameSessionOrchestrator` (`Prepare` / `EnterRunning` / `Tick` / `Shutdown`).
- [x] Refactor local SecretDiscovery validation to enter through the canonical application/session composition lifecycle: `GameSessionOrchestrator` owns prepare/run/tick/shutdown, initialization is `ISessionRuntimeGraph.InitializeNewGame`, and per-frame work is an `ISessionUpdateStep`.
- [x] Add fail-closed architecture regression proving the local validation implements `ISessionRuntimeGraphFactory` + `ISessionRuntimeGraph` and retains a canonical `GameSessionOrchestrator` owner; built-player scenario also requires a `...app session running:` log.
- [ ] Run fresh exact-SHA targeted validation after the SceneIssue scene/harness refactor and prove the standalone SceneIssue replay no longer launches Gallery.
- [ ] Full-resolution module-local screenshot review proves the natural approach visibly communicates an intentional anomaly and plausible traversal/investigation hypothesis without glow/icon/signage.
- [ ] Full-resolution module-local screenshot review proves the breakable barrier visibly communicates a plausible break/destruction hypothesis without becoming a universal marker.
- [ ] Local built-scene visual review is `production-quality`: no stale terrain, placeholder signs, universal glow, floating/intersecting geometry, void/underside, or invalid framing.

## Cost / integration / closure

- [x] Planner/discovery/anomaly work is one-shot/event-driven; no per-frame search/polling added.
- [x] Cave composition is bounded by traversal candidates; presentation changes are bounded to selected clue realization and local validation vegetation.
- [x] All named/created exact CI requests were left untouched while queued/running and diagnosed only after completion.
- [ ] All acceptance criteria green from exact tests + full-app-harness module-local built-player evidence.
- [ ] Move assigned SceneIssue `open -> closed`, set `status=fixed` and `resolvedUtc`, and complete supported resolution fields.
- [ ] Recheck and integrate then-current `origin/master` into `fixes/agent-5` immediately before final promotion if master advances.
- [ ] Open final `fixes/agent-5` -> `master` PR and enable auto-merge immediately.
- [ ] Required PR `affected` gate plus canonical standalone Kentridge/full-app integration gate pass as required by current repo policy.
- [ ] Confirm PR merged and closed SceneIssue visible on `origin/master`.
