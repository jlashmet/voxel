# Voxel Game Systems Checklist

This document tracks systems for **this repository as it exists**, not a generic game-development checklist. A system belongs here only when it is evidenced by code/specs/current SceneIssues, or when it is a clearly demonstrated gap needed to connect those existing systems into the intended game.

## Design-review legend

- `[x]` reviewed and approved as a needed system/design direction
- `[ ]` candidate still to review individually

Each approved candidate receives its own design document under `Docs/game-systems/` so decisions are retained as the list is worked through.

## Existing game/runtime systems

These are existing foundations and are not candidates to rebuild from scratch.

- Destructible/buildable voxel-world architecture and runtime foundation
- Large-world streaming architecture
- Custom collision / character-world query architecture
- Multiplayer architecture using Unity Transport rather than Netcode for GameObjects
- Server-authoritative world replication design
- Player-state replication / shared tick-loop architecture
- Prediction and reconciliation architecture for a mutable voxel world
- Multiplayer interest-management architecture
- Late-join / reconnect architecture
- Combat API (`Game.Combat.Api`)
- Combat runtime (`Game.Combat.Runtime`)
- Combat session / participant / team contracts
- Combat health and alive/dead state in the combat prototype
- Chain-combat board/runtime
- Chain reactions / reaction resolution
- Enemy tactical combat AI
- Combat round-readiness / execution coordination
- Inventory API and runtime
- Quest API and runtime
- Campaign runtime / campaign composition
- Story runtime/content foundations
- Cutscene foundations
- Input foundations
- WorldBuilder framework
- Structure/world composition foundations
- Roads/world-route generation foundations

## Candidate full-game systems

- [x] **01. [Production combat integration](game-systems/01-production-combat-integration.md)**
  - Reuse the existing combat runtime and connect semantic encounters to world/story/campaign flow.
- [x] **02. [Actor vitality, damage & defeat](game-systems/02-actor-vitality-damage-defeat.md)**
  - Vitality belongs to the character/actor rather than combat; defeat is an authoritative event-driven state transition.
- [x] **03. [Gameplay character runtime](game-systems/03-gameplay-character-runtime.md)**
  - One generic authoritative character runtime for players, NPCs, recruits, and enemies; enemies are a composition, not a separate actor hierarchy.
- [x] **04. [Character AI, autonomous life, perception & intent](game-systems/04-character-ai-autonomous-life-perception-intent.md)**
  - Characters can pursue persistent lives outside combat; shared semantic perception, planning, and intent also support tactical AI and simulation LOD.
- [x] **05. [Encounter activation, membership & lifecycle](game-systems/05-encounter-activation-membership-lifecycle.md)**
  - Encounters are temporary authoritative gameplay situations, distinct from cutscenes and combat; they coordinate membership, activation, resolution, and cleanup for persistent and temporary characters.
- [x] **06. [Gameplay-state replication](game-systems/06-gameplay-state-replication.md)**
  - Extend the existing authoritative custom network spine with explicit gameplay snapshots/deltas for characters, vitality, encounters, combat, inventory, quests, and campaign state.
- [x] **07. [Multiplayer party & session formation](game-systems/07-multiplayer-party-session-formation.md)**
  - Stable party/member/slot identities and readiness/session orchestration above the existing transport; party leadership remains distinct from server gameplay authority.
- [x] **08. [Player disconnect, reconnect & continuity](game-systems/08-player-disconnect-reconnect-continuity.md)**
  - Connections are temporary; preserve durable party-member, player-slot, and controlled-character identity across unexpected disconnects and authoritative resynchronization.
- [x] **09. [Gameplay inventory ownership & transactions](game-systems/09-gameplay-inventory-ownership-transactions.md)**
  - Generalize the existing deterministic inventory runtime with stable inventory identity, authoritative add/remove/transfer transactions, semantic change events, and reuse across characters and containers.
- [x] **10. [World loot, pickup & item transfer](game-systems/10-world-loot-pickup-item-transfer.md)**
  - Bridge existing world-object interactions to authoritative inventory transactions with race-safe claims, container transfers, drops, and item-conservation guarantees.
- [ ] **11. Production quest / objective integration**
- [ ] **12. WorldBuilder-to-gameplay encounter integration**
- [ ] **13. WorldBuilder-to-gameplay interactable integration**
- [ ] **14. End-to-end campaign/game-flow director**
- [ ] **15. Victory / failure / completion flow**
- [ ] **16. Save / session persistence**
- [ ] **17. Production HUD**
- [ ] **18. Inventory UI**
- [ ] **19. Quest / objective UI**
- [ ] **20. Multiplayer teammate / session UI**
- [ ] **21. Gameplay audio integration**
- [ ] **22. Combat / interaction VFX and feedback**
- [ ] **23. Game menus / settings / start flow**
- [ ] **24. Integrated built-player vertical slice**
- [ ] **25. Multiplayer end-to-end gameplay tests**
- [ ] **26. Full-game/session progression loop**

## Current SceneIssues / explicitly planned work

These are already represented by active SceneIssue work and should not be duplicated by the candidate system designs.

- Mountain dragon cutscene (`20260828-180417-000-VoxelShowcaseMountainDragonCutscene`)
- Kentridge macro-world physical realization (`20260829-020634-000-KentridgeMacroWorldPhysicalRealization`)
- WorldBuilder typed structural socket composition (`20260829-034505-000-WorldBuilderTypedStructuralSocketComposition`)
- Stylized water shader integration (`20260829-034812-000-WaterRenderingShowcaseStylizedShaderIntegration`)
- WorldBuilder spatial reservation system (`20260829-050529-000-WorldBuilderSpatialReservationSystem`)
- Dragon mesh voxelization (`20260829-050700-000-VoxelShowcaseDragonMeshVoxelization`)
- Exploration interactables / secrets showcase (`20260830-014314-000-ExplorationInteractablesSecretsShowcase`)
- WorldBuilder road presentation quality (`20260830-120242-000-WorldBuilderRoadPresentationQuality`)
- Module validation scenes / built-player testing architecture (`20260830-132455-000-ModuleValidationScenesBuiltPlayerTestingArchitecture`)
- WorldBuilder secret-discovery clue generation (`20260830-164351-000-WorldBuilderSecretDiscoveryClueGeneration`)

## Not assumed

The checklist must **not** automatically add generic survival/base-building features just because they are common in games. In particular, this document currently does **not** assume the game needs caravan management, crafting, resource harvesting/economy, power grids, recruit workers, fort capture, day/night gameplay, boss systems, equipment/loadouts, or arbitrary weapon categories. Add any of these only when repository/design evidence says they belong to this game.

## Maintenance rule

When reviewing each candidate:

1. Inspect current `master` and current SceneIssues first.
2. Prefer integrating/generalizing an existing system over inventing a parallel replacement.
3. Keep shared systems reusable, semantic, and configuration-driven.
4. Keep scene/place/campaign-specific policy in composition/content.
5. Record the approved design in its own `Docs/game-systems/NN-*.md` document before moving to the next candidate.
6. Require an independent reuse/integration proof where practical.
