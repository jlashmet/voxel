# Chain Combat Prototype

A deliberately small playable experiment for Mounting Force's turn-based, multiplayer-oriented chain-reaction combat.

## Goal

Test whether players can learn a roster's physical capabilities, arrange the battlefield, reserve emergent decisions, and invent long cooperative chains **without the UI calculating or highlighting combos**.

The prototype exposes causes and effects. It does not show compatible reactions, valid combo paths, combo counts, recommended characters, or suggested targets.

## Run it

1. Check out `prototype/chain-combat` and let Unity compile.
2. In Unity, choose **Mounting Force > Chain Combat Prototype > Open & Play**.
3. The launcher opens the current cascade lab.
4. Select a friendly capsule for proactive play, reserve unresolved physical events for P1-P4, and mark each player Ready when their proactive play is done.

The prototype intentionally does not modify Build Settings or require a committed scene asset.

## Four command groups

The current local hot-seat battle stands in for four network players:

- **P1:** Stephen, Brutus
- **P2:** Weldon
- **P3:** Madeline, Mira
- **P4:** Grom, Skitter

## One active recruit per player

Each round, the first recruit a player successfully uses for proactive play becomes that player's **active recruit** for the round.

That recruit gets:

- one reposition;
- one proactive action;
- its normal personal reaction, if a matching event later occurs.

The move and action may happen in either order. A failed target/placement attempt does not commit the activation.

A second recruit in the same command group cannot take another proactive turn that round. It **can still claim and execute reactions**. This is the core scaling experiment: adding dozens of recruits should expand the player's reaction/toolbox possibilities without creating dozens of normal turns.

The activation overlay shows only who each player committed and whether that recruit's move/action are spent. It does not reveal reaction compatibility.

## Player Ready state

The enemy phase is no longer controlled by one global End Round button. Each living player group independently marks itself **Ready** when it is done with proactive play.

Ready means only:

- that player cannot start or finish additional proactive moves/actions;
- the player may still reserve physical events;
- every living recruit in that player's roster may still claim/execute reactions normally.

Ready is revocable until the enemy phase actually begins. A player can also Ready without activating a recruit at all, which is the explicit “pass my proactive turn” case.

Enemies may act only when:

1. every living player group is Ready; and
2. no physical event is still unresolved.

This prevents one player from accidentally ending everyone else's turn while keeping reactions live throughout the cooperative round.

`ChainRoundReadinessCoordinator` owns this multiplayer/application state above the deterministic combat board, parallel to reaction reservation.

## Player-first reaction reservation

Reaction ownership is intentionally split into two decisions:

1. **Reserve the physical event for a player.** P1-P4 can say “I’ve got this” without selecting an ability yet.
2. **Choose the concrete answer.** The reserving player selects one of their recruits, tries the capability they think applies, then aims/executes it.

Reservation does **not** prove that the reserving player has a valid answer. A player may reserve an event, inspect their roster, try an ability that does not apply, and remain the owner while thinking again. This prevents the reservation UI from becoming a disguised combo hint.

A concrete recruit/ability choice can be released while keeping the player reservation. The player can reconsider without reopening a click race. Releasing the player reservation gives the physical event back to the whole party.

A reservation owns **one physical decision only**. When that reaction resolves and creates a new collision/tree impact/etc., the new event begins unreserved so another player can take the handoff.

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

A normal round now has four intertwined layers:

1. **Proactive activation:** each player chooses at most one recruit to move/set up/attack with.
2. **Player reservation:** when a meaningful physical event occurs, one player takes ownership of deciding whether/how to answer it.
3. **Concrete reaction:** any living recruit belonging to that player may attempt its own reaction once per round if the player believes its capability applies.
4. **Ready gate:** players independently close proactive play; the enemy phase begins only after everyone is Ready and all physical decisions are finished.

The simulation pauses on three event types in this slice:

- airborne;
- creature collision;
- tree impact.

The game reports only what physically happened: participants, location, direction when relevant, and impact force. It does not enumerate characters who can answer it.

A reserved event cannot be globally passed out from under its owner. The owner must either execute an answer or release the reservation. An unreserved airborne event can be passed to let motion continue; passing a stopped collision/tree event ends that branch.

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
- event reservation shows only player ownership;
- Ready shows only whether that player has closed proactive play;
- no eligible-reactor highlights or combo arrows are drawn.

Authoritative gameplay remains integer/grid/deterministic. The smoothing and event-marker layers are presentation only and never feed Unity transforms back into simulation state.

## Cooperative chain telemetry

The board tracks:

- deliberate cascade steps;
- distinct player command groups participating;
- actual player-to-player handoffs.

This is post-action feedback for tuning cooperative play, not a pre-action hint system.

## Architecture

`ChainCombatBoard.cs` owns deterministic combat: unit state, player activation ownership, force/motion, physical events, concrete reaction claims/execution, environment stress, and round refresh.

`ChainReactionReservationCoordinator.cs` owns player-level reservation of the current physical decision. It deliberately does not determine whether the player has a compatible reaction.

`ChainRoundReadinessCoordinator.cs` owns per-player Ready state and the all-ready/no-unresolved-event gate into the enemy phase. Ready never disables reactions.

`ChainCombatLabController.cs` is the main presentation/input shell. Additional prototype components add proactive setup controls, player-activation status, physical-event markers, and smoothed visual playback. Those presentation systems do not calculate combo recommendations.

## CI

The combat prototype uses one consolidated automatic workflow: `.github/workflows/chain-combat-ci.yml`. It now runs the prototype plus V2-V7 suites sequentially with branch-level `cancel-in-progress` concurrency so new combat iterations supersede stale ones instead of flooding the self-hosted Unity runner. Older per-version workflows remain manual-only fallbacks.

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
- final reaction-frequency/command-resource tuning;
- timeout/disconnect policy for a player who reserves an event and then stalls.

The next major question after the coordination layer is stable is enemy counterplay: enemies need readable, disruptable intentions that force the party to improvise rather than treating every fight as a static combo sandbox.
