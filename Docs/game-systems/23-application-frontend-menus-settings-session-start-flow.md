# 23. Application frontend, menus, settings & session start flow

**Status:** Approved

## Purpose

Provide one thin local application/frontend flow for boot, menus, settings, new/resumed game entry, multiplayer-session presentation hosting, in-game menu flow, outcome screens, and clean return/exit behavior without creating another authoritative gameplay/session state machine.

The defining rule is:

> Menus decide what the player is asking to do; the owning gameplay, session, persistence, input, and platform systems decide whether and how it happens.

Conceptually:

```text
local application/frontend shell
        |
        +--> New Game
        +--> Continue
        +--> Multiplayer/session screen
        +--> Settings
        +--> In-game menu
        +--> Outcome/end screens
        |
        v
semantic application intents
        |
        +--> 14 session orchestration
        +--> 16 persistence/restore
        +--> 07/08 party/session
        +--> 20 multiplayer presentation
        +--> input/presentation/platform services
```

## 1. Keep application flow distinct from gameplay/session state

System 14, system 07, and the networking stack already own separate authoritative/session/technical lifecycles. System 23 must not collapse them into a giant global `GameState` enum.

A small local application lifecycle is enough, conceptually:

```text
Boot
  -> FrontEnd
  -> StartingSession
  -> InGame
  -> ReturningToFrontEnd
  -> FrontEnd

Boot/FrontEnd/InGame
  -> Exiting
```

These states describe what the local application shell is doing, not combat, campaign, party, networking, or authoritative simulation.

## 2. Screen navigation is local presentation state

Screens such as:

```text
MainMenu
Settings
SaveSelection
PartyScreen
InGameMenu
OutcomeScreen
ConfirmDialog
```

are local navigation/presentation concepts.

Opening or closing a screen does not create duplicate authoritative gameplay/session state.

## 3. Reuse the existing input-context stack

The input foundation already exposes semantic contexts including `Exploration`, `Combat`, `Ui`, and `Disabled`, with stackable leases.

A menu surface should conceptually:

```text
open menu
    -> Push(Ui)

close menu
    -> release lease
        -> previous context resumes
```

This supports nested UI correctly:

```text
Exploration
  -> InGameMenu / Ui
      -> Settings / Ui
          -> ConfirmDialog / Ui
```

Do not build a parallel menu-input ownership system.

## 4. Input device abstraction is a separate required foundation

Production input uses Unity's Input System as the physical-device normalization/binding layer for keyboard/mouse, gamepad/joystick, touch/mobile controls, and future supported devices.

`Game.Input.Api` remains the engine-neutral semantic input layer consumed by gameplay and presentation.

The detailed decision is recorded in [`input-device-abstraction.md`](input-device-abstraction.md).

The required dependency direction is:

```text
physical devices
    -> Unity Input System actions/bindings/control schemes/device pairing
        -> Game.Input.Runtime
            -> Game.Input.Api semantic actions + contexts
                -> gameplay/UI consumers
```

Do not create separate production gameplay readers for keyboard, controller, and mobile.

## 5. Menus must also use semantic device-independent input

Menu navigation and actions should use the same local-player/input-user abstraction as gameplay.

The frontend must not become keyboard-specific while gameplay uses a shared device abstraction.

A `Confirm` intent remains `Confirm` whether produced by:

- keyboard Enter/Space;
- gamepad face button;
- touch/on-screen control.

Physical binding is configuration below system 23.

## 6. New Game delegates to system 14

A New Game button must not directly instantiate authoritative runtimes, spawn characters, initialize quests/inventories, or start simulation.

Conceptually:

```text
New Game
    -> NewSessionRequest
        -> system 14
            compose normal runtime graph
            initialize fresh authoritative state
            reach GameplayReady
```

System 14 remains the canonical fresh-game path.

## 7. Continue delegates to systems 16 and 14

Resume follows:

```text
Continue
    -> choose durable save/session
    -> system 16 load + validate
    -> system 14 compose the normal runtime graph
    -> system 16 restore owning subsystem state
    -> GameplayReady
```

There is no separate loaded-game runtime or special `ContinueGameScene` architecture.

## 8. Save-selection UI consumes persistence metadata

System 16 owns persistence mechanism and authoritative restore semantics. System 23 may own selection/presentation policy.

A lightweight save entry may expose presentation metadata such as:

```text
SaveId
campaign/content display metadata
saved-at timestamp
optional play/session metadata
compatibility/loadability status
```

The frontend should not deserialize the complete authoritative game merely to list saves.

## 9. Do not assume a particular save policy

System 23 must not hardcode assumptions such as:

- exactly three slots;
- autosave slot zero;
- save only at checkpoints;
- save every N minutes;
- never save during combat.

The configured game/persistence policy determines which semantic save operations are available.

## 10. Multiplayer entry flows through system 07

Frontend actions such as create/host/join/accept invite route through system 07's party/session formation semantics and provider seam.

System 23 must not directly open sockets, assign `PartyMemberId`/`PlayerSlot`, spawn player characters, or begin authoritative gameplay.

## 11. Multiplayer screens host system 20 presentation

System 23 chooses screen/navigation composition.

System 20 owns the reusable party/session presentation model.

Conceptually:

```text
23 SessionScreen
    hosts
        20 PartyRosterPresenter
        20 ReadinessPresenter
        20 SessionStatusPresenter
```

System 07/08 remain authoritative for the underlying session state.

## 12. Start Game is a semantic request

A multiplayer Start button means conceptually:

```text
RequestStartSession()
```

The authoritative session layer validates leadership/readiness/compatibility/policy and coordinates the transition.

The button does not directly load the gameplay scene, start simulation, or spawn characters.

## 13. Scene realization is application implementation, not button authority

Some Unity bootstrap/scene mechanism will realize frontend/gameplay environments.

Keep that behind one thin application composition boundary rather than allowing individual screens/buttons to issue arbitrary scene transitions.

Conceptually:

```text
ApplicationFlowCoordinator
    FrontEnd -> StartingSession -> InGame
    InGame -> ReturningToFrontEnd -> FrontEnd
```

Authoritative runtime startup/teardown still belongs to system 14 and the owning session systems.

## 14. Opening the in-game menu does not automatically pause simulation

Opening the in-game menu initially means local gameplay controls yield to the `Ui` context.

In multiplayer, authoritative simulation normally continues while one player has a menu open.

Do not make menu ownership equivalent to:

```text
Time.timeScale = 0
```

True simulation pause, if later required, must be an explicit capability of the owning simulation/session policy.

## 15. Settings are local preferences unless explicitly gameplay configuration

Presentation/device preferences such as graphics, audio levels, UI scale, control bindings, sensitivity, and accessibility are local user/device/profile configuration.

They do not belong in system 16's authoritative `GameSessionSnapshot`.

Authoritative startup/gameplay configuration, if demonstrated later, remains separate and session-owned.

Do not infer generic gameplay options such as difficulty, friendly fire, permadeath, or arbitrary rule switches merely because a Settings screen exists.

## 16. Only expose settings backed by real capabilities

Do not pre-build a giant speculative settings schema.

Expose settings only when the corresponding runtime capability exists, such as:

- audio volume/routing from system 21;
- supported renderer/display options;
- supported input bindings/rebinding;
- demonstrated accessibility preferences.

The owning subsystem validates/applies the effective setting.

## 17. Input rebinding uses Unity Input System bindings

When production supports rebinding, system 23 provides the UI over the existing semantic input actions.

Conceptually:

```text
Interact
    current binding: E
    -> Rebind
        -> Unity Input System binding override
            -> same semantic Interact action
```

Do not invent a second key-binding database or make gameplay understand physical control names.

Binding overrides/preferences are local settings, not authoritative game-session state.

## 18. Binding/glyph presentation is separate from gameplay semantics

HUD and menus may need current control presentation such as:

```text
[E] Open
[X] Open
[touch icon] Open
```

That display comes from the input binding/control-scheme presentation layer defined by the input-device abstraction.

Gameplay/content exposes semantic actions only.

Switching from keyboard to controller can refresh prompts without changing WorldObject/combat/character semantics.

## 19. LocalPlayerId must map to an actual local input user

Frontend/input composition must preserve the input foundation's `LocalPlayerId` semantics.

Conceptually:

```text
LocalPlayerId
    -> local input user/player
        -> paired device(s)
            -> semantic actions
```

This enables multiple local controllers/users and avoids treating a single global keyboard/gamepad state as every player.

System 23 does not own device pairing rules itself, but it must not bypass that abstraction.

## 20. Settings may use staged/apply semantics where required

Some local settings can apply immediately; others may require validation, confirmation, recreation, or application restart.

The settings presenter may own local editing state such as:

```text
original
pending
dirty
```

but capability owners remain the source of truth for applied configuration.

## 21. Preferences use a narrow local persistence boundary

A simple local preferences store is sufficient, conceptually:

```text
IUserPreferencesStore
    Load()
    Save(...)
```

Do not make gameplay systems depend directly on Unity `PlayerPrefs`, and do not turn system 23 into a generic persistence database.

The concrete platform store may vary later without changing gameplay/session APIs.

## 22. Outcome/end screens consume system 15

System 15 owns the immutable authoritative `GameOutcome`.

System 23 may navigate to an appropriate result/end screen after orchestration reaches the corresponding aftermath state.

It must not infer victory/failure from enemy counts, character death, or transport/session shutdown.

## 23. Returning to the frontend performs semantic teardown

`Return to Main Menu` must coordinate clean session departure/shutdown before destroying the local gameplay presentation environment.

Conceptually:

```text
ReturnToFrontEnd
    -> request/coordinate session departure or shutdown
    -> system 14 stops accepting gameplay commands
    -> party/network/session cleanup where applicable
    -> runtime graph disposed
    -> realize FrontEnd
```

## 24. Leave Game uses systems 07/08 semantics

In multiplayer:

```text
Leave Game
    -> semantic session leave
    -> authoritative party/session transition
    -> transport/orchestration cleanup
    -> FrontEnd
```

Do not fake an explicit leave by disconnecting the socket or unloading a scene first.

## 25. Quit Application and Leave Session are distinct

Quitting while in a live session may require session leave/shutdown and runtime cleanup before the final platform/application quit action.

System 23 coordinates the local sequence but does not replace the owning session semantics.

## 26. Do not invent save-on-exit policy

Exiting or returning to the frontend does not automatically imply an authoritative save unless configured game/session policy explicitly requires it.

## 27. Startup failures are semantic presentation inputs

Session creation/load/join may fail for semantic reasons such as incompatibility, unavailable content, failed restore validation, or join rejection.

System 23 should receive presentation-ready semantic failure categories from owning systems rather than interpreting raw exceptions/transport codes.

Only add failure categories required by real implementations.

## 28. Loading presentation reflects real readiness

A loading screen may show actual lifecycle/readiness supplied by responsible systems.

It must not use a fake timer to decide gameplay is ready.

The final transition to gameplay controls happens only after the normal `GameplayReady` barrier.

## 29. One production frontend shell

Do not create per-scene production frontends such as:

```text
KentridgePauseMenu
DragonSceneMainMenu
ShowcaseSettingsMenu
```

Module-local validation scenes may use focused validation drivers/UI without becoming alternative production application shells.

## 30. Presentation technology remains replaceable

Application/navigation semantics must not depend on a specific Unity UI technology.

UI Toolkit, uGUI, or another presentation implementation may realize the screens while the semantic application flow remains independently testable.

## 31. Local screen state remains local

Current screen, modal stack, focus, scroll position, selected settings category, and pending confirmation state are local presentation state.

They do not replicate and do not belong in authoritative system-16 persistence.

## 32. Headless server independence

A dedicated/headless server can create sessions, form parties, compose system 14, persist/restore, resolve outcomes, and shut down with no system-23 frontend/menu/settings assembly loaded.

## Acceptance / reuse proof

### Fresh session

```text
Boot
-> MainMenu
-> New Game
-> system 14 normal fresh-session composition
-> GameplayReady
-> gameplay
```

No menu code constructs gameplay domains directly.

### Resume

```text
Boot
-> Continue
-> select valid save metadata
-> system 16 load/validate
-> same system-14 runtime graph
-> restore
-> GameplayReady
```

### Multiplayer host

```text
MainMenu
-> SessionScreen
-> system 07 party formation
-> system 20 roster/readiness presentation
-> Start request
-> authoritative readiness barrier
-> system 14 gameplay startup
```

### Multiplayer join

Use the configured system-07 join provider, bind the stable party member, present system-20 synchronization state, and enter gameplay only at `GameplayReady`.

### In-game menu during multiplayer

Open the menu on one client. That client enters `Ui` input context while authoritative simulation and other clients continue. Closing the menu restores the prior context.

### Nested menu input

Verify nested `Ui` context leases restore the immediate previous UI owner before finally returning to gameplay input.

### Keyboard/mouse, gamepad, touch

Drive the same frontend `Confirm`/`Cancel` semantic actions from keyboard/mouse, gamepad, and touch/on-screen controls through the shared input-device abstraction. Frontend code must not branch into separate device-specific menu command paths.

### Dynamic binding presentation

Present a semantic action binding in a menu/HUD, change active control scheme or binding, and verify the display updates without changing gameplay content or action semantics.

### Multiple local players

Resolve separate `LocalPlayerId` values to separate paired local input users/devices and verify UI/gameplay reads remain isolated.

### Settings isolation

Two clients in one authoritative session may use different graphics/audio/input preferences while observing identical authoritative gameplay state.

### Return to frontend

Request semantic departure/shutdown, complete system/session/network teardown, then realize FrontEnd. No stale runtime graph remains.

### Headless

Run new-session, multiplayer, persistence, outcome, and shutdown flows with no system-23 assembly loaded.

## Out of scope

- authoritative gameplay orchestration — system 14
- game outcome policy — system 15
- persistence serialization/storage — system 16
- HUD/inventory/quest UI semantics — systems 17-19
- party/session presentation semantics — system 20
- audio/VFX implementation — systems 21-22
- physical-device implementation beyond using the shared input abstraction
- public matchmaking/server browser
- platform account/friends/social systems
- voice/text chat
- achievements
- cloud-save provider integration
- speculative difficulty/gameplay-option framework
- pause as a universal simulation rule
- scene-management logic embedded in individual buttons
- generic application framework unrelated to this game's demonstrated needs

## Architectural constraints

- System 23 owns local frontend/navigation flow, not authoritative gameplay state.
- New and resumed sessions converge on the same system-14 runtime graph.
- System 16 supplies durable-session persistence; system 23 owns only save-selection/menu policy.
- Multiplayer screens consume systems 07/08 authority and system-20 presentation rather than transport details.
- Menus reuse the existing semantic `Ui` input-context stack.
- Unity Input System is the physical-device abstraction; `Game.Input.Api` remains the game-semantic abstraction.
- Do not create separate production keyboard/gamepad/mobile gameplay or menu readers.
- `LocalPlayerId` resolves a real local input user/device pairing.
- Gameplay/content never hardcodes physical binding names for prompts.
- Rebinding changes local input bindings, not gameplay semantics, and is not authoritative session state.
- Opening an in-game menu does not automatically pause authoritative simulation.
- Local preferences are separate from authoritative saves and gameplay configuration.
- Only settings backed by real subsystem capabilities are exposed.
- Individual screens do not directly start simulation, spawn gameplay objects, or tear down networking.
- Outcome screens consume system 15 rather than inferring victory/failure.
- Return/quit paths perform semantic session teardown before local application transition.
- Headless servers operate with no system-23 dependency.
