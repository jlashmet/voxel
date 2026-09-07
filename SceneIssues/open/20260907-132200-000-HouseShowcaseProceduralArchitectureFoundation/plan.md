# HouseShowcase procedural architecture foundation

## Observed state and acceptance

Current source `d89495bcc` already uses real voxel Storage and production Rendering. `HouseShowcase` consumes `GuildHousePrototypeComposition`, while `Game.Structures` already owns semantic guild programs, deterministic spatial planning, production voxel authoring, region-driven materials, roofs/openings/foundations, and a module-local guild-house validation scene. The missing foundation is a reusable architecture registration/config/review contract: HouseShowcase currently selects guild kinds and fixed scene configuration directly rather than consuming provider/profile metadata that later town work can extend.

Acceptance is the full issue note: semantic profile + provider/archetype + seed + explicit reusable parameters; deterministic regeneration and meaningful seed variation; production voxel/material/texture/SDF-compatible realization with traversal/collision; HouseShowcase exterior/interior review controls; data-driven references and deterministic review poses; module-local built-player validation; four valid seeds; direct image review; Kentridge integration; concise extension documentation. Furniture/final town art/world placement remain out of scope.

## Ownership and selected approach

`Assets/Game/Structures` owns shared architecture contracts, provider registry, deterministic generation descriptors/results, and guild-house provider adaptation. It is runtime/player-visible and must update its own `Validation/` production scene/scenario plus EditMode tests. `Assets/Game/Composition/Showcase/SceneRuntime` owns HouseShowcase selection, regeneration, review poses, interior inspection, reference-comparison presentation, and integration metadata; it must add/update a Showcase-local validation scene/scenario. `Assets/Scenes/HouseShowcase` remains integration only.

H1: a new architecture engine is required. H2: existing guild-house production generation is reusable if exposed behind semantic provider/config contracts. Inspection supports H2: `GuildHouseSpatialPlanner` and `GuildHousePrototypeAuthoring` already own production topology/realization. Select the narrow extension: add generic contracts/registry and an adapter/provider over existing guild generation; do not fork geometry, materials, Storage, rendering, or collision.

## Blast radius and remaining gates

Preserve deterministic integer occupancy and existing budgets. Shared contracts must not contain HouseShowcase coordinates, town names, or magic material IDs. Provider-specific parameters may map semantically to existing generators; unrelated archetypes can later use different providers.

Remaining gates: focused same-input/variation/reuse/parameter tests; module-local Structures and Showcase built-player validation; HouseShowcase four-seed exterior/interior/reference captures and direct visual inspection; material/texture/SDF support metadata proof through production paths; canonical Kentridge standalone; exact-SHA targeted CI; final diff/ownership/budget review; then open→closed bookkeeping and PR+auto-merge.