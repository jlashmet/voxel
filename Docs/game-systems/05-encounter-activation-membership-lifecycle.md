# 05. Encounter Activation, Membership & Lifecycle

## Decision

Create a first-class runtime encounter system, distinct from both cutscenes and combat.

- **Cutscene** = authored choreography/presentation. It moves/faces actors, plays dialogue/camera/audio, and waits for presentation/gameplay operations to complete.
- **Encounter** = temporary authoritative gameplay situation. It owns participant membership/context, activation/resolution state, and semantic outcome.
- **Combat** = one possible gameplay subsystem an encounter can activate.

An encounter may have no cutscene, a cutscene may exist without an encounter, and an encounter may use intro/outro cutscenes.

## Why this is needed

WorldBuilder already owns semantic world structure such as sites, settlements, NPCs, objectives, story rules, secrets, loot tables, and authored placement relationships. The missing layer is not another spatial-generation system; it is runtime coordination for situations that temporarily involve characters in a shared gameplay context.

This must work for both:

1. **Persistent world characters** that already have autonomous lives, such as town guards.
2. **Encounter-created characters** such as temporary roadside attackers.

Encounter membership must not create a second character model.

## Core identities

Use separate semantic and runtime identities:

- `EncounterRef` — stable content identity, for example `kentridge_gate_ambush`.
- `EncounterInstanceId` — one runtime execution of an encounter definition.
- `CharacterId` — existing character identity from the generic character runtime.

An encounter definition refers to semantic content. An active encounter instance refers to concrete `CharacterId` members.

## Encounter definition

A definition should describe only semantic/configuration data required by the situation, such as:

- encounter identity
- semantic location/site requirement
- participant/member requirements
- encounter-local roles or team/relationship assignments
- activation requirements
- completion policy
- lifetime policy for encounter-created characters
- whether the encounter invokes combat

Shared encounter APIs must not embed scene coordinates, prefab names, or campaign-specific character IDs as hard-coded policy.

## Participant sources

### Existing persistent characters

Examples: guards, townspeople, companions, authored NPCs.

Activation resolves required existing characters to `CharacterId`s and temporarily enrolls them in the encounter.

Their normal autonomous-life state is interrupted by the encounter. When the encounter ends, they remain characters and resume/replan their normal lives.

### Encounter-created characters

Examples: bandits created for an ambush.

The encounter may request character creation through the generic character runtime. Once created, these are ordinary characters with `CharacterId`s; they are not special combat-only enemy objects.

The encounter records that it owns their temporary lifetime so cleanup can occur after resolution/presentation/loot requirements are satisfied.

## Lifecycle

Recommended lifecycle:

`Dormant -> Activating -> Active -> Resolving -> Completed`

Only add cancellation/abortion states when demonstrated gameplay requires them.

### Activation responsibilities

Activation should:

1. resolve the encounter definition
2. resolve its semantic world location
3. resolve required existing characters
4. create required temporary characters
5. assign encounter membership/context
6. establish encounter-local roles/teams/relationships
7. activate required gameplay systems, including combat where applicable
8. transition to `Active`

The system should reject invalid activation rather than partially creating an encounter with missing required members.

## Membership is contextual

A character should not permanently become an "enemy" because it participates in one encounter.

Encounter membership supplies temporary context, for example:

- encounter role
- encounter team/side
- encounter objective role
- lifetime ownership flag

Relationship/team interpretation remains encounter/gameplay context rather than a permanent `IsEnemy` field on the generic character.

## Combat relationship

For combat encounters:

`Encounter -> Production Combat Integration -> existing combat runtime`

The encounter defines/resolves membership. Production combat integration maps those `CharacterId`s to combat participants. Combat executes authoritative combat rules and returns a semantic result. The encounter consumes that result when evaluating resolution.

Combat must not spawn characters or own their persistent lifetime.

## Completion policies

Do not bake "all enemies dead" into the generic encounter abstraction.

Reusable completion policies may include, as required by actual content:

- opposing side defeated
- specified character defeated
- objective/interaction completed
- actor reaches/leaves a region
- escape succeeds
- external story condition completes

The first production slice can implement only the completion forms actually needed.

## Resolution and cleanup

On resolution:

### Persistent characters

- leave encounter membership
- remain in the character registry
- retain authoritative vitality/state
- autonomous AI resumes/replans life

### Encounter-owned temporary characters

- remain until dependent presentation/loot/gameplay work has finished
- then follow the encounter's explicit cleanup policy

Character defeat must not directly destroy presentation objects or bypass encounter cleanup. Defeat originates from the vitality system and is observed by interested systems.

## Events

Expose semantic lifecycle events such as:

- `EncounterActivated`
- `CharacterJoinedEncounter`
- `CharacterLeftEncounter`
- `EncounterCompleted`

`EncounterCompleted` should carry a semantic `EncounterOutcome` rather than leaking combat internals.

Story, quests, networking, HUD/presentation, and other systems can react to these events independently.

## WorldBuilder boundary

WorldBuilder should remain responsible for realizing valid semantic places and spatial opportunities.

Encounter runtime should remain responsible for activating and coordinating gameplay situations using those places.

A later WorldBuilder-to-gameplay encounter integration system can bridge semantic encounter location requirements to realized world locations without putting runtime encounter policy into WorldBuilder.

## Cutscene boundary

Keep the distinction explicit:

- cutscenes choreograph/present
- encounters coordinate authoritative gameplay context

Typical flow may be:

`Encounter activates -> optional intro cutscene -> interactive gameplay/combat -> encounter resolves -> optional outro cutscene`

But none of those phases requires the other abstraction to exist.

## Reuse proof

Require two independent fixtures:

### Road ambush

- activates an encounter
- creates temporary hostile characters
- starts combat through production combat integration
- resolves from semantic combat outcome
- cleans up temporary membership/lifetime correctly

### Town guard confrontation

- resolves existing autonomous town guards
- temporarily enrolls them in an encounter
- interrupts normal-life behavior with tactical behavior
- resolves the encounter
- releases the guards back to autonomous town life without recreating them

The second fixture is required to prove encounters do not own a parallel character model.

## Explicitly out of scope

This system does not own:

- generic character implementation
- vitality/damage/defeat
- AI decision-making
- combat rules
- WorldBuilder spatial generation
- cutscene choreography
- loot rules
- quest/story policy
- multiplayer replication
