# Worldbuilding Interactables Tasks

Status: `[ ]` todo, `[~]` in progress, `[x]` complete.

## Foundation

- [x] Document completion contract: every supported kind gets concrete behavior + deterministic geometry.
- [x] Shared stable world-object identity/state/signal/persistence foundation.
- [x] Decoration-to-world-object promotion bridge.
- [x] Add common geometry-emission API and per-kind geometry recipes.
- [x] Add concrete per-kind interaction behavior dispatch beyond generic flag toggles.
- [x] Add geometry/behavior validation coverage for every registered kind (tests authored; Unity execution still required).

## Batch A — traversal and barriers

Geometry and concrete behavior are implemented for this batch; generator integration remains before the kinds meet the full completion contract.

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

- [~] Torch
- [~] Lantern
- [~] Brazier
- [~] Fireplace

## Batch E — traps, secrets, and destruction

- [~] Trap
- [~] SpikeTrap
- [~] DartTrap
- [~] FallingBlockTrap
- [~] Crusher
- [~] SecretDoor
- [~] RotatingWall
- [~] BreakableWall

## Batch F — vehicles and world utility

- [~] MineCart
- [~] Cart
- [~] Checkpoint
- [~] SpawnPoint

## Generator integration

- [ ] Castle doors and gatehouse controls.
- [ ] Castle secret-room mechanism.
- [ ] Castle/dungeon traps.
- [ ] Castle vertical traversal/elevator example.
- [ ] Cave/mine carts, switches, lights and traps.
- [~] Decoration-generated containers/furniture use common runtime behavior (promotion exists; presentation/runtime activation integration remains).

## Reusable mechanism presets

- [x] Lever -> door.
- [x] Secret-room control.
- [x] Pressure-plate trap.
- [x] Powered elevator.
- [x] Gatehouse controls.
- [x] Powered lights.
- [ ] Timed/resetting trap.
- [ ] Multi-switch/chained control.
- [ ] Elevator call-button network.
- [ ] Lock/key-style gating hook.
