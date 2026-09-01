# 22. Combat / interaction VFX & semantic feedback

**Status:** Approved

## Purpose

Provide reusable client-side visual feedback for combat and world interaction without turning visual effects into gameplay authority or creating a second simulation of combat, destruction, or WorldObject state.

> **Gameplay says what happened; VFX decides how that confirmed fact is made visible.**

Conceptually:

```text
semantic gameplay state/events
        |
        +--> vitality / damage
        +--> combat results
        +--> interaction results
        +--> WorldObject state transitions
        +--> loot/progression when appropriate
        |
        v
VFX presentation adapters
        |
        v
semantic VfxCueRef + origin/context
        |
        v
VFX runtime
        |
        v
Unity realization
```

Systems #17 HUD, #21 audio, and #22 VFX are sibling presentation consumers of semantic gameplay truth. Do not introduce a generic `FeedbackManager` that becomes a new owner of gameplay semantics.

## Design

### 1. Keep Unity VFX types out of gameplay APIs

Combat, vitality, characters, interactions, and WorldObjects must not expose Unity presentation types such as `ParticleSystem`, `VisualEffect`, `GameObject` effect prefabs, `Material`, `Renderer`, or direct `PlayImpactEffect()`-style calls.

Gameplay results likewise must not carry prefab names, material choices, colors, or concrete effect asset identities.

### 2. Domain events do not carry VFX asset identity

Do not add `VfxCueRef` merely because a domain event may need presentation. A semantic event such as `ActorDamaged` remains reusable by AI, HUD, audio, replication, tests, quests, and headless simulation.

Presentation policy maps semantic context to a presentation cue:

```text
ActorDamaged
    + semantic actor/content context
        -> VFX presentation policy
            -> VfxCueRef
```

### 3. Keep the reusable presentation API narrow

If implementation demonstrates multiple consumers, a suitable boundary is conceptually:

```text
Game.Vfx.Api
    VfxCueRef
    VfxPresentationRequest
    VfxOrigin
    IVfxPresentation

Game.Vfx.Runtime
    cue/content resolution
    instance lifecycle/pooling
    spatial resolution
    culling/budgeting
    Unity realization
```

Cross-module consumers may depend on `Game.Vfx.Api`, never `Game.Vfx.Runtime`. Most gameplay domains should need neither dependency; composition/presentation adapters consume gameplay APIs and invoke VFX.

### 4. `VfxCueRef` is semantic presentation identity

Examples may eventually include `combat.hit.light`, `combat.defeat`, `world.interaction.activate`, or `world.mechanism.transition`.

A cue does not encode whether Unity realization uses particles, VFX Graph, a mesh, shaders, a short-lived prefab, or a procedural combination. That remains presentation/content policy.

### 5. Do not standardize a rendering technique prematurely

The #22 contract must not be designed around ParticleSystem, VFX Graph, shaders, decals, prefab spawning, terrain rendering, HLOD, or any other particular renderer implementation. #22 consumes presentation infrastructure; it does not become another renderer.

### 6. One-shot effects come from transitions/events

Examples include:

- an authoritative hit producing an impact effect;
- `ActorDefeated` producing a defeat effect;
- an accepted mechanism activation producing an activation effect;
- a WorldObject transition such as `Closed -> Opening` producing a transition effect.

A snapshot stating that an actor is defeated or a gate is open must not replay the historical death/opening effect. This is essential for reconnect, late join, restore, and replication repair.

### 7. Persistent visual treatment derives from current state

Ongoing presentation, when demonstrated by content, is reconstructed from current state rather than historical events. Examples could include a powered mechanism treatment, a local interaction-target affordance, or a local combat-selection treatment.

The distinction is:

```text
one-shot effect        -> event/transition driven
persistent treatment  -> current-state driven
```

### 8. Separate local anticipation from authoritative consequence

Purely local anticipatory feedback may render immediately, such as a weapon swing trail, hover highlight, or local target affordance.

Effects that communicate authoritative consequence—impact, damage, defeat, successful activation, authoritative destruction—follow the authoritative result.

### 9. Predicted and authoritative effects reconcile

When local prediction creates an effect and the authoritative result later arrives, presentation policy must avoid blindly rendering duplicates. Anticipation and confirmation may be intentionally distinct, or prediction may be reconciled/deduplicated.

Reuse existing event/tick/revision identity from #06 where practical rather than inventing a network-wide VFX event identity.

### 10. Resolve spatial presentation through semantic identity

Effects attached to gameplay entities use stable identities such as `CharacterId` or `WorldObjectId`, not permanent Unity `Transform` references.

Conceptually:

```text
VfxOrigin
    Character(CharacterId)
    WorldObject(WorldObjectId)
    WorldPoint(...)
```

The local presentation layer resolves the currently realized Unity representation at playback time, allowing streaming/re-realization without changing gameplay identity.

### 11. Keep impact geometry engine-neutral

When a real effect requires spatial impact data, carry only the minimal engine-neutral geometry required by presentation, such as world position, surface direction/normal, and optionally a semantic surface/material trait.

Do not expose `RaycastHit`, `Collider`, `Transform`, or `Renderer` through shared combat or interaction APIs merely for VFX.

### 12. WorldObject presentation remains WorldObject presentation

#22 must not create shadow object types such as `VfxDoor`, `VfxLever`, or `VfxChest` with copied gameplay state.

Instead:

```text
WorldObject semantic transition
    -> presentation adapter
        -> optional transient VFX
```

Normal WorldObject presentation remains responsible for the object's persistent realized state.

### 13. Authoritative voxel destruction is not VFX

A destructive operation may both mutate voxel/world state and emit dust/debris, but those are separate responsibilities:

```text
authoritative world mutation:
    explosion -> voxel/world mutation system

presentation:
    confirmed explosion/result -> dust/debris effect
```

If VFX is disabled, the identical authoritative voxel mutation must still occur.

### 14. Cosmetic debris is not authoritative geometry

Visual debris may fly, fade, disappear, be culled, or use client-local randomness. It must not become authoritative collision/gameplay state by accident.

If debris is intended to persist as a real gameplay object with collision, damage, inventory, or interaction semantics, ownership leaves the VFX domain and belongs to the appropriate gameplay/world system.

### 15. Avoid mesh-dependent durable identity

A mutable voxel world can regenerate meshes, so #22 must not rely on durable mesh triangle, renderer, or collider identities. Transient effects may use the currently realized surface, but persistent gameplay state requires semantic/world identity owned outside VFX.

### 16. Presentation metadata may refine effects

The same semantic event may resolve different effects based on character, object, or content presentation metadata. For example, `ActorDefeated` may map differently for unrelated character archetypes; an activated mechanism may map differently by mechanism presentation metadata.

Such variation belongs to composition/content, not enemy subclasses or WorldObject gameplay branches created solely for VFX selection.

### 17. VFX failure never changes gameplay

Failure to instantiate an effect, client culling, exhausted effect budgets, missing content, or a currently unrealized emitter must never roll back or modify damage, defeat, interaction, pickup, WorldObject state, quest progression, or voxel destruction.

### 18. Multiplayer replication remains semantic

Servers replicate gameplay facts, not prefab-spawn commands. A client receives semantic authoritative state/events and derives its own suitable visual presentation locally.

This keeps headless servers free of VFX implementation, allows quality-level differences between clients, and keeps network contracts gameplay-oriented.

### 19. Reconnect and restore do not replay historical one-shots

Reconnect, late join, and #16 restore reconstruct current persistent visual treatments from current state but do not replay old hits, explosions, defeats, or interaction effects.

Snapshot application before `GameplayReady` likewise must not cause historical state to produce a burst of transient effects. Only new post-ready transitions drive new one-shot VFX.

### 20. Client VFX budgets are implementation details

Pooling, lifetime, LOD, distance culling, maximum active count, batching, quality tiers, and effect replacement remain presentation-runtime concerns and never alter authoritative event processing.

### 21. Keep camera, haptics, and unrelated feedback separate

Do not automatically make #22 a universal game-feel framework. Camera shake, hit stop, controller vibration, screen-space post-processing, and floating damage numbers may consume the same semantic facts but have distinct ownership/lifecycle concerns.

Floating damage numbers belong to UI/HUD presentation. Camera behavior must respect existing camera/cutscene ownership. Haptics are a separate device-presentation concern.

### 22. Audio and VFX do not call each other

A semantic event fans out independently:

```text
HitResolved
    +--> #21 AudioPresenter
    +--> #22 VfxPresenter
    +--> #17 HUD presenter where applicable
```

Do not build a VFX manager that directly plays audio, shakes the camera, updates HUD, or otherwise owns cross-channel feedback semantics.

### 23. Introduce only demonstrated presentation adapters

Likely adapters may include:

```text
VitalityVfxPresenter
CombatVfxPresenter
WorldInteractionVfxPresenter
```

Additional adapters such as loot or progression VFX should be added only when real content demonstrates the need. Do not prebuild adapters for every domain.

## Acceptance / reuse proof

- **Vitality reuse:** damage two unrelated characters through #02. Both use the same semantic vitality-to-VFX integration while allowing different content treatment. No vitality code references Unity VFX types.
- **Interaction reuse:** activate two unrelated WorldObjects through #13. Their normal authoritative behaviors run first; #22 independently presents the confirmed transitions.
- **Authoritative destruction:** run a destructive world operation with VFX disabled and prove the identical authoritative voxel mutation still occurs.
- **Multiplayer:** client A causes an authoritative hit visible to client B. Client B receives normal semantic replication and presents one local effect; no prefab/effect asset ID is sent by the server.
- **Prediction:** a locally anticipated attack followed by authoritative confirmation produces the intended anticipation/confirmation presentation without duplicate impact effects.
- **Reconnect:** disconnect while hits/interactions occur and reconnect. Current state reconstructs without replaying historical transient effects.
- **State-driven presentation:** demonstrate one persistent visual treatment, destroy/rebuild its Unity realization, and prove the treatment reconstructs from current semantic state.
- **Alternate sink:** a fake `IVfxPresentation` verifies semantic cue/origin selection without Unity.
- **Headless:** combat, vitality, WorldObject interaction, and world destruction behave identically without #22 loaded.

## Explicitly out of scope

- damage or defeat authority;
- combat legality or hit resolution;
- authoritative voxel/world destruction;
- collision authority;
- WorldObject state machines;
- persistent WorldObject visual realization;
- renderer architecture;
- HLOD/far-world visibility;
- terrain/water/shader architecture;
- general animation architecture;
- VFX asset production;
- audio (#21);
- HUD/screens (#17-#20);
- camera-system overhaul;
- haptics;
- generic post-processing framework;
- generic global event bus;
- persistent cosmetic playback state;
- a universal `FeedbackManager` or game-feel framework.

## Architectural constraints

- Gameplay facts are authoritative; VFX is derived presentation.
- Unity VFX/rendering objects never cross gameplay API boundaries.
- Domain events do not carry VFX asset identity solely for presentation.
- Transient effects come from transitions; persistent treatments come from current state.
- Character/WorldObject IDs resolve presentation rather than becoming `Transform` references.
- Confirmed consequence effects follow authority; purely anticipatory/local effects may be immediate.
- Cosmetic debris is not authoritative voxel/world geometry.
- Clients derive effects locally from normal semantic replication; servers never send prefab-spawn commands.
- Reconnect/restore never replay historical one-shot effects.
- VFX failure/culling has zero effect on gameplay correctness.
- #17 HUD, #21 audio, and #22 VFX remain independent sibling consumers.
- Headless gameplay operates identically without #22.
