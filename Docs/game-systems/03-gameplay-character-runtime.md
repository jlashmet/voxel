# 03. Gameplay Character Runtime

Status: approved design direction

## Purpose

Provide one authoritative, reusable runtime representation for gameplay characters so players, NPCs, recruits, and enemies do not each invent separate actor state.

Enemies are a composition of this system, not a separate inheritance hierarchy or god-object controller.

## Core principles

- Character identity is stable and independent of combat sessions, cutscenes, quests, and presentation objects.
- Character definitions describe reusable configuration; character instances hold runtime state.
- Gameplay-relevant pose is authoritative state, not a Unity `Transform` treated as the source of truth.
- Capabilities are composed around the character rather than accumulated into a monolithic `EnemyController`.
- Scene/place/campaign-specific policy remains in composition/content.

## Character identity

Introduce a stable `CharacterId` (or equivalent semantic runtime identifier).

Other subsystem identifiers may resolve to it:

- `NpcRef -> CharacterId`
- player slot -> `CharacterId`
- `CombatParticipantId -> CharacterId`

Subsystems should not create duplicate runtime representations of the same character.

## Definition vs instance

A character definition may reference reusable configuration such as:

- archetype/content identity
- presentation reference
- vitality configuration
- movement capability/configuration
- AI configuration reference when AI-controlled
- interaction capability when applicable

A runtime character instance owns state such as:

- `CharacterId`
- authoritative position
- authoritative facing
- active/removed lifecycle state
- composed capability references/state

Specific content such as a particular goblin or named NPC belongs in data/composition, not shared runtime rules.

## Character registry

Provide an authoritative lookup/registry seam, e.g. `ICharacterRegistry`, that can:

- resolve a `CharacterId`
- register/remove runtime characters
- resolve semantic NPC bindings
- resolve player-slot bindings

Existing integrations such as `IWorldBoundCutsceneActorProvider` should become thin adapters over this shared runtime rather than maintaining a separate actor universe.

## Authoritative pose and movement seam

The character runtime owns gameplay-relevant position and facing in engine-independent state.

Expose semantic operations for authoritative placement/movement/facing without embedding the reason for movement into the character runtime. Player input, AI, cutscenes, networking, and other systems may all consume the same movement seam.

Pathfinding and AI decision-making are outside this system.

## Capability composition

A gameplay character can compose capabilities such as:

- vitality / defeat state (system 02)
- movement
- presentation adapter
- player input or AI controller
- combat participation when applicable
- interaction capability when applicable

Avoid a monolithic `EnemyController` that owns AI, health, combat, animation, loot, networking, and quest behavior.

## Lifecycle

Support the generic mechanism:

`Create/Register -> Active -> Removed`

This system provides character lifecycle mechanics only. Encounter policy such as which enemies to spawn, where, and when belongs to encounter spawning/lifecycle (system 05).

## Defeat integration

Vitality belongs to the character. When system 02 transitions a character from alive to defeated and emits an authoritative defeat event, independent consumers react:

- AI stops controlling the defeated character
- combat reevaluates encounter state
- presentation handles defeat/death visuals
- loot may react
- story/quests may react
- encounter/spawn lifecycle may eventually remove the character

No consumer should need to invoke a monolithic `enemy.Die()` workflow.

## Enemy composition

An enemy is primarily a gameplay character that is AI-controlled and may participate on an opposing combat team.

Do not bake a global `IsEnemy` switch deep into the character abstraction. Team/allegiance is context/configuration and may vary by encounter or future faction rules.

## Out of scope

- AI decisions, perception, and targeting (system 04)
- encounter spawn policy (system 05)
- combat rules (system 01)
- vitality calculations (system 02)
- loot (system 10)
- animation/VFX implementation
- networking implementation
- quest/story policy

## Reuse proof

Validation should demonstrate at least two independent consumers:

1. An AI-controlled enemy character using the shared character runtime and vitality.
2. A non-combat NPC/cutscene character resolved through `IWorldBoundCutsceneActorProvider` (or its successor adapter) using the same character runtime.

This proves the system is a generic gameplay actor runtime rather than a renamed enemy framework.
