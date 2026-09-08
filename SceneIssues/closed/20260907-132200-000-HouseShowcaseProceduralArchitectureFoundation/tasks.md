# Execution checklist

Work the next unchecked non-blocked item. Add work only for acceptance, correctness/regression, reuse boundaries, or demonstrated visual defects.

## 1. Shared architecture contract
- [x] Define semantic style/profile, provider/archetype, seed, and explicit reusable generation-parameter contracts in `Game.Structures` without scene coordinates, town names, or raw material IDs.
- [x] Define provider registration/discovery so later town issues add profiles/providers without editing HouseShowcase hard-coded selection logic; allow different providers for residence/shop/inn/church/warehouse/civic/castle classes.
- [x] Define deterministic generation identity/result metadata sufficient to prove same-input reproduction and identify realized bounds/storeys/openings/circulation plus production presentation capabilities.
- [x] Add a data-driven canonical review contract: reference-image descriptors, deterministic exterior/interior camera poses, profile/provider/seed/parameter identity, and durable comparison metadata. Raw pixel similarity is not an oracle.

## 2. Existing production provider
- [x] Adapt the existing guild-house production path as the first architecture provider rather than duplicating its spatial planner/authoring.
- [x] Expose demonstrated parameters covering footprint/proportions, storeys/floor height where supported, shell/roof/gable/opening/foundation/trim/detail semantics; reject unsupported combinations explicitly rather than silently changing intent.
- [x] Preserve region/profile-driven production materials/textures and expose presentation/SDF capability metadata needed by future curved providers without creating a second material/SDF authority.
- [x] Prove same profile+provider+seed+parameters yields identical authoritative plan/output identity; prove at least four seeds create nontrivial structural variation while retaining valid scale/openings/floors/stairs/interiors where applicable.
- [x] Add production-path behavioral regressions for provider registry reuse, deterministic identity, seed variation, parameter validation, topology/circulation, opening/floor/storey/stair invariants, and no scene-specific policy leakage.

## 3. Game.Structures module-local validation
- [x] Update/create a focused `Assets/Game/Structures/Validation/` scene and `*.player-scenario.json` using real Storage/structure authoring/presentation paths to exercise architecture registration, regeneration, four seeds, multi-storey/interior traversal, and production material/rendering behavior.
- [x] Inspect its built-player captures directly; fix any demonstrated missing structure parts, bad scale/support/contact, unusable openings/stairs/circulation, or non-production presentation before passing.

## 4. HouseShowcase consumer and reviewer UX
- [x] Refactor `HouseShowcase` to consume the shared profile/provider registry and generation parameters; scene composition may own placement but not architecture authority.
- [x] Provide reviewer selection/regeneration for profile, provider/archetype, seed, and supported parameters with deterministic state/log metadata.
- [x] Provide deterministic exterior review poses plus interior/player inspection sufficient to verify entrances, doors, windows, floors, stairs, storeys, ceiling heights, circulation, collision and traversal.
- [x] Integrate the shared reference/review contract and produce reviewer-friendly paired reference/runtime comparison metadata/artifacts without embedding final town art.
- [x] Update `Assets/Scenes/HouseShowcase.player-scenario.json` to exercise canonical configuration plus at least three additional seeds, configuration switching, exterior captures, interior inspection, and reference-comparison capture.

## 5. Showcase module-local validation and integration
- [x] Update/create a focused Showcase-owned validation scene under `Assets/Game/Composition/Showcase/.../Validation/` for registry-driven selection/regeneration/review behavior using the real production architecture path.
- [x] Pass affected module EditMode/unit tests and repository-derived module-local players on the exact feature SHA; zero-test/skipped/missing captures are failures.
- [x] Run exact-source standalone HouseShowcase at the required 1600x900 review configuration. Retain source SHA, profile/provider, seed/parameters, pose/reference metadata, logs and all captures.
- [x] Inspect every required HouseShowcase built-player image directly for silhouette/massing, roof form, proportions, openings, foundations, material/texture identity, scale, support/contact, interior usability, seams/missing geometry and production finish; add/fix any demonstrated acceptance defects.
- [x] Verify the canonical seed plus at least three additional seeds are meaningfully structurally different, correctly scaled and navigable, not cosmetic recolors/noise.

## 6. Reuse documentation, integration, closure readiness
- [x] Add a concise extension note explaining how a later town SceneIssue supplies a semantic style profile, provider/archetype, seed/parameters, materials/textures/SDF presentation data, and concept-reference review data without parallel architecture systems.
- [x] Run canonical standalone `KentridgePlayableSlice` and confirm no renderer/streaming/material/collision regression or duplicate architecture enabling path.
- [x] Review final diff, module ownership, deterministic integer-world boundary, device budgets and absence of scene/town/material magic policy.
- [x] Use `ci-test/fixes/agent-1` for required exact-SHA targeted CI; leave queued/running requests untouched and resolve any product failures on `fixes/agent-1`.
- [x] Complete all acceptance evidence and prepare the assigned issue for the required `open/` -> `closed/` bookkeeping. Per `SceneIssues/README.md`, current-master reconciliation, PR creation, auto-merge, the required `affected` gate, and confirmation on `origin/master` follow the closure bookkeeping and are not pre-claimed here.

## Verified evidence

- Exact feature source: `1efa11232cfbfee5a91fa1ae79c5319490ca0f72`.
- Exact targeted-CI transport: `b9d7fcb542b340853cf902cebda2cbca2a4b0e23`; workflow run `34178318979` completed successfully.
- Repository-derived module validation passed all selected EditMode/PlayMode assemblies and players, including `ArchitectureFoundationRuntimeValidation`, `HouseShowcaseArchitectureValidation`, and canonical `KentridgePlayableSlice` integration.
- Structures built-player evidence proves four distinct deterministic realization hashes/footprints, production Glass, signed-distance boundary samples, two storeys, one occupancy-backed stair flight, and a walkable entrance/interior volume.
- HouseShowcase built-player evidence proves canonical exterior/interior review, deterministic replay, three additional seed variants, explicit 3-storey configuration switching, semantic profile switching, and durable reference/runtime metadata.
- Direct screenshot inspection found complete supported shells/roofs/openings/foundations, readable production materials/textures, usable stair/interior circulation, meaningful massing variation, and no demonstrated seams or missing production geometry. The 30-second standalone replay's final transient frame occurs immediately after a rebuild; the corresponding longer HouseShowcase validation captures show the rebuilt variant correctly.