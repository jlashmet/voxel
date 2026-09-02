# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect SceneIssue captures/marked regions; none are present.
- [x] Inspect `Docs/worldbuilder-secret-clues-design.md`, canonical secret topology, reusable world-object interaction, and discovery authority.
- [x] Discriminate hypotheses: canonical hidden-destination selection already exists; deterministic route/readability/clue planning was the missing layer.
- [x] Remove primitive/parallel player-visible validation approaches after they failed production-quality review.
- [x] Per user correction, remove all `WorldbuildingGalleryShowcase` secret acceptance integration and Gallery-specific regression fixtures.
- [ ] **BLOCKER:** `issue.json` still requires representative secret examples and exact built validation in `WorldbuildingGalleryShowcase`, while the user explicitly prohibited integrating this feature into that Gallery. Workflow rules forbid changing acceptance. Keep open until resolved.

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
- [x] Add reusable `CaveSecretPocketComposition` with deterministic physical-conflict fallback and no-match/zero-mutation behavior.
- [x] Add reusable semantic cave clue anchors; exact run `33440180807` passed.
- [x] Add deterministic normal-voxel clue presentation that preserves verified false-wall occupancy.
- [x] Replace broad random speckling with a deterministic branching fracture restricted to the cave-facing barrier layer.
- [x] `BoundaryEvidenceIsDeterministicFractureOnCaveFaceAndPreservesVerifiedSeal` passed exact run `33537413920`.

## Dedicated module-local validation scene

- [x] Create `Assets/Game/WorldBuilder/Validation/SecretDiscovery/` as the focused production-path visual proof surface.
- [x] Scene consumes production voxel world generation, cave authoring/composition, material/coating rendering, vegetation, meshing, and destruction.
- [x] Remove obsolete `worldbuilder-secret-discovery.module-validation.json`; current repository convention discovers module-local tests and paired `Validation/` scene/scenario automatically.
- [x] Deterministic walkthrough: entrance -> interior -> deeper cave -> fracture approach -> close wall -> breach -> hidden pocket.
- [x] Destroy the authored false wall through `ShowcaseWorld.Explode`; wall-destroyed log is required.
- [x] 24-second player capture at 3-second intervals produces eight frames.
- [x] Exact run `33537413920` passed all automatic WorldBuilder focused tests, dedicated player validation, and Kentridge integration before the master merge.
- [x] Full-resolution 9s/12s/15s frames visibly show the fracture before destruction.
- [x] Full-resolution 18s frame shows the breached route; player log reports 607 voxels removed.
- [x] Full-resolution 21s frame shows the authored hidden pocket from the opened route.
- [x] Built-player run has required ready/destruction logs and no `NullReferenceException` / `MissingReferenceException`.
- [x] The 0s image is a pre-ready transient; it is not used as acceptance evidence. The 3s frame is the first valid entrance proof.
- [ ] Revalidate the merged/convention-correct exact feature SHA through `ci-test/fixes/agent-5`, including convention-discovered module tests, dedicated player scene, and Kentridge integration; inspect full-resolution artifacts.

## Built-player / representative acceptance

- [x] Dedicated cave example communicates intentional pre-solve fracture evidence without emissive/glowing markers.
- [x] False wall remains solid before destruction; regression rechecks the complete verified barrier after clue application.
- [x] Production destruction opens the route and normal traversal space is visually reachable afterward.
- [ ] Required representative examples are visible and understandable in `WorldbuildingGalleryShowcase` at gameplay scale — blocked by the user prohibition above.
- [ ] Exact built-application SceneIssue validation proves the required Gallery secret examples — blocked by the same acceptance conflict.

## Cost / blast radius / closure

- [x] Planner/discovery code is one-shot/event-driven; no per-frame search/polling loop was added.
- [x] Cave composition is bounded by traversal candidates and stops on non-physical failures; no frame polling/search cost added.
- [x] Clue fracture authors 35 coating voxels once at scene generation; no recurring runtime work.
- [x] Dedicated destruction is validation orchestration only; production explosion removed 607 voxels in the proof run.
- [x] Merged `origin/master` `b1b69290a59278b0e7caba798641c76a9866aa5c` into the feature branch at `dee150fa000597d6abe1a2693e3a15d429266fb5`; tests were reconciled into the current module-local WorldBuilder test layout.
- [ ] Repeat final blast-radius/cost judgment after merged exact-SHA CI artifacts are available.
- [ ] All required acceptance criteria and exact-SHA gates green — blocked by unresolved Gallery acceptance conflict plus merged-SHA revalidation.
- [ ] Move assigned SceneIssue directly `open -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Fetch/merge current `origin/master` again immediately before promotion, then push exact feature head to `origin/master` non-force.
