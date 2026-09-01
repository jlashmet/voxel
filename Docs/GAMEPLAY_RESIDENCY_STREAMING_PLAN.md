# Gameplay Residency / Simulation Streaming Plan

**Status:** architecture direction / implementation not started  
**Companion:** `Docs/GAMEPLAY_RESIDENCY_STREAMING_TASKS.md`  
**Related:** `Docs/WORLDBUILDER_HYBRID_AUTHORED_PROCEDURAL_PLAN.md`  
**Gameplay-plan alignment:** systems 03, 04, 06, 12, 13, 14, and 16 on `docs/game-systems-checklist`

## Goal

The game must be able to contain far more semantic content than can be fully simulated, replicated, or presented at once.

Voxel/world streaming already answers which physical world regions are resident. This plan adds the corresponding gameplay concept for characters, WorldObjects, encounters, generated settlement content, and other spatial gameplay entities without making voxel streaming own gameplay state.

The defining rule is:

> **Gameplay identity and durable semantic state outlive physical-world, simulation, network, and Unity presentation residency.**

A character does not stop existing because its model unloads. A door does not become a new door because its region reloads. A generated settlement does not need every NPC, object, and encounter instantiated merely because its semantic definition exists.

## Distinct layers

Do not collapse these concerns into one generic `Loaded` flag.

1. **World definition** — immutable authored/generated facts and stable identities.
2. **Durable authoritative state** — mutable semantic facts that must survive unload/save/restore.
3. **Gameplay simulation residency** — how much runtime simulation an entity/region currently receives.
4. **Physical world residency** — voxel/terrain/structure regions required for detailed spatial gameplay.
5. **Network interest** — which authoritative state a client needs and at what update fidelity.
6. **Presentation residency** — Unity GameObjects, renderers, animation, audio, VFX, colliders, and other client/runtime presentation.

These layers coordinate through narrow contracts but retain separate ownership.

## Residency levels

Use a small semantic fidelity model rather than one boolean. Initial target levels are:

### Dormant

The entity exists semantically but receives no continuous detailed simulation.

Typical retained state may include:

- stable identity;
- authoritative durable state owned by the domain;
- logical region/site/location relationship;
- exact pose only where correctness requires retaining it;
- current coarse activity/goal where applicable;
- vitality/defeat state;
- inventory or inventory identity as owned by Inventory;
- sparse WorldObject state deltas;
- completed/active semantic facts required by progression/story.

Dormant state must not require Unity scene objects, navigation agents, detailed perception, per-frame ticking, or client replication.

### Coarse

The entity receives cheap semantic simulation without requiring full physical realization.

Examples:

- a merchant advances from `WorkingAtSmithy` to `AtHome` without pathfinding every meter;
- a guard changes patrol phase or logical site;
- a generated settlement updates a small number of background semantic facts;
- a character's long-horizon AI advances at a coarse cadence without detailed perception;
- timers or scheduled semantic activities advance when their owning domain supports it.

Coarse simulation must use the same authoritative identity/state model as detailed simulation. It is reduced fidelity, not a second character or WorldObject model.

### Detailed

The entity participates in full gameplay simulation appropriate to its domain.

Examples may include:

- exact authoritative position/facing;
- navigation/path following;
- detailed perception and short-horizon AI;
- interaction range/reachability checks;
- active encounter/combat participation;
- locally required collision/spatial queries;
- corresponding presentation realization;
- high-fidelity network replication for interested clients.

Detailed residency normally requires the necessary physical world region to be resident first.

## Residency is desired fidelity, not ownership

Introduce one coordinating gameplay-residency capability that computes **desired fidelity** from semantic demands. It must not become a god registry that owns character, WorldObject, encounter, inventory, quest, or story state.

Conceptually:

`world/player relevance + domain pins + active gameplay demands`

→ `GameplayResidencyDecision(scope/entity, minimum fidelity, reason)`

→ domain-specific residency adapters

→ owning domain performs its own transition.

The coordinator may know stable IDs, spatial/logical scope, and requested minimum fidelity. It must not inspect or mutate private domain state.

## Demand sources and pinning

Residency should be demand-driven and composable. A domain can request a minimum fidelity for a stable scope/entity and later release that demand.

Examples:

- nearby authoritative player interest → Detailed for nearby characters/WorldObjects;
- active encounter → Detailed for encounter participants and required site;
- active cutscene → Detailed for required actors/site;
- long-horizon background simulation policy → Coarse for selected settlements/characters;
- server administration/debug validation → explicit temporary pin;
- ordinary far-away generated settlement → Dormant.

Multiple demands combine by taking the highest required fidelity. Do not let one system directly unload another system's required entity.

Pins must be semantic and reference-counted/tokenized or otherwise owner-safe so one requester cannot accidentally release another requester's demand.

## Physical world handshake

Gameplay residency and `VoxelEngine.Streaming` remain separate owners.

- world streaming owns physical voxel/region residency;
- gameplay residency owns desired gameplay simulation fidelity;
- a transition to Detailed may request/pin the required physical region through a narrow streaming API;
- Detailed activation occurs only after required world residency reports ready;
- releasing detailed gameplay demand releases only the gameplay-owned world pin;
- world streaming remains free to retain the region for other consumers.

Do not make characters or encounters call engine Runtime implementation types directly. Composition wires the required API capability.

Avoid circular activation:

`request Detailed`
→ request required world-region residency
→ wait for world ready
→ domain realizes Detailed state
→ presentation/network adapters observe resulting authoritative state.

## Spatial scope and stable ownership

Every spatially streamable semantic entity needs a deterministic way to determine its current residency scope without depending on Unity object lifetime.

The scope may be a world region/cell, resolved site, settlement, route segment, or another stable spatial identity.

Characters can migrate between scopes. Their current logical/authoritative location moves with them; their identity does not.

Generated WorldObjects derive stable identities from deterministic world generation and retain sparse mutable state independently of presentation lifetime.

## Characters

System 03 remains the owner of character identity/runtime state and system 04 owns AI behavior.

Residency integration must support:

`Dormant ↔ Coarse ↔ Detailed`

without replacing `CharacterId` or creating `DormantCharacter` / `NetworkCharacter` / `SceneCharacter` shadow models.

### Detailed → Coarse/Dormant

Before releasing expensive simulation:

- finish the current authoritative mutation boundary;
- capture the domain-owned semantic state needed for later realization;
- release detailed navigation/perception/presentation resources;
- retain stable identity and durable state;
- retain or derive a logical location sufficient for later realization.

### Coarse/Dormant → Detailed

- ensure required world region/site is physically resident;
- resolve a valid authoritative pose from retained exact pose or semantic location/site realization;
- restore detailed AI/navigation/interaction capabilities from normal character state;
- do not replay historical actions merely to reconstruct the current character.

The same character can move through town life → encounter → combat → town life across residency changes without changing identity.

## WorldObjects

`WorldObject` remains the sole authoritative interactive-world substrate.

Most generated unchanged objects should require no permanently allocated runtime behavior when their region is not resident.

Target lifecycle:

`deterministic generated definition + stable WorldObjectId`

→ region becomes resident

→ runtime registry realizes applicable objects and overlays sparse retained state

→ interaction/state changes persist through existing WorldObject authority

→ region leaves residency

→ presentation/runtime realization is released

→ sparse state remains keyed by the same WorldObjectId

→ later realization reconstructs the same logical object.

Do not equate a Unity `GameObject` or collider with WorldObject lifetime.

## Encounters and temporary actors

Inactive encounter definitions do not need live encounter instances.

An active encounter initially establishes a Detailed minimum-fidelity demand for:

- its realized site/region;
- persistent participants;
- temporary participants required by the encounter.

This conservative rule protects correctness until encounter-specific coarse simulation is demonstrated. It is not a permanent claim that every future encounter must remain detailed indefinitely.

When an encounter resolves/cleans up, release only its own demands. Persistent characters remain their same character identities and fall back to whatever other residency demands require.

## Quests, story, inventory, and other non-spatial state

Not every domain needs simulation streaming.

Small semantic state such as unified progression, campaign/story facts, and many inventories may remain resident because they are cheap and globally relevant. Do not force every subsystem through the residency abstraction for visual symmetry.

Where a large domain later demonstrates a memory/runtime need, it can add a domain-owned residency adapter without changing the overall model.

A far-away quest target may therefore remain a stable semantic reference even when the corresponding character/site has no detailed realization.

## Procedural WorldBuilder integration

The hybrid WorldBuilder plan can produce very large amounts of deterministic semantic content. Residency must let that content remain cheap.

A generated settlement should not imply immediate creation of every runtime entity. Prefer:

`seed + authored constraints + generation policy`

→ deterministic semantic definition for a spatial scope

→ stable generated identities

→ sparse mutable state only for changed/important facts

→ runtime realization only when residency demands it.

Where practical, immutable generated definitions may be regenerated from stable inputs when a scope becomes relevant rather than retained as a permanently expanded object graph. Any such regeneration must reproduce stable identities exactly.

## Network interest is downstream, not the residency owner

System 06 network interest remains a separate concern.

The authoritative server may simulate an entity Detailed while only some clients are interested. Conversely a newly interested client receives a current authoritative snapshot rather than causing a second gameplay entity to be created.

Conceptually:

`authoritative gameplay residency`
→ semantic replication snapshots/deltas
→ existing network interest management
→ interested client replicas/presentation.

Network disconnect or loss of one client's interest must never destroy authoritative gameplay identity/state.

## Presentation is downstream

Unity presentation follows authoritative detailed residency/client relevance as appropriate.

Presentation may create/destroy:

- GameObjects;
- renderers;
- animation graphs;
- audio emitters;
- VFX;
- interaction proxies/colliders where presentation owns them.

Presentation teardown must not delete authoritative semantic state. Presentation rebuild must bind back to existing stable IDs.

## Transition ordering and hysteresis

Residency transitions must be deterministic at authoritative mutation boundaries.

Avoid rapid load/unload thrashing near boundaries. The coordinator should support policy-level hysteresis such as:

- larger unload radius than load radius;
- minimum dwell time where useful;
- bounded transition budgets per authoritative tick;
- priority for player-critical/encounter-critical demands.

These are residency policy/configuration, not character/world-object rules.

## Failure behavior

A failed Detailed realization must not silently destroy the dormant/coarse entity.

Examples:

- world region failed to become resident;
- retained semantic location cannot produce a valid detailed pose;
- required generated WorldObject definition cannot be deterministically reconstructed;
- encounter requests a site that no longer resolves.

Keep the authoritative semantic entity/state intact, reject or delay the detailed transition with a semantic diagnostic, and let the owning gameplay flow decide how to respond.

## Save/restore

System 16 remains the durable-session owner/coordinator.

Persist domain-owned semantic state, stable identities, generated-world identity/seed/revision, and sparse world deltas. Do not persist Unity residency machinery, loaded GameObjects, navigation agents, network interest, or transient residency tokens.

After restore, normal gameplay composition reconstructs residency from current player/world/domain demands.

## Initial public contract shape

Exact naming should follow repository conventions after implementation inventory, but the shared API should remain narrow and semantic. A conceptual shape is:

```text
GameplayResidencyLevel
  Dormant
  Coarse
  Detailed

GameplayResidencyScope
  stable spatial/entity identity

ResidencyDemand
  requester token
  scope/entity
  minimum level
  reason/category

IGameplayResidency
  AcquireDemand(...)
  ReleaseDemand(token)
  QueryDesiredLevel(...)
```

Domain adapters react to decisions/events and own their transitions. Do not expose mutable global dictionaries or allow callers to directly set another domain's internal loaded state.

## Ownership summary

- **VoxelEngine.Streaming:** physical world-region residency.
- **Gameplay residency coordinator:** combines semantic demand and chooses desired gameplay fidelity.
- **Characters:** character identity/state/pose/lifecycle.
- **CharacterAI:** AI state, perception, intent, coarse/detailed behavior.
- **WorldObject:** object identity/state/behavior/sparse persistence.
- **Encounters:** encounter instances/membership/lifecycle.
- **Inventory:** inventory state/transactions.
- **Quest/Progression:** progression state.
- **Story:** authored consequences/sequencing.
- **Net:** client interest and replication transport.
- **Presentation:** Unity object/render/audio/VFX realization.
- **Composition:** wires concrete adapters/capabilities and policies.

## Acceptance

The architecture is complete when all of the following are proven:

1. One persistent town character can transition Detailed → Coarse/Dormant → Detailed while retaining the same `CharacterId`, vitality, semantic activity, and required durable state.
2. The character can continue a coarse autonomous-life transition while no Unity presentation or detailed pathfinding/perception exists.
3. A generated WorldObject can unload its runtime/presentation, retain a changed sparse state, and later realize with the same `WorldObjectId` and state.
4. An active encounter can pin the required site/participants Detailed and release only its own demands on cleanup.
5. Detailed gameplay activation waits for required physical world residency rather than duplicating terrain/site placement.
6. A distant client receives no unnecessary high-frequency entity updates while authoritative server state remains coherent; a newly interested client receives current snapshots rather than historical replay.
7. Save/restore reconstructs semantic state and then derives residency normally; no runtime/presentation residency objects are persisted.
8. A sparse procedurally generated settlement can exist semantically without eagerly instantiating all of its characters/WorldObjects.
9. No parallel dormant/detailed character model, second WorldObject state store, second encounter runtime, or replacement network-interest system is introduced.
10. Residency transition diagnostics identify the stable entity/scope, requested level, demand reason, and failed prerequisite when realization cannot proceed.

## First implementation discriminator

Before introducing a broad coordinator, prove the smallest cross-domain slice:

- one persistent character bound to a resolved settlement/site;
- one generated mutable WorldObject in the same region;
- one explicit residency demand source;
- one physical-world residency prerequisite;
- transition both entities out and back in while preserving identity/state.

If this cannot be expressed through narrow existing Character/WorldObject/Streaming APIs, add only the smallest missing semantic capability at the owning boundary before expanding the coordinator.