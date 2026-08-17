# WorldBuilder Region-Aware Decoration Guide

Branch: `agent/worldbuilding-decorations`

This guide translates the imported Mounting Force world-content handoff into voxel-side decoration art direction. It does **not** claim that every proposed color/material choice was explicitly authored in the legacy game. The source contracts prove settlement identities, attached place types, some social/lore distinctions, music/aesthetic groupings, and several special visual outliers. The voxel project owns the modern material/color interpretation.

## Evidence boundary

Verified/inferred source facts used here:

- settlement roots: Kentridge, Hightown, Moordell, Rossdam, Fairy Village, Orc Village;
- Kentridge has houses, inn, pub, church, magic shop, weapon/armor shops, warehouse and well;
- Hightown has church, magic shop, weapon/armor shops, mayor house, pub, cave and under-church spaces;
- Moordell has inn, pub, magic shop, weapon/armor shops, grave content and nearby underground/caves;
- dialogue explicitly describes Moordell as a **rich town full of lords and nobles** and a puppet of the empire;
- Rossdam contains a king chamber and is grouped with `CastleTheme.mp3`;
- Fairy Village uses a verified unique embedded visual set centered on its treehouse and has inn/pub/magic/accessory/weapon-shop children;
- Orc Village has its own authored music grouping and weapon/armor/magic/pub services;
- Kentridge is grouped predominantly with `PleasantCreekLoop.mp3`;
- Hightown has its own dominant `hightown.mp3` grouping;
- Moordell is grouped predominantly with `EnchantedFestivalLoop.mp3`;
- Fairy Village Treehouse is grouped predominantly with `Caketown_1.mp3`;
- shared visual vocabulary is intentionally compact, so a small palette abstraction with regional overrides is encouraged.

The exact material/color palettes below are **voxel-side art direction derived from those identities**, not recovered RGB canon.

## Kentridge — warm, modest, lived-in starting town

**Identity:** approachable agrarian/merchant settlement; ordinary households and services; less affluent than Moordell.

**Palette direction:** warm timber, pale/small masonry, earth, cloth, warm window light; restrained gold/crystal.

**Material emphasis:** Wood, MasonrySmall, Dirt, Cloth, Grass, LitWindow.

**Object emphasis:** wells, barrels/crates, laundry, firewood, practical furniture, farm produce, baskets, workshop tools, modest shop counters, tavern content, church votives, basic magic-shop shelves, quest/adventurer notices.

**Avoid overuse:** monumental stone, lavish gold trim, giant magical machinery, noble trophy furniture.

**Scene density:** medium and lived-in. Clutter should communicate actual daily use.

## Hightown — elevated old-stone town with church/cave undertones

**Identity:** distinct town center with church and under-church spaces, cave access, mayoral/merchant services. The name and place graph support an older, more vertical/stone-forward interpretation, but that is voxel-side art direction rather than hard lore.

**Palette direction:** cool slate/stone, weathered timber, muted cloth, white flowers and warm interior lights.

**Material emphasis:** Slate, MasonryMedium, Wood, Cloth, FlowerWhite, LitWindow.

**Object emphasis:** stone planters, shrines, church furniture, archive/scholar pieces, lanterns, stair/vertical-space dressing, cave-edge storage, restrained merchant displays, old signs and memorial details.

**Magical character:** scholarly/sacred rather than whimsical—scrolls, astrolabes, rune boards, church relics, hidden-under-church occult pieces.

## Moordell — wealthy noble imperial town

**Identity:** explicitly rich, full of lords/nobles, politically tied to the empire.

**Palette direction:** dressed masonry, dark polished wood, rich cloth, gold accents, controlled crystal/magical highlights.

**Material emphasis:** MasonryMedium/Large, Wood, Cloth, Gold, Glass, Crystal.

**Object emphasis:** noble furniture, wardrobes/vanities, mirrors, jewelry, wine cabinets, display cases, heraldry, fine market stalls, jewelers, expensive alchemy, formal gardens, statues, fountains, servants' service furniture, polished shop displays.

**Magical character:** curated and expensive—enchanting benches, crystal cabinets, levitation pedestals, divination pieces; magic as status and commerce.

**Scene density:** high but orderly. More matching sets and symmetry than Kentridge.

## Rossdam — royal/castle center

**Identity:** king chamber and castle-theme grouping; should read as the strongest formal/royal center among the named settlements.

**Palette direction:** large stone/slate masses, dark wood, gold, rich cloth, strong warm light, occasional royal crystal accents.

**Material emphasis:** MasonryLarge, DarkStone, Slate, Wood, Gold, Cloth, LitWindow.

**Object emphasis:** throne/royal furniture, grand tables, tall bookcases, trophies, banners, candelabra/chandeliers, formal weapon/armor displays, reliquaries, royal portraits, map tables, courtly guest furniture, ceremonial magic and secure treasure storage.

**Magical character:** institutional/court magic—ward totems, portal keystones, enchanted displays, ritual/temple objects appropriate to palace or castle spaces.

**Scene density:** high, spacious, intentionally composed. Fewer junk piles; more architectural-scale statements.

## Fairy Village — organic enchanted treehouse culture

**Identity:** verified unique visual set centered on a treehouse; culturally separate settlement with magic/accessory/inn/pub/shop content.

**Palette direction:** warm/light wood, moss/grass, flowers, cascade blues, crystal glow, bright cloth and luminous accents.

**Material emphasis:** Wood, Moss, Grass, FlowerWhite/Yellow/Pink/Blue, Cascade, Crystal, LitWindow.

**Object emphasis:** fairy lanterns, enchanted plants, flower planters, living roots, mushroom furniture, tiny shrines, charm displays, hanging fabrics, suspended shelves, natural bowls/baskets, crystal flowers, mana blossoms, wisps, familiar perches/nests, magical nature props.

**Forms:** more rounded/organic when cheap to author; hanging and vertically layered content is especially appropriate.

**Avoid overuse:** heavy dark masonry, massive coffins, bulky industrial workshop silhouettes.

**Scene density:** high detail, small scale, layered vertically rather than floor-cluttered.

## Orc Village — rugged frontier craft and trophy culture

**Identity:** distinct orc settlement with its own music and the standard pub/magic/weapon/armor service set. Detailed material culture is not explicitly recovered, so the interpretation below is voxel-side fantasy art direction.

**Palette direction:** heavy timber, dark stone, earth, muted cloth, firelight, bone/trophy accents.

**Material emphasis:** Wood, DarkStone, Dirt, Cloth, MasonrySmall, LitWindow.

**Object emphasis:** oversized tables/benches, rough racks, smithing/repair content, hides, bone totems, trophy skulls, meat/cooking, kegs, heavy chests, practical magic, ward totems, beast materials, hunting gear and carved posts.

**Magical character:** practical/shamanic—totems, braziers, rune carving, charms, enchanted weapons, monster trophies rather than polished crystal cabinets.

**Scene density:** medium-high but asymmetrical and robust. Prefer chunky silhouettes and open work areas.

## Cross-region object strategy

The 400 stable archetypes are **not** intended to belong uniquely to one region. Region profiles influence selection weights, material/presentation style, scale, ornamentation and optional clutter.

Examples:

- `AlchemyTable` can appear in every town: rough herbs/glass in Kentridge, scholarly instruments in Hightown, polished gold/crystal in Moordell, court ritual apparatus in Rossdam, botanical/fairy variants in Fairy Village, heavy rune-carved version in Orc Village.
- `MarketStall` can be timber/produce-heavy in Kentridge, stone-and-cloth in Hightown, ornate in Moordell, regulated/formal near Rossdam, hanging/treehouse style for fairies, and heavy-hide/timber for orcs.
- `QuestBoard` can become a village notice board, guild contract wall, royal proclamation board, fairy charm-request tree, or carved orc challenge board without changing stable semantic identity.

## Region-aware generation requirements

1. Add a stable `DecorationRegionTheme`/profile layer above `DecorationStyleFamily`.
2. A region profile supplies default style family, default wealth bias, material palette guidance and preferred content-theme weights.
3. Per-building context may override region defaults: a poor Moordell cellar can still be shabby; a rich Kentridge manor can still be ornate.
4. Region identity should influence object choice as well as recoloring. A Fairy Village bedroom should actually select more plants/charms/hanging content; an Orc Village pub should select heavier racks/trophies/cooking content.
5. Do not let region palettes create hard topology or story constraints; this matches the imported visual-handoff policy.
6. Preserve special visual outliers deliberately, especially Fairy Village Treehouse, Forest Maze and Mountains.

## Next content implications

The remaining stable IDs 301–400 already align well with region distinction:

- 301–320 magical nature strongly supports Fairy Village and wilderness;
- 321–340 traps/puzzles support caves, crypts, castle dungeons and wizard spaces;
- 341–360 sacred content can differentiate Kentridge/Hightown/Rossdam religious spaces;
- 361–380 wizard-school/archive content supports Hightown/Moordell/Rossdam magic institutions;
- 381–400 cursed/haunted aftermath can appear differently around graveyards, caves, ruins and story-critical locations.

Future 401+ identities should include region-signature variants only when they represent genuinely different semantic objects. Pure color/material/style changes should remain presentation/profile variation rather than consuming new stable IDs.
