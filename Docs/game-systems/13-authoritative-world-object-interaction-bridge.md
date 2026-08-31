# 13. Authoritative world-object interaction bridge

**Status:** Approved

## Purpose

Connect gameplay characters and player/AI intent to the repository's existing authoritative `WorldObject` interaction runtime without creating a second interactable hierarchy or moving object-specific behavior into character/gameplay code.

The repository already has a mature WorldObject substrate with stable identity, capabilities, state, signals/actions, persistence, deterministic geometry, generated-content placement, runtime registries, and dynamic presentation. System 13 therefore exists only as the gameplay-facing ingress and validation boundary.

Conceptually:

`CharacterId + WorldObjectId + semantic action`

→ interaction authorization/validation

→ existing WorldObject runtime

→ state transition / signals / persistence

→ semantic result/events

## Existing foundation to reuse

The existing WorldObject architecture already owns:

- stable object identity independent of presentation
- semantic capabilities
- runtime state and state machines
- object-specific interaction behavior
- signal/action routing
- persistence deltas and sparse retained state
- generated structure/decoration integration
- streamed registry lifecycle
- backend-neutral presentation plans and Unity realization
- reusable mechanism presets such as lever→door, pressure-plate traps, powered elevators, chained controls, and lock/key-style gating hooks

System 13 must not reimplement any of those responsibilities.

## 1. WorldObject remains the canonical interactable

Generated doors, levers, gates, traps, containers, secret mechanisms, elevators, lights, and other interactive world features continue to use the existing WorldObject identity/state/behavior model.

System 13 must not introduce a parallel `GameplayInteractable` object whose state has to be synchronized with WorldObject state.

There is one authoritative world-object state.

## 2. Interaction request identifies actor, object, and semantic action

Gameplay enters the interaction system through a semantic request, conceptually:

- `CharacterId`
- `WorldObjectId`
- requested semantic action/capability

Examples include Open, Close, Activate, Use, Pull, Push, Enter, or another repository-native action identity where the WorldObject catalog distinguishes it.

Shared gameplay must not issue Unity-specific calls such as `SendMessage`, directly mutate presentation objects, or set object state fields.

## 3. Presentation selection resolves to stable object identity

Player input may use a Unity raycast or another presentation-facing selection mechanism, but that result must resolve to the stable authoritative `WorldObjectId` before it enters shared gameplay.

The Unity `GameObject`, collider, or proxy is presentation/input plumbing rather than persistent gameplay identity.

Streaming or presentation rebuilds therefore do not change the logical object being interacted with.

## 4. Actor/context validation is the bridge's core responsibility

The existing WorldObject runtime knows what the object can do. System 13 adds the minimal actor-side/context validation required to decide whether a gameplay character may request that action now.

Validation may include, where required by demonstrated gameplay:

- character exists and is active
- target object exists and is currently available
- requested action is actually offered by the target
- character is within authoritative interaction range
- reachability/line-of-use constraints when an action requires them
- character state permits interaction

Do not turn this into a broad stat/permission framework. Add richer requirements only when actual gameplay needs them.

## 5. WorldObject runtime still executes object behavior

After authorization, the bridge delegates to the existing WorldObject runtime.

Example:

`character activates lever`

→ bridge validates actor/context

→ existing lever behavior changes lever state

→ existing signal routing targets connected gate

→ gate behavior changes authoritative gate state

→ existing persistence records the player-visible state changes

System 13 must not centralize door, lever, trap, elevator, light, container, or secret-mechanism logic into a giant interaction service.

## 6. Cross-domain effects use narrow semantic adapters/events

Some interactions cross from WorldObject behavior into another authoritative gameplay domain. Those integrations should occur through narrow semantic contracts rather than giving WorldObject dependencies on every gameplay system.

Examples:

### Containers

WorldObject owns the container's world interaction/open state. Item transfer delegates to systems 09 and 10.

WorldObject must not directly edit a character inventory.

### Quest/story-relevant objects

Interaction may emit/report a semantic gameplay fact. System 11/story/campaign policy decides whether progression changes.

The object does not embed quest-step identifiers or progression policy in shared behavior.

### Encounter-triggering mechanisms

An interaction may emit a semantic event/request that campaign or encounter composition maps to system 05/12 behavior.

The lever, alarm, gate, or trigger object does not become an encounter manager.

## 7. Secret discovery remains a separate authority

WorldBuilder secret planning decides what secret routes/clues exist and where they are realized.

The interactable runtime owns route mechanism behavior and state.

A discovery runtime owns first-discovery detection, persistence, party credit, and reward policy.

System 13 only provides the character/gameplay ingress into the already-existing interaction authority. It does not create secret planning, clue generation, secret persistence, or reward policy.

## 8. Server-authoritative from the start

For multiplayer, clients submit interaction intent rather than authoritative object state.

Conceptually:

`Character X requests action Y on WorldObject Z`

The authoritative simulation validates and applies the request.

Clients must not submit claims such as `Door Z is now open`.

Resulting authoritative WorldObject state/events are replicated through system 06.

## 9. Competing requests are processed deterministically

Multiple characters may attempt to interact with the same object during the same simulation window.

System 13 must ensure requests enter authoritative execution in deterministic simulation order while reusing WorldObject's existing state-machine/action semantics.

Do not create a second generic concurrency state machine around WorldObject.

Domain-specific races remain with their owning systems, for example:

- competing pickup claims → system 10
- inventory transfers → system 09
- encounter membership → system 05

## 10. Streaming does not change gameplay identity

When a streamed object's Unity presentation unloads, its stable WorldObject identity and persisted authoritative state remain conceptually the same object.

When the object is realized again, presentation resolves back to that same identity/state.

System 13 therefore never treats `GameObject` lifetime as gameplay-state lifetime.

## 11. Interaction discovery is capability-driven

Input/UI/AI should be able to determine conceptually:

> Which semantic actions may Character A perform on WorldObject B now?

The result is derived from:

- WorldObject capabilities/state
- actor/context eligibility

Avoid a global type switch over every object kind such as Door/Chest/Lever/Trap.

The point of the existing WorldObject substrate is that many object families share the same semantic interaction path while keeping object-specific behavior behind their registered definitions/state machines.

## 12. Semantic interaction result

The bridge should return or publish a small semantic result rather than presentation details.

Conceptually it may include:

- accepted/rejected
- actor identity
- object identity
- requested action
- semantic rejection reason where useful
- authoritative resulting event/change reference where needed by downstream systems

It should not return animation names, prompt strings, sound names, prefab references, or other presentation policy.

## Reuse / integration proof

### Mechanism interaction

1. A character targets an existing generated lever.
2. Presentation/input resolves the lever to its stable WorldObject identity.
3. The character submits semantic interaction intent.
4. System 13 validates actor/context.
5. Existing WorldObject lever behavior executes.
6. Existing signal routing activates the connected door/gate.
7. Existing persistence reflects the resulting object state.

This proves system 13 reused the WorldObject runtime instead of replacing it.

### Container interaction

1. A character interacts with an existing generated container.
2. System 13 validates the interaction.
3. WorldObject owns the container's world-state transition.
4. Item transfer delegates to systems 09/10.
5. No WorldObject implementation directly mutates character inventory.

This proves the boundary also works across authoritative gameplay domains.

## Out of scope

- WorldObject identity/state/capabilities/behavior/persistence
- WorldObject geometry or Unity presentation
- mechanism signal routing
- generated physical placement
- secret/clue planning and realization
- discovery persistence/rewards
- inventory transactions (system 09)
- loot/pickup/container item transfer (system 10)
- quest/objective progression (system 11)
- encounter lifecycle/world realization (systems 05/12)
- gameplay-state replication (system 06)
- HUD prompts (system 17)
- audio/VFX feedback (systems 21/22)

## Architectural constraints

- `WorldObject` remains the single authoritative interactive-world substrate.
- Shared APIs use stable semantic identities and actions rather than Unity object references.
- Character/context authorization remains narrow and authoritative.
- Object-specific behavior stays inside the existing WorldObject runtime/content definitions.
- Cross-domain consequences use semantic adapters/events rather than direct state mutation across subsystem boundaries.
- Multiplayer clients request intent; the authoritative simulation determines resulting object state.
- Streaming/presentation lifetime must not redefine WorldObject identity or persisted state.

The defining rule is:

> WorldObject answers "what does this object do?" System 13 answers "may this gameplay character do it now?" and safely routes that intent into the authoritative runtime.
