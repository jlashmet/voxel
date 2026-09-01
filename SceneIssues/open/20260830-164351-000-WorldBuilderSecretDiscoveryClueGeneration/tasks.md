# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect SceneIssue captures/marked regions; none are present.
- [x] Inspect `Docs/worldbuilder-secret-clues-design.md`, canonical secret topology, reusable world-object interaction, and discovery authority.
- [x] Discriminate hypotheses: canonical hidden-destination selection already exists; deterministic route/readability/clue planning was the missing layer.
- [x] Remove primitive/parallel player-visible validation approaches after they failed production-quality review.
- [x] Per user correction, remove all `WorldbuildingGalleryShowcase` secret acceptance integration and Gallery-specific regression fixtures.

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
- [x] Replace broad random clue speckling with a deterministic branching fracture restricted to the cave-facing barrier layer.
- [x] Add a fracture regression proving deterministic placement, continuous vertical extent, sparse surface-only coverage, and unchanged solid barrier topology.

## Dedicated module-local validation scene

- [x] Create `Assets/Game/WorldBuilder/Validation/SecretDiscovery/` as the sole visual acceptance surface.
- [x] Scene consumes production voxel world generation, production cave authoring, `CaveSecretPocketComposition`, production material/coating IDs, normal voxel rendering, production vegetation/tree systems, and production destruction.
- [x] Register the dedicated player scenario in `worldbuilder-secret-discovery.module-validation.json`.
- [x] Replace the static camera with a deterministic authored-geometry walkthrough: exterior entrance -> entrance interior -> deeper cave -> clue approach -> clue/wall close view -> breach -> hidden pocket reveal.
- [x] Destroy the authored false wall during the built-player sequence through `ShowcaseWorld.Explode` and require a wall-destroyed log event.
- [x] Expand player capture to 24 seconds at 3-second intervals with at least seven frames.
- [x] Exact walkthrough run `33532261836` produced the requested cave-entry/destruction/reveal sequence and was accepted overall; visual review identified only clue readability as insufficient.
- [x] Dedicated consumer now renders the fracture with a dark soot coating rather than broad moss speckling.
- [ ] Exact crack-pattern feature head compiles and passes focused/module validation.
- [ ] At least two pre-destruction frames make the fracture clue and destroyable false wall readable at gameplay scale.
- [ ] A post-destruction frame clearly shows the breached wall and what is behind it.
- [ ] Final reveal frame clearly shows the hidden pocket from inside the opened route.

## Built-player / representative acceptance

- [ ] Player can infer the authored secret from intentional pre-solve fracture evidence without universal glowing markers or wall-spamming.
- [ ] False wall remains intact and blocks traversal before the destruction step.
- [ ] Production destruction opens the authored route and normal traversal space is visually reachable afterward.
- [ ] Built-player run has no startup/runtime exceptions and all required log assertions are present.

## Cost / blast radius / closure

- [x] Planner/discovery code is one-shot/event-driven; no per-frame search/polling loop was added.
- [x] Cave composition attempts at most the bounded traversal candidate set and stops immediately on non-physical authoring failures; no frame-loop cost was added.
- [x] Dedicated validation presentation uses existing production renderer/material/vegetation/destruction paths rather than introducing a second player-visible renderer.
- [ ] All required acceptance criteria and exact-SHA gates green.
- [ ] Compare final feature head against current `origin/master` and document blast radius/cost.
- [ ] Move assigned SceneIssue directly `open -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master`, revalidate if the exact SHA changes as required, then push exact feature head to `origin/master` non-force.
