# Plan — HouseShowcase socketed prop composition

## Acceptance
Create `HouseShowcase` as a built-player integration scene with a left-side house selector, house-specific prop multi-select, visible seed, and Regenerate action. The right side renders the chosen production house and selected furnishings through the real house/decor/socket/material/rendering path. The camera must support practical exterior and interior inspection. Same input seed must replay deterministically; Regenerate must choose a new seed and visibly vary the generated result while remaining valid.

## Ownership / architecture
Primary production ownership is expected under `Assets/Game/Structures`: `GuildHouseProgramCatalog`, house prototype/planning/authoring, decoration catalogs/resolvers, and socket placement. `HouseShowcase` is only a composition/UI consumer. Enumerate houses and applicable props from production semantic authorities; do not create showcase-owned identity tables. At minimum cover all `GuildHouseKind` values and any other production-generatable house archetypes with room/socket semantics. If a canonical enumeration boundary is missing, add the narrow shared production registry and prove an independent consumer can use it.

Selected props act as an allowed furnishing palette. Placement remains production-owned and must honor room role/context, socket/mount compatibility, clearance, bounds, circulation, and non-overlap. Report selected-but-unplaced items instead of forcing them. Reuse shared PropShowcase/browser and inspection-camera components if they exist by implementation time.

## Current hypotheses / discriminating work
1. Existing guild-house planning + furnished authoring already exposes enough semantic inputs to filter allowed archetypes and regenerate by seed; only a reusable selection constraint and showcase composition layer are needed.
2. The current furnished path may bake required/optional archetype choice too early, requiring a narrow shared furnishing-policy input and/or canonical house registry before the showcase can constrain selected props correctly.

First discriminate by tracing house prototype creation, seed ownership, room program resolution, and the decoration/socket resolver from `GuildHouseProgramCatalog` through `GuildHouseFurnishedPrototypeAuthoring`. Record whether selected-archetype filtering can be injected without scene-specific branching.

## Blast radius / cost
Avoid broad structure rewrites. Measure rebuild time and retained resource counts across repeated house switches/regenerations. Do not weaken rendering/world budgets. Any shared seed/palette extension must preserve existing consumers when not supplied.

## Validation gates
- Structures module-local EditMode/unit coverage and focused `Assets/Game/Structures/Validation/` built-player scene/scenario using production paths.
- Behavioral regressions for catalog parity, applicability filtering, deterministic replay, new-seed variation, socket/clearance validity, and cleanup.
- `HouseShowcase` built-player scenario with durable exterior + interior captures from at least two house kinds and a regeneration comparison.
- Exact-SHA targeted CI, then normal SceneIssue closure and PR `affected` gate.
