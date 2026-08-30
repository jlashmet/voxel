# Voxel Game Systems Checklist

This document tracks systems for **this repository as it exists**, not a generic game-development checklist. A system belongs here only when it is evidenced by code/specs/current SceneIssues, or when it is a clearly demonstrated gap needed to connect those existing systems into the intended game.

Legend:
- `[x]` confirmed present on `master` at the time this document was started
- `[ ]` incomplete/current work/gap that still needs validation or implementation

## Existing game/runtime systems

- [x] Destructible/buildable voxel-world architecture and runtime foundation
- [x] Large-world streaming architecture
- [x] Custom collision / character-world query architecture
- [x] Multiplayer architecture using Unity Transport rather than Netcode for GameObjects
- [x] Server-authoritative world replication design
- [x] Player-state replication / shared tick-loop architecture
- [x] Prediction and reconciliation architecture for a mutable voxel world
- [x] Multiplayer interest-management architecture
- [x] Late-join / reconnect architecture
- [x] Combat API (`Game.Combat.Api`)
- [x] Combat runtime (`Game.Combat.Runtime`)
- [x] Combat session / participant / team contracts
- [x] Combat health and alive/dead state in the combat prototype
- [x] Chain-combat board/runtime
- [x] Chain reactions / reaction resolution
- [x] Enemy tactical combat AI
- [x] Combat round-readiness / execution coordination
- [x] Inventory API and runtime
- [x] Quest API and runtime
- [x] Campaign runtime / campaign composition
- [x] Story runtime/content foundations
- [x] Cutscene foundations
- [x] Input foundations
- [x] WorldBuilder framework
- [x] Structure/world composition foundations
- [x] Roads/world-route generation foundations

## Current SceneIssues / explicitly planned work

- [ ] Mountain dragon cutscene (`20260828-180417-000-VoxelShowcaseMountainDragonCutscene`)
- [ ] Kentridge macro-world physical realization (`20260829-020634-000-KentridgeMacroWorldPhysicalRealization`)
- [ ] WorldBuilder typed structural socket composition (`20260829-034505-000-WorldBuilderTypedStructuralSocketComposition`)
- [ ] Stylized water shader integration (`20260829-034812-000-WaterRenderingShowcaseStylizedShaderIntegration`)
- [ ] WorldBuilder spatial reservation system (`20260829-050529-000-WorldBuilderSpatialReservationSystem`)
- [ ] Dragon mesh voxelization (`20260829-050700-000-VoxelShowcaseDragonMeshVoxelization`)
- [ ] Exploration interactables / secrets showcase (`20260830-014314-000-ExplorationInteractablesSecretsShowcase`)
- [ ] WorldBuilder road presentation quality (`20260830-120242-000-WorldBuilderRoadPresentationQuality`)
- [ ] Module validation scenes / built-player testing architecture (`20260830-132455-000-ModuleValidationScenesBuiltPlayerTestingArchitecture`)
- [ ] WorldBuilder secret-discovery clue generation (`20260830-164351-000-WorldBuilderSecretDiscoveryClueGeneration`)

## Full-game integration gaps to verify

These are intentionally conservative. They should be checked against the repository before being expanded into SceneIssues.

- [ ] Verify combat runtime is integrated into the primary playable game flow rather than remaining mainly prototype/showcase code
- [ ] Verify combat state/damage/death are shared reusable gameplay services rather than duplicated between prototype and production composition
- [ ] Verify enemy tactical AI is reusable by production encounters and not tied to the chain-combat demo
- [ ] Verify multiplayer replication covers gameplay state above the voxel world: combat participants/results, quests, inventory, and other authoritative game state that actually needs networking
- [ ] Verify a complete multiplayer session can exercise the intended game loop end-to-end in a built player
- [ ] Verify campaign, quest, combat, inventory, WorldBuilder, and exploration systems are composed into one production gameplay flow
- [ ] Verify the game has clear completion/failure state for its intended playable experience
- [ ] Verify player-facing HUD/UI exists for the production gameplay state that actually requires it
- [ ] Verify audio/VFX/feedback coverage for the production gameplay systems that are retained
- [ ] Verify save/session persistence requirements against the actual game design before adding persistence work beyond the voxel engine's existing session-scoped design
- [ ] Add an end-to-end vertical-slice validation that proves the retained systems work together, including multiplayer where applicable

## Not assumed

The checklist must **not** automatically add generic survival/base-building features just because they are common in games. In particular, this document currently does **not** assume the game needs caravan management, crafting, resource harvesting/economy, power grids, recruit workers, fort capture, day/night gameplay, boss systems, equipment/loadouts, or arbitrary weapon categories. Add any of these only when repository/design evidence says they belong to this game.

## Maintenance rule

When updating this checklist, inspect current `master` and current `SceneIssues` first. Prefer marking an existing system as partial/integration-needed over inventing a parallel replacement system. Keep shared systems reusable/config-driven and keep scene/campaign-specific policy in composition.