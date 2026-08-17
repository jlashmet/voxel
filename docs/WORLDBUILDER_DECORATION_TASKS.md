# WorldBuilder Procedural Decoration Tasks

Branch: `agent/worldbuilding-decorations`
Base: `agent/worldbuilding-structures-caves`
Plan: `docs/WORLDBUILDER_DECORATION_SYSTEM_PLAN.md`
Large content catalog: `docs/WORLDBUILDER_DECORATION_CONTENT_CATALOG.md`

This checklist is the source of truth for implementation progress. Mark each item complete in the same change that completes the work whenever practical.

## Current implementation notes

- The generic semantic resolver, deterministic scene scheduler, stable prop IDs, socket/exclusion model, style/wealth/condition profiles, backend dispatch, runtime batching/detail policy, and persistence overlays are implemented.
- Castle bedroom and great-hall furniture have procedural integration paths; many other legacy castle details remain available for incremental migration.
- Reusable furniture/content currently includes beds, dressers, rugs, paintings, torches, tables, chairs, benches, storage, fireplace/candles/chandeliers/lanterns, textiles, martial displays, compact tabletop clutter, and utility-room scene recipes.
- Cave runtime content includes `CaveCampScene`, natural cave environmental families, and occupied/mine environmental families.
- The natural/mine runtime source is present, but dedicated `NaturalCaveDecorationTests.cs` and `MineCaveDecorationTests.cs` source files are not currently present on the branch. Do not treat earlier notes implying those test files were committed as validation evidence.
- Unity/CI execution and visual/performance evidence remain separate completion gates.
- Large-scale content expansion now uses a planned two-level identity model: existing coarse prop families remain behavior/placement classes while stable content-archetype identity distinguishes hundreds of actual objects.

## Setup and architecture

- [x] **DEC001** Create `agent/worldbuilding-decorations` from `agent/worldbuilding-structures-caves`.
- [x] **DEC002** Inspect current `Game.Structures.Api`, `Game.Structures.Runtime`, and `Game.WorldBuilder` ownership boundaries.
- [x] **DEC003** Document the procedural decoration architecture and implementation order.
- [x] **DEC004** Create this tracked task list and establish the rule that completed implementation work is checked off here.

## Foundation API

- [x] **DEC010** Define backend-independent decoration context/value types: structure/space kind, style, wealth, condition, environment tags, deterministic seed inputs.
- [x] **DEC011** Define semantic placement socket kinds and socket data.
- [x] **DEC012** Define prop recipe/descriptor vocabulary.
- [x] **DEC013** Define resolved `DecorationPlacement` output with stable semantic identity and anchor relationship data.
- [x] **DEC014** Define decoration scene recipe/slot vocabulary.
- [x] **DEC015** Add validation rules for invalid dimensions, unsupported sockets, impossible clearances, and invalid scene dependency graphs.
- [x] **DEC016** Add focused API tests for foundation value types and validation rules.

## Determinism and identity

- [x] **DEC020** Implement deterministic seed derivation for structure -> space -> scene -> prop slot.
- [x] **DEC021** Implement stable generated prop IDs that do not depend on runtime iteration order.
- [x] **DEC022** Add tests proving identical context/seed produces identical placements/IDs and controlled seed changes produce variation.

## Space analysis and placement

- [x] **DEC030** Define `DecorationSpace` plus associated semantic socket and exclusion data.
- [x] **DEC031** Implement rectangular/interior room floor socket extraction.
- [x] **DEC032** Implement wall socket extraction with usable span, facing, and height constraints.
- [x] **DEC033** Implement corner and ceiling socket extraction.
- [x] **DEC034** Represent door, stair, navigation, gameplay, and hazard exclusion regions.
- [x] **DEC035** Implement placement collision/clearance checks and deterministic candidate ordering.
- [x] **DEC036** Add placement invariant tests.

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

- [x] **DEC060** Expose one castle bedroom/interior as a semantic `DecorationSpace`.
- [x] **DEC061** Run the bedroom scene through castle authoring/build output.
- [x] **DEC062** Add castle integration tests proving the first five props resolve in a representative room.
- [x] **DEC063** Add debug/look-dev visibility for decoration sockets, exclusions, anchor relationships, and resolved placements.

## Style, wealth, and condition

- [x] **DEC070** Add style/culture profiles.
- [x] **DEC071** Add wealth tiers.
- [x] **DEC072** Add condition/damage tiers.
- [x] **DEC073** Add tests proving context changes variation without breaking placement invariants.

## Cave reuse

- [x] **DEC080** Add cave-space adapter for walkable floor patches.
- [x] **DEC081** Derive cave wall, ceiling, alcove, and ledge placement candidates.
- [x] **DEC082** Add cave hazard/exclusion support.
- [x] **DEC083** Implement `CaveCampScene` using the same scene/placement abstractions.
- [x] **DEC084** Prove castle and cave decoration feed the same core semantic concepts in source/tests.

## Render/build backends

- [x] **DEC090** Define backend dispatch contract.
- [x] **DEC091** Implement box-assembly backend.
- [x] **DEC092** Implement true thin-surface backend.
- [x] **DEC093** Add voxel/structure stamp backend.
- [x] **DEC094** Add procedural-mesh hook.
- [x] **DEC095** Add optional light/emissive/particle hooks.

## Runtime scale and persistence

- [x] **DEC100** Separate semantic/interactable metadata from static render geometry.
- [x] **DEC101** Add batching/static-combination path.
- [x] **DEC102** Add distance/detail policy.
- [x] **DEC103** Define persistence delta contract keyed by deterministic prop ID.
- [x] **DEC104** Add state-override tests for regenerated baselines.

## First content expansion

- [x] **DEC110** Add table/chair/bench families and dining scene.
- [x] **DEC111** Add chest/shelf/bookcase/storage families.
- [x] **DEC112** Add fireplace/candle/chandelier/standing-lamp families.
- [x] **DEC113** Add banners/curtains/shields/weapons/armor display families.
- [x] **DEC114** Add books, pottery, food, tools, containers, and tabletop clutter.
- [x] **DEC115** Add guard post, kitchen, dining hall, library/study, chapel/shrine, barracks, throne room, cellar, and storage scene source.
- [x] **DEC116** Add natural cave runtime families: stones, roots, mushrooms, crystals, bones, puddles, formations.
- [x] **DEC117** Add mine/occupied-cave runtime families: supports, rails/carts, ropes, lanterns, crates, tools, ladders.
- [ ] **DEC118** Restore dedicated natural-cave regression source and Unity metadata.
- [ ] **DEC119** Restore dedicated occupied/mine-cave regression source and Unity metadata.

## Large-scale content library — 400+ archetype target

- [ ] **DEC130** Implement scalable content-archetype identity/variant encoding while retaining coarse `DecorationPropFamily` behavior classes.
- [ ] **DEC131** Implement the first 42 archetype recipes spanning smithy, tavern, crypt, market, stable, prison, and civic packs.
- [ ] **DEC132** Implement generic content authoring-shape grammar so new archetypes usually require catalog data rather than a bespoke authorer.
- [ ] **DEC133** Implement content-scene slot wrapper that reuses `DecorationSceneScheduler` and `DecorationPlacementResolver`.
- [ ] **DEC134** Add coherent smithy scene: hearth, anvil, bellows, quench tub, grindstone, tool board.
- [ ] **DEC135** Add coherent tavern-bar scene: bar, keg rack, mug rack, serving shelf, firewood, game table.
- [ ] **DEC136** Add coherent crypt scene: sarcophagus/coffin anchor, ossuary, bier, urns, grave marker.
- [ ] **DEC137** Add coherent market scene: stall, canopy/sign, goods display, scale, baskets.
- [ ] **DEC138** Add coherent stable scene: manger, trough, hay, saddle/tack, hitching fixture.
- [ ] **DEC139** Add coherent prison scene: cage/stocks anchor, shackles, key board, bucket, restraint bench.
- [ ] **DEC140** Add coherent civic-corner scene: notice board, well/fountain, lamp, public trough, handcart.
- [ ] **DEC141** Add catalog/scene deterministic tests for the first 42 archetypes and representative multi-seed rooms.
- [ ] **DEC142** Add carpentry/general-workshop pack.
- [ ] **DEC143** Add textile/leather/pottery craft pack.
- [ ] **DEC144** Add kitchen/bakery/brewery/winery pack.
- [ ] **DEC145** Expand market/shop/merchant pack.
- [ ] **DEC146** Expand stable/farm/animal-husbandry pack.
- [ ] **DEC147** Add street/civic/courtyard/garden pack.
- [ ] **DEC148** Expand military/guard/training pack.
- [ ] **DEC149** Expand prison/dungeon/interrogation pack.
- [ ] **DEC150** Expand crypt/graveyard/funerary pack.
- [ ] **DEC151** Expand chapel/temple/shrine/ritual pack.
- [ ] **DEC152** Expand library/study/school/scholar pack.
- [ ] **DEC153** Add alchemy/magic/occult/science pack.
- [ ] **DEC154** Add noble/leisure/music/luxury pack.
- [ ] **DEC155** Add household/lived-in prop pack.
- [ ] **DEC156** Expand mine/quarry/industry pack.
- [ ] **DEC157** Add dock/fishing/waterfront pack.
- [ ] **DEC158** Add camp/travel/hunting/expedition pack.
- [ ] **DEC159** Add ruin/abandonment/damage/aftermath pack.
- [ ] **DEC160** Add regional/faction/cultural dressing pack.
- [ ] **DEC161** Add festivals/ceremonies/temporary-world-state pack.
- [ ] **DEC162** Reach 200 stable archetypes with catalog integrity tests.
- [ ] **DEC163** Reach 400 stable archetypes with catalog integrity, batching, and representative scene-density tests.
- [ ] **DEC164** Add exterior/settlement adapters so streets, gardens, markets, farmyards, and docks consume the same socket/exclusion vocabulary.
- [ ] **DEC165** Add content look-dev/debug view that labels archetype kind in addition to coarse family.

## Completion gates

- [ ] **DEC120** First castle bedroom milestone meets the plan definition of success in executed Unity tests/look-dev.
- [ ] **DEC121** Cave reuse milestone meets the plan definition of success in executed Unity tests/look-dev.
- [ ] **DEC122** Performance pass demonstrates representative decoration density without per-prop heavyweight runtime overhead and records Unity profiling results.
- [ ] **DEC123** Update architecture/runtime documentation with final integration contracts and examples.
