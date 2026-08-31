# 17. Production gameplay HUD & semantic presentation

**Status:** Approved

## Purpose

Provide the always-present, moment-to-moment gameplay presentation for a local player by projecting existing authoritative or replicated gameplay state into a small presentation model.

The defining rule is:

> The HUD tells the player what the game currently knows and what they can currently ask it to do; it never becomes the place where those gameplay facts are decided.

Conceptually:

```text
authoritative gameplay
    -> system 06 replication where multiplayer requires it
        -> local semantic gameplay/read state
            -> HUD presenters / presentation models
                -> Unity HUD view
```

The HUD is a consumer of gameplay truth, not another gameplay state machine.

## 1. Scope: moment-to-moment gameplay presentation

System 17 owns information that players need continuously or immediately while controlling a character.

Initial production responsibilities include demonstrated needs such as:

- locally controlled character vitality/defeat state;
- contextual interaction prompts/actions;
- relevant immediate combat/encounter state;
- targeting/selection presentation where gameplay requires it;
- short-lived semantic gameplay notifications where demonstrated;
- HUD visibility/mode appropriate to exploration, combat, UI, cutscene, disabled, or no-controlled-character state.

It does not absorb every game UI.

Explicit boundaries:

- inventory browsing/details -> **18**
- quest/objective journal/tracking UI -> **19**
- teammate/party/reconnect/session UI -> **20**
- gameplay audio -> **21**
- combat/interaction VFX -> **22**
- menus/settings/start/load/end flow -> **23**

Those systems may contribute small widgets to the gameplay HUD where appropriate, but they retain ownership of their own presentation semantics.

## 2. HUD state is derived, not authoritative

Do not create parallel gameplay fields such as `HudHealth`, `HudCombatActive`, or `HudCanInteract` that can diverge from owning systems.

Instead, presentation is derived from semantic state.

Examples:

```text
ActorVitalitySnapshot -> VitalityPresenter -> health-bar model

CharacterId + interaction eligibility -> InteractionPresenter -> prompt model

CombatLifecycleState -> CombatPresenter -> combat-context model
```

A Unity view may cache the last rendered values for animation/interpolation, but that cache is presentation state rather than gameplay truth.

## 3. Bind the HUD through local-player identity

The HUD binds through the local player's durable gameplay relationship rather than searching the scene for an object named `Player`.

Conceptually:

```text
LocalPlayerId
    -> current session/member/slot binding
        -> controlled CharacterId
            -> HUD semantic sources
```

The existing input API already distinguishes `LocalPlayerId` from gameplay feature state. Preserve that separation between a local input user and the character they currently control.

Changing the controlled character should rebind the HUD rather than create another HUD implementation.

This supports multiplayer, reconnect, future respawn/rebinding, no-character/spectator states, and scene/runtime reconstruction.

## 4. Vitality presentation consumes system 02

Character vitality/defeat remains system 02 state.

The HUD may derive:

- current vitality;
- maximum vitality where exposed;
- normalized health fraction;
- defeated/incapacitated semantic state.

The HUD must not calculate damage, infer defeat from animation, maintain a parallel health total, or decide whether a character is defeated.

The same vitality presentation path must work during exploration and combat. Do not create combat-owned duplicate health authority.

## 5. Interaction prompts consume system 13

System 13 owns authoritative character/context eligibility for WorldObject interactions.

System 17 turns the resulting semantic action opportunity into presentation.

Conceptually:

```text
Character 12 may Activate WorldObject 87
    + current local input binding
        -> [E] Activate
```

The interaction contract supplies semantic action identity, not final display strings or controller glyphs.

Therefore:

- system 13 owns the semantic action such as `Activate`;
- input/presentation mapping owns which control currently invokes it;
- localization/presentation owns the displayed label;
- HUD combines and renders them.

Do not place strings such as `Press E` inside WorldObjects, quests, combat code, or encounter definitions.

## 6. Prompts reference semantic input, not physical keys

The existing input system models semantic input snapshots and contexts rather than making gameplay depend directly on physical devices.

The HUD should follow that direction.

A prompt should reference a semantic input action such as `Primary`, `Secondary`, `Confirm`, `Cancel`, or a richer action added when a demonstrated gameplay need requires it.

The view resolves the currently active binding/device presentation.

Changing keyboard binding, controller type, or localized text must not require changing gameplay content.

## 7. Combat HUD presents the existing combat model

Combat already owns semantic lifecycle, sessions, participants, action legality, and authoritative resolution.

System 17 may present whichever immediate combat information production gameplay demonstrates it needs, for example:

- combat active/inactive context;
- selected/current target;
- legal/current action opportunities;
- readiness/round information;
- participant vitality;
- immediate authoritative action results.

The HUD must not maintain a second turn state machine, combat participant list, action-legality implementation, target-validity implementation, or winning-team calculation.

As system 01 exposes richer presentation-facing semantic state, system 17 consumes it.

## 8. Encounter state and combat state stay distinct

System 05 intentionally distinguishes encounters from combat. The HUD preserves that distinction.

An active encounter may warrant a contextual alert, objective/status cue, or interaction change without showing combat-specific controls.

Do not infer:

```text
Encounter active == Combat active
```

Presentation follows the semantic lifecycle exposed by the owning systems.

## 9. Snapshots establish truth; events animate changes

Use current semantic state/snapshots to establish what the HUD should show now.

Use semantic events/deltas to present what just happened.

Example:

```text
current vitality snapshot: 72 / 100
    -> establishes health bar

DamageApplied event
    -> may trigger flash / damage-number / directional feedback
```

If an event is missed because of reconnect, scene reload, or HUD recreation, current HUD truth must still rebuild correctly from the snapshot.

The HUD must never require replaying event history to rediscover current state.

## 10. Reconnect and late join rebuild from current state

When system 08 completes authoritative resynchronization, the local HUD reconstructs from current replicated state.

Example:

1. Player disconnects while their character has 5 vitality.
2. The authoritative character changes to 3 vitality while they are disconnected.
3. Reconnect/resynchronization finishes.
4. The HUD binds to current `CharacterId` state and immediately displays 3 vitality.

It must not retain the stale value of 5 because a transient event was missed.

The same rule applies to interaction and combat presentation.

## 11. Gameplay-ready is the presentation barrier

A transport connection existing is not enough to show an actionable gameplay HUD.

The HUD follows the session/gameplay-ready lifecycle established by systems 08 and 14.

Conceptually:

```text
Connecting / Restoring / Synchronizing
    -> gameplay HUD hidden or non-actionable

GameplayReady
    -> bind controlled CharacterId
    -> populate semantic HUD state
    -> enable normal gameplay presentation
```

Connection/recovery messaging itself belongs primarily to systems 20/23 rather than becoming gameplay-HUD authority.

## 12. Input-context integration

The current input API exposes semantic contexts:

- `Exploration`
- `Combat`
- `Ui`
- `Disabled`

System 17 consumes rather than replaces that mechanism.

Examples:

- **Exploration:** ordinary vitality plus contextual world-interaction information.
- **Combat:** combat-relevant HUD elements/actions.
- **Ui:** suppress gameplay interaction prompts/actions invalid while a dedicated UI owns input.
- **Disabled:** hide or disable actionable HUD elements.

A cutscene or orchestration layer may also request HUD presentation policy through composition, but the HUD does not independently decide whether gameplay input is enabled.

## 13. Use a thin HUD shell with feature presenters

Avoid one giant `GameplayHudController` that imports every gameplay runtime.

Prefer a thin shell hosting independent semantic presenters, conceptually:

```text
GameplayHud
    VitalityPresenter
    InteractionPresenter
    CombatPresenter
    optional demonstrated transient-feedback presenter
```

Each presenter depends on narrow public read contracts from the owning subsystem and exposes a presentation model to the Unity-specific view.

Later systems can integrate similarly without moving their domain logic into system 17. For example, system 19 may provide a small tracked-objective widget while still owning the broader quest/objective UI.

## 14. Dedicated screens remain independently replaceable

Inventory, quests, party/session, and menus may use different layouts or even different Unity UI technologies later.

The gameplay systems should not care, and system 17 should not define a universal application-wide windowing framework merely because several screens exist.

If repeated infrastructure becomes demonstrably useful across systems 17-23, extract it when reuse is proven rather than inventing a speculative `GameUiFramework` first.

## 15. Unity UI technology is an implementation detail

There is not enough repository evidence to make UGUI, UI Toolkit, TextMeshPro, or another rendering package part of the shared gameplay architecture.

Keep the semantic boundary technology-neutral:

```text
semantic read state
    -> presenter/view model
        -> Unity-specific view adapter
```

The first implementation may choose the Unity UI technology appropriate to the project without leaking that dependency into combat, characters, WorldObjects, quests, networking, or other gameplay APIs.

## 16. Presentation-only state stays local

Some state legitimately belongs only to the HUD, for example:

- element fade progress;
- animation time;
- expanded/collapsed presentation;
- last displayed value for interpolation;
- tooltip timing;
- temporary highlight state.

That state is ephemeral presentation state. It should generally not replicate, persist through system 16, or affect authoritative gameplay.

Example:

```text
health = 72
    -> authoritative gameplay truth

health bar animating from 81 toward 72
    -> local HUD presentation state
```

## 17. HUD-originated commands use normal semantic APIs

A HUD element may originate player intent, such as a combat action widget or interaction prompt.

But clicking/pressing it routes through the same semantic input/command path as ordinary gameplay input.

The HUD never directly mutates vitality, inventory, WorldObject state, combat state, or quest state.

Presentation expresses intent. Authoritative gameplay validates and executes it.

## 18. Multiplayer authority

Each client constructs its local HUD from authoritative replicated state relevant to that client.

The server does not author pixels, layout, strings, animation state, or Unity widget trees.

Likewise, a client's HUD does not become authoritative merely because it displays a server-owned value.

```text
replicated authoritative CharacterId/vitality/etc.
    -> local HUD model
        -> local presentation
```

## 19. Headless server independence

Gameplay assemblies must remain capable of running in a dedicated/headless server with no HUD implementation loaded.

Nothing in vitality, combat, encounters, WorldObjects, inventory, progression, story, or session orchestration should require a HUD object to exist.

This is both an architectural boundary and a reuse test.

## 20. Outcome presentation boundary

System 15 exposes the immutable semantic game outcome.

System 17 may react by making ordinary gameplay controls non-actionable or exposing a minimal transition cue if composition requires it.

The full victory/failure screen, restart/continue/menu choices, credits, or return-to-lobby flow belong with system 23/session presentation rather than becoming permanent HUD responsibilities.

So:

```text
17: gameplay-facing state ceases to be actionable
23: the player-facing end/session flow decides what screen comes next
```

## Acceptance / reuse proof

### Local vitality across gameplay modes

1. Bind the HUD to a controlled `CharacterId`.
2. Damage that character during exploration.
3. Verify the HUD reflects system-02 vitality.
4. Enter combat with the same character.
5. Damage them again.
6. Verify the same vitality presentation path updates without creating combat-owned health state.

### Two unrelated WorldObjects

1. Target an existing lever offering `Activate`.
2. System 13 exposes the semantic available action.
3. HUD presents the current input binding plus action label.
4. Target a different WorldObject family such as an openable container/door.
5. Verify the same interaction-presenter path handles the different semantic action.

This proves the HUD is capability-driven rather than a switch over object types.

### Reconnect reconstruction

1. Display a character's current vitality/interaction/combat state.
2. Disconnect the local client.
3. Change authoritative state while disconnected.
4. Reconnect and complete system-08 resynchronization.
5. Recreate/bind the HUD from the synchronized snapshot.
6. Verify it shows current truth without requiring missed historical events.

### Headless independence

Run the relevant gameplay scenario with no HUD/presentation assembly loaded. Perform character, interaction, encounter, and combat behavior and verify authoritative results are identical.

### Alternate-view reuse

Feed the same HUD presentation model into a minimal test/fake view rather than the production Unity view and verify vitality, interaction, and combat presentation state.

This proves the semantic/presentation seam is reusable independently of a specific scene hierarchy or UI technology.

## Out of scope

- inventory screen/journal — system 18
- quest/objective screen/journal — system 19
- teammate/party/reconnect/session UI — system 20
- gameplay audio — system 21
- combat/interaction VFX — system 22
- pause/settings/start/save/load/end-game screens — system 23
- authoritative gameplay rules
- generic UI/window framework
- server-side presentation
- persistence of HUD animation/layout state

## Architectural constraints

- HUD consumes authoritative semantic state; it never owns gameplay truth.
- Bind through local-player/session identity to the current controlled `CharacterId`, not scene searches.
- Current snapshots establish truth; events/deltas provide transient presentation.
- Reconnect/late join can rebuild the HUD completely from current authoritative state.
- Interaction prompts derive from semantic actions plus current input bindings rather than hardcoded keys/strings.
- Combat, encounter, vitality, inventory, progression, and WorldObject authorities remain in their owning systems.
- Dedicated inventory, progression, party/session, and menu UIs remain separate systems.
- Presentation-specific state stays local and non-authoritative.
- Shared gameplay does not depend on a Unity UI technology.
- Headless gameplay works without system 17 loaded.
