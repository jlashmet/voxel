# WorldBuilder Fantasy Guild Houses

Branch: `agent/worldbuilding-decorations`

Guild houses are building-scale compositions layered over the stable decoration library. They should feel like institutions with recognizable public/private spaces, not ordinary houses containing a themed prop pile.

## Initial guild set

The first source program catalog contains ten guild identities:

1. Adventurers Guild
2. Wizards Guild
3. Knights Order / Fighters Guild
4. Assassins Guild
5. Druids Circle / Lodge
6. Thieves Guild
7. Clerics / Healers Order
8. Rangers / Hunters Lodge
9. Bards College
10. Alchemists Guild

Future guilds can include necromancers, monster hunters, artificers/enchanters, merchants, sailors, miners, masons/builders, scribes/cartographers, beast tamers, summoners, elemental orders and region-specific magical societies.

## Building identity

A guild house is described as a room program. Required rooms establish identity; optional rooms scale with building footprint, wealth, settlement and seed. Existing stable decoration IDs are reused wherever possible so region/material presentation can vary without creating duplicate semantic IDs.

### Adventurers Guild

Public contract hall with quest/bounty boards, registry desk and map table; common hall; trophy wall; strongbox/vault; optional dormitory, kitchen and stable/caravan yard. It should feel busy, practical and full of evidence of completed expeditions.

### Wizards Guild

Library/archive, enchanting workshop, ritual chamber, spell-practice classroom, guildmaster research office, optional magical vault and forbidden archive. Verticality, floating/enchanted details, crystal light and dense scholarship should distinguish it from an ordinary library.

### Knights Order

Formal common/order hall, equipment/training room, oath shrine, trophy/heraldry hall, optional command office and stable. This is fantasy chivalric content rather than a modern military facility: armor stands, weapon racks, banners, oath relics, ceremonial seating and horse tack.

### Assassins Guild

Hidden contract room, poison/alchemy workshop, target/training room, concealed vault, optional infirmary. The public facade may be deliberately mundane; the identity lives in hidden rooms, narrow passages, concealed storage, dark materials and sparse controlled lighting.

### Druids Circle / Lodge

Living garden/courtyard, tree or stone shrine, ritual circle, herb workshop, optional mushroom/common grove and hidden spirit nook. Prefer organic architecture, roots, vines, stones, moonwell/wisp content and outdoor-to-indoor continuity.

### Thieves Guild

Common den, contract board, hidden treasury, lock/tool workshop, optional dormitory. Favor concealed doors, low ceilings/alcoves, stolen mixed-quality furnishings, lockboxes, maps, ropes and escape-route clutter rather than formal institutional presentation.

### Clerics / Healers Order

Shrine/chapel, infirmary, scripture/library room, common/prayer hall and optional reliquary vault. Sacred presentation should vary strongly by region and faith while retaining the same functional room program.

### Rangers / Hunters Lodge

Rustic common hall, gear workshop, trophy wall, stable/animal yard, optional bunk room and herb garden. Hunting trophies, saddles, ropes, lanterns, field supplies and natural materials dominate.

### Bards College

Performance/rehearsal hall, social common room, song/script library, optional guildmaster office and dormitory. Instruments, music stands, seating, posters/notices and richer textiles provide immediate silhouette/color identity.

### Alchemists Guild

Large laboratory/workshop, ingredient/reference library, reagent vault, common/prep room, optional guildmaster study and dangerous hidden ritual/experimental room. It should feel more practical/chemical than a Wizards Guild even when both use magic.

## Region behavior

Guild identity and settlement identity are independent axes. Do not create `MoordellWizardDesk`, `KentridgeWizardDesk`, etc. A Wizards Guild in Moordell can be wealthy, polished and crystal-heavy; one in Kentridge can use rough timber, practical shelves and fewer luxury details; Fairy Village can lean organic/fae; Orc Village can use heavier carved forms and shamanic/rune presentation.

Some guilds should be more common in particular places, but the content system should not hard-ban unusual combinations unless world lore requires it.

## Building-scale generation plan

1. Select guild kind, settlement region, wealth, condition and seed.
2. Choose a shell/footprint suitable for the minimum room count.
3. Allocate required rooms first.
4. Allocate optional rooms according to remaining footprint and program weights.
5. Connect public rooms to the entrance; keep secretive rooms deeper and permit hidden access for Assassin/Thieves/forbidden Wizard content.
6. Convert each room to `DecorationSpace` and resolve its semantic prop composition using the existing socket/exclusion pipeline.
7. Apply region presentation after semantic identity is fixed.
8. Add exterior identity: sign/heraldry, entry treatment, garden/stable/yard where appropriate.
9. Preserve stable generated IDs so looted/destroyed/moved guild contents persist as deltas.

## Next guild-specific archetype block

The current guild programs deliberately reuse IDs 1-400. A later append-only block should add signature pieces only where the existing library cannot express the identity well. Candidate IDs 401+ include:

- guild crest plaque / hanging guild sign
- guildmaster chair and guildmaster desk variants
- oath stone and knight oath banner
- armor maintenance rack and tournament shield wall
- assassin target silhouette, poison lock cabinet, concealed weapon panel, coded contract board
- druid seed shrine, animal-totem pole, living-root seat, herb drying tree
- thieves lock-practice board, stolen-goods sorting table, concealed floor cache
- healer cot, medicine screen, blessing table
- ranger bowyer station, fletching bench, hunting map wall
- bard stage riser, instrument cabinet, song-board, costume trunk
- alchemist safety hood/chimney, reagent sorting wheel, unstable experiment cage

These should be added only when they materially improve visual identity; existing generic objects should continue to be reused otherwise.

## Source status

`GuildHouseProgramCatalog` implements the ten initial building programs and maps each room to existing canonical decoration IDs. NUnit regression source verifies every program is structurally valid, has multiple required identity rooms and references only IDs in the canonical 1-400 range.

The next implementation milestone is a deterministic guild-house shell/room allocator plus a representative Wizards Guild and Druids Lodge end-to-end authoring fixture.
