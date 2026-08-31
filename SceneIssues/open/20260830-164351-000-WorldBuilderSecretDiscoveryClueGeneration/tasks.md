# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect SceneIssue captures/marked regions; none are present.
- [x] Inspect `Docs/worldbuilder-secret-clues-design.md`, canonical secret topology, reusable world-object interaction, and discovery authority.
- [x] Discriminate hypotheses: canonical hidden-destination selection already exists; deterministic route/readability/clue planning was the missing layer.
- [x] Merge current master and re-read production-validation requirements.
- [x] Remove showcase-local primitive clue realization and its presentation regression after user/scope correction.
- [x] Remove the dedicated primitive WorldBuilder validation scene/module declaration because it duplicated production presentation instead of consuming it.

## Stable planning contracts

- [x] Stable `SecretRouteId`, `SecretClueId`, semantic clue-anchor identity, route kind, clue channel, hidden-volume relation, importance/readability, and bypass policy.
- [x] Route/clue plans retain canonical `SecretRef` / `ResolvedSecretPlan` identity; no second hidden-space solver.
- [x] Semantic clue anchors encode observability/channel/role/distance/dependency metadata without prefab names or capture coordinates.
- [x] Authored clue-chain contracts support site/NPC sources and secret-scoped memory topics without owning persistent save state.

## Deterministic planning and validation

- [x] Standard secrets require meaningful pre-solve evidence; Major secrets require at least two clues across independent channels unless explicitly overridden.
- [x] Required clues must be pre-solve observable; hidden/post-solve-only anchors cannot satisfy readability.
- [x] Circular route/clue dependencies fail validation.
- [x] Same seed + same inputs is stable independent of candidate enumeration order.
- [x] Natural/systemic traversal requires no interactable.
- [x] Diagnostics cover missing canonical secret, duplicate identities, source mismatch, missing anchors, circular dependency, readability shortfall, and bypass-policy failure.

## Generated cave composition / bypass / discovery regressions

- [x] `ProtectedShell`, `AuthoredBreakablesOnly`, and `SystemicBypassAllowed` are explicit route policies.
- [x] Protected shell rejects trivial bypass and undesignated breakable leakage when supplied topology evidence.
- [x] Authored-breakable route requires designated breakables and rejects surrounding leakage.
- [x] Multiple natural + mechanism routes preserve one canonical discovery identity.
- [x] `SecretRouteWorldObjectIntegrationTests.MechanismAndNaturalRoutesShareCanonicalDiscoveryAcrossInteractionAndReload` passed targeted run `33419056074`.
- [x] Revisit/reload/repeated mechanism activation is idempotent through canonical `SecretDiscoveryState`.
- [x] `SecretGeneratedCaveBypassIntegrationTests.VerifiedGeneratedCaveBarrierFeedsAuthoredBreakableBypassPolicy` passed targeted run `33420376990`.
- [x] Generated cave regression authors a real `CaveSecretPocket`, resolves it through `CaveSecretPocketSecretCandidateProvider` / canonical `SecretPlanner`, scans actual barrier/connector/pocket occupancy, and feeds verified topology into bypass policy.
- [x] Add reusable `CaveSecretPocketComposition` that selects production cave traversal semantics, authors the verified pocket, retries deterministic physical conflicts, and returns a canonical projection without owning scene/presentation state.
- [x] Add behavioral regressions for deterministic fallback after a preferred-terminal physical conflict and for a no-match request causing zero voxel mutation.
- [x] Add reusable semantic cave clue anchors and focused regression; exact run `33440180807` passed.
- [x] Add deterministic normal-voxel-coating clue presentation that preserves verified false-wall occupancy.

## Dedicated module-local validation scene

- [x] Create `Assets/Game/WorldBuilder/Validation/SecretDiscovery/` scene rather than using the Worldbuilding Gallery as the development surface.
- [x] Scene consumes production voxel world generation, production cave authoring, `CaveSecretPocketComposition`, production material/coating IDs, normal voxel rendering, and production vegetation/tree systems.
- [x] Register the dedicated player scenario in `worldbuilder-secret-discovery.module-validation.json`.
- [x] Add a behavioral regression for deterministic clue coating and preserved secret barrier topology.
- [x] Run `33445911882`; it failed before visual capture on a compile error (`Coatings.None` did not exist).
- [x] Fix that compiler defect by treating zero as the byte coating sentinel rather than introducing a new presentation dependency.
- [ ] Exact current-head CI compiles/runs the focused regression and automatically required dedicated module validation.
- [ ] Inspect full-resolution dedicated-scene screenshots; reject obscured, misframed, non-production, or visually ambiguous evidence.

## Built-player / representative acceptance

- [x] Earlier exact `WorldbuildingGalleryShowcase` replay reached a usable rendered state without runtime exceptions.
- [x] Full-resolution evidence was inspected; primitive validation/gallery clue implementations were not production-quality and were rejected.
- [x] Two materially different primitive presentation fixes failed the same quality symptom; experiment 005 isolated the parallel-renderer root cause.
- [ ] Representative natural terrain/cave route example with environmental/traversal clues and no required interactable is visually proven at gameplay scale.
- [ ] Interactable-backed mechanism representative is visually proven through a supported generated feature, or unsupported architectural realization is documented and an allowed alternative supported generated feature is used.
- [ ] Player can infer and reach representative secrets from intentional pre-solve evidence without universal glowing markers or wall-spamming.
- [ ] Add only a thin final `WorldbuildingGalleryShowcase` acceptance consumer after reusable dedicated-scene proof is green.
- [ ] Exact built `WorldbuildingGalleryShowcase` full-resolution screenshots pass clue readability, route legibility, accidental bypass, placeholder/sign-like evidence, and production-quality review.

## Cost / blast radius / closure

- [x] Planner/discovery code is one-shot/event-driven; no per-frame search/polling loop was added.
- [x] Cave composition attempts at most the bounded traversal candidate set and stops immediately on non-physical authoring failures; no frame-loop cost was added.
- [x] Dedicated validation presentation uses existing production renderer/material/vegetation paths rather than introducing a second player-visible renderer.
- [ ] All required acceptance criteria and exact-SHA gates green.
- [ ] Compare final feature head against current `origin/master` and document blast radius/cost.
- [ ] Move assigned SceneIssue directly `open -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master`, revalidate if the exact SHA changes as required, then push exact feature head to `origin/master` non-force.
