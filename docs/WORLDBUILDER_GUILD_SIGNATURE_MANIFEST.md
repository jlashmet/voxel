# WorldBuilder Guild Signature Manifest

Branch: `agent/worldbuilding-decorations`
Parent manifest: `docs/WORLDBUILDER_DECORATION_MASTER_MANIFEST.md`

This file is the append-only canonical extension for guild-signature decoration identities. IDs 1-400 remain defined by the parent master manifest. IDs 401-440 below are now **implemented in source**, with deterministic recipes, presentation backends, room-layer placement and regression source.

## Implemented IDs 401-440

401 GuildCrestPlaque; 402 HangingGuildSign; 403 GuildmasterChair; 404 GuildmasterDesk; 405 MembershipRosterBoard; 406 InitiationPedestal; 407 OathStone; 408 KnightOathBanner; 409 ArmorMaintenanceRack; 410 TournamentShieldWall; 411 AssassinTargetSilhouette; 412 PoisonLockCabinet; 413 ConcealedWeaponPanel; 414 CodedContractBoard; 415 DruidSeedShrine; 416 AnimalTotemPole; 417 LivingRootSeat; 418 HerbDryingTree; 419 LockPracticeBoard; 420 StolenGoodsSortingTable; 421 ConcealedFloorCache; 422 HealerCot; 423 MedicineScreen; 424 BlessingTable; 425 RangerBowyerStation; 426 FletchingBench; 427 HuntingMapWall; 428 BardStageRiser; 429 InstrumentCabinet; 430 SongBoard; 431 CostumeTrunk; 432 AlchemistFumeHood; 433 ReagentSortingWheel; 434 UnstableExperimentCage; 435 WizardGuildSeal; 436 SpellRankBoard; 437 FamiliarFeedingStation; 438 AdventurerPartyTable; 439 TrophyMonsterMount; 440 GuildDonationChest.

## Rendering policy

The same breadth-first rule applies as IDs 1-400. Rectilinear guild furniture/signage can start as box or thin-surface geometry. Organic totems/root seats, trophy mounts, reagent wheels and similar signature silhouettes may use procedural-mesh requests and can be upgraded later without changing stable ID or persistence identity.

## Composition policy

Guild signature props are a sparse additive layer over ordinary room scenes. A Knights Order still reuses armory/shrine/common-room content, then gains oath stones, knight banners and tournament displays. An Assassins Guild still reuses contract/alchemy/vault content, then gains coded boards, poison cabinets and concealed weapon panels. This keeps the global library reusable while making each institution recognizable.
