# 03 Gameplay character runtime — implementation plan

**Target module:** `Assets/Game/Characters/Api` / `Runtime` (`Game.Characters.Api`, `Game.Characters.Runtime`).

**Starting SHA:** `ef5240c7b24550dab86d0ed75388d6c99a44d47b` (current `origin/master` when agent-2 began this assignment).

## API

`CharacterId`, stable character definition/role metadata needed by consumers, authoritative transform/kinematic semantic state, lifecycle state, registry/query interfaces, character-created/removed/state events. Keep player/NPC/enemy distinctions as composition/traits rather than separate runtime hierarchies.

## Runtime

1. Implement one authoritative registry and lifecycle for all gameplay characters.
2. Bind generated/world/campaign character identities to stable `CharacterId` values.
3. Move scene/bootstrap-owned actor records behind the registry.
4. Provide movement/world-query integration through existing world/collision APIs without embedding voxel implementation details in the public API.
5. Supply narrow hooks for Vitality, AI, Encounters, Sessions, replication, persistence, and cutscene actor adapters.

## T03-001 — existing actor authority inventory

The repository currently has several representations for the same conceptual gameplay characters. None is yet a single persistent gameplay-character authority.

### Player

- `Game.Composition.Kentridge.Playable.KentridgeCharacterHost` owns one `CharacterMotor` plus a nested `PlayerActor`. Authoritative position/velocity live in the motor, facing in the nested actor, and presentation visibility/transform in a GameObject. The player has no stable semantic character id; campaign/cutscene lookup identifies it only as player slot `0`.
- WorldBuilder authoring uses `PlayerSlot` (`int` index) for cutscene targets. That is an authored/session-facing slot reference, not a persistent character identity.
- `KentridgeForestBanditEncounter` independently identifies the same local player as `CombatParticipantId("kentridge-player")`.
- `ShowcaseMultiplayerSession` independently owns `_localPlayerId : ushort`, `Dictionary<ushort, CharacterMotor> _serverMotors`, and `Dictionary<ushort,uint> _connectionByPlayer`. These are networking/session identities and motor state; System 07 remains their owner. Characters should bind an external player/session identity to a `CharacterId`, not absorb connection/session semantics.

### Authored NPCs

- WorldBuilder owns semantic authored `NpcRef`/`NpcHandle` identities and placement intent. `KentridgeNpcWorldPlacementResolver` deterministically produces `ResolvedNpcWorldPlacement` (`NpcRef`, site role/site, conversation requirement, realized world point) and explicitly leaves gameplay actor creation outside WorldBuilder/WorldGen.
- `KentridgeCharacterHost` owns a scene-local `Dictionary<NpcRef,NpcActor>`. `PrepareNpcs` destroys every existing NPC GameObject, clears the dictionary, and recreates actors from resolved placements. Each nested `NpcActor` stores position plus a presentation GameObject/animation driver; there is no persistent lifecycle state or stable gameplay-character id beyond the authored `NpcRef` key.
- `IKentridgeCampaignActorHost` currently describes its implementation as owning authoritative player/NPC objects and exposes `PrepareNpcs` plus cutscene actor lookup. `KentridgeCampaignSessionBootstrap` validates authored player-slot/NpcRef bindings, calls `PrepareNpcs`, and then uses the host for cutscene resolution. This is the main Kentridge composition seam to migrate behind `Game.Characters.Api` while retaining campaign/cutscene semantics in their owners.

### Recruit / party member

- The same authored Medrare NPC is `NpcRef("medrare")` in `KnownOpeningCampaignContent`, physically represented by the Kentridge NPC actor, but recruitment persists a separate case-different string through `StoryEffect.JoinPartyMember("Medrare")`.
- `CampaignRuntime` stores joined members in `HashSet<string>` / `CampaignProgressSnapshot.string[]`. This is durable progression state but is not bound to `NpcRef` or any physical actor. The migration should bind that semantic party-member reference to the same `CharacterId`; Characters must not take ownership of story progression policy.

### Enemy

- `KentridgeForestBanditEncounter` owns `List<GameObject> _bandits` as the physical enemy registry, `bool[] _grounded` as scene-local transient state, and `_encounterResolved` as encounter lifecycle state. Bandit transforms live directly on GameObjects.
- Combat separately creates `CombatParticipantId("forest-bandit-1".."forest-bandit-3")`. Those tactical ids and Combat lifecycle remain Combat-owned, but each must be bindable to the stable gameplay `CharacterId` for the corresponding bandit.
- Defeat is therefore currently conflated with encounter/combat state and presentation lifetime. T03 must make defeated-vs-removed explicit without moving tactical combat rules into Characters.

### Existing engine Characters module

- `Assets/VoxelEngine/Characters` already has `VoxelEngine.Characters.Api` and `.Runtime`, but its current public surface is equipment/character-part mechanics and Runtime contains animation/equipment/visual mechanics. Kentridge also consumes engine `CharacterMotor`/presentation mechanics.
- This engine module is not the requested persistent gameplay-character authority. New `Game.Characters.Api` / `.Runtime` should own gameplay identity/lifecycle and reuse lower-level engine mechanics where needed rather than moving campaign/combat/session semantics into VoxelEngine or duplicating mechanics.

### Explicit non-targets / ownership boundaries

- `KentridgeRegionLife` ambient wildlife clusters belong to AmbientLife/ecology and are not demonstrated persistent gameplay characters; do not migrate them opportunistically.
- WorldBuilder retains authored `NpcRef`, site/placement policy, and generated-world realization facts.
- Sessions/networking retain player/connection ids; Combat retains participant ids and tactical lifecycle; Story/Campaign retains recruitment/progression policy; Cutscenes retain actor choreography; presentation GameObjects are never authoritative identity.

### Duplicate identity map to migrate

- Kentridge player: player slot `0` + scene `PlayerActor`/`CharacterMotor` + combat id `kentridge-player` (+ network player id in multiplayer compositions) -> one bound `CharacterId`.
- Authored NPC: `NpcRef` + resolved placement + scene `NpcActor` GameObject -> one bound `CharacterId`.
- Medrare recruit: `NpcRef("medrare")` + physical NPC actor + campaign party member string `"Medrare"` -> one `CharacterId` with progression remaining Campaign-owned.
- Forest bandit: scene GameObject/list index + `CombatParticipantId("forest-bandit-N")` -> one `CharacterId`, with Combat remaining tactical authority.

Tooling note: repository code-search indexing returned no broad-term results and a local clone was unavailable in this environment; the inventory was completed through GitHub repository tree/directory/file reads. This is not an acceptance blocker.

## Tests / proof

Create player/NPC/enemy compositions through the same runtime; stable identity across save/restore; removal/defeat distinction; headless deterministic tests; independent non-Kentridge fixture.

## Do not build

No enemy subclass tree, inventory ownership implementation, AI planner, combat rules, or presentation GameObject authority.
