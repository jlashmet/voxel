# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect the SceneIssue directory for captures/marked regions; none are present.
- [x] Inspect `Docs/worldbuilder-secret-clues-design.md`, existing secret topology contracts, and production `SecretPlanner`.
- [x] Discriminate root-cause hypotheses: canonical hidden-destination selection already exists; route/readability/clue planning is the missing WorldBuilder layer.
- [x] Re-check current master for generic interactable/inspect/discovery prerequisite. `20260830-014314-000-ExplorationInteractablesSecretsShowcase` remains open and no verified reusable interaction/discovery API is present; built-player mechanism/discovery integration is externally blocked.
- [x] Confirm module-local built-player validation architecture has landed on master; do not use it to fake the still-missing interaction/discovery prerequisite.
- [x] Trace the actual WorldBuilder -> voxel realization path. `WorldBuilderVoxelCatalogue.Build` currently accepts only `AuthoredTownPlan` and forwards to the Kentridge settlement backend; there is no secret/route/clue input or secret-specific voxel realization in the production adapter. The raster/storage path also retains voxel material/surface semantics rather than secret-route provenance. Therefore semantic `SecretBypassEvidence` cannot honestly be claimed as validation of generated secret voxel geometry yet.

## Stable planning contracts

- [x] Add stable `SecretRouteId`, `SecretClueId`, and semantic clue-anchor identity.
- [x] Add `SecretImportance`, route kind, clue channel, semantic anchor role, hidden-volume relation, and explicit voxel bypass policy.
- [x] Express route plans against the existing stable `SecretRef`; multiple legal routes retain one discovery identity.
- [x] Add semantic clue anchors with supported channels, pre-solve observability, useful distance band, hidden-volume relation, and optional route dependency/explained-route metadata; no prefab names, transforms, or capture coordinates.
- [x] Add authored clue-chain contracts for site/NPC sources, required/optional behavior, content keys, and secret-scoped `memory://secrets/<id>` topic without owning persistent save storage.
- [x] Keep clue/route planning layered on authoritative `ResolvedSecretPlan`; no second hidden-space solver.

## Deterministic route/readability planning

- [x] Add `SecretDiscoveryPlanner` consuming the canonical `ResolvedSecretPlan` plus resolved semantic site bindings.
- [x] Standard defaults to at least one meaningful pre-solve clue; Major defaults to at least two clues across independent channels; explicit minimum override remains possible.
- [x] Required readability candidates must be pre-solve observable and outside/boundary evidence; hidden/post-solve-only anchors cannot satisfy required policy.
- [x] Circular clue dependency on the same route it explains fails validation.
- [x] Same seed + same inputs is stable and does not depend on site/source enumeration order.
- [x] Route output is stable-sorted and all routes preserve the same `SecretRef` discovery identity.
- [x] Natural/systemic traversal route requires no interactable, proving route planning does not assume every secret uses an interactable.
- [x] Planner emits inspectable diagnostics for missing canonical secret, duplicate route/anchor IDs, route-secret mismatch, missing anchor site, circular dependency, insufficient clue count/diversity, and bypass-policy failures.

## Voxel bypass policy

- [x] Represent `ProtectedShell`, `AuthoredBreakablesOnly`, and `SystemicBypassAllowed` explicitly per route.
- [x] Protected shell rejects trivial unintended bypass or undesignated breakable leakage when supplied geometry-analysis evidence.
- [x] Authored-breakable route requires designated breakables and rejects surrounding destructibility/trivial bypass leakage when supplied geometry-analysis evidence.
- [x] Systemic bypass may remain a legitimate alternate route while preserving the same secret discovery identity.
- [ ] Validate these policy facts against actual generated voxel geometry in the built representative content. **Blocked on missing production secret geometry realization:** current `WorldBuilderVoxelCatalogue` only realizes `AuthoredTownPlan`/Kentridge and does not consume `ResolvedSecretPlan`/route plans, so there are no generated secret voxels to scan without inventing a parallel realization path.

## Authored clue-chain / discovery seam

- [x] Add `SecretCluePlanner` consuming authoritative resolved secret/site/NPC plans.
- [x] Required clue with no legal resolved source fails explicitly; optional unresolved clue is omitted without invalidating the plan.
- [x] Same world seed + same secret/source candidates produces identical ordered clue plan.
- [x] Equivalent source alternatives can vary deterministically across seeds without generation-order dependence.
- [x] NPC rumor source requires a resolved NPC with conversation capability.
- [x] Add event-driven `SecretDiscoveryLedger` seam: starts undiscovered, clue observation does not reveal target, explicit discovery is idempotent in-memory, capture/restore is deterministic.
- [ ] Replace/bind the temporary discovery seam to the canonical runtime discovery authority once that owning API lands; WorldBuilder must not become save/reward authority. **Blocked: owning interactables/discovery SceneIssue remains open on current master.**

## Behavioral regressions

- [x] Required hidden-cache chain resolves environmental hint -> shelter/interior readable clue -> final inspectable entrance clue.
- [x] NPC/rumor variant resolves through an authoritative conversation-capable NPC assignment.
- [x] Missing required clue source produces deterministic hard failure.
- [x] Optional missing source is omitted without failing the overall clue plan.
- [x] Duplicate clue id and duplicate stage fail validation.
- [x] Missing canonical resolved secret fails validation.
- [x] Same-seed determinism regression.
- [x] Alternate-seed source variation regression where multiple equivalent sources exist.
- [x] Discovery/memory capture-restore regression.
- [x] Regression proves clue planning consumes canonical `ResolvedSecretPlan` candidate/entrance rather than selecting a second hidden location.
- [x] Standard/Major clue count and Major independent-channel policy regressions.
- [x] Pre-solve observability and circular-dependency regressions.
- [x] Multiple natural + mechanism route identity regression.
- [x] Protected-shell, authored-breakable leakage, and systemic-bypass policy regressions.

## CI evidence

- [x] First targeted run `33355841283` reached C# compile and failed only because test fixture lambdas captured `out` parameters (`CS1628`); production behavior was not implicated.
- [x] Fixed fixture with local values before assigning `out` parameters; no repeated behavioral symptom.
- [x] Exact source SHA `492133648d2f278e23bdfd501d8fb391d948a569`: run `33355968467` completed green, including focused `SecretCluePlannerTests` and repository-derived automatic module validation.
- [x] Later module-player attempt reached compile and exposed a validation-fixture API misuse (`WorldBlueprintBuilder.RequireSite` did not exist); fixed by using the public `Region(...).Site(...)` authoring API rather than widening internals.
- [x] Exact source SHA `d3de5b1fe3a5cf2b43b01ebff41cee41ff071242`: run `33360442372` completed green with 5 focused `SecretDiscoveryPlannerTests`, automatic `worldbuilder` + `kentridge-integration` module validation, and both real-player builds/runs. Full-resolution WorldBuilder screenshots exposed a magenta/error-material defect in the validation tableau.
- [x] Fix the demonstrated validation-scene material defect with explicit supported `Sprites/Default` material and an availability assertion; no production planner behavior changed.
- [x] Exact latest feature SHA `6f68a84ecbb5c2e081c3ab666a19a7903161a347`: run `33361608731` completed green through 5 focused tests, automatic module validation, Kentridge real-player validation, dedicated WorldBuilder real-player build/run, screenshot capture, artifact upload, and final `ci/single-test` status.

## Built-player / representative acceptance — blocked on canonical integration

- [x] Dedicated module-owned WorldBuilder validation scene builds and runs as a standalone player (30 s) without runtime exceptions; ready log reports `clues=2 channels=2 routes=2 deterministic=true bypassRejected=true markerShader=Sprites/Default`.
- [x] Full-resolution dedicated WorldBuilder validation captures inspected after the shader fix: dark-green approach surface, cyan hidden-volume marker, orange route markers, and yellow clue markers render correctly; no magenta/error material remains.
- [ ] Architectural generated secret with interactable-backed route is realized through the accepted reusable interactable abstraction, not a WorldBuilder/showcase-local interaction state machine. **Blocked: owning interactables SceneIssue remains open on current master.**
- [ ] Hidden ruin/chamber representative route/clue realization is exercised against generated content. **Blocked in part by missing production secret geometry realization in `WorldBuilderVoxelCatalogue`.**
- [ ] Natural terrain/cave secret demonstrates traversal/environmental clues with no required interactable. **Planning semantics are covered; representative generated-content realization is not yet present.**
- [ ] Multiple legitimate routes register one stable secret with the canonical discovery authority. **Blocked: canonical discovery authority not yet verified on master.**
- [ ] Revisit/reload/repeated mechanism activation does not duplicate discovery credit or rewards. **Blocked: canonical discovery persistence/reward API not yet verified on master.**
- [ ] Exact built `WorldbuildingGalleryShowcase` reaches usable rendered state without runtime exceptions and exercises representative generated clues/routes per issue acceptance.
- [ ] Player follows intentional pre-solve evidence to the generated secret without universal glowing-secret markers or wall-spamming.
- [ ] Full-resolution `WorldbuildingGalleryShowcase` screenshots inspected for clue readability, route legibility, accidental voxel bypasses, placeholder/sign-like evidence, and obvious capture-specific geometry.

## Cost / blast radius / closure

- [x] Planner implementation is one-shot/event-driven; there is no `MonoBehaviour`, `Update`, polling loop, or per-frame world search in the new planning/discovery code.
- [x] Representative validation is bounded at 2 retained routes, 3 candidate clue anchors, 2 selected clues across 2 independent channels, plus one deliberately invalid protected-shell route used only for rejection evidence.
- [x] Feature diff against current master is limited to the WorldBuilder secret planning API/runtime, dedicated module validation fixture, focused tests, and this SceneIssue; no adjacent production system refactors are included.
- [ ] All required checkboxes and acceptance criteria green.
- [ ] Move SceneIssue directly `open -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master`, rerun/retry gates if SHA changes as required, then push exact feature head to `origin/master` non-force.
