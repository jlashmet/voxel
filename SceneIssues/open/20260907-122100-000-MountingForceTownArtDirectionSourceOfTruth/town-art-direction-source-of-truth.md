# Mounting Force Town Art Direction — Source of Truth

This document is the authoritative town-environment art reference created by SceneIssue `20260907-122100-000-MountingForceTownArtDirectionSourceOfTruth`. It intentionally supersedes current voxel town imagery as **visual** authority. Historical facts are separated from newly authored Project North Star art direction so later agents can tell what came from Mounting Force and what was deliberately invented for the remake.

## 1. Provenance, evidence, and confidence

### Primary historical source
- Repository: `jlashmet/mounting-force`
- Pinned commit: `f37512e0175f46e14ad2d7aeb5ecea290af14a14`
- Project manifest: `MountingForce.xcodeproj/project.pbxproj`
- Narrative corroboration used where called out below, especially `Art/AttackMoordell.txt`.

The project manifest is the roster/inventory baseline because it enumerates packaged scene groups, TMX maps, character text, story text, shops, houses, and special interiors. Its `Scenes` group separates Kentridge, Hightown, Moordell, Rossdam, and Orc-Town from non-town locations such as Forest, Graveyard, and Logan-Castle. It also packages `fairy-village.tmx` plus a substantial Fairy Village building group. `orc-village.tmx`, its shops, its feast text, and the `Orc-Town` cutscene group establish that Orc Village/Town is a true settlement rather than a single encounter map.

### Secondary source checked for competing canon
- Repository: `jlashmet/MountingForce2`
- Pinned commit: `5e111d5a72e441824311b4a3a176f72e9c86f653`

Its tracked Assets tree contains no Cambridge/Kentridge/Hightown path match at the pinned commit and therefore does not establish a replacement town roster. It is not used to override the original project.

### Coordinator-canonical source gap
The SceneIssue explicitly requires **Cambridge** and explicitly states that Cambridge must still read as a water town through docks, waterfront architecture, bridges or water crossings, and water-oriented public infrastructure. No Cambridge path/string was found in either pinned connected Mounting Force repository. Cambridge is therefore included because the coordinator requirement is canonical for this assignment, but only those stated water-town characteristics are treated as source-backed. All additional Cambridge buildings and motifs below are labeled **derived**.

### Disconfirmed or excluded candidates
- **Wharfington**: no match in the pinned original project manifest and no corroborating tracked asset in the secondary repository. It is not promoted into the canonical roster from an unverified lead.
- **Forest, Graveyard, Logan-Castle, bandit hideout, fighting areas, caves, and overworld junctions**: packaged as non-town encounter/travel/special locations; excluded from the town concept quota.

## 2. Frozen canonical settlement roster

The complete settlement roster for this reference pack is:

1. **Kentridge** — original-source town.
2. **Hightown** — original-source town.
3. **Moordell** — original-source town.
4. **Rossdam** — original-source town.
5. **Orc Village / Orc-Town** — original-source settlement; both names refer to the same settlement family in the packaged source.
6. **Fairy Village** — original-source settlement.
7. **Cambridge** — coordinator-canonical water town; historical repository source gap explicitly recorded.

This roster is frozen before concept generation. No concept set may silently add another town or remove one of these seven.

## 3. Shared Project North Star environment language

Every town belongs to one game, but no two towns may collapse into the same generic medieval village.

### Shape grammar
- Readable, stylized, handcrafted masses first; detail second.
- Strong rooflines, porches, towers, gates, arcades, bridges, stairs, terraces, docks, trees, and civic landmarks create instantly legible silhouettes.
- Slightly exaggerated proportions are encouraged: deeper eaves, thicker posts, broader stairs, clearer door/window rhythms, oversized civic focal elements, and legible material breaks.
- Avoid photoreal micro-detail, sterile procedural repetition, voxel-block aesthetics, and toy-like low-poly simplification.

### Material language
- Natural stone, timber, plaster, fired tile/slate, metal, woven cloth, wood shingles, glass/crystal, earth, water, vegetation, and town-specific specialty materials.
- Surfaces should feel tactile and authored, with restrained wear concentrated where use tells a story.
- Materials must support town identity: the same generic stone/timber kit cannot dominate every settlement.

### Color and lighting
- Storybook saturation with grounded values: colorful enough to identify districts and cultures, restrained enough to keep shapes readable.
- Warm/cool contrast is preferred over flat global tinting.
- Daylight concepts should prioritize readable architecture and terrain; evening/interior concepts may use pools of practical light without becoming muddy.
- No heavy fog or bloom that hides town topology.

### Terrain integration
- Buildings grow from the town's geography rather than sitting on a flat showcase pad.
- Roads, stairs, drainage, retaining walls, docks, terraces, bridges, edge farms, tree roots, cliffs, canals, and defensive boundaries are part of the architectural identity.

### Interiors
- Interior structure must visibly relate to exterior construction: roof pitch, wall thickness, columns, windows, fireplaces, floor levels, and structural materials must make spatial sense.
- Props support function and culture, not clutter for its own sake.

### Anti-drift rules
1. Existing voxel town screenshots are implementation history, not visual canon.
2. Do not recolor one generic building kit seven ways and call the towns distinct.
3. Every town must be identifiable in grayscale from silhouette/topology before color is considered.
4. Every town must have at least one terrain/infrastructure signature and one architectural signature.
5. Source-backed named places must remain recognizable by function even when their exact 2D layouts are not reproduced.
6. Derived elements may enrich a town but may not contradict source-backed identity or named-place requirements.
7. No character design or gameplay UI is part of this pack.

---

# 4. Town briefs and locked inventories

The inventory in each subsection is locked **before** that town's concepts are generated.

## Kentridge — working hearth-town under pressure

### Historical/source-backed identity
Kentridge is repeatedly treated as a town in the original project and narrative. `Art/AttackMoordell.txt` establishes that its people are short on food/supplies and contrasts it with wealthy Moordell. The project contains a unusually dense set of Kentridge houses, shops, civic/religious spaces, well, inn, and warehouse maps.

### Locked source-backed named/specific places
- Mayor house — `Art/kentridge-mayor-house.tmx`
- Armor shop — `Art/kentridge-armor-shop.tmx`
- Magic shop — `Art/kentridge-magic-shop.tmx`
- Church — `Art/kentridge-church.tmx`
- Inn — `Art/kentridge-inn.tmx`
- Town well — `Art/kentridge-well.tmx`
- Rebecca house — `Art/kentridge-rebecca-house.tmx`
- Awon house and back room — `Art/kentridge-awon-house.tmx`, `Art/kentridge-awon-house-back-room.txt`
- Sarah house — `Art/kentridge-sarah-house.tmx`
- Katie house — `Art/kentridge-katie-house.tmx`
- Abandoned house — `Art/kentridge-abandoned-house.tmx`
- Warehouse/lower warehouse — source manifest entry `kentridge-warehouse-lower.tmx` plus warehouse battle text
- Distribution/supply pressure is story-backed through Kentridge narrative and its contrast with Moordell.

### Derived Project North Star art direction
- **Theme:** resilient working town, communal, practical, visibly maintained despite scarcity.
- **Architecture:** warm plaster-and-timber houses on rough stone plinths; deep eaves; irregular but orderly lanes; modest civic buildings rather than monumental ones.
- **Materials/palette:** mossy gray stone, honey/aged timber, warm cream plaster, muted terracotta, moss green, faded blue cloth accents.
- **Economy/culture:** crafts, small trade, food/supply distribution, local services; useful spaces are cared for even when resources are tight.
- **Terrain:** rolling green foothill/forest edge with gentle grade changes, retaining stones, kitchen gardens, carts, and edge fields.
- **Signature:** central well + market/supply space, compact church silhouette, working warehouse edge, lived-in lane network.
- **Infrastructure:** stone/dirt roads with shallow drainage, footpaths, small market square, well, cart loading apron, low retaining walls, edge farms/gardens.

### Required six-image concept set
1. Overall/elevated view: town topology with central well/market, church, warehouse edge, gardens.
2. Exterior: mayor house or church, showing Kentridge civic scale.
3. Exterior: magic shop/inn/warehouse, showing commercial working-town language.
4. Interior: mayor house/church.
5. Interior: magic shop/inn/warehouse.
6. Street/public space: well and supply-market square with roads/drainage/loading infrastructure.

## Hightown — terraced church town above the road

### Historical/source-backed identity
The original project has a dedicated Hightown scene group, distribution story, mayor house, church, shops, pub, Timmy house, cave, and multi-level under-church content. `Art/AttackMoordell.txt` groups Hightown with Kentridge as receiving smaller food portions than Moordell. The name plus the church/under-church source supports elevation/religious prominence only as a design cue, not as a claim about an unrecovered exact map layout.

### Locked source-backed named/specific places
- Church — `Art/hightown-church.tmx`
- Under-church / under-church level 2 — `Art/hightown-under-church.tmx`, `Art/hightown-under-church2.tmx`
- Mayor house — `Art/hightown-mayor-house.tmx`
- Armor shop — `Art/hightown-armor-shop.tmx`
- Magic shop — `Art/hightown-magic-shop.tmx`
- Weapon shop — `Art/hightown-weapon-shop.tmx`
- Pub — `Art/hightown-pub.tmx`
- Timmy house and back room — `Art/hightown-timmy-house.tmx`, `Art/hightown-timmy-house-back-room.tmx`
- Hightown cave — `Art/hightown-cave.tmx`
- Distribution public event — `Art/hightown-distribution.txt` and before/after-distribution text

### Derived Project North Star art direction
- **Theme:** wind-bright terraced hill settlement centered on faith, civic gathering, and steep circulation.
- **Architecture:** pale stone lower walls, dark slate roofs, tall narrow gables, buttressed church massing, covered stair landings and arcades.
- **Materials/palette:** chalk/pale granite, charcoal slate, desaturated blue, plum, silver-gray timber, warm amber interiors.
- **Economy/culture:** service/trade town with strong communal/religious civic identity; modest prosperity constrained by supply hardship.
- **Terrain:** visibly steeper than Kentridge; stacked terraces, stairs, switchback road, retaining walls, rock outcrops.
- **Signature:** church tower and stepped forecourt above the town; crypt/under-church hints through foundation massing and stair portals.
- **Infrastructure:** stairs, switchback cart road, stone gutters, retaining walls, gate/arch at major terrace transitions, distribution forecourt.

### Required six-image concept set
1. Overall/elevated terraced town with church dominating upper ridge.
2. Exterior church/forecourt.
3. Exterior pub or shop terrace.
4. Interior church/under-church threshold.
5. Interior pub/shop/Timmy house.
6. Street/public space: stepped distribution square with retaining walls, stairs, gutters, and road connection.

## Moordell — privileged imperial noble town

### Historical/source-backed identity
`Art/AttackMoordell.txt` explicitly calls Moordell “a town to the north,” “full of Lords and nobles,” and “known as a puppet of the empire,” and says it receives more than its fair share of supplies/food compared with Kentridge and Hightown. This is the strongest direct socioeconomic town statement in the source material.

### Locked source-backed named/specific places
- Distribution setting/event — `Art/Moordell-Distribution.txt`
- Generic town building source — `Art/moordell-building1.tmx`
- Mayor interaction — `Art/moordell-ask-mayor.txt`
- Armor shop — `Art/moordell-armor-shop.tmx`
- Magic shop — `Art/moordell-magic-shop.tmx`
- Weapon shop — `Art/moordell-weapon-shop.tmx`
- Inn — `Art/moordell-inn.tmx`
- Pub — `Art/moordell-pub.tmx`
- Grave — `Art/moordell-grave.tmx` and grave battle text
- Gertrude house / underground route — `Art/overworld-moordell-underground-gertrude-house.tmx`, related underground map/text
- Wizard-trials/cave routes exist in the Moordell overworld area; treat them as edge-region destinations, not central civic buildings.

### Derived Project North Star art direction
- **Theme:** orderly, prosperous, politically favored town where public wealth is impossible to miss.
- **Architecture:** formal masonry townhouses, carved lintels, tall windows, symmetrical civic facades, enclosed courtyards, ironwork, mansard/steep slate-tile roofs.
- **Materials/palette:** creamy limestone, dark burgundy tile/slate, polished dark timber, brass/gold accents, deep imperial red, restrained evergreen.
- **Economy/culture:** nobles, administrators, merchants, conspicuously stocked food/supply system; public order and patronage.
- **Terrain:** broad planned terraces/avenues rather than Kentridge's organic lanes; manicured edge gardens and estate walls.
- **Signature:** formal distribution hall/arcade facing a fountain plaza; noble residences and gates frame sightlines.
- **Infrastructure:** paved avenues, formal drains, fountains, walled gardens, iron gates, covered market/distribution arcade, carriage courts.

### Required six-image concept set
1. Overall/elevated planned noble town with formal avenue/plaza.
2. Exterior distribution hall or mayor/noble civic building.
3. Exterior inn/pub/shop street or noble townhouse.
4. Interior civic/distribution hall.
5. Interior inn/shop/noble residence.
6. Street/public space: fountain/distribution plaza showing privileged supply abundance and formal infrastructure.

## Rossdam — fortified royal-market town

### Historical/source-backed identity
The source establishes Rossdam as a full town scene with battle/cutscene content, a king's chamber, Rorik house, multiple equipment shops, and character text. The source does **not** provide a concise socioeconomic description comparable to Moordell, so the military/royal-market visual framing below is derived from those concrete place functions rather than asserted as original narrative fact.

### Locked source-backed named/specific places
- King's chamber — `Art/rossdam-king-chamber.tmx`
- Rorik house — `Art/rossdam-rorik-house.tmx`
- Armor shop — `Art/rossdam-armor-shop.tmx`
- Magic shop — `Art/rossdam-magic-shop.tmx`
- Weapon shop — `Art/rossdam-weapon-shop.tmx`
- Henry quest source — `Art/rossdam-henry-give-quest.txt`, `Art/rossdam-henry-quest-complete.txt`
- Battle/civic conflict — `Art/rossdam-battle-start.txt`, `rossdam-battle-end.txt`, `Code/ArrestedRossdam.m`

### Derived Project North Star art direction
- **Theme:** defensible royal-market town where authority, arms trade, and commerce overlap.
- **Architecture:** red-brown stone/brick, dark half timber, iron-braced gates, crenellated or towered civic compound, dense shop rows under covered walks.
- **Materials/palette:** iron-rich red stone/brick, soot-dark timber, charcoal roofs, oxidized copper/teal accents, cream interior plaster.
- **Economy/culture:** equipment trade and court/command presence; practical, guarded, busy rather than luxurious.
- **Terrain:** crossroads/river-adjacent or low-ridge defensive geometry is derived; use a compact wall/gate loop and strong arrival corridor without claiming an exact historical geography.
- **Signature:** royal/command compound and market gate aligned to equipment-shop street.
- **Infrastructure:** walls/gates, paved market road, covered shop frontage, guard stairs, service alleys, cart courts, civic forecourt.

### Required six-image concept set
1. Overall/elevated fortified market town with royal/command compound.
2. Exterior king/command civic complex or Rorik house.
3. Exterior weapon/armor/magic shop row.
4. Interior king's chamber/command hall.
5. Interior weapon/armor/magic shop.
6. Street/public space: fortified market gate and equipment-trade street with carts/covered walks.

## Orc Village / Orc-Town — communal forge-and-feast settlement

### Historical/source-backed identity
The project packages `Art/orc-village.tmx`, a dedicated `orc-village` group with armor/weapon/magic shops and pub, an `Orc-Town` cutscene group, character text, and `orc-village-feast.txt`. Those are direct settlement facts. No source evidence justifies depicting the settlement as crude, savage, or disposable.

### Locked source-backed named/specific places
- Main village — `Art/orc-village.tmx`
- Armor shop — `Art/orc-village-armor-shop.tmx`
- Weapon shop — `Art/orc-village-weapon-shop.tmx`
- Magic shop — `Art/orc-village-magic-shop.tmx`
- Pub — `Art/orc-village-pub.tmx`
- Feast gathering/event — `Art/orc-village-feast.txt`
- Character text and battle text — `Art/orc-village-character-text.txt`, `orc-town-battle-start.txt`, `orc-town-battle-end.txt`

### Derived Project North Star art direction
- **Theme:** confident clan-built community organized around craft, shared meals, protection, and hospitality.
- **Architecture:** massive timber frames, dark basalt/rubble plinths, broad low roofs, smoke vents, carved structural posts, heavy cloth awnings; robust rather than ramshackle.
- **Materials/palette:** basalt/charcoal stone, smoke-browned timber, forged black iron, ochre clay, deep moss green, rust red and woven geometric textiles.
- **Economy/culture:** smithing/equipment trade, communal pub/feast culture, strong craft identity.
- **Terrain:** rocky warm upland or volcanic-edge visual cues are derived; use exposed stone and tough vegetation without making lava mandatory.
- **Signature:** central feast circle/longhall + forge smoke stacks; shop fronts read as workshops.
- **Infrastructure:** timber palisade/stone gate, hard-packed roads, drainage channels, forge yards, communal feast court, loading racks, wells/cisterns.

### Required six-image concept set
1. Overall/elevated craft settlement centered on longhall/feast court and forge roofs.
2. Exterior armor/weapon workshop.
3. Exterior magic shop/pub/longhall.
4. Interior forge/armor or weapon shop.
5. Interior pub/feast hall or magic shop.
6. Street/public space: feast court / workshop street with gate, drainage, loading and communal infrastructure.

## Fairy Village — luminous woodland tree-village

### Historical/source-backed identity
The source packages `Art/fairy-village.tmx`, a treehouse, inn, pub, accessory/magic/weapon shops, Mary and Rita houses, village cave, village background art, character text, and wizard-battle text. Treehouse + cave + named residences are direct anchors for a woodland-integrated settlement.

### Locked source-backed named/specific places
- Main village — `Art/fairy-village.tmx`
- Treehouse — `Art/fairy-village-treehouse.tmx`
- Inn — `Art/fairy-village-inn.tmx`
- Pub — `Art/fairy-village-pub.tmx`
- Accessory shop — `Art/fairy-village-accessory-shop.tmx`
- Magic shop — `Art/fairy-village-magic-shop.tmx`
- Weapon shop — `Art/fairy-village-weapon-shop.tmx`
- Mary house — `Art/fairy-village-mary-house.tmx`
- Rita house — `Art/fairy-village-rita-house.tmx`
- Village cave — `Art/fairy-village-cave.tmx`
- Background reference — `Art/fairy-village-bg.jpg`, `Art/fairy-village.png`

### Derived Project North Star art direction
- **Theme:** airy woodland settlement woven through ancient trunks, roots, flowers, and glowing mineral/glass details; enchanting but physically buildable.
- **Architecture:** curved laminated timber, root-hugging platforms, leaf-like roof shells, slender bridges, round/arched windows, occasional translucent crystal/glass panels.
- **Materials/palette:** pale warm wood, moss/fern green, turquoise/teal, lilac and flower accents, milky crystal, warm golden practical lights.
- **Economy/culture:** craft/magic/accessory trade, hospitality, woodland stewardship.
- **Terrain:** multi-level giant-tree grove with root terraces and a cave edge; vertical circulation is a defining feature.
- **Signature:** inhabited giant treehouse cluster linked by graceful bridges, with ground-level cave and market clearings.
- **Infrastructure:** elevated bridges/walkways, root stairs, rainwater channels, lantern paths, small market clearing, cave approach, ground-to-canopy lifts/ramps only where physically plausible.

### Required six-image concept set
1. Overall/elevated multi-level tree-village and root/cave topology.
2. Exterior treehouse or Mary/Rita house.
3. Exterior magic/accessory shop or inn.
4. Interior treehouse/residence.
5. Interior magic/accessory shop/inn/pub.
6. Street/public space: bridge-linked market clearing / root plaza showing vertical circulation and water/lantern infrastructure.

## Cambridge — canal-and-dock water town

### Source-backed coordinator requirement
Cambridge is explicitly required by this SceneIssue to remain immediately legible as a **water town** through docks, waterfront architecture, bridges/water crossings, and water-oriented public infrastructure. No original connected-repository Cambridge building file was located; therefore there are no falsely claimed original named buildings below.

### Locked coordinator-backed requirements
- Docks / working waterfront.
- Waterfront architecture.
- Bridges or other water crossings.
- Water-oriented public infrastructure.
- Town identity must read as water-centric at first glance.

### Derived Project North Star named roles for concept consistency
These are new production art-direction anchors, not claims about the original game's named map files:
- **Harbor Market Hall** — covered market opening directly to quay and boats.
- **Boatyard / Boatwright Shed** — timber slipway, repair gantry, sail/rope storage.
- **Canal Inn** — public house spanning or turning a canal corner with water-level service door.
- **Tidehouse / Cistern Court** — civic water-management building and public basin/gauge.
- **Bridge Gate** — recognizable arched crossing marking the civic center.

### Derived Project North Star art direction
- **Theme:** bright, industrious canal town whose streets and public life are inseparable from water.
- **Architecture:** cream/blue plaster over stone quay walls, dark timber galleries, steep tiled roofs, boat doors, arcaded waterfront shopfronts, frequent balconies and cranes.
- **Materials/palette:** pale limestone, weathered oak, cobalt/aqua paint, oxidized copper, warm terracotta, sailcloth cream, sparkling blue-green water.
- **Economy/culture:** boat repair, fishing/trade, market exchange, rope/sail work, water transport.
- **Terrain:** canals/inlets define blocks; stone quays, timber piles and low stepped landings replace generic roadside edges.
- **Signature:** layered arched bridges and a dense harbor market silhouette reflected in water.
- **Infrastructure:** docks, slips, cranes, bollards, stairs to water, bridges, culverts, storm drains, cistern/fountain court, quay markets, towpaths, flood/tide markers.

### Required six-image concept set
1. Overall/elevated canal-and-harbor town with bridges, docks, market, boatyard, and civic water court.
2. Exterior Harbor Market Hall / Tidehouse civic waterfront.
3. Exterior Boatyard / Canal Inn.
4. Interior Harbor Market Hall / Tidehouse.
5. Interior Boatyard / Canal Inn.
6. Street/public space: bridge/quay/cistern plaza showing docks, crossings, drains, steps and water-service infrastructure.

---

# 5. Game-wide comparison index

| Town | Silhouette / topology | Core materials | Palette | Economic/cultural read | Signature infrastructure / landmark |
|---|---|---|---|---|---|
| Kentridge | Organic foothill lanes, modest gables | fieldstone, warm timber, plaster, terracotta | cream, moss, ochre, faded blue | resilient working/craft town under supply pressure | central well + supply market + warehouse edge |
| Hightown | Stepped terraces, tall church | pale stone, slate, silvered timber | chalk, charcoal, blue, plum | communal/religious hill town under supply pressure | church forecourt + stairs/retaining walls + under-church threshold |
| Moordell | Broad planned avenues, formal facades | limestone, dark timber, slate/tile, brass | cream, burgundy, gold, evergreen | nobles/imperial favor, abundant supply | formal distribution/fountain plaza + estate gates |
| Rossdam | Compact walls/gates, dense shop street | red stone/brick, dark timber, iron, copper | rust red, charcoal, teal, cream | royal/command presence + equipment trade | market gate + royal/command compound |
| Orc Village | Broad low roofs, heavy frames, feast core | basalt, massive timber, forged iron, woven cloth | charcoal, ochre, moss, rust red | craft/smithing + communal feast culture | longhall/feast court + workshop/forge yards |
| Fairy Village | Vertical tree platforms, curved bridges | pale wood, living roots, crystal/glass | fern, teal, lilac, gold | magical craft + woodland stewardship | treehouse cluster + canopy bridges + root/cave plaza |
| Cambridge | Canal blocks, bridge arches, harbor roofscape | limestone quay, oak, plaster, copper | cream, cobalt, aqua, terracotta | boat/fishing/trade water economy | docks + arched crossings + market/tide-cistern court |

## Fast anti-drift test for downstream work
A grayscale silhouette strip with labels removed should still distinguish all seven:
- Kentridge = low organic working-town cluster around a well.
- Hightown = steep stair terraces and church tower.
- Moordell = formal avenue/plaza and noble facades.
- Rossdam = gate/wall + royal/market axis.
- Orc Village = broad heavy timber longhall/forge settlement.
- Fairy Village = vertical tree platforms and curved bridges.
- Cambridge = water channels, quays, docks and repeated bridge arches.

If a future asset cannot pass that test, it has drifted toward generic town art and should be revised before it becomes source material.

# 6. Concept production rules

Every town receives six **separate** environment concept images, not one six-panel sheet:
- `01-overall-elevated.png`
- `02-exterior-signature.png`
- `03-exterior-secondary.png`
- `04-interior-signature.png`
- `05-interior-secondary.png`
- `06-street-public-space.png`

Concepts must:
- contain no character concepting or gameplay UI;
- be original, stylized, readable, non-photoreal, and production-reference quality;
- depict the locked source-backed place/function where one exists;
- label newly authored choices only in the catalog/brief, not by adding fake historical signage to an image;
- show believable construction and circulation;
- carry the shared Project North Star language while preserving the town-specific silhouette, materials, palette, culture/economy, terrain, and signature infrastructure defined above.
