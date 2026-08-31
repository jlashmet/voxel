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
- [x] `SecretRouteWorldObjectIntegrationTests.MechanismAndNaturalRoutesShareCanonicalDiscoveryAcrossInteractionAndReload` passed targeted run `33419056074` on exact source `86138410045da72ecd2b23f4eca7167f4cb034e3`.
- [x] Revisit/reload/repeated mechanism activation is idempotent through canonical `SecretDiscoveryState`.
- [x] `SecretGeneratedCaveBypassIntegrationTests.VerifiedGeneratedCaveBarrierFeedsAuthoredBreakableBypassPolicy` passed targeted run `33420376990` on exact source `5c8fda4957b6d1b69c8f115e164f3228c437b822`.
- [x] Generated cave regression authors a real `CaveSecretPocket`, resolves it through `CaveSecretPocketSecretCandidateProvider` / canonical `SecretPlanner`, scans actual barrier/connector/pocket occupancy, and feeds verified topology into bypass policy.
- [x] Add reusable `CaveSecretPocketComposition` that selects production cave traversal semantics, authors the verified pocket, retries deterministic physical conflicts, and returns a canonical projection without owning scene/presentation state.
- [x] Add behavioral regressions for deterministic fallback after a preferred-terminal physical conflict and for a no-match request causing zero voxel mutation.
- [x] Declare automatic module validation for secret planning/runtime, cave bridge/composition, and focused regressions without a fake player scene.
- [ ] Exact current-head CI compiles/runs `CaveSecretPocketCompositionTests` and all automatically required module tests.

## Built-player / representative acceptance

- [x] Earlier exact `WorldbuildingGalleryShowcase` replay reached a usable rendered state without runtime exceptions.
- [x] Full-resolution evidence was inspected; primitive validation/gallery clue implementations were not production-quality and were rejected.
- [x] Two materially different primitive presentation fixes failed the same quality symptom; experiment 005 isolated the parallel-renderer root cause.
- [x] Run `33433401020` proved the prior generated-cave focused test and Kentridge built-player module gate still pass after scope cleanup; overall run failed only because SceneIssue replay was invoked with the wrong path form. See `ci-operations.md`.
- [ ] Exact-head targeted CI after production-boundary cleanup and reusable cave composition.
- [ ] Representative generated secret/clue realization is visible through a production generated-world/presentation consumer rather than `GameObject.CreatePrimitive` or one-off gallery geometry.
- [ ] Architectural/interactable-backed representative example through a supported generated feature, or documented limitation with an alternative supported generated feature per issue policy.
- [ ] Natural terrain/cave representative example with environmental/traversal clues and no required interactable.
- [ ] Player can infer and reach representative secrets from intentional pre-solve evidence without universal glowing markers or wall-spamming.
- [ ] Exact built `WorldbuildingGalleryShowcase` full-resolution screenshots pass clue readability, route legibility, accidental bypass, placeholder/sign-like evidence, and production-quality review.

## Cost / blast radius / closure

- [x] Planner/discovery code is one-shot/event-driven; no per-frame search/polling loop was added.
- [x] Cave composition attempts at most the bounded traversal candidate set (currently fixed-capacity) and stops immediately on non-physical authoring failures; no frame-loop cost was added.
- [x] Current production diff is limited to reusable WorldBuilder planning/runtime, cave composition adapters, focused tests/module metadata, and this SceneIssue; showcase-local/primitive validation drift has been removed.
- [ ] All required acceptance criteria and exact-SHA gates green.
- [ ] Move assigned SceneIssue directly `open -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master`, revalidate if the exact SHA changes as required, then push exact feature head to `origin/master` non-force.
