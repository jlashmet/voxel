# Chain Combat Prototype

A deliberately small playable experiment for Mounting Force's turn-based, multiplayer-oriented chain-reaction combat.

## Goal

Test whether players can learn a roster's physical capabilities, arrange the battlefield, claim emergent reaction windows, and invent long cooperative chains **without the UI calculating or highlighting combos**.

The prototype exposes causes and effects. It does not show compatible reactions, valid combo paths, combo counts, recommended characters, or suggested targets.

## Run it

1. Check out `prototype/chain-combat` and let Unity compile.
2. In Unity, choose **Mounting Force > Chain Combat Prototype > Open & Play**.
3. The launcher asks before discarding any dirty scene, opens an empty temporary scene, adds the current cascade-lab components, and enters Play Mode.
4. Select a friendly capsule, choose a proactive action or attempt a reaction claim, then aim directly on the board.

The prototype intentionally does not modify Build Settings or require a committed scene asset.

## Four command groups

This is still local hot-seat input with one mouse, but the battlefield is divided into four player-owned command groups:

- **P1:** Stephen, Brutus
- **P2:** Weldon
- **P3:** Madeline, Mira
- **P4:** Grom, Skitter

### One active recruit per player

Each round, the first recruit a player successfully uses for proactive play becomes that player's **active recruit** for the round.

That recruit gets:

- one reposition;
- one proactive action;
- its normal personal reaction, if a matching event later occurs.

The move and action may happen in either order. A failed target/placement attempt does not commit the activation.

A second recruit in the same command group cannot take another proactive turn that round. It **can still claim and execute reactions**. This is the core scaling experiment: adding dozens of recruits should expand the player's reaction/toolbox possibilities without creating dozens of normal turns.

The activation overlay shows only who each player committed and whether that recruit's move/action are spent. It does not reveal reaction compatibility.

## Recruits

### Stephen — P1

- Move / Strike
- **Uppercut:** launch an adjacent enemy with force 5.
- **Follow Through reaction:** after a nearby creature collision, kick either participant in a chosen direction with force 5.

### Brutus — P1

- Move / Strike
- **Shoulder Hurl:** throw an adjacent enemy in a chosen direction with force 5.
- **Catch & Throw reaction:** claim a nearby airborne creature, catch it beside Brutus, and rethrow it with force 7.

### Weldon — P2

- Move / Strike
- **Gust:** push an enemy away with force 3.
- **Crosswind reaction:** redirect a nearby airborne creature's existing momentum without replacing its force.

### Madeline — P3

- Move / Strike
- **Converge:** drive one enemy toward another with force 4 to deliberately build future collision geometry.
- **Repulse reaction:** after a collision, choose either participant and blast it in a chosen direction with force 4.

### Mira — P3

- Move / Strike
- **Linked Portals:** place an entrance and exit. Moving bodies preserve direction and remaining force through the pair.
- **Force Multiplier:** a moving body crossing the rune amplifies its remaining force, capped to keep the experiment bounded.

### Grom — P4

- Move / Strike
- **Notch Tree:** prepare a standing tree for a chosen fall direction and add structural stress.
- **Timber reaction:** after a meaningful tree impact, commit the struck tree to a fall direction. Following a prepared notch increases reach and damage.

### Skitter — P4

- Move / Strike
- **Harpoon:** proactively pull an enemy toward Skitter with force 4, potentially manufacturing a collision.
- **Hook Yank reaction:** after a collision or tree impact, pull an involved creature toward Skitter with force 5.

## Combat rhythm

A normal round now has two intertwined economies:

1. **Proactive activation:** each player chooses one recruit to move/set up/attack with.
2. **Reaction ownership:** every living recruit can still attempt its own reaction once per round when a physical event it understands occurs.

The simulation pauses on three event types in this slice:

- airborne;
- creature collision;
- tree impact.

A physical event starts **unclaimed**. The game reports only what physically happened: participants, location, direction when relevant, and impact force. It does not enumerate characters who can answer it.

A player selects a recruit and attempts the reaction they think applies. The first valid claim becomes authoritative ownership of that event. Claiming is not execution: the owner must still choose the participant/direction/aim. The owner may release the claim so another player can take it.

A claimed event cannot be globally passed out from under its owner. An unclaimed airborne event can be passed to let its motion continue; passing a stopped collision/tree event ends that branch.

## Force and environment

Momentum is not just travel distance. Impact force determines damage and whether some environmental events are meaningful enough to react to.

- weak collisions do little damage;
- amplified/high-force collisions hurt more;
- weak tree bumps do not automatically become Timber opportunities;
- trees accumulate stress;
- a correctly used Grom notch produces a stronger environmental payoff;
- portals preserve force;
- force multipliers can turn the same launch into a much harder eventual collision.

This keeps the physical simulation learnable: players can reason about *how hard* something is moving, not only binary state tags.

## Readability without solving

The world communicates the fact, not the answer:

- airborne bodies are visibly elevated;
- moving bodies animate through their resolved path rather than teleporting between ordinary grid cells;
- collision/tree-impact locations get a world-space physical-event marker;
- impact markers expose force/severity;
- struck trees visibly stress/shake;
- notched trees show their prepared direction;
- no eligible-reactor highlights or combo arrows are drawn.

Authoritative gameplay remains integer/grid/deterministic. The smoothing and event-marker layers are presentation only and never feed Unity transforms back into simulation state.

## Cooperative chain telemetry

The board tracks:

- deliberate cascade steps;
- distinct player command groups participating;
- actual player-to-player handoffs.

This is post-action feedback for tuning cooperative play, not a pre-action hint system.

## Example to discover, not UI-script

The initial positions intentionally admit multiple routes through the same physical facts. The README does not provide an exact click sequence. The experiment is successful only if players can infer useful chains from capability knowledge and battlefield geometry themselves.

## Architecture

`ChainCombatBoard.cs` owns the current deterministic cascade experiment: unit state, player activation ownership, force/motion, physical events, claim reservation, reaction execution, environment stress, and round refresh.

`ChainCombatLabController.cs` is the main presentation/input shell. Additional prototype components add proactive setup controls, player-activation status, physical-event markers, and smoothed visual playback. Those presentation systems use the authoritative board but do not calculate combo recommendations.

The split is intended to support eventual server-authoritative multiplayer commands without replacing prototype physics rules.

## Intentionally missing

- network transport / server replication;
- simultaneous multi-device input;
- real voxel terrain/destruction integration;
- polished character animation/VFX/audio;
- pathfinding;
- sophisticated enemy disruption/counterplay;
- large production roster/content authoring pipeline;
- campaign progression;
- persistent battle setup;
- final reaction-frequency/command-resource tuning.

Those should wait until the small lab proves that player activation + capability knowledge + claimable reaction handoffs are actually fun.
