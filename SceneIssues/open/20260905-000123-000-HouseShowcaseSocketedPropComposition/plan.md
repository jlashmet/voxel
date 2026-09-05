# Plan — HouseShowcase socketed prop composition

## Acceptance
Create `HouseShowcase` as a built-player integration scene with a left-side production house selector, house-specific optional prop multi-select, visible seed, Regenerate, and practical exterior/interior inspection. The preview must use the real structure/decor/socket/material/voxel/rendering path. Same house + palette + seed is deterministic; Regenerate preserves house/palette, changes seed, and produces a different valid generated result where production generation supports variation.

## Observed ownership / baseline
Implementation base is `51797c954490425964e602d6bb2252a0d7a7c5aa`. `GuildHouseProgramCatalog` owns all ten production `GuildHouseKind` values. Seed flows through `GuildHousePrototypeComposition` into `GuildHouseSpatialPlanner` (`worldSeed ^ structureId`) and affects room selection/topology, so structural variation is production-owned.

Decoration identity is stable across bootstrap/expansion catalogs; canonical recipes own family, mount, accepted sockets, size, clearance and backend. Generated `GuildHouseRoomComposition` already carries each selected room program's `RequiredArchetypes` and `OptionalArchetypes`. `DecorationPlacementResolver` owns socket acceptance, bounds, exclusions and non-overlap.

No landed `PropShowcase` catalog browser or shared free-orbit/inspection camera was found. `Game.Structures` has no module-local `Validation/`, so this feature must add one. `HouseShowcase` remains an integration/UI consumer; Structures owns semantic queries, palette and placement.

## Hypothesis result / selected fix
Filtering final placements is too late. The canonical query slice (`fe2bc7e3e3ec04358890db569ffc2b79b94e8cbe`) added house enumeration, semantic metadata, normalized decoration identity and required/optional applicability; exact-SHA request `b5e62dfb25a9cd428cf97ad227d563787732e23e` passed.

A second production finding changed the palette implementation detail: the legacy `GuildHouseRoomDecorationResolver` maps guild/room roles to fixed decoration scenes, and those scenes do not necessarily schedule every archetype advertised by the house program. Therefore palette-aware composition will consume the generated room's production `RequiredArchetypes`/`OptionalArchetypes` directly, resolve descriptors through `DecorationCanonicalCatalog`, and delegate placement to `RectangularDecorationSpaceAnalyzer` + `DecorationPlacementResolver`. Required room fixtures remain mandatory; optional identities are filtered before placement. The legacy no-palette resolver remains unchanged for existing consumers.

Selected optional identities are attempted deterministically across generated compatible rooms. If no generated room contains the identity, report `RoomUnavailable`; if compatible generated rooms exist but valid placement cannot be found, report `NoValidPlacement`. Never force overlap or fallback coordinates.

## Blast radius / validation gates
Keep changes within Structures semantic/runtime/tests/validation plus the HouseShowcase integration consumer and only the minimum reusable voxel-composition hook if required. Measure repeated rebuild/resource counts; do not weaken budgets. Current durable checklist commit after the green query slice is `bc26fb22fa385d7b38c0d558a1e4e7b1d40dd5b8`.

- Structures regressions: catalog parity/applicability, palette correctness, same-seed determinism, new-seed structural variation, socket/clearance/non-overlap, cleanup.
- New `Assets/Game/Structures/Validation/` built-player scene/scenario using production authoring/rendering.
- `HouseShowcase` built-player evidence: two materially different house kinds, differing prop lists, multi-select, exterior/interior inspection, regeneration before/after.
- Final exact-SHA targeted CI, direct `open/`→`closed/`, current-master merge, PR + auto-merge `affected` gate.
