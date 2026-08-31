# Plan — WorldBuilder Secret Discovery Clue Generation

## Evidence / hypothesis discrimination

- No captures or marked-region overlays are present in this SceneIssue directory, so there is no image-localized defect to inspect before code.
- Hypothesis A — the secret-location solver itself is missing or non-deterministic: **disproved by source inspection**. `Game.WorldBuilder.Runtime.SecretPlanner` already resolves required and policy secrets from authoritative `SecretCandidate`s, reserves physical candidates, validates entrance topology, uses stable seeded selection, and fails closed when required candidates cannot resolve.
- Hypothesis B — a second secret-location/planning mechanism is needed for clue generation: **rejected**. Clues consume `SecretRef`, `SiteRef`, `ResolvedSecretPlan`, and existing site resolution rather than choosing hidden spaces independently.
- Hypothesis C — the missing foundation is a deterministic authored clue/route-readability layer tied to resolved secrets: **supported and implemented**. Stable route/clue IDs, semantic clue anchors, deterministic selection, readability policy, bypass-policy semantics, diagnostics, and focused regressions now exist on this branch.
- Hypothesis D — built-player inspect/read/discover realization can immediately use the generic interactable/discovery system: **disproved on current master**. `20260830-014314-000-ExplorationInteractablesSecretsShowcase` remains open and no verified canonical reusable interaction/discovery persistence authority is available. Do not duplicate those missing APIs.
- Hypothesis E — actual generated voxel bypass can be validated by scanning the existing WorldBuilder voxel output: **disproved by source tracing**. `WorldBuilderVoxelCatalogue.Build` currently accepts only `AuthoredTownPlan` and delegates to the Kentridge settlement backend; it has no `ResolvedSecretPlan`/route/clue realization input. The structures rasterizer produces real voxel occupancy but storage retains material/surface semantics, not secret-route provenance. There is therefore no production generated secret geometry against which to claim the requested shell/breakable evidence yet.
- Hypothesis F — the dedicated module validation scene itself was visually usable after first successful player execution: **disproved by full-resolution capture**. Default primitive materials rendered magenta under the player URP path. This was isolated to the validation tableau and fixed by explicitly using supported `Sprites/Default`; the exact-SHA rerun shows correct colors and no runtime exception.

## Implemented independent work

1. Added semantic route/clue planning contracts to `Game.WorldBuilder.Api`, reusing stable WorldBuilder identities rather than scene object references or capture coordinates.
2. Added `SecretCluePlanner` and `SecretDiscoveryPlanner` in `Game.WorldBuilder.Runtime`; both consume authoritative resolved secret/site/NPC data and do not select a second hidden destination.
3. Added explicit `ProtectedShell`, `AuthoredBreakablesOnly`, and `SystemicBypassAllowed` policy semantics plus deterministic diagnostics. Geometry-derived evidence remains an input seam until secret geometry realization exists.
4. Added the narrow event-driven `SecretDiscoveryLedger` seam only for deterministic planning/runtime-boundary regression; it does not become save/reward authority and must be replaced/bound when the canonical discovery API lands.
5. Added focused behavioral regressions for required/optional clue sources, NPC rumor capability, stable identity, same-seed determinism, deterministic alternate-source variation, Standard/Major readability, pre-solve observability, circular dependency rejection, route identity, bypass policy, and discovery capture/restore.
6. Added a module-owned standalone WorldBuilder validation scene rather than using a top-level gallery as the focused test fixture. The scene validates the public authoring API, deterministic replay, clue/channel counts, route identity, and protected-shell rejection.
7. Fixed two validation-harness compile defects without widening production internals, then fixed the demonstrated magenta validation material defect from full-resolution player evidence.
8. Latest exact feature SHA `6f68a84ecbb5c2e081c3ab666a19a7903161a347` passed targeted run `33361608731`: 5 focused `SecretDiscoveryPlannerTests`, automatic `worldbuilder` + `kentridge-integration` module validation, both real-player builds/runs, screenshots, artifact upload, and final commit status.
9. Full-resolution dedicated WorldBuilder player captures show the intended dark-green approach, cyan hidden volume, orange routes, and yellow clues; player log reports `clues=2 channels=2 routes=2 deterministic=true bypassRejected=true markerShader=Sprites/Default`.
10. Cost/blast radius is bounded: representative plan has 2 retained routes, 3 clue candidates, 2 selected clues/2 channels, no per-frame planner search, and the feature diff is confined to WorldBuilder secret planning, focused tests, module validation, and this SceneIssue.

## Remaining blocked acceptance

1. **Reusable interactable route realization:** wait for the canonical reusable interactable API owned by `20260830-014314-000-ExplorationInteractablesSecretsShowcase`; do not add a WorldBuilder-local mechanism state machine.
2. **Canonical discovery/reward persistence:** wait for the owning runtime discovery authority; then bind stable `SecretRef`/route metadata and prove revisit/reload/repeated activation idempotence.
3. **Generated secret voxel realization:** the existing `WorldBuilderVoxelCatalogue` has no secret-plan realization stage. Actual ProtectedShell/AuthoredBreakablesOnly geometry validation and representative ruin/cave secret voxels require that production composition path rather than a test-only parallel catalogue.
4. **Representative gallery acceptance:** once the above owners/realization exist, compose architectural, ruin/chamber, and natural traversal examples into `WorldbuildingGalleryShowcase`, run the exact built scene, follow intentional pre-solve evidence, and inspect full-resolution captures for readability, accidental bypass, placeholder language, and capture-specific geometry.

## Closure gates

- Keep the SceneIssue open while any acceptance item above is blocked; do not weaken acceptance because the planning layer and dedicated validation are green.
- After prerequisites land, merge current `origin/master` into `fixes/agent-5`, bind to those canonical APIs, add/extend regressions and representative generated content, and rerun exact-SHA targeted/module/player gates only through `ci-test/fixes/agent-5`.
- Only after every acceptance checkbox is evidenced: move this issue directly from `open` to `closed`, set `status=fixed` and `resolvedUtc`, merge the then-current `origin/master`, and push that exact feature head to `origin/master` non-force.
