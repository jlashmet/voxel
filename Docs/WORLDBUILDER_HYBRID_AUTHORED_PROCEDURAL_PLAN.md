# WorldBuilder Hybrid Authored + Procedural Generation Plan

**Status:** architecture direction / implementation not started  
**Baseline:** `master` at `71e5b6b146cb7dd3b7da0305d0ab42bcc9cea22e`  
**Related design review:** `docs/game-systems-checklist` (reviewed for compatibility; do not merge from it here)

## Goal

WorldBuilder must support one continuum from heavily authored places to fully procedural ones.

Kentridge is the canonical authored-anchor case: its identity, important sites, named characters, main quests, story/cutscene requirements, and other intentional facts are prescribed. The generator is still free to solve placement, circulation, ordinary buildings, incidental population, decoration, secrets, and other unspecified detail.

A disposable village is the opposite end of the same system: content may specify only settlement archetype, biome/region, population range, route access, seed, and generation policy. WorldBuilder fills its sites, services, ordinary NPCs, optional quests, and physical layout procedurally.

There must not be separate authored/procedural town runtimes or an `Authored/Procedural/Hybrid` mode switch. The distinction is only **how many semantic facts are constrained before generation**.

## Required architecture

Use one pipeline:

`authored requirements + generation policy + seed`

→ **procedural semantic expansion**

→ resolved semantic world definition

→ existing `BlueprintCompiler` / planning contracts

→ site/world resolution

→ physical voxel realization

Procedural expansion must produce the same ordinary semantic definitions used by authored content. Downstream gameplay must not care whether a fact was authored or generated.

Examples:

- an authored Rebecca and a generated innkeeper both become normal NPC/character definitions and ultimately `CharacterId` bindings;
- an authored main quest and a generated side quest both use the normal Quest/progression definitions and runtime;
- an authored weapon shop and a generated blacksmith both become semantic settlement services bound to ordinary generated sites;
- Inventory owns authoritative stock/transactions; WorldBuilder may request/configure a merchant/service but must not become an inventory runtime;
- Story owns campaign consequences; WorldBuilder may produce optional generated content facts but must not become a second Story runtime.

## Current foundation to preserve

The newer WorldBuilder API is already close to the target:

- `WorldHierarchySpecs` models regions, biomes, routes, settlement archetypes/population, route access, and nested site ownership.
- `CampaignBlueprint` / authoring handles model sites, NPCs, cutscenes, objectives, story rules, secrets, and loot using stable semantic identities.
- `SiteRef` is a semantic role while `ResolvedSiteId` is the generated site selected to fulfill it.
- site resolution already works through archetype/capability/hierarchy/reachability/distance facts rather than scene coordinates.
- `TownArchitectureProgram` is moving toward reusable residential/commercial/civic/landmark roles rather than town-specific realization identities.
- `KentridgeTownPlanner` already demonstrates partial hybrid behavior: named site roles are required while their physical placement/circulation is solved deterministically.

Do not replace these foundations with a new world-description system.

## Concrete debt to remove first

`HightownTownPlanner` exposes the main legacy coupling: a supposedly generated town still derives its slot count and structure archetypes from `KentridgeRole` / Kentridge realization assumptions. Generic settlement realization must consume semantic structure/site intent, not Kentridge role IDs.

The target physical input should be closer to:

`site/service role + structure archetype + district + architecture program + lot/spatial constraints + seed`

rather than `KentridgeRole`.

Kentridge-specific identity and story policy stay in Kentridge content; reusable settlement/structure realization stays generic.

## Semantic expansion responsibilities

The expansion stage may fill only unspecified semantic content and must honor authored hard requirements.

Expected reusable policies include:

- settlement composition/density and required service coverage;
- residential/commercial/civic/landmark site generation;
- semantic services such as inn, general goods, blacksmith, armorer, healer, etc.;
- ordinary population generation and assignment to generated sites/services;
- optional quest-template instantiation from resolved world/NPC/service facts;
- optional secrets/loot opportunities using existing semantic contracts.

Generated identities must be deterministic for the same world seed and stable inputs so persistence, networking, quest references, and NPC bindings remain valid.

## Quest, NPC, shop, and gameplay ownership

Procedural generation is a **producer of domain configuration**, never a parallel gameplay implementation.

- **Characters:** use the gameplay character runtime. `NpcRef`/generated NPC identity resolves to the same `CharacterId` model used for authored NPCs.
- **Quests/objectives:** generated quest templates instantiate the same quest/progression definitions consumed by the unified progression runtime. No procedural quest runtime.
- **Shops/services:** WorldBuilder owns the semantic fact that a settlement/site provides a service and may generate its operator/content configuration. Inventory/economy owns authoritative stock and transactions.
- **Encounters:** generated or authored encounter intent resolves through the same WorldBuilder → composition → encounter binding seam.
- **Story:** authored campaign-critical sequencing stays authored. Procedural side content may emit normal semantic definitions/facts but cannot mutate Story/Quest state directly.

## Gameplay-plan alignment

The `docs/game-systems-checklist` branch is directionally compatible:

- the checklist explicitly prefers generalizing existing systems, semantic/configuration-driven APIs, and keeping place/campaign policy in composition;
- system 03 requires one character runtime for players/NPCs/recruits/enemies, which is the correct destination for generated NPCs;
- system 09 keeps inventory ownership generic and semantic, which cleanly supports generated merchants without moving stock authority into WorldBuilder;
- system 11 says gameplay reports facts, progression evaluates goals, and Story decides consequences; generated quests should enter at the definition/content side of that same runtime;
- system 12 already separates authored semantic site/NPC identity from generated realization and forbids duplicate placement logic;
- system 14 composes `realized world + campaign content` into the normal runtime graph, which should remain true after semantic expansion;
- system 26 correctly keeps geography separate from campaign progression and keeps campaign-critical sequencing in authored content.

Two clarifications are required when these plans are integrated:

1. Systems 11 and 26 list procedural quest generation as out of scope. Preserve that scope boundary, but explicitly state that future procedural quest generation is a content-definition producer feeding system 11, not a replacement progression engine.
2. System 14 currently describes `CampaignRuntime` as responsible for progression/story/quests/cutscenes. Its final wording should keep orchestration/dispatch distinct from the authoritative state and rules owned by Quest/Progression, Story, Cutscenes, and other domains.

## Implementation sequence

1. Remove Kentridge-role leakage from generic settlement/structure realization and prove Hightown/another independent town can generate without `KentridgeRole`.
2. Introduce a semantic settlement/service composition model that can express both required roles and generator-fill policies.
3. Add deterministic procedural semantic expansion before compilation/resolution; authored requirements are immutable constraints and generated additions use stable seed-derived identities.
4. Add population/NPC generation that feeds the normal character/NPC placement boundaries.
5. Add service/shop generation that delegates authoritative inventories to Inventory.
6. Add quest-template instantiation that produces normal unified-progression definitions; keep campaign-critical Kentridge quests/story authored.
7. Prove three consumers: heavily authored Kentridge + procedural filler, partially prescribed Hightown, and a sparse fully generated settlement.

## Acceptance

- Kentridge can prescribe named characters/sites/main progression while unspecified town content remains procedural.
- A sparse settlement definition can produce a deterministic playable town with sites, services, NPCs, and optional quest content without Kentridge-specific identities.
- Both paths converge to the same semantic/runtime contracts after expansion.
- No duplicate Quest, Character, Inventory, Encounter, Story, structure, or placement runtime is introduced.
- No reusable settlement realization depends on `KentridgeRole` or another campaign-specific identity.
- Same seed + same authored requirements + same generation policy yields stable semantic identities and equivalent resolved content.
- Authored hard requirements cannot be silently replaced or weakened by procedural filling.

## Working hypotheses / first discriminator

**H1:** the newer WorldBuilder semantic contracts are sufficient and the primary missing layer is semantic expansion plus removal of Kentridge-shaped realization coupling.  
**H2:** existing site/settlement contracts lack one or more semantic concepts needed to express required-vs-generated settlement content without leaking implementation policy.

First implementation discriminator: build a tiny independent sparse-settlement fixture using only current semantic contracts and a generic site/service intent. Any information that cannot be expressed without referring to Kentridge or concrete voxel realization identifies the smallest API addition required before implementing the expander.