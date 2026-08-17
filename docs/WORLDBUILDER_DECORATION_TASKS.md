# WorldBuilder Procedural Decoration Tasks

Branch: `agent/worldbuilding-decorations`
Base: `agent/worldbuilding-structures-caves`
Plan: `docs/WORLDBUILDER_DECORATION_SYSTEM_PLAN.md`

This checklist is the source of truth for implementation progress. Mark each item complete in the same change that completes the work whenever practical.

## Current implementation notes

- The generic semantic resolver and the first five bedroom prop families are implemented.
- `DecorationSceneScheduler` provides deterministic required/optional slot selection, weighted optional priority, dependency closure, and anchor-before-dependent ordering.
- Decoration context has explicit style-family IDs plus game-owned presentation profiles. Style changes material/silhouette policy, wealth changes ornamentation and optional scene density, and condition changes damage/material/light treatment without moving those game presentation concerns into the semantic API.
- Noble/courtly bedrooms may deterministically add an optional accent torch after the required five-prop baseline; optional placement failure never invalidates the required scene.
- A castle-bedchamber adapter derives semantic space plus stair/navigation, window, fireplace, sitting-area, and retained wall-hanging exclusions from `CastlePlan` and resolves the generic bedroom scene.
- Castle floor 1 routes through `CastleProceduralBedroomAuthoring`; the required bed, rug, dresser, painting, and wall torch are procedural while existing beams, fireplace, sitting cluster, wall hangings, and chandelier remain until their scene recipes are migrated.
- DEC110 adds reusable table/chair/bench families plus a relational dining scene with table anchor, required opposed benches, and wealth-driven optional head chairs.
- Castle floor 0 now routes through `CastleProceduralGreatHallAuthoring`: the dining furniture is procedural while the legacy beams, fireplace, throne, chandelier, wall hangings, place settings, lamps, and seeded side details remain. Undersized/custom keeps that cannot satisfy dining clearances fall back to the complete legacy floor-0 authoring.
- DEC111 adds reusable chest, shelf, bookcase, crate, and barrel recipes plus a shared box-assembly authoring backend. Container families carry deterministic IDs and persistence-relevant container/lootable/movable/destructible flags; storage room composition remains owned by DEC115.
- `CastleBedroomDecorationDebugGizmo` provides selected Scene-view look-dev visibility for room bounds, sockets/facings, exclusions, resolved placements, labels, and anchor relationships.
- Cave reuse is implemented through an explicit `CaveWalkablePatch` contract. The cave adapter emits walkable-floor/wall/ceiling candidates plus alcove/ledge candidates and navigation/hazard exclusions without guessing private cave-network turn state.
- `CaveCampScene` resolves campfire, bedroll, and lantern placements through the same `DecorationSceneScheduler` and `DecorationPlacementResolver` used by castle rooms. Cross-adapter tests feed both castle and cave sockets directly to the core resolver.
- Render/build outputs are separated by intent: box assemblies for voxel furniture, aggregated true thin-surface quads for rugs/wall art, voxel stamps for world-integrated details such as campfires, data-only procedural-mesh requests, and condition-aware light/particle hooks. The old one-voxel thin-surface structure path remains only as a compatibility fallback for the current castle build.
- Runtime planning separates interaction/persistence metadata from render batches, isolates movable props, combines static props by backend rather than by prop, and applies a distance/detail policy before secondary clutter is materialized. A 1,000-prop regression verifies static batch count stays bounded by backend count.
- Persistence overlays are keyed by deterministic `GeneratedPropId`; moved, looted, and destroyed deltas reapply to a regenerated baseline without perturbing untouched props.
- NUnit regressions are committed for scheduling, context variation, cave reuse, render backends, runtime batching/detail policy, persistence deltas, dining semantics/integration including a 64-seed great-hall pass, storage-family determinism/placement, a 128-seed bedroom stress pass, representative castle integration, and the existing castle build progression path. They still require execution in the Unity test environment/CI.

## Setup and architecture

- [x] **DEC001** Create `agent/worldbuilding-decorations` from `agent/worldbuilding-structures-caves`.
- [x] **DEC002** Inspect current `Game.Structures.Api`, `Game.Structures.Runtime`, and `Game.WorldBuilder` ownership boundaries.
- [x] **DEC003** Document the procedural decoration architecture and implementation order.
- [x] **DEC004** Create this tracked task list and establish the rule that completed implementation work is checked off here.

## Foundation API

- [x] **DEC010** Define backend-independent decoration context/value types: structure/space kind, style, wealth, condition, environment tags, deterministic seed inputs.
- [x] **DEC011** Define semantic placement socket kinds and socket data (floor, wall, corner, ceiling, anchor-relative sockets).
- [x] **DEC012** Define prop recipe/descriptor vocabulary: family, footprint, clearance, supported sockets, orientation, backend, interaction flags.
- [x] **DEC013** Define resolved `DecorationPlacement` output with stable semantic identity and anchor relationship data.
- [x] **DEC014** Define decoration scene recipe/slot vocabulary for required, optional, weighted, and dependent props.
- [x] **DEC015** Add validation rules for invalid dimensions, unsupported sockets, impossible clearances, and invalid scene dependency graphs.
- [x] **DEC016** Add focused API tests for the foundation value types and validation rules.

## Determinism and identity

- [x] **DEC020** Implement deterministic seed derivation for structure -> space -> scene -> prop slot.
- [x] **DEC021** Implement stable generated prop IDs that do not depend on runtime iteration order.
- [x] **DEC022** Add tests proving identical context/seed produces identical placements/IDs and controlled seed changes produce variation.

## Space analysis and placement

- [x] **DEC030** Define `DecorationSpace` plus associated semantic socket and exclusion data while keeping the base space value-oriented.
- [x] **DEC031** Implement rectangular/interior room floor socket extraction.
- [x] **DEC032** Implement wall socket extraction with usable span, facing, and height constraints.
- [x] **DEC033** Implement corner and ceiling socket extraction.
- [x] **DEC034** Represent door, stair, navigation, gameplay, and hazard exclusion regions.
- [x] **DEC035** Implement placement collision/clearance checks and deterministic candidate ordering.
- [x] **DEC036** Add tests that placements remain supported, in bounds, non-overlapping where required, and outside exclusions.

## First prop families

- [x] **DEC040** Implement parameterized bed recipe/authoring data.
- [x] **DEC041** Implement parameterized dresser/cabinet recipe/authoring data.
- [x] **DEC042** Implement rug/carpet/runner recipe/authoring data with thin-surface intent.
- [x] **DEC043** Implement painting/frame/tapestry recipe/authoring data with wall-surface intent.
- [x] **DEC044** Implement wall torch recipe/authoring data with light/emissive hook intent.
- [x] **DEC045** Add deterministic variation tests across prop dimensions/style/material parameters.

## Bedroom vertical slice

- [x] **DEC050** Implement `BedroomScene` with bed as the primary anchor.
- [x] **DEC051** Place rug relative to the bed rather than independently.
- [x] **DEC052** Place dresser against a compatible secondary wall with standing clearance.
- [x] **DEC053** Place painting above a compatible wall/furniture region.
- [x] **DEC054** Place wall torch using wall socket and spacing rules.
- [x] **DEC055** Add scene tests for required/optional slot resolution and dependency order.
- [x] **DEC056** Verify multiple seeds remain coherent and deterministic without leaving room bounds.

## Castle integration

- [x] **DEC060** Expose one castle bedroom/interior as a semantic `DecorationSpace` rather than hard-coded prop placement.
- [x] **DEC061** Run the bedroom scene through castle authoring/build output.
- [x] **DEC062** Add castle integration tests proving the first five props resolve in a representative room.
- [x] **DEC063** Add debug/look-dev visibility for decoration sockets, exclusions, anchor relationships, and resolved placements.

## Style, wealth, and condition

- [x] **DEC070** Add style/culture profiles that influence materials, silhouettes, and optional detail.
- [x] **DEC071** Add wealth tiers that influence prop quality, count, ornamentation, and scene density.
- [x] **DEC072** Add condition/damage tiers for maintained, worn, abandoned, and ruined spaces.
- [x] **DEC073** Add tests proving context changes variation without breaking placement invariants.

## Cave reuse

- [x] **DEC080** Add cave-space adapter for walkable floor patches.
- [x] **DEC081** Derive cave wall, ceiling, alcove, and ledge placement candidates.
- [x] **DEC082** Add cave hazard/exclusion support.
- [x] **DEC083** Implement `CaveCampScene` using the same scene/placement abstractions.
- [x] **DEC084** Prove castle and cave decoration use the same core resolver in tests.

## Render/build backends

- [x] **DEC090** Define backend dispatch contract between semantic placements and visual/build output.
- [x] **DEC091** Implement box-assembly backend for furniture-scale props.
- [x] **DEC092** Implement true thin-surface backend for rugs, paintings, banners, and maps; aggregated presentation quads no longer require a full voxel slab.
- [x] **DEC093** Add voxel/structure stamp backend where world-integrated geometry is appropriate.
- [x] **DEC094** Add procedural-mesh hook for detail that should not be voxelized.
- [x] **DEC095** Add optional light/emissive/particle hooks for torches, candles, lamps, chandeliers, fires, and magic props.

## Runtime scale and persistence

- [x] **DEC100** Separate semantic/interactable prop metadata from static render geometry.
- [x] **DEC101** Add batching/static-combination path so distant static decorations do not require one heavyweight runtime object each.
- [x] **DEC102** Add distance/detail policy for tiny clutter and secondary decoration.
- [x] **DEC103** Define persistence delta contract keyed by deterministic prop ID.
- [x] **DEC104** Add tests for destroyed/looted/moved state overriding deterministic regenerated baseline.

## Content expansion

- [x] **DEC110** Add table/chair/bench families and dining scene.
- [x] **DEC111** Add chest/shelf/bookcase/storage families.
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
