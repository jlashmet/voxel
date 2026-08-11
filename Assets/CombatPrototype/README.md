# Chain Combat Prototype

A deliberately small playable experiment for Mounting Force's turn-based, multiplayer-oriented chain-reaction combat.

## Goal

Test whether a player can learn a small roster's capabilities, position recruits and constructs, and invent long physical chains **without the UI calculating or highlighting combos**.

The prototype exposes what each recruit can do. It does not show compatible reactions, valid combo paths, combo counts, or suggested targets.

## Run it

1. Check out `prototype/chain-combat` and let Unity compile.
2. In Unity, choose **Mounting Force > Chain Combat Prototype > Open & Play**.
3. The launcher asks before discarding any dirty scene, opens an empty temporary scene, adds the prototype controller, and enters Play Mode.
4. Click a friendly capsule, choose an action in the right panel, then click the board.

This prototype intentionally does not modify Build Settings or require a committed scene asset.

## Command groups

This is currently local hot-seat input with one mouse, but recruits are labeled as two command groups to approximate two co-op players:

- **P1:** Stephen, Mira
- **P2:** Weldon, Madeline, Grom

The player can select either group's recruits at any time. That is deliberate: normal actions are free-order so we can experiment with planning and cross-player handoffs before adding transport/replication.

## Recruits

### Stephen

- Move / Strike
- **Uppercut:** launches an adjacent enemy away from Stephen with integer momentum.

### Mira

- Move / Strike
- **Linked Portals:** place an entrance and exit. A moving body entering either exits the other with the same direction and remaining force.
- **Force Multiplier:** a moving body crossing the rune doubles its remaining force, capped to keep the experiment bounded.

### Weldon

- Move / Strike
- **Crosswind reaction:** if a creature is airborne within range, manually choose its new cardinal flight direction. Momentum is preserved.

### Madeline

- Move / Strike
- **Repulse reaction:** after a collision within range, choose one collision participant and manually choose the direction to blast it.

### Grom

- Move / Strike
- **Timber reaction:** after a tree impact within range, manually choose which cardinal direction the tree falls. A fallen tree crushes units along four cells.

## Combat rhythm

Every friendly recruit gets:

- one normal action per round;
- one reaction per round.

Actions can create a physical event. The simulation pauses on three event types in this slice:

- airborne;
- creature collision;
- tree impact.

The game does **not** say which recruit can react. Select someone whose capability you think applies, use the ability, and aim it. You can also pass the event. Passing an airborne event lets its momentum continue; passing a stopped collision/tree event ends that branch.

Enemies take a small deterministic move/attack step when the round ends, then friendly action/reaction resources refresh.

## Example to discover, not UI-script

The initial positions make a multi-character physical chain possible using the roster and the tree on the east side. The README intentionally does not spell out the exact clicks. The point of the experiment is to see whether the capability descriptions and spatial presentation are enough for a player to figure it out.

Portals and force multipliers let you build different routes over later rounds rather than relying entirely on the initial map geometry.

## Architecture

`CombatCore.cs` owns all gameplay-relevant state and uses an integer grid, integer HP, integer force, deterministic ordering, and explicit reaction events. It does not reference UnityEngine.

`CombatPrototypeController.cs` is presentation/input only. It creates primitive Unity geometry, converts clicks into grid positions, and renders the current authoritative state. Unity transforms, colors, camera math, and the ground-plane raycast do not feed floating-point values back into authoritative simulation except by quantizing the clicked cell to integer coordinates.

The split is intentional so the experiment can later be driven by server-authoritative commands rather than replacing prototype physics code.

## Intentionally missing

- network transport / server replication;
- simultaneous multi-device input;
- real voxel terrain/destruction integration;
- animation and polished VFX;
- pathfinding;
- AI beyond a deterministic nearest-target step;
- character progression / large recruit roster;
- persistent battle setup;
- combo scoring or rewards.

Those should wait until we know whether positioning + capability knowledge + interactive reaction handoffs are actually fun.
