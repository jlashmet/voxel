# WorldBuilder Procedural Decoration Tasks

Branch: `agent/worldbuilding-decorations`
Base: `agent/worldbuilding-structures-caves`
Plan: `docs/WORLDBUILDER_DECORATION_SYSTEM_PLAN.md`
Master manifest: `docs/WORLDBUILDER_DECORATION_MASTER_MANIFEST.md`
Region art direction: `docs/WORLDBUILDER_REGION_DECORATION_GUIDE.md`
Large brainstorm catalog: `docs/WORLDBUILDER_DECORATION_CONTENT_CATALOG.md`

This checklist is the source of truth for implementation progress. Mark each item complete in the same change that completes the work whenever practical.

## Current implementation notes

- The game content target is a **fantasy RPG in a magic world**. New decoration work should favor magical, medieval/fantasy, ancient, rustic, religious, dungeon, wilderness, monster, adventuring, trade, domestic, and fantastical content.
- Region-aware content is now a first-class requirement. Kentridge, Hightown, Moordell, Rossdam, Fairy Village and Orc Village have voxel-owned decoration profiles informed by the imported Mounting Force world/visual guidance.
- Exact color/material choices are modern voxel art direction, not claimed as recovered RGB canon. Region identity is evidence-backed; palette interpretation is intentionally game-owned.
- The generic semantic resolver, deterministic scene scheduler, stable prop IDs, socket/exclusion model, style/wealth/condition profiles, backend dispatch, runtime batching/detail policy, and persistence overlays are implemented.
- The stable content manifest contains **300 implemented archetypes** and exact reserved identities through **400**. The encoding leaves room through ID 1023.
- IDs 261-280 implement monster-lair/creature-occupation content. IDs 281-300 implement adventurer-guild/quest/caravan content, with MonsterDen, SpiderNest, AdventurerGuildHall and CaravanStaging source scenes.
- The 261-300 scene path now supports region-weighted optional selection and region-specific material/ornament presentation while retaining global semantic IDs. Older 1-260 scene/catalog paths still require migration onto that policy.
- `DecorationRegionLookDevComposition` resolves the same AdventurerGuildHall semantic scene for all six named settlements for direct comparison in tests/look-dev tooling.
- Rendering policy is breadth-first: box/voxel assemblies are valid initial implementations; use smooth/curved/procedural forms immediately when they are not meaningfully more work, and upgrade signature silhouettes later without changing semantic IDs.
- Unity/CI execution and visual/performance evidence remain separate completion gates.

## Completed architecture and first content foundation

- [x] **DEC001-DEC117** Core decoration architecture, first castle/cave integration, render backends, runtime scale/persistence, and first content expansion.
- [ ] **DEC118** Restore dedicated natural-cave regression source and Unity metadata.
- [ ] **DEC119** Restore dedicated occupied/mine-cave regression source and Unity metadata.
- [x] **DEC130-DEC144** Scalable archetype identity, shared shape grammar, first 114 archetypes and coherent foundational scenes.
- [x] **DEC146** Farm/animal-husbandry source pack.
- [x] **DEC147** Fantasy street/courtyard/garden source pack.
- [x] **DEC150** Graveyard/funerary/catacomb source pack.
- [x] **DEC153** Alchemy/magic/occult/observatory source pack.
- [x] **DEC162** Reach 200 stable archetypes with integrity and representative multi-seed scene test source.
- [x] **DEC166-DEC174** Craft and food-production scene milestones.
- [x] **DEC176** Farmyard composition.
- [x] **DEC178-DEC184** Alchemy, ritual, observatory, graveyard, catacomb, garden and fantasy-street scene source.

## Fantasy content expansion

- [ ] **DEC145** Expand fantasy market/shop/merchant pack beyond IDs 201-220.
- [ ] **DEC149** Expand dungeon/prison/secret-passage content.
- [ ] **DEC151** Expand chapel/temple/shrine/ritual content.
- [ ] **DEC152** Expand library/wizard-study/school/scholar content.
- [x] **DEC185** Replace drafted military block with magical fantasy content at IDs 221-240.
- [x] **DEC186** Add EnchantersWorkshop, FamiliarRoom and ArcaneGallery scene definitions.
- [ ] **DEC154** Expand noble/leisure/music/luxury fantasy content.
- [ ] **DEC155** Expand household/lived-in fantasy content beyond IDs 241-260.
- [ ] **DEC156** Expand mine/quarry/dwarven/underground-industry content.
- [ ] **DEC157** Add dock/fishing/waterfront fantasy pack.
- [ ] **DEC158** Add further adventurer/camp/travel/hunting/expedition content beyond IDs 281-300.
- [ ] **DEC159** Add ruin/abandonment/damage/aftermath pack.
- [ ] **DEC160** Add regional/faction/cultural fantasy dressing.
- [ ] **DEC161** Add festivals/ceremonies/temporary-world-state pack.
- [x] **DEC187** Add first monster-lair content block at IDs 261-280.
- [ ] **DEC188** Add magical-nature content at reserved IDs 301-320.
- [x] **DEC189** Add first adventurer/guild content block at IDs 281-300.
- [ ] **DEC190** Add treasure/loot-display vocabulary.
- [ ] **DEC191** Add trap/puzzle/environmental-interaction dressing at reserved IDs 321-340.
- [ ] **DEC192** Add fantasy food/feast variety.
- [ ] **DEC163** Reach **400 stable fantasy archetypes** with integrity, batching and representative scene-density test source.
- [ ] **DEC164** Add exterior/settlement adapters so fantasy streets, gardens, markets, farmyards, shrines and docks consume real exterior geometry.
- [ ] **DEC165** Add content look-dev/debug view that labels archetype kind in addition to coarse family.

## Region-aware world dressing

- [x] **DEC197** Read/import the settlement-root, visual-palette and inferred-lore guidance for Kentridge, Hightown, Moordell, Rossdam, Fairy Village and Orc Village.
- [x] **DEC198** Document evidence-backed regional themes plus voxel-owned material/color direction in `WORLDBUILDER_REGION_DECORATION_GUIDE.md`.
- [x] **DEC199** Implement `DecorationRegionProfile` defaults for all six named settlements, including style family, wealth bias, material guidance and preferred content tags.
- [x] **DEC200** Add source tests proving all six region profiles are valid, distinct and allow per-building wealth overrides.
- [x] **DEC201** Add the initial region-aware content-weighting path, first applied to optional selection for IDs 261-300 without changing required scene anchors or stable IDs.
- [x] **DEC202** Add the initial region-aware presentation override layer, first applied to the 261-300 geometry emitter so the same semantic prop can use settlement-specific materials/ornament without duplicate IDs.
- [x] **DEC203** Add a representative six-settlement comparison composition using the same AdventurerGuildHall semantic room across Kentridge, Hightown, Moordell, Rossdam, Fairy Village and Orc Village.
- [ ] **DEC204** Expand region-density source coverage beyond the guild comparison: Kentridge lived-in/practical, Hightown sacred/scholarly, Moordell wealthy/ordered, Rossdam royal/formal, Fairy Village organic/enchanted, Orc Village rugged/trophy/craft across multiple scene families.
- [ ] **DEC205** Preserve the verified special visual outliers (Fairy Village Treehouse, Forest Maze, Mountains) as explicit palette/profile overrides rather than flattening them into generic settlement styling.
- [ ] **DEC206** Migrate older stable IDs/scenes 1-260 onto shared region content-tag weighting where optional selection is meaningful.
- [ ] **DEC207** Thread region presentation overrides through older 1-260 geometry emitters without duplicating semantic IDs.
- [ ] **DEC208** Add an Editor/debug look-dev view that renders the six-region comparison side-by-side or in selectable fixtures rather than source data only.

## Composition milestones

- [ ] **DEC175** Multi-stall fantasy market district composition with trade-specialized and magical stalls.
- [ ] **DEC177** Fantasy village/town square composition with well/fountain, shrine/notice board, seating, carts, merchants and seasonal overlays.
- [ ] **DEC193** Wizard tower composition spanning study, alchemy, enchanting, observatory and familiar spaces.
- [ ] **DEC194** Dungeon wing composition spanning cells, traps, shrine/ritual spaces, treasure and monster occupation.
- [x] **DEC195** Adventurer guild hall source composition with quest board, registry/map furniture, storage/trophies and contract surfaces.
- [x] **DEC196** First monster-lair source compositions: MonsterDen and SpiderNest.

## Completion gates

- [ ] **DEC120** First castle bedroom milestone meets the plan definition of success in executed Unity tests/look-dev.
- [ ] **DEC121** Cave reuse milestone meets the plan definition of success in executed Unity tests/look-dev.
- [ ] **DEC122** Performance pass demonstrates representative decoration density without per-prop heavyweight runtime overhead and records Unity profiling results.
- [ ] **DEC123** Update architecture/runtime documentation with final integration contracts and examples.
