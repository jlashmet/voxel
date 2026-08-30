# 01 — Production Combat Integration

## Status

Approved design direction.

## Purpose

Integrate the existing production combat runtime into the actual game flow rather than creating another combat system.

The core boundary is:

`World / Story / Interaction -> EncounterRef -> Combat Integration -> existing ICombatService -> EncounterOutcome -> Story / Quest / Campaign`

Campaign and story should reason about semantic encounter outcomes such as "the Gate Ambush was won," not about attacks, hit points, turns, or individual combat implementation details.

## Existing foundation to reuse

The repository already has a production combat API/runtime with encounter sessions, participants/teams, movement, attacks, turns, hit points, winner detection, completion, input handling, and autonomous battle-driving support. This system should be integrated, not replaced.

## Responsibilities

### 1. Encounter definition

Introduce a semantic `EncounterRef` (for example `kentridge_gate_ambush`).

Encounter content/config defines:
- which combatants belong to the encounter,
- where/how the encounter connects to the world,
- meaningful outcomes such as player victory or defeat.

Shared combat code must not contain scene-, place-, or campaign-specific policy.

### 2. Encounter trigger

Gameplay requests `StartEncounter(EncounterRef)` through a semantic integration boundary.

The trigger may originate from story progression, entering a location, interacting with something, or another gameplay system. The integration layer resolves the semantic encounter into the existing combat runtime's encounter request.

### 3. World-to-combat participant binding

Existing world/player/enemy actors receive stable combat participant bindings.

Combat should operate on bound gameplay actors rather than silently creating a disconnected second representation of those actors.

### 4. Combat lifecycle coordinator

Own game-level encounter state such as:

`NotStarted -> Active -> Resolved`

Responsibilities include:
- starting the existing combat service,
- tracking which semantic encounter is active,
- preventing incompatible concurrent encounter starts,
- observing combat completion,
- producing the semantic encounter outcome.

### 5. Semantic combat outcome

Produce an outcome equivalent to:

`EncounterCompleted(encounterRef, winningTeam)`

Other game systems consume this semantic event rather than inspecting combat-runtime internals.

### 6. Campaign/story integration

Story/campaign rules may react to encounter results.

Example:

`gate_ambush completed / player won -> complete objective -> advance quest -> unlock next story event`

Campaign composition should depend on semantic combat integration contracts, not attack/HP/turn mechanics.

### 7. Presentation integration

Authoritative combat state drives the bound world actors.

Movement, action, defeat, animation, and other presentation are downstream of authoritative gameplay state. Scene-specific MonoBehaviours must not own combat rules.

### 8. Reuse/vertical integration proof

Provide an independent integration fixture proving:

`gameplay trigger -> encounter starts -> combat progresses -> winner produced -> campaign/story observes semantic result`

The fixture should validate the integration boundary rather than merely exercising the combat implementation in isolation.

## Explicitly out of scope

This system does not own:
- actor vitality/damage/defeat architecture,
- the general enemy actor framework,
- reusable enemy AI/perception/targeting,
- general encounter spawning,
- multiplayer replication,
- HUD,
- combat VFX/audio.

Those are separate systems so this work stays focused on integrating the existing combat runtime into production game flow.

## Architectural constraints

- Reuse the existing combat runtime rather than creating a parallel combat implementation.
- Keep shared APIs semantic and configuration-driven.
- Keep scene/campaign-specific encounter policy in composition/content.
- Campaign/story should consume semantic encounter outcomes, not combat internals.
- World actors and combat participants should have an explicit stable binding.
