# 02 — Actor Vitality, Damage & Defeat

## Status

Approved design direction.

## Core principles

1. **Vitality belongs to the character/actor, not to a combat session.**
2. **Defeat/death is event-driven from the authoritative vitality state transition.**

Combat, AI, quests/story, presentation, networking, loot, and other systems may react independently to defeat without owning or duplicating vitality state.

## Purpose

Provide one reusable authoritative vitality model for gameplay actors so combat and other gameplay systems do not each maintain their own hit-point/death implementation.

The intended boundary is:

`Actor -> VitalityState -> Damage/Healing -> VitalityChanged / ActorDefeated`

## Existing duplication to remove over time

The current production combat service owns combat hit points internally, while the chain-combat prototype has its own `UnitState.MaxHp`, `Hp`, and `IsAlive` model. Production integration should converge on actor-owned vitality instead of adding another parallel health implementation.

## Responsibilities

### 1. Stable actor identity

Vitality attaches to a gameplay actor/entity identity such as `ActorId`, not to `CombatParticipantId`.

Combat maps its participant identity to the owning actor identity:

`CombatParticipantId -> ActorId`

Leaving combat must not implicitly destroy persistent character vitality.

### 2. Vitality state

Keep the shared model intentionally small:
- `CurrentHealth`
- `MaxHealth`
- `IsAlive` / defeated state

Only add additional state such as invulnerability when demonstrated gameplay requirements need it.

Do not turn this system into a general RPG-stat framework.

### 3. Authoritative damage mutation

Gameplay code must not directly perform arbitrary `health -= amount` mutations.

Use a single semantic mutation path such as:

`ApplyDamage(DamageRequest)`

A damage request may carry only the semantics actually required by gameplay, for example:
- target actor,
- amount,
- source actor when applicable,
- cause/type when gameplay meaningfully distinguishes it.

Damage categories must not be invented speculatively.

### 4. Damage result

Return an explicit result such as `DamageResult` containing enough authoritative information for consumers to react without recomputing the transition:
- previous health,
- resulting health,
- amount actually applied,
- whether the damage caused defeat.

### 5. Event-driven defeat

Defeat is an authoritative state transition:

`Alive -> Defeated`

That transition emits a semantic event equivalent to:

`ActorDefeated(actorId, sourceActorId, cause)`

The vitality system does not perform presentation or encounter policy itself.

Consumers may include:
- combat team/winner evaluation,
- enemy presentation/death animation,
- encounter completion,
- quest/story progression,
- networking,
- loot or other later gameplay systems.

Defeat must be emitted exactly once for the transition rather than once per subsequent damage attempt.

### 6. Combat becomes a consumer

Combat decides whether a legal attack occurred and how much damage it requests.

Vitality decides the resulting actor health and whether the actor became defeated.

Combat then reacts to vitality/defeat state to determine combat-level outcomes such as whether one team has no living participants.

Production `CombatService` should therefore stop owning its private hit-point dictionary as actor vitality is integrated.

### 7. Lifetime is owned by the actor

Vitality survives or disappears according to actor lifetime, not encounter lifetime.

Example:

`player: 6 HP -> fight -> 3 HP -> encounter ends -> still 3 HP`

A disposable enemy may disappear when its actor is removed, while persistent player/character health remains attached to that character.

### 8. Server-authoritative mutation

Damage has one authoritative mutation path suitable for the repository's multiplayer architecture.

A client may request/express an action such as attacking actor X. It must not authoritatively set actor X's resulting health. The authoritative gameplay side validates the action and applies damage.

## Explicitly out of scope

Do not expand this system into:
- general character attributes,
- equipment calculations,
- buffs/debuffs,
- elemental resistance trees,
- armor unless later gameplay specifically requires it,
- stamina,
- regeneration,
- knockback,
- hit detection,
- animation/ragdolls,
- respawning.

Those concerns should remain separate or be added only from demonstrated game requirements.

## Reuse proof / acceptance direction

Provide an engine-independent fixture proving the vitality boundary outside the main combat integration:

`actor starts at 10 -> takes 3 -> 7 -> takes 8 -> 0 -> exactly one ActorDefeated event`

Then prove the production combat runtime consumes the same vitality implementation rather than maintaining its own health state.

## Architectural constraints

- Vitality is per character/actor.
- Defeat is event-driven.
- There is one authoritative mutation path.
- Shared vitality code contains no scene/campaign-specific policy.
- Presentation and downstream consequences react to state/events rather than being invoked directly from the health model.
