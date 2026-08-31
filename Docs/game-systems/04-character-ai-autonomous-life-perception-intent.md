# 04. Character AI, Autonomous Life, Perception & Intent

## Status

Approved design direction.

## Goal

Provide one reusable AI-control architecture for any non-player-controlled character, including town NPCs, companions, guards, creatures, and hostile combatants. Combat AI is one mode of character behavior, not the definition of AI.

## Core principles

- AI controls the generic gameplay character runtime from system 03.
- Characters can have persistent autonomous lives outside combat.
- Separate long-horizon life behavior from short-horizon tactical behavior.
- AI consumes semantic perception rather than directly inspecting arbitrary Unity objects.
- AI emits semantic intents; authoritative gameplay systems validate and execute them.
- Relationship/allegiance is semantic and contextual; do not bake `IsEnemy` into the generic character model.
- Defeat is event-driven through system 02. AI does not own health or death state.
- Configuration and reusable policies should express most behavioral variation; avoid one bespoke controller class per character type.

## Character life model

A character may maintain long-lived gameplay state such as:

- role/archetype, for example merchant, blacksmith, guard, civilian, companion, creature
- home/work/important-place relationships
- current activity
- current goal
- current destination or interaction target
- relevant remembered/known facts when gameplay requires memory
- interruption state and resumption/replanning state

Examples of life activities include working, traveling, socializing, resting, using a location, visiting another actor, guarding an area, or wandering within an allowed region.

This is gameplay state, not merely an animation timeline.

## Long-horizon and tactical planning

### Life planning

Life planning operates at a relatively coarse cadence and chooses goals such as:

- go to work
- return home
- visit a social location
- operate a shop or workstation
- patrol or guard an area
- seek another character
- rest or idle within a meaningful location

Content may supply routines, preferences, places, and constraints. The runtime should determine how to satisfy those goals rather than hard-coding every NPC as a frame-by-frame scripted schedule.

If meaningful world time later exists, schedules may use it. A full day/night system is not a prerequisite for believable character lives.

### Tactical planning

Tactical behavior handles urgent local situations such as:

- combat
- danger/fleeing
- protecting another actor
- investigating a disturbance
- reacting to an immediate interaction

A tactical mode temporarily interrupts life behavior. When the interruption ends, the character resumes or replans its long-horizon goal.

Example:

`Working -> PerceivesThreat -> Flee/Combat -> ThreatResolved -> ReplanLifeGoal`

## Perception

AI should operate on a semantic `PerceptionSnapshot` or equivalent rather than searching raw scene objects.

Useful perceived facts may include:

- perceived character identity
- position
- relationship to self
- visible/known state
- alive/defeated state when legitimately observable
- relevant interactables/locations
- relevant obstacles or world hazards
- actions currently available to the character

The perception layer determines what the AI is allowed to know. The decision layer should not read unrestricted authoritative world state and accidentally cheat.

## Relationships

Use a semantic relationship/faction policy rather than `IsEnemy` flags embedded throughout the codebase.

A minimal relationship model may include:

- Ally
- Neutral
- Hostile

Expand only when demonstrated gameplay requires it.

The same framework must therefore support enemies, guards, civilians, recruits, neutral NPCs, and companions.

## Intent and command boundary

AI produces semantic intents such as:

- move to a destination
- move near a target
- interact with a target
- attack a target
- use an available ability
- flee from a threat
- hold position
- perform a life activity

AI does not directly:

- modify hit points
- write Unity transforms as authoritative gameplay state
- complete quests
- mutate combat outcome
- spawn/despawn arbitrary actors

The appropriate authoritative system validates and executes the intent.

## Inspectable intent

Preserve the useful idea from the existing chain-combat prototype that tactical AI can commit to inspectable intentions rather than silently retargeting every frame.

An `AiIntent` may include:

- acting character
- semantic action
- target character/location
- optional destination
- planned/executed/invalidated state

This allows readable/telegraphed enemy behavior where desired without requiring every AI action to be exposed to the player.

## Target selection and policies

Target selection should be reusable policy, not duplicated inside every action.

Examples include:

- nearest hostile
- most vulnerable hostile
- protect an ally
- investigate a disturbance source
- reach a work/home/social target

Behavior variation should primarily come from configuration such as AI profiles, capabilities, priorities, and target-selection policies. Unique mechanics may provide specialized actions without requiring a deep subclass hierarchy.

## Planning cadence

Do not require every character to fully replan every Unity frame.

Planning may occur on:

- relevant perception changes
- completion/failure of a current intent
- tactical round/action boundaries
- coarse autonomous-life intervals
- controlled authoritative server ticks

## Simulation LOD

Town populations and other persistent actors require simulation detail to scale with relevance.

Nearby/important actors may use detailed:

- perception
- navigation
- interaction
- animation/presentation realization

Distant or off-screen characters should be able to update coarsely, for example retaining semantic state such as `AtSmithy/Working`, without executing full pathfinding and perception every frame.

When detailed simulation becomes necessary, the character should realize from its coarse semantic state at a valid and believable position/state.

Simulation LOD must preserve authoritative gameplay outcomes; it should reduce simulation detail, not create a second incompatible character model.

## Integration with approved systems

- System 02 owns vitality, damage, and defeat transitions.
- System 03 owns character identity, runtime state, pose, and generic movement/control seams.
- AI observes `CharacterDefeated` and stops/replans without polling character HP every frame.
- System 01/combat validates tactical combat actions produced by AI.
- Encounter spawning/lifecycle decides which encounter actors exist; AI decides what an AI-controlled actor does after it exists.

## Existing code to reuse conceptually

`ChainEnemyTacticalAI` contains useful concepts such as deterministic scoring, target vulnerability/distance evaluation, action intents, and committed tactical plans. Production AI should preserve those useful mechanics while removing coupling to `ChainCombatBoard`, concrete recruit kinds, direct HP mutation, and reflection-based execution bridges.

## Out of scope

This system does not own:

- vitality/damage/death rules
- generic character lifecycle
- encounter spawning policy
- combat authority
- quest/story progression policy
- loot
- animation/VFX/audio presentation
- multiplayer replication implementation

## Reuse proof

At minimum, prove the same AI framework with substantially different consumers:

1. a town NPC that pursues an autonomous life goal outside combat, can be interrupted, and resumes/replans afterward
2. a tactical hostile or companion that perceives actors, chooses a target/action, and submits authoritative semantic intents

The proof should demonstrate that both use the same character, perception, intent, and planning abstractions rather than two parallel AI systems.
