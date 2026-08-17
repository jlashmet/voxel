# WorldBuilder Procedural Decoration Tasks

Branch: `agent/worldbuilding-decorations`
Base: `agent/worldbuilding-structures-caves`
Plan: `docs/WORLDBUILDER_DECORATION_SYSTEM_PLAN.md`
Large content catalog: `docs/WORLDBUILDER_DECORATION_CONTENT_CATALOG.md`

This checklist is the source of truth for implementation progress. Mark each item complete in the same change that completes the work whenever practical.

## Current implementation notes

- The game content target is a **fantasy RPG in a magic world**. New decoration work should favor magical, medieval/fantasy, ancient, rustic, religious, dungeon, wilderness, monster, adventuring, trade, domestic, and fantastical content. Do not spend catalog budget on modern buildings, modern civic infrastructure, or military/war-room/training-yard content.
- Ordinary medieval weapons/armor can still appear where they make sense as fantasy-world shop, guard, adventurer, trophy, dungeon, or castle dressing; they are not a standalone military-content priority.
- The generic semantic resolver, deterministic scene scheduler, stable prop IDs, socket/exclusion model, style/wealth/condition profiles, backend dispatch, runtime batching/detail policy, and persistence overlays are implemented.
- Castle bedroom and great-hall furniture have procedural integration paths; many other legacy castle details remain available for incremental migration.
- Cave runtime content includes `CaveCampScene`, natural cave environmental families, and occupied/mine environmental families.
- Unity/CI execution and visual/performance evidence remain separate completion gates.
- The content identity space contains **260 stable archetypes in source**. IDs 1-200 cover the original craft, domestic, dungeon, arcane, funerary, farm and settlement packs. IDs 201-220 add fantasy merchants; IDs 221-240 are explicitly magical/enchanter/familiar/arcane-gallery content; IDs 241-260 add lived-in/noble household content.
- IDs 221-240 were originally drafted as military/training content but were replaced before further expansion with enchanter workstations, rune/wand/staff making, enchanted displays, familiars, levitating books, magic mirrors, divination, elemental braziers, mana fonts, portal/ward pieces, fairy lights and enchanted plants.
- The stable-ID encoding leaves room through ID 1023, so the 400+ target remains append-only.

## Completed architecture and first content foundation

- [x] **DEC001-DEC117** Core decoration architecture, first castle/cave integration, render backends, runtime scale/persistence, and first content expansion.
- [ ] **DEC118** Restore dedicated natural-cave regression source and Unity metadata.
- [ ] **DEC119** Restore dedicated occupied/mine-cave regression source and Unity metadata.
- [x] **DEC130-DEC144** Scalable archetype identity, shared shape grammar, first 114 archetypes and coherent smithy/tavern/crypt/market/stable/prison/civic/craft/food-production scenes.
- [x] **DEC146** Farm/animal-husbandry source pack.
- [x] **DEC147** Fantasy street/courtyard/garden source pack; avoid modern-city-specific follow-ons.
- [x] **DEC150** Graveyard/funerary/catacomb source pack.
- [x] **DEC153** Alchemy/magic/occult/observatory source pack.
- [x] **DEC162** Reach 200 stable archetypes with integrity and representative multi-seed scene test source.
- [x] **DEC166-DEC174** Craft and food-production scene milestones.
- [x] **DEC176** Farmyard composition.
- [x] **DEC178-DEC184** Alchemy, ritual, observatory, graveyard, catacomb, garden and fantasy-street scene source.

## Fantasy content expansion — next priorities

- [ ] **DEC145** Expand fantasy market/shop/merchant pack: jewelers, apothecaries, potion shops, scroll/book merchants, curios, magical component vendors, traveling merchants and enchanted shop displays. IDs 201-220 provide the first slice.
- [ ] **DEC149** Expand dungeon/prison/torture/secret-passage content: cells, chains, cages, keys, mechanisms, hidden doors, trap dressing, oubliette fixtures, confiscated loot and jailer spaces.
- [ ] **DEC151** Expand chapel/temple/shrine/ritual content: altars, relics, sacred lamps, icons, offerings, fonts, prayer furniture, holy/magical symbols, pilgrimage and cult variants.
- [ ] **DEC152** Expand library/wizard-study/school/scholar content: books, scrolls, maps, globes, lecterns, magical tomes, scriptorium tools, magical schools and archive variants.
- [x] **DEC185** Replace the drafted 221-240 military block with magical fantasy content: enchanter, rune/wand/staff crafting, familiar room, levitation/portal/ward, elemental and enchanted-object archetypes.
- [x] **DEC186** Add initial EnchantersWorkshop, FamiliarRoom and ArcaneGallery scene definitions.
- [ ] **DEC154** Expand noble/leisure/music/luxury fantasy content: salons, feasting, instruments, trophies, heraldry, wardrobes, jewelry, canopy furniture and magical luxury variants.
- [ ] **DEC155** Expand household/lived-in fantasy content: laundry, toys, cradles, pets/familiars, hearth clutter, sewing, tools, food storage, servant spaces and family shrines. IDs 241-260 provide the first slice.
- [ ] **DEC156** Expand mine/quarry/dwarven/underground-industry content, favoring fantasy mechanisms and magical ore processing over industrial-era machinery.
- [ ] **DEC157** Add dock/fishing/waterfront fantasy pack: nets, boats, oars, fish racks, rope, crates, piers, ferries, river shrines and magical waterfront details.
- [ ] **DEC158** Add adventurer/camp/travel/hunting/expedition pack: tents, bedrolls, maps, packs, monster trophies, cooking, traps, camp shrines, caravans and magical expedition gear.
- [ ] **DEC159** Add ruin/abandonment/damage/aftermath pack: rubble, broken furniture, burned/flooded/cursed variants, webs, nests, bones, overgrowth, magical corruption and abandoned belongings.
- [ ] **DEC160** Add regional/faction/cultural fantasy dressing: heraldry, guilds, clans, races/cultures, religious motifs, local pottery/textiles, magical traditions and architecture-adjacent dressing.
- [ ] **DEC161** Add festivals/ceremonies/temporary-world-state pack: feast days, markets, weddings, funerals, harvest festivals, tournaments/fairs as civilian spectacle, magical celebrations and seasonal decorations.
- [ ] **DEC187** Add monster-lair content: dragon hoards, goblin clutter, giant furniture, undead crypt dressing, spider nests, beast dens, slime/corruption, cult lairs and boss-room trophies.
- [ ] **DEC188** Add magical-nature content: glowing fungi, fairy rings, enchanted trees, rune stones, spirit shrines, magical crystals, floating rocks, elemental growths and cursed vegetation.
- [ ] **DEC189** Add adventurer/guild content: quest boards, trophy walls, maps, bounty ledgers, gear racks, party tables, training props, supply lockers and guild-specific magical dressing.
- [ ] **DEC190** Add treasure/loot-display vocabulary: coin piles, gem heaps, reliquaries, magical chests, cursed treasure, artifact pedestals, vault shelving and hidden caches.
- [ ] **DEC191** Add trap/puzzle/environmental-interaction dressing: pressure plates, rune locks, rotating statues, crystal sockets, lever walls, chain mechanisms, magical seals, movable blocks and clue displays.
- [ ] **DEC192** Add fantasy food/feast variety: whole animals, pies, breads, fruit, cheeses, hanging herbs, exotic monster ingredients, magical drinks, banquet displays and kitchen mess.
- [ ] **DEC163** Reach **400 stable fantasy archetypes** with integrity, batching and representative scene-density test source.
- [ ] **DEC164** Add exterior/settlement adapters so fantasy streets, gardens, markets, farmyards, shrines and docks consume real exterior geometry.
- [ ] **DEC165** Add content look-dev/debug view that labels archetype kind in addition to coarse family.

## Composition milestones

- [ ] **DEC175** Multi-stall fantasy market district composition with trade-specialized and magical stalls.
- [ ] **DEC177** Fantasy village/town square composition with well/fountain, shrine/notice board, seating, carts, merchants and seasonal overlays.
- [ ] **DEC193** Wizard tower composition spanning study, alchemy, enchanting, observatory and familiar spaces.
- [ ] **DEC194** Dungeon wing composition spanning cells, traps, shrine/ritual spaces, treasure and monster occupation.
- [ ] **DEC195** Adventurer guild hall composition spanning quest board, trophy area, supply/storage, social tables and magical services.
- [ ] **DEC196** Monster-lair compositions with species/faction-specific clutter and loot relationships.

## Completion gates

- [ ] **DEC120** First castle bedroom milestone meets the plan definition of success in executed Unity tests/look-dev.
- [ ] **DEC121** Cave reuse milestone meets the plan definition of success in executed Unity tests/look-dev.
- [ ] **DEC122** Performance pass demonstrates representative decoration density without per-prop heavyweight runtime overhead and records Unity profiling results.
- [ ] **DEC123** Update architecture/runtime documentation with final integration contracts and examples.
