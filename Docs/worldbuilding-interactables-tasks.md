# Worldbuilding Interactables Tasks

Status: `[ ]` todo, `[~]` in progress, `[x]` complete.

## Foundation

- [x] Document completion contract: every supported kind gets concrete behavior + deterministic geometry.
- [x] Shared stable world-object identity/state/signal/persistence foundation.
- [x] Decoration-to-world-object promotion bridge.
- [x] Add common geometry-emission API and per-kind geometry recipes.
- [x] Add concrete per-kind interaction behavior dispatch beyond generic flag toggles.
- [x] Add live scene runtime: interaction -> state delta -> signal routing -> target action -> persistence.
- [x] Add deterministic timed/reset runtime updates without frame-level save state.
- [x] Add generated castle and mine/cave scene factories.
- [x] Add decoration placement -> live WorldObject runtime activation bridge.
- [x] Add scene registry retaining sparse state across unload/reload and save snapshot/restore.
- [x] Add backend-neutral dynamic presentation plans for doors/gates/elevators/traps/lights/etc.
- [x] Add state-change notifications and presentation refresh sink contract.
- [x] Add concrete Unity dynamic presentation sink + scene host (proxy geometry, animation, collider, light, particles).
- [x] Add StaticOnly geometry emission mode so dynamic Unity proxies are not also baked into voxels.
- [x] Add geometry/behavior/runtime validation coverage for every registered kind (tests authored; Unity execution still required).

## Batch A — traversal and barriers

Geometry, concrete behavior, generated-content placement, and dynamic presentation are implemented for this batch. Kinds remain `[~]` only until Unity validation executes.

- [~] Door
- [~] Gate
- [~] Portcullis
- [~] Drawbridge
- [~] Elevator
- [~] MovingPlatform
- [~] Ladder
- [~] Rope
- [~] Zipline
- [~] Teleporter

## Batch B — controls and mechanisms

Geometry, concrete behavior, signal semantics, generated-content placement, and control presentation are implemented. Pending Unity validation only.

- [~] Lever
- [~] Switch
- [~] Button
- [~] PullChain
- [~] PressurePlate
- [~] Winch
- [~] Valve
- [~] Generator
- [~] FuseBox

## Batch C — containers and usable furniture

Geometry, concrete behavior, persistence semantics, and generated-content placement are implemented. Pending Unity validation only.

- [~] Chest
- [~] Dresser
- [~] Cabinet
- [~] Crate
- [~] Barrel
- [~] WeaponRack
- [~] Bookshelf
- [~] Bed
- [~] Chair
- [~] Bench
- [~] Altar
- [~] Bell

## Batch D — lighting and fire

Geometry, concrete behavior, generated-content placement, and light/particle presentation are implemented. Pending Unity validation only.

- [~] Torch
- [~] Lantern
- [~] Brazier
- [~] Fireplace

## Batch E — traps, secrets, and destruction

Geometry, concrete behavior, timed/reset behavior where applicable, generated-content placement, and dynamic presentation are implemented. Pending Unity validation only.

- [~] Trap
- [~] SpikeTrap
- [~] DartTrap
- [~] FallingBlockTrap
- [~] Crusher
- [~] SecretDoor
- [~] RotatingWall
- [~] BreakableWall

## Batch F — vehicles and world utility

Geometry, concrete behavior, and generated-content placement are implemented. Pending Unity validation only.

- [~] MineCart
- [~] Cart
- [~] Checkpoint
- [~] SpawnPoint

## Generator integration

- [x] Castle doors and gatehouse controls.
- [x] Castle secret-room mechanism.
- [x] Castle/dungeon traps.
- [x] Castle vertical traversal/elevator example.
- [x] Cave/mine carts, switches, lights and traps.
- [x] Interaction-rich castle annex covers otherwise-unused object families.
- [x] Mine/cave expansion covers secondary traversal, utility, storage and control families.
- [x] Every registered WorldObject kind now has at least one generated-content placement path.
- [x] Decoration-generated containers/furniture use common live runtime behavior while preserving GeneratedPropId identity.
- [x] Expansion passes emit only newly-authored geometry instead of duplicating base-object writes.
- [x] Dynamic-proxy scene factories/registry modes emit static voxel content only.

## Reusable mechanism presets

- [x] Lever -> door.
- [x] Secret-room control.
- [x] Pressure-plate trap.
- [x] Powered elevator.
- [x] Gatehouse controls.
- [x] Powered lights.
- [x] Timed/resetting trap.
- [x] Multi-switch/chained control.
- [x] Elevator call-button network.
- [x] Lock/key-style gating hook.

## Remaining integration / validation

- [ ] Run authored WorldObject tests in Unity and fix compile/runtime failures.
- [x] Dynamic presentation path: planning + push refresh + concrete Unity sink/scene host; proxy visuals are intentionally gameplay-first placeholders.
- [ ] Reconcile latest `agent/worldbuilding-decorations` changes into this branch (source branch is still actively moving; connector tree merge is currently blocked).
- [~] Streaming lifecycle: registry/state retention is complete; wire registry ownership into the final game composition/bootstrap path.
