# WorldBuilder Procedural Decoration Tasks

Branch: `agent/worldbuilding-decorations`
Base: `agent/worldbuilding-structures-caves`
Plan: `docs/WORLDBUILDER_DECORATION_SYSTEM_PLAN.md`

This checklist is the source of truth for implementation progress. Mark each item complete in the same change that completes the work whenever practical.

## Setup and architecture

- [x] **DEC001** Create `agent/worldbuilding-decorations` from `agent/worldbuilding-structures-caves`.
- [x] **DEC002** Inspect current `Game.Structures.Api`, `Game.Structures.Runtime`, and `Game.WorldBuilder` ownership boundaries.
- [x] **DEC003** Document the procedural decoration architecture and implementation order.
- [x] **DEC004** Create this tracked task list and establish the rule that completed implementation work is checked off here.

## Foundation API

- [ ] **DEC010** Define backend-independent decoration context/value types: structure/space kind, style, wealth, condition, environment tags, deterministic seed inputs.
- [ ] **DEC011** Define semantic placement socket kinds and socket data (floor, wall, corner, ceiling, anchor-relative sockets).
- [ ] **DEC012** Define prop recipe/descriptor vocabulary: family, footprint, clearance, supported sockets, orientation, backend, interaction flags.
- [ ] **DEC013** Define resolved `DecorationPlacement` output with stable semantic identity and anchor relationship data.
- [ ] **DEC014** Define decoration scene recipe/slot vocabulary for required, optional, weighted, and dependent props.
- [ ] **DEC015** Add validation rules for invalid dimensions, unsupported sockets, impossible clearances, and invalid scene dependency graphs.
- [ ] **DEC016** Add focused API tests for the foundation value types and validation rules.

## Determinism and identity

- [ ] **DEC020** Implement deterministic seed derivation for structure -> space -> scene -> prop slot.
- [ ] **DEC021** Implement stable generated prop IDs that do not depend on runtime iteration order.
- [ ] **DEC022** Add tests proving identical context/seed produces identical placements/IDs and controlled seed changes produce variation.

## Space analysis and placement

- [ ] **DEC030** Define `DecorationSpace` with usable bounds, surfaces, sockets, and exclusion regions.
- [ ] **DEC031** Implement rectangular/interior room floor socket extraction.
- [ ] **DEC032** Implement wall socket extraction with usable span, facing, and height constraints.
- [ ] **DEC033** Implement corner and ceiling socket extraction.
- [ ] **DEC034** Represent door, stair, navigation, gameplay, and hazard exclusion regions.
- [ ] **DEC035** Implement placement collision/clearance checks and deterministic candidate ordering.
- [ ] **DEC036** Add tests that placements remain supported, in bounds, non-overlapping where required, and outside exclusions.

## First prop families

- [ ] **DEC040** Implement parameterized bed recipe/authoring data.
- [ ] **DEC041** Implement parameterized dresser/cabinet recipe/authoring data.
- [ ] **DEC042** Implement rug/carpet/runner recipe/authoring data with thin-surface intent.
- [ ] **DEC043** Implement painting/frame/tapestry recipe/authoring data with wall-surface intent.
- [ ] **DEC044** Implement wall torch recipe/authoring data with light/emissive hook intent.
- [ ] **DEC045** Add deterministic variation tests across prop dimensions/style/material parameters.

## Bedroom vertical slice

- [ ] **DEC050** Implement `BedroomScene` with bed as the primary anchor.
- [ ] **DEC051** Place rug relative to the bed rather than independently.
- [ ] **DEC052** Place dresser against a compatible secondary wall with standing clearance.
- [ ] **DEC053** Place painting above a compatible wall/furniture region.
- [ ] **DEC054** Place wall torch using wall socket and spacing rules.
- [ ] **DEC055** Add scene tests for required/optional slot resolution and dependency order.
- [ ] **DEC056** Verify multiple seeds remain coherent and deterministic without leaving room bounds.

## Castle integration

- [ ] **DEC060** Expose one castle bedroom/interior as a semantic `DecorationSpace` rather than hard-coded prop placement.
- [ ] **DEC061** Run the bedroom scene through castle authoring/build output.
- [ ] **DEC062** Add castle integration tests proving the first five props resolve in a representative room.
- [ ] **DEC063** Add debug/look-dev visibility for decoration sockets, exclusions, anchor relationships, and resolved placements.

## Style, wealth, and condition

- [ ] **DEC070** Add style/culture profiles that influence materials, silhouettes, and optional detail.
- [ ] **DEC071** Add wealth tiers that influence prop quality, count, ornamentation, and scene density.
- [ ] **DEC072** Add condition/damage tiers for maintained, worn, abandoned, and ruined spaces.
- [ ] **DEC073** Add tests proving context changes variation without breaking placement invariants.

## Cave reuse

- [ ] **DEC080** Add cave-space adapter for walkable floor patches.
- [ ] **DEC081** Derive cave wall, ceiling, alcove, and ledge placement candidates.
- [ ] **DEC082** Add cave hazard/exclusion support.
- [ ] **DEC083** Implement `CaveCampScene` using the same scene/placement abstractions.
- [ ] **DEC084** Prove castle and cave decoration use the same core resolver in tests.

## Render/build backends

- [ ] **DEC090** Define backend dispatch contract between semantic placements and visual/build output.
- [ ] **DEC091** Implement box-assembly backend for furniture-scale props.
- [ ] **DEC092** Implement thin-surface backend for rugs, paintings, banners, and maps.
- [ ] **DEC093** Add voxel/structure stamp backend where world-integrated geometry is appropriate.
- [ ] **DEC094** Add procedural-mesh hook for detail that should not be voxelized.
- [ ] **DEC095** Add optional light/emissive/particle hooks for torches, candles, lamps, chandeliers, fires, and magic props.

## Runtime scale and persistence

- [ ] **DEC100** Separate semantic/interactable prop metadata from static render geometry.
- [ ] **DEC101** Add batching/static-combination path so distant static decorations do not require one heavyweight runtime object each.
- [ ] **DEC102** Add distance/detail policy for tiny clutter and secondary decoration.
- [ ] **DEC103** Define persistence delta contract keyed by deterministic prop ID.
- [ ] **DEC104** Add tests for destroyed/looted/moved state overriding deterministic regenerated baseline.

## Content expansion

- [ ] **DEC110** Add table/chair/bench families and dining scene.
- [ ] **DEC111** Add chest/shelf/bookcase/storage families.
- [ ] **DEC112** Add fireplace/candle/chandelier/standing-lamp families.
- [ ] **DEC113** Add banners/curtains/shields/weapons/armor display families.
- [ ] **DEC114** Add books, pottery, food, tools, containers, and tabletop clutter.
- [ ] **DEC115** Add guard post, kitchen, dining hall, library/study, chapel/shrine, barracks, throne room, cellar, and storage scenes.
- [ ] **DEC116** Add natural cave families: stones, roots, mushrooms, crystals, bones, puddles, formations.
- [ ] **DEC117** Add mine/occupied-cave families: supports, rails/carts, ropes, lanterns, crates, tools, ladders.

## Completion gates

- [ ] **DEC120** First castle bedroom milestone meets the plan definition of success.
- [ ] **DEC121** Cave reuse milestone proves no castle-specific dependency in core decoration resolution.
- [ ] **DEC122** Performance pass demonstrates decoration density scales to large structures/world regions without per-prop heavyweight runtime overhead.
- [ ] **DEC123** Update architecture/runtime documentation with final integration contracts and examples.
