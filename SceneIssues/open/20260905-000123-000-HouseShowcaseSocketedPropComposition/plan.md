# Plan — HouseShowcase socketed prop composition

## Acceptance
Create `HouseShowcase` as a built-player integration scene with a left-side production house selector, house-specific optional prop multi-select, visible seed, Regenerate, and practical exterior/interior inspection. The preview must use the real structure/decor/socket/material/voxel/rendering path. Same house + palette + seed is deterministic; Regenerate preserves house/palette, changes seed, and produces a different valid generated result where production generation supports variation.

## Observed ownership / baseline
Implementation base is `51797c954490425964e602d6bb2252a0d7a7c5aa`. `GuildHouseProgramCatalog` currently owns the complete production guild-house set: all ten `GuildHouseKind` values. No other equivalent production house archetype with the same semantic room/socket authoring boundary was found. Seed already flows through `GuildHousePrototypeComposition` into `GuildHouseSpatialPlanner` (`worldSeed ^ structureId`) and affects room selection/topology, so structural variation is production-owned.

Decoration identity is already stable across the bootstrap and expansion catalogs; their recipes own family, mount, accepted sockets, desired cells, clearance and backend. `DecorationSceneScheduler` selects required/optional semantic slots before `DecorationPlacementResolver`, which then enforces production socket acceptance, bounds, exclusions and non-overlap. `GuildHouseFurnishedPrototypeAuthoring` currently resolves complete room scenes with no furnishing palette input.

No landed `PropShowcase` catalog browser or shared free-orbit/inspection camera was found. `Game.Structures` currently has no module-local `Validation/`, so this feature must add one. `HouseShowcase` remains a separate integration/UI consumer; Structures owns semantic queries/palette/placement.

## Hypothesis result / selected fix
Hypothesis 2 is confirmed: filtering final placements is too late because deselected props could already consume optional scheduling budget or block selected props. Add a narrow reusable read-only house/prop query surface over existing programs/catalog recipes and a furnishing palette applied to **optional semantic slots before scheduling**. Required/integrated slots remain non-deselectable. Preserve existing behavior when no palette is supplied. Reuse the existing scheduler and placement resolver; do not add a catalog, solver, fallback coordinates, or renderer. Selected-but-unplaced reporting comes from selected applicable identities versus production placements, with deterministic reasons for absent room/capacity/clearance.

For rendering, reuse the shipped voxel composition path used by the Kentridge playable slice (`VoxelEngine.Showcase`/normal rendering composition) rather than primitives or a parallel renderer. Expose only the smallest reusable realization hook needed by the showcase/Structures validation.

## Blast radius / validation gates
Keep changes within Structures semantic/runtime/tests/validation plus the HouseShowcase integration consumer and only the minimum reusable voxel-composition hook if required. Measure repeated rebuild/resource counts; do not weaken budgets.

- Structures EditMode regressions: catalog parity/applicability, palette correctness, same-seed determinism, new-seed structural variation, socket/clearance/non-overlap, cleanup.
- New `Assets/Game/Structures/Validation/` built-player scene/scenario using production authoring/rendering.
- `HouseShowcase` built-player evidence: two materially different house kinds, differing prop lists, multi-select, exterior/interior inspection, regeneration before/after.
- Exact-SHA targeted CI, direct `open/`→`closed/`, current-master merge, PR + auto-merge `affected` gate.
