# WorldBuilder Decoration Content Catalog

Branch: `agent/worldbuilding-decorations`
Companion plan: `docs/WORLDBUILDER_DECORATION_SYSTEM_PLAN.md`

## Goal

Build a reusable library large enough that castles, towns, houses, inns, churches, farms, caves, mines, dungeons, roads, docks, ruins, camps, and later regions can be richly dressed without hand-authoring every object.

The target is deliberately large:

- **400+ semantic content archetypes**
- **60+ relational scene recipes**
- **thousands of deterministic visual variants** from seed, culture/style, wealth, condition, biome/environment, and local scene context
- no requirement for one GameObject or one bespoke prefab per generated object

The catalog should grow by adding data/recipes to stable coarse behavior classes rather than expanding the core `DecorationPropFamily` enum for every spoon, anvil, coffin, cart, sign, or hay bale.

## Scale strategy

A content item has two identities:

1. **coarse behavior family** — existing `DecorationPropFamily` such as table, shelf, crate, barrel, fireplace, lantern, bench, chest, altar, weapon rack, etc. This controls placement behavior, interaction semantics, detail policy, persistence class, and backend defaults;
2. **stable content archetype** — a catalog kind encoded into deterministic variant data. This identifies the actual thing being authored: anvil, bellows, sarcophagus, market stall, manger, stocks, notice board, well, and so on.

This lets the existing resolver, sockets, exclusions, generated IDs, batching, persistence, and detail policies remain useful while allowing hundreds of distinct content kinds.

The initial archetype encoding reserves enough stable kind space for roughly **1,000 archetypes** before the encoding contract would need another version.

## Content packs

The lists below are targets, not a requirement to implement every item before using the system. Each pack should be independently useful and should include at least one coherent scene recipe.

### 1. Smithy and forge — target 20+

Anvil, bellows, forge hearth, charcoal forge, grindstone, quench tub, smith tool board, hammer rack, tong rack, coal bin, charcoal sacks, ingot stack, billet rack, scrap pile, horseshoe rack, unfinished blade rack, finished weapon stand, armor repair stand, vise, punch block, swage block, tempering tray, apron hook, chimney hood.

Scenes: village smithy, castle armorer, abandoned forge, mine repair station, master smith workshop.

### 2. Carpentry and general workshop — target 20+

Carpenter bench, saw horse, hand saw rack, plane rack, chisel board, clamp rack, lumber stack, plank rack, dowel bin, work stool, joinery table, shaving pile, wood scrap basket, lathe, bow drill, brace rack, mallet shelf, glue pot station, measuring-stick board, ladder rack, wheelwright jig, wheel stack, repair trestle, tool chest.

Scenes: carpenter shop, wheelwright, repair workshop, castle maintenance room.

### 3. Textile, leather, pottery, and craft — target 24+

Loom, spinning wheel, yarn basket, spindle rack, dye vat, drying line, folded cloth stack, bolt rack, cutting table, mannequin/dress form, sewing stool, leather stretching frame, hide rack, tanning tub, bootmaker bench, last rack, pottery wheel, kiln, clay bin, drying shelf, amphora rack, glaze jars, basket-weaving frame, wicker stack.

Scenes: weaver shop, tailor room, tannery, cobbler, potter workshop.

### 4. Tavern, inn, and feast — target 24+

Bar counter, back bar, keg rack, tap barrel, mug rack, bottle shelf, serving shelf, tray stack, plate rack, cutlery crock, firewood stack, game table, dice table, card table, dart/knife target, notice board, coat hooks, boot rack, umbrella/staff stand, wash basin, spittoon/bucket, private booth divider, musician corner, stage riser, menu board, cellar hatch.

Scenes: common room, tavern bar, private dining room, roadside inn, rowdy alehouse, wealthy inn lounge.

### 5. Kitchen, pantry, bakery, and brewery — target 28+

Prep table, butcher block, chopping board, hanging pot rack, pan rack, cauldron stand, oven, bread oven, roasting spit, spit crank, wash sink, water barrel, flour bin, grain sack stack, spice shelf, herb drying rack, meat hook rail, sausage rack, cheese shelf, pie rack, bread cooling rack, pantry cabinet, vegetable basket, fish crate, brewery vat, mash tun, fermenter, wine press, bottle rack, cask stand.

Scenes: castle kitchen, cottage kitchen, bakery, brewery, winery, pantry, butcher stall.

### 6. Market, shop, and merchant — target 28+

Market stall, fabric canopy, produce stand, fish stall, butcher stall, baker stall, pottery stall, weapon stall, armor stall, book stall, apothecary stall, hanging scale, counter scale, basket stack, display crate, sample table, merchant sign, hanging sign, chalk board, price board, awning, lockbox, cash chest, wrapping table, packing station, handcart, dolly, merchandise rack, queue rail.

Scenes: open-air market, covered bazaar aisle, general store, specialty shop, traveling merchant.

### 7. Stable, farm, and animal husbandry — target 30+

Manger, water trough, hay bale, haystack, saddle rack, tack hooks, bridle rack, blanket rail, horseshoe board, grooming kit shelf, hitching post, stall gate, feed bin, grain sack pile, manure barrow, pitchfork rack, shovel rack, chicken coop, nesting boxes, rabbit hutch, pig trough, milking stool, milk churn rack, butter churn, plow, harrow, seed drill, scarecrow, beehive, bee skep, fence gate, wagon wheel stack.

Scenes: stable aisle, horse stall, barn, dairy corner, chicken yard, farmyard, apiary.

### 8. Street, civic, courtyard, and garden — target 32+

Notice board, town sign, street sign, milestone, waystone, lamp post, bollard, hitching post, bench, well, fountain, bird bath, planter, flower box, hedge planter, trellis, pergola, topiary, statue pedestal, statue, sundial, public trough, trash basket, sweep pile, broom rack, drain grate, rain barrel, hand pump, public wash basin, market clock, bell post, flag pole, festival pole, cart parking rail.

Scenes: town square, castle courtyard, alley, garden, plaza, village green, gate approach.

### 9. Military, guard, and training — target 28+

Weapon rack, spear stand, shield rack, armor stand, helmet shelf, quiver rack, bow rack, crossbow stand, map table, campaign chest, war drum, horn rack, standard rack, target dummy, archery target, melee practice post, training pell, sandbag, barricade, caltrop bin, siege ammunition rack, stone shot pile, bolt rack, oil jar rack, guard desk, duty roster board, bunk footlocker, signal brazier.

Scenes: guard room, armory, barracks, training yard, gatehouse ready room, war room.

### 10. Prison, dungeon, and interrogation — target 26+

Shackles, wall manacles, ankle irons, chain bundle, stocks, pillory, iron cage, hanging cage, barred partition, jailer desk, key board, key ring hook, prison bucket, straw pile, prisoner cot, restraint bench, interrogation table, evidence shelf, confiscated-goods chest, torch cage, cell number plaque, food slot, water jug shelf, guard chair, punishment post, dungeon winch.

Scenes: jailer station, prison cell, dungeon corridor, holding room, interrogation room.

### 11. Crypt, graveyard, and funerary — target 30+

Sarcophagus, coffin, open coffin, funeral bier, ossuary shelf, skull niche, bone box, urn stand, urn shelf, grave marker, headstone, grave slab, tomb chest, memorial plaque, memorial statue, cenotaph, mortuary table, embalming table, incense stand, candle stand, mourning bench, flower offering stand, wreath hook, reliquary niche, catacomb marker, burial shroud rack, grave-digger tools, soil pile, crypt gate, tomb seal.

Scenes: noble crypt, catacomb chamber, village graveyard, mortuary, ruined tomb, ossuary.

### 12. Chapel, temple, shrine, and ritual — target 30+

Altar, side altar, lectern, pulpit, kneeler, prayer bench, votive candle stand, incense brazier, holy-water font, offering box, reliquary, icon stand, ritual basin, processional cross stand, banner pole, choir stand, bell rope, confession screen, sacrament cabinet, scripture stand, relic pedestal, shrine niche, pilgrimage token board, flower offering shelf, ritual drum, gong, prayer wheel, ceremonial lamp, sacred curtain, processional litter.

Scenes: chapel, cathedral side chapel, roadside shrine, monastery prayer room, cult chamber.

### 13. Library, study, school, and scholar — target 30+

Writing desk, reading desk, lectern, map table, globe stand, map rack, scroll rack, book cart, book ladder, manuscript stand, inkwell tray, quill rack, writing slope, document chest, filing pigeonholes, specimen cabinet, display case, chalk board, slate board, abacus stand, ruler rack, telescope, astrolabe stand, star chart, drafting table, architect table, model stand, archive box stack, seal press, correspondence tray.

Scenes: library, scriptorium, scholar study, classroom, map room, archive.

### 14. Alchemy, magic, occult, and science — target 36+

Cauldron, alembic, retort stand, condenser rack, reagent shelf, reagent crate, bottle rack, potion rack, mortar table, herb rack, specimen jar shelf, crystal stand, crystal cluster pedestal, rune circle, summoning circle, ward stones, rune stones, orb pedestal, scrying mirror, spellbook lectern, wand rack, staff rack, ritual dagger stand, magic candle cluster, essence condenser, arcane coil, portal frame, portal plinth, constellation projector, telescope, alchemy furnace, distillation furnace, bone charm rack, occult mask display, chained tome stand, magical containment cage.

Scenes: alchemy lab, wizard study, ritual chamber, observatory, forbidden archive.

### 15. Noble, leisure, music, and luxury — target 30+

Vanity, full mirror, hand mirror stand, wardrobe, jewelry chest, jewelry display, perfume tray, cosmetics table, harp, lute stand, instrument rack, music stand, chess table, board-game table, trophy pedestal, trophy cabinet, wine table, chaise, cushioned bench, foot stool, screen divider, canopy frame, dressing screen, portrait easel, sculpture pedestal, decorative globe, fancy clock, tea table, serving cart, fireplace screen.

Scenes: noble bedroom, salon, music room, dressing room, trophy gallery, private lounge.

### 16. Household and lived-in clutter — target 40+

Laundry basket, laundry line, folded linen stack, broom, mop, bucket, wash tub, basin, chamber pot, coat peg, hat peg, shoe rack, boot tray, umbrella/staff stand, toy chest, wooden toys, doll shelf, cradle, high chair, pet bed, pet bowl, firewood basket, kindling box, ash bucket, sewing basket, repair basket, blanket pile, pillow pile, spare chair stack, folding stool, wall clock, calendar board, family shrine, key hook, mail shelf, candle box, tinder box, cleaning shelf, towel rail, curtain tiebacks.

Scenes: cottage living room, family bedroom, wash room, entry hall, attic, servant room.

### 17. Mine, quarry, and industry — target 30+

Support beam, rail, mine cart, ore cart, rope coil, pulley, block and tackle, lantern, crate, tool rack, ladder, blasting box, drill stand, pick rack, shovel rack, ore bin, sorting table, assay table, scale, timber stack, rail stack, spare wheel stack, track switch lever, warning sign, tunnel marker, ore chute, hoist, winch, lift cage, quarry sled.

Scenes: mine face, rail junction, hoist room, ore sorting room, quarry station.

### 18. Dock, fishing, and waterfront — target 30+

Mooring bollard, rope coil, net pile, hanging fishing net, drying net rack, fish rack, fish crate, bait box, tackle rack, rod rack, harpoon rack, anchor, chain pile, buoy stack, lobster/crab pot, basket traps, dock cart, hand winch, cargo crane, pulley post, gangplank, boat cradle, oar rack, sail rack, mast spar rack, water barrel, dock lantern, customs desk, cargo manifest board, warehouse pallet.

Scenes: fishing dock, cargo dock, boathouse, fish market, dock warehouse.

### 19. Camp, travel, hunting, and expedition — target 30+

Tent, lean-to, bedroll, campfire, cooking tripod, cook pot, spit, folding stool, camp table, pack stack, saddle bags, bedroll bundle, drying line, hide drying rack, trophy skull stand, bow stand, quiver pile, trap rack, fishing kit, water skin rack, water barrel, travel chest, map board, field desk, lantern pole, firewood pile, tent stakes, rope bundle, supply crate, mess kit rack.

Scenes: military camp, hunter camp, caravan stop, cave expedition camp, refugee camp.

### 20. Ruins, abandonment, damage, and aftermath — target 36+

Rubble pile, broken beam, collapsed shelf, broken chair, broken table, shattered crate, burst barrel, overturned cart, broken wagon wheel, torn banner, fallen tapestry, shattered statue, statue fragment, broken column drum, fallen door, boarded window, barricaded door, scorch patch, ash pile, soot stain, water-damage patch, mossy furniture, abandoned bedding, debris heap, discarded tools, abandoned luggage, smashed pottery, broken glass pile, spiderweb cluster, rat nest, bone scatter, collapsed scaffolding, broken ladder, severed chain, grave collapse, cave-in marker.

Scenes: recently attacked room, ancient ruin, burned house, flooded cellar, abandoned mine, collapsed crypt.

### 21. Regional, faction, and cultural dressing — target 30+

Faction shield plaques, heraldic signs, regional pottery, local textile patterns, clan poles, ancestor boards, guild signs, trade emblems, militia insignia, royal crests, religious icons, pilgrimage markers, border stones, tax seals, wanted boards, victory trophies, defeated-faction trophies, occupation notices, rebel markings, merchant guild plaques, mason marks, carpenter marks, smith marks, brewer marks, family name boards, house numbers, ceremonial masks, regional lanterns, regional shrines, regional grave markers.

Scenes: faction-controlled street, guild hall, occupied town, border checkpoint, clan settlement.

### 22. Festivals, ceremonies, and temporary world states — target 30+

Festival bunting, pennant strings, lantern strings, flower garlands, wreaths, feast tables, temporary stage, musician platform, market tent, tournament rail, joust target, archery target, prize stand, trophy table, ceremonial arch, wedding canopy, funeral canopy, procession markers, bonfire, maypole/festival pole, gift pile, offering pile, parade barrier, spectator bench, ribbon pole, announcement board, seasonal scarecrow, harvest display, winter wood stack, celebration barrels.

Scenes: market day, feast day, tournament, wedding, funeral, harvest festival, occupation rally.

## First implementation wave — 42 archetypes

The first scalable catalog wave should land these seven packs together because they exercise floor, wall, ceiling, integrated, movable/container, thin-display, and light-adjacent behavior:

**Smithy:** anvil, bellows, forge hearth, grindstone, quench tub, smith tool board.

**Tavern:** bar counter, keg rack, mug rack, serving shelf, firewood stack, game table.

**Crypt:** sarcophagus, coffin, ossuary shelf, funeral bier, urn stand, grave marker.

**Market:** market stall, hanging scale, basket stack, merchant sign, produce stand, fabric canopy.

**Stable:** manger, hay bale, saddle rack, water trough, hitching post, tack hooks.

**Prison:** shackles, stocks, iron cage, key board, prison bucket, restraint bench.

**Civic:** notice board, well, fountain, lamp post, public trough, handcart.

## Scene rules

Large catalog size alone will not make the world feel authored. Scene recipes should encode relationships:

- forge hearth near bellows; anvil offset from hearth; quench tub near anvil; tool board on adjacent wall;
- tavern bar against service wall; keg rack behind/near bar; mugs on wall; game table away from service path;
- sarcophagus or bier as crypt anchor; urns/ossuary along walls; grave markers or offerings in secondary positions;
- market stall anchors canopy/sign/display goods; adjacent stalls vary by trade and wealth;
- stable manger/trough against wall, saddle/tack on wall, hay in corners, hitching outside circulation;
- prison cage/stocks/restraint bench never block required guard circulation; key board remains guard-side;
- civic well/fountain is a spatial anchor; notice board/lamp/trough/cart dressing stays around circulation rather than across it.

## Variation dimensions

Every archetype should eventually be able to vary along several independent axes without changing its stable semantic identity:

- culture/style family
- wealth tier
- condition/damage tier
- structure or scene seed
- dimensions within safe bounds
- silhouette grammar
- primary/secondary/accent material policy
- ornament count
- clutter children
- orientation and wall/floor choice
- occupancy state (working, idle, abandoned, looted, damaged)
- region/biome substitutions

A single `Anvil` archetype can therefore yield village, military, noble-armorer, frontier, worn, abandoned, and ruined variants without becoming seven separate authored assets.

## Scale gates

Content expansion is successful when:

1. adding a new archetype is primarily a catalog entry plus an authoring shape/presentation choice, not new resolver architecture;
2. archetype identity remains deterministic and persistence-safe;
3. dozens of instances can be static-batched or emitted into integrated world geometry;
4. scene recipes remain coherent under many seeds;
5. exterior and interior content share the same socket/exclusion concepts;
6. the catalog can pass 400 archetypes without turning every item into a heavyweight runtime entity.
