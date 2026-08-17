# WorldBuilder Decoration Master Manifest

Branch: `agent/worldbuilding-decorations`
Region art direction: `docs/WORLDBUILDER_REGION_DECORATION_GUIDE.md`

This file is the canonical stable content identity manifest for the procedural decoration library.

## Scope and rules

The game is a **fantasy RPG in a magical world**. Content should support castles, villages, inns, shops, farms, temples, wizard towers, magical schools, monster lairs, caves, crypts, dungeons, ruins, forests, roads, guilds, markets, docks, camps, and other fantasy spaces. Dedicated modern-building, industrial-modern, or military-infrastructure packs are out of scope. Ordinary fantasy guards, weapons, armor, fortifications, adventurers, and martial equipment remain valid when they naturally belong in the setting.

Stable IDs are append-only. Once an ID ships or is used by persistence/test content, its semantic identity must not be silently reassigned.

Rendering is intentionally independent from semantic identity. **Use boxes/voxel assemblies first when that is faster. Use smooth/curved/procedural primitives immediately when they are no harder.** High-visibility props can later move from box assembly to curved/SDF/procedural geometry without changing stable ID, placement semantics, persistence identity, or scene composition.

Region identity changes selection weights, material/presentation style, scale and clutter—not stable semantic identity. The same `AlchemyTable`, `QuestBoard` or `MarketStall` should render and compose differently in Kentridge, Moordell, Fairy Village or Orc Village without consuming duplicate IDs solely for recoloring.

Status terms:

- **implemented** — stable semantic ID and recipe exist in source;
- **reserved** — exact stable identity is documented here but source recipe is not implemented yet.

## Implemented IDs 1–300

### 1–42 — foundational world content

1 Anvil; 2 Bellows; 3 ForgeHearth; 4 Grindstone; 5 QuenchTub; 6 SmithToolBoard; 7 BarCounter; 8 KegRack; 9 MugRack; 10 ServingShelf; 11 FirewoodStack; 12 GameTable; 13 Sarcophagus; 14 Coffin; 15 OssuaryShelf; 16 FuneralBier; 17 UrnStand; 18 GraveMarker; 19 MarketStall; 20 HangingScale; 21 BasketStack; 22 MerchantSign; 23 ProduceStand; 24 FabricCanopy; 25 Manger; 26 HayBale; 27 SaddleRack; 28 WaterTrough; 29 HitchingPost; 30 TackHooks; 31 Shackles; 32 Stocks; 33 IronCage; 34 KeyBoard; 35 PrisonBucket; 36 RestraintBench; 37 NoticeBoard; 38 Well; 39 Fountain; 40 LampPost; 41 PublicTrough; 42 Handcart.

### 43–60 — carpentry and wheelwright

43 CarpenterBench; 44 SawHorse; 45 LumberStack; 46 PlankRack; 47 ToolChest; 48 ChiselBoard; 49 PlaneRack; 50 ClampRack; 51 WoodScrapBasket; 52 Lathe; 53 WheelwrightJig; 54 WheelStack; 55 RepairTrestle; 56 MeasuringBoard; 57 GluePotStation; 58 MalletShelf; 59 DowelBin; 60 ShavingPile.

### 61–84 — textile, leather, pottery, craft

61 Loom; 62 SpinningWheel; 63 YarnBasket; 64 SpindleRack; 65 DyeVat; 66 DryingLine; 67 FoldedClothStack; 68 BoltRack; 69 CuttingTable; 70 DressForm; 71 LeatherStretchingFrame; 72 HideRack; 73 TanningTub; 74 BootmakerBench; 75 PotteryWheel; 76 Kiln; 77 ClayBin; 78 DryingShelf; 79 AmphoraRack; 80 GlazeJarRack; 81 BasketWeavingFrame; 82 WickerStack; 83 SewingStool; 84 LeatherToolBoard.

### 85–114 — kitchen, bakery, brewery, winery, pantry

85 PrepTable; 86 ButcherBlock; 87 HangingPotRack; 88 PanRack; 89 CauldronStand; 90 BreadOven; 91 RoastingSpit; 92 WashSink; 93 WaterBarrel; 94 FlourBin; 95 GrainSackStack; 96 SpiceShelf; 97 HerbDryingRack; 98 MeatHookRail; 99 CheeseShelf; 100 BreadCoolingRack; 101 PantryCabinet; 102 VegetableBasket; 103 FishCrate; 104 BreweryVat; 105 MashTun; 106 Fermenter; 107 WinePress; 108 BottleRack; 109 CaskStand; 110 PieRack; 111 SausageRack; 112 FoodPrepShelf; 113 KettleStand; 114 CellarCaskStack.

### 115–144 — alchemy, wizardry, occult, observatory

115 AlchemyTable; 116 AlembicStand; 117 RetortRack; 118 MortarStation; 119 IngredientCabinet; 120 HerbariumShelf; 121 CrystalStand; 122 RuneTable; 123 ScryingBasin; 124 AstrolabeStand; 125 TelescopeTripod; 126 Orrery; 127 SpellbookLectern; 128 ScrollRack; 129 WandRack; 130 StaffStand; 131 PotionShelf; 132 ReagentChest; 133 DistillationFurnace; 134 ArcaneBrazier; 135 SummoningCircle; 136 RitualPedestal; 137 CandleCluster; 138 SkullReliquary; 139 SpecimenJarRack; 140 SpecimenCage; 141 EnchantingAnvil; 142 ManaCrystalCluster; 143 ChalkRuneBoard; 144 StarChart.

### 145–168 — graveyard, crypt, funerary

145 TombSlab; 146 GraveStone; 147 GraveCross; 148 GraveFence; 149 MausoleumDoor; 150 OssuaryNiche; 151 BonePile; 152 SkullStack; 153 BurialUrn; 154 OfferingBowl; 155 MourningBench; 156 FuneralCandleStand; 157 IncenseBrazier; 158 ShroudRack; 159 GraveDiggerTools; 160 SoilMound; 161 BrokenHeadstone; 162 MemorialPlaque; 163 CryptGate; 164 CorpseCart; 165 FlowerOffering; 166 CatacombShelf; 167 BurialChest; 168 ReliquaryCasket.

### 169–200 — farmyard, garden, street, civic exterior

169 FarmFence; 170 FarmGate; 171 Scarecrow; 172 Haystack; 173 GrainSilo; 174 FeedBin; 175 ChickenCoop; 176 RabbitHutch; 177 Beehive; 178 CompostPile; 179 Wheelbarrow; 180 Plow; 181 Harrow; 182 SeedChest; 183 WaterPump; 184 RainBarrel; 185 Clothesline; 186 WashTub; 187 GardenBench; 188 FlowerPlanter; 189 HedgeSection; 190 Trellis; 191 Arbor; 192 Statue; 193 Sundial; 194 StreetBench; 195 Bollard; 196 Signpost; 197 Milestone; 198 TrashHeap; 199 FirewoodPile; 200 WateringCanRack.

### 201–220 — fantasy merchants and shops

201 JewelerBench; 202 GemDisplayCase; 203 CoinScale; 204 Lockbox; 205 ClothMerchantTable; 206 ShoeDisplayRack; 207 WeaponMerchantRack; 208 ArmorMerchantStand; 209 BookMerchantShelf; 210 ScrollDisplay; 211 ApothecaryCounter; 212 HerbDrawerCabinet; 213 PotionDisplayCase; 214 GeneralStoreCounter; 215 SackDisplay; 216 ProduceBasketStand; 217 ButcherDisplay; 218 FishmongerSlab; 219 ShopSignHanging; 220 AwningStriped.

### 221–240 — enchanted workshop and magical household

221 EnchantersWorkbench; 222 RuneCarvingTable; 223 WandmakersBench; 224 StaffmakersRack; 225 CrystalCabinet; 226 EnchantedWeaponStand; 227 EnchantedArmorStand; 228 SpellScrollCabinet; 229 FamiliarPerch; 230 FamiliarNest; 231 FloatingBookStand; 232 LevitationPedestal; 233 MagicMirror; 234 DivinationTable; 235 ElementalBrazier; 236 ManaFont; 237 PortalKeystone; 238 WardTotem; 239 FairyLanternCluster; 240 EnchantedPlantStand.

### 241–260 — lived-in and noble interior

241 Wardrobe; 242 VanityTable; 243 WashBasinStand; 244 ChamberPot; 245 FoldingScreen; 246 WritingDesk; 247 SideTable; 248 Footstool; 249 Settee; 250 Chaise; 251 GrandMirror; 252 Candelabra; 253 MusicStand; 254 LuteRack; 255 Harp; 256 Harpsichord; 257 TrophyCase; 258 WineCabinet; 259 JewelryCasket; 260 PerfumeTray.

### 261–280 — monster lairs and creature occupation

261 MonsterNest; 262 EggClutch; 263 CocoonBundle; 264 GiantWebSheet; 265 WebbedVictim; 266 BoneTotem; 267 TrophySkullPile; 268 GnawedBonePile; 269 ClawMarkedPost; 270 ScentMarkerTotem; 271 SlimePool; 272 SlimeTrailPatch; 273 AcidPool; 274 MoltedShellPile; 275 ShedScalePile; 276 BeastBedding; 277 BurrowMound; 278 HoardScrapPile; 279 MonsterFoodCache; 280 ChainedPreyCage.

### 281–300 — adventurer guild, questing, caravan

281 QuestBoard; 282 BountyBoard; 283 GuildRegistryDesk; 284 AdventurerMapTable; 285 ExpeditionSupplyRack; 286 PotionSatchelRack; 287 BedrollRack; 288 RopeGearRack; 289 LanternGearRack; 290 GuildTrophyWall; 291 MonsterContractBoard; 292 PartyNoticeBoard; 293 GuildStrongbox; 294 MemberLockerBank; 295 TrainingManualShelf; 296 CartographersDesk; 297 CaravanSupplyCrate; 298 PackSaddleStand; 299 TravelCharmDisplay; 300 WaystoneAttunementPedestal.

## Reserved fantasy IDs 301–400

These names are the next canonical content identities. Source implementation may arrive pack-by-pack, but these IDs should not be reused for unrelated concepts.

### 301–320 — magical nature, fae, enchanted forest

301 GlowingMushroomCluster; 302 GiantMushroomSeat; 303 ManaBlossom; 304 CrystalFlowerPatch; 305 EnchantedVineCluster; 306 LivingRootArch; 307 FairyRing; 308 FairyHouseNook; 309 SpiritLanternPlant; 310 WhisperingStone; 311 RuneStoneCircle; 312 Moonwell; 313 SunCrystalBloom; 314 FloatingSeedCluster; 315 WispNest; 316 EnchantedTreeShrine; 317 DruidStoneAltar; 318 HerbalistWildPatch; 319 MagicalPondLilies; 320 PetrifiedMagicTree.

### 321–340 — fantasy traps, dungeon mechanisms, puzzles

321 StonePressurePlate; 322 RunePressurePlate; 323 DartSlit; 324 SpikeFloorPanel; 325 FlameJetNozzle; 326 PoisonVent; 327 SwingingBladePivot; 328 PendulumAxeMount; 329 FallingBlockTrigger; 330 PortcullisWinch; 331 ChainWinch; 332 PuzzleLeverPedestal; 333 RotatingStatuePedestal; 334 RuneDial; 335 GemSocketPuzzle; 336 FloorTilePuzzle; 337 MirrorPuzzleStand; 338 MagicSealDoor; 339 WardEmitterPillar; 340 TreasureTrapChest.

### 341–360 — temples, shrines, sacred magic

341 SacredAltar; 342 SideShrine; 343 PrayerBench; 344 Kneeler; 345 VotiveCandleStand; 346 HolyWaterFont; 347 OfferingChest; 348 RelicPedestal; 349 ReliquaryShrine; 350 SacredLectern; 351 ScriptureStand; 352 IncenseStand; 353 RitualBasin; 354 ShrineBell; 355 SacredBannerStand; 356 ProcessionalStaffRack; 357 PilgrimTokenBoard; 358 BlessingBrazier; 359 SacredCurtain; 360 DivineCrystalFocus.

### 361–380 — wizard school, magical library, scholar spaces

361 FloatingBookshelf; 362 EnchantedLectern; 363 StudentSpellDesk; 364 ApprenticeAlchemyDesk; 365 RunePracticeBoard; 366 SpellTargetDummy; 367 WandPracticeRack; 368 FamiliarStudyPerch; 369 MagicalGlobe; 370 ConstellationProjector; 371 AnimatedMapTable; 372 ForbiddenBookCage; 373 ChainedTomeStand; 374 ScrollSortingRack; 375 QuillAndInkStation; 376 ScriptoriumDesk; 377 ArcaneArchiveChest; 378 MagicalSpecimenCabinet; 379 PortalLessonFrame; 380 FacultyResearchDesk.

### 381–400 — cursed ruins, magical corruption, haunted aftermath

381 BrokenPortalFrame; 382 CrackedManaCrystal; 383 ArcaneScorchPatch; 384 CorruptionGrowth; 385 CursedVineCluster; 386 HauntedMirror; 387 SpectralCandleCluster; 388 FloatingDebrisCluster; 389 CursedChainBundle; 390 PetrifiedAdventurer; 391 PetrifiedMonster; 392 AbandonedRitualCircle; 393 BrokenRunePillar; 394 ShatteredMagicStatue; 395 CollapsedSpellShelf; 396 PossessedFurniture; 397 ShadowNest; 398 EctoplasmPool; 399 SealedCursedChest; 400 AncientMagicSeal.

## Scene roadmap tied to the 301–400 block

Enchanted grove, fairy clearing, druid shrine, trap corridor, puzzle chamber, treasure vault, village shrine, grand temple, magical school classroom, wizard library, forbidden archive, cursed laboratory, haunted manor room, corrupted ruin chamber.

## Rendering maturity policy

Every implemented archetype must have a path to visible output. That path may begin as a box/voxel assembly. Use the existing curved/SDF/procedural primitives when they are equally straightforward or materially improve a signature silhouette. Examples that should eventually prefer curved/procedural treatment include wheels, cauldrons, urns, bowls, barrels, fountains, wells, cushions, portals, magical circles, organic roots, monster eggs, mushrooms, crystals, chains, and ornate magical furniture. Rectilinear shelves, tables, crates, racks, timber frames, slabs, boards, and signs can remain crisp/box-based where visually appropriate.

The content catalog and task list should link back to this manifest. When source adds a reserved ID, change only its status from reserved to implemented; do not rename/reassign the number casually.
