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
Formal common/order hall, equipment/training room, oath shrine, trophy/heraldry hall, optional guildmaster office and stable. This is fantasy chivalric content rather than a modern military facility: armor stands, weapon racks, banners, oath relics, ceremonial seating and horse tack.

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

## Reserved guild-signature archetypes 401–440

These are append-only reserved identities. The first implementation should continue reusing IDs 1–400; implement these when the signature object materially improves guild readability.

401 GuildCrestPlaque; 402 HangingGuildSign; 403 GuildmasterChair; 404 GuildmasterDesk; 405 MembershipRosterBoard; 406 InitiationPedestal; 407 OathStone; 408 KnightOathBanner; 409 ArmorMaintenanceRack; 410 TournamentShieldWall; 411 AssassinTargetSilhouette; 412 PoisonLockCabinet; 413 ConcealedWeaponPanel; 414 CodedContractBoard; 415 DruidSeedShrine; 416 AnimalTotemPole; 417 LivingRootSeat; 418 HerbDryingTree; 419 LockPracticeBoard; 420 StolenGoodsSortingTable; 421 ConcealedFloorCache; 422 HealerCot; 423 MedicineScreen; 424 BlessingTable; 425 RangerBowyerStation; 426 FletchingBench; 427 HuntingMapWall; 428 BardStageRiser; 429 InstrumentCabinet; 430 SongBoard; 431 CostumeTrunk; 432 AlchemistFumeHood; 433 ReagentSortingWheel; 434 UnstableExperimentCage; 435 WizardGuildSeal; 436 SpellRankBoard; 437 FamiliarFeedingStation; 438 AdventurerPartyTable; 439 TrophyMonsterMount; 440 GuildDonationChest.

## Source status

`GuildHouseProgramCatalog` implements ten initial building programs and maps their rooms to existing canonical decoration IDs 1–400. `GuildHouseRoomSelector` deterministically chooses required/optional rooms for different shell capacities. `GuildHouseTopologyPlanner` assigns public-to-private semantic depth and marks deep Assassin/Thieves hidden/vault spaces for concealed access. NUnit regression source covers program validity, deterministic selection and topology semantics.

The next implementation milestone is spatial shell/room allocation, converting those allocated rooms into real `DecorationSpace` instances, and then representative Wizards Guild and Druids Lodge end-to-end authoring fixtures.
