# Worldbuilding Interactables Plan

## Goal

Make generated interactables complete gameplay content, not just metadata. Every supported world-object kind must have both concrete interaction behavior and visible geometry/presentation emitted by a deterministic authoring path.

## Architecture

- Decorations remain responsible for contextual placement and style selection.
- Structures remain responsible for topology and structural placement.
- Both may emit `WorldObject` descriptors.
- `WorldObject` owns stable identity, capabilities, runtime state, signals/actions, persistence, and behavior.
- A world-object content definition owns the default geometry/presentation recipe and concrete behavior for its kind.
- Persistent identity must remain independent of visual style/variant changes.

## Completion contract for each world-object kind

A kind is not considered complete until it has:

1. Deterministic geometry/presentation emission.
2. Concrete interaction behavior appropriate to the kind.
3. State-machine/default-state definition when stateful.
4. Signal/action handling when a signal source or target.
5. Collision/navigation semantics when relevant.
6. Persistence semantics for player-visible state changes.
7. Tests covering geometry emission and primary behavior.
8. At least one structure/decoration integration path where the object can actually appear in generated content, unless it is intentionally infrastructure-only (for example SpawnPoint).

## Content priorities

Prioritize breadth and usefulness over visual polish. First complete the existing catalog before adding more kinds.

### Batch A — traversal and barriers

Door, Gate, Portcullis, Drawbridge, Elevator, MovingPlatform, Ladder, Rope, Zipline, Teleporter.

### Batch B — controls and mechanisms

Lever, Switch, Button, PullChain, PressurePlate, Winch, Valve, Generator, FuseBox.

### Batch C — containers and usable furniture

Chest, Dresser, Cabinet, Crate, Barrel, WeaponRack, Bookshelf, Bed, Chair, Bench, Altar, Bell.

### Batch D — lighting and fire

Torch, Lantern, Brazier, Fireplace.

### Batch E — traps, secrets, and destruction

Trap, SpikeTrap, DartTrap, FallingBlockTrap, Crusher, SecretDoor, RotatingWall, BreakableWall.

### Batch F — vehicles and world utility

MineCart, Cart, Checkpoint, SpawnPoint.

## Reusable mechanism content

Maintain and expand reusable mechanism presets so generators can emit functional combinations with little bespoke code: lever-controlled doors, secret rooms, pressure-plate traps, powered elevators, gatehouses, powered lights, timed traps, chained switches, lift call buttons, and lock/key-style gating.

## Validation strategy

For each batch, add tests first or alongside implementation. Validate deterministic geometry output, stable IDs, expected state transitions, signal routing, and persistence deltas. Then wire representative examples into castle/cave/decorative generation.

## Working rule

Do not add another world-object kind unless its geometry and primary interaction behavior are implemented, or it is explicitly recorded as an incomplete task in the task tracker.
