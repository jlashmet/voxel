# Procedural Architecture Foundation

This document describes how to extend the reusable procedural-architecture foundation introduced for HouseShowcase without moving scene, town, guild, or renderer policy into shared APIs.

## Ownership

- `Assets/Game/Structures/Api/ArchitectureModel.cs` owns semantic request/result contracts: structure class, profile/provider descriptors, supported parameter flags, deterministic identities, realization summaries, and review metadata shapes.
- `Assets/Game/Structures/Runtime/ArchitectureRegistry.cs` owns runtime registration and discovery. Callers select semantic profile/provider/archetype keys; they do not switch on guild enums, town names, material ids, or showcase modes.
- Each `IArchitectureProvider` owns validation and translation from the generic request into its existing production planner/authoring path. Unsupported parameters must be rejected explicitly rather than silently ignored or clamped.
- Production geometry remains in owning structure implementations. A provider should adapt existing planners/authoring rather than duplicate shell, roof, opening, stair, foundation, or interior algorithms.
- Material identity, texture presentation, signed-distance reconstruction, collision occupancy, and renderer lifecycle remain in their existing production modules. Architecture code selects semantic production materials/primitives; it does not introduce a showcase renderer.
- Composition owns place-specific choices. Town themes, guild/town mappings, exact camera poses, reference images, capture names, and reviewer workflows belong in composition/validation code rather than shared architecture APIs.
- Decoration and furnishing are separate downstream systems. The architecture foundation ends at structural shell/openings/circulation/presentation handoff.

## Adding a provider

1. Implement `IArchitectureProvider` in the owning runtime module.
2. Give it a stable provider key and one or more stable archetype keys.
3. Publish an `ArchitectureProviderDescriptor` with the semantic structure class, exact supported parameter flags, and production presentation capabilities.
4. Validate every supplied parameter. Reject unsupported or out-of-range values with a useful error; do not silently accept a no-op setting.
5. Make `TryPlan` deterministic from the request semantics. Same request inputs must produce the same request and realization identities.
6. Make `TryAuthor` call the owning production authoring path. If a capability such as signed-distance presentation is advertised, require the authoring capability needed to guarantee it rather than falling back silently.
7. Register the provider with `ArchitectureRegistry` from owning runtime registration code.
8. Add focused tests for determinism, multiple seed realizations, invalid parameter rejection, registration/reuse, and the production presentation invariants the provider advertises.
9. Add a focused `<Module>/Validation/` built-player scene and sibling `.player-scenario.json` that proves the production path independently of HouseShowcase.

## Adding a style profile

Register an `ArchitectureStyleProfileDescriptor` with a stable key, display name, and semantic tags. A style profile is discovery/composition metadata; it must not encode material ids or scene-specific generation branches into the shared contract. Providers may interpret semantic profile keys through their existing policy/adapters, but the shared registry stays data-driven.

## HouseShowcase reviewer contract

`HouseShowcase` is a generic consumer of `ArchitectureRegistry` and `IArchitectureProvider`. It may expose profile/provider/archetype selection, direct seed entry, supported parameter controls, deterministic regeneration, exterior/interior framing, and free-fly inspection. It must not regain guild-specific generation switches or furnishing/decor ownership.

Canonical visual review is registered through `ArchitectureReviewRegistry`. Reference images are framing/quality evidence, not pixel-similarity acceptance. Durable capture metadata must record review/reference/pose keys, semantic request inputs, seed/parameters, deterministic identities, and the capture name so a reviewer can reproduce the exact comparison.

## Validation baseline

For changes to this foundation, run the focused architecture tests and both architecture built-player validation scenes, then run the established Kentridge regression:

- `Assets/Game/Structures/Validation/ArchitectureFoundationRuntimeValidation.unity`
- `Assets/Game/Composition/Showcase/SceneRuntime/Validation/HouseShowcaseArchitectureValidation.unity`
- `Assets/Game/Composition/Kentridge/Playable/Validation/KentridgeEncounterRealizationValidation.unity`

Final evidence should include canonical exterior/interior views, the canonical reference comparison, three additional deterministic seed realizations, and at least one supported-configuration switch. Inspect the built-player captures visually before closure; passing log assertions alone is not sufficient for visual acceptance.

## Review checklist for future extensions

Before merging a new architecture provider or major profile family, verify that shared APIs remain semantic/configuration-driven; production geometry/material/SDF/texture/collision authority has not moved into Showcase; unsupported parameters fail explicitly; same inputs are deterministic; different seeds can produce meaningful structural variation; interiors/openings/circulation remain traversable from authoritative occupancy; validation scenes own their lifecycle and cleanup; and no unbounded per-regeneration caches, registrations, render resources, or magic scene identifiers were introduced.
