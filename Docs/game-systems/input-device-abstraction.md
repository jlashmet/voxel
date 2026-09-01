# Input device abstraction

**Status:** Approved

## Purpose

Define the boundary between physical input devices and game-semantic input so gameplay, UI, and presentation do not become coupled to keyboard/mouse, gamepad, joystick, touch, or any other concrete device family.

The defining rule is:

> Unity's Input System owns physical-device normalization and binding; `Game.Input.Api` owns game-semantic input consumed by gameplay and presentation.

Conceptually:

```text
keyboard / mouse
controller / gamepad / joystick
touch / mobile controls
future supported devices
        |
        v
Unity Input System
    actions
    bindings
    control schemes
    device pairing / InputUser or PlayerInput
        |
        v
Game.Input.Runtime
        |
        v
Game.Input.Api semantic actions + contexts
        |
        +--> exploration
        +--> combat
        +--> UI
        +--> gameplay/presentation consumers
```

## 1. Use Unity's Input System as the hardware abstraction

Do not build independent production readers such as:

```text
KeyboardPlayerInputReader
GamepadPlayerInputReader
MobilePlayerInputReader
```

that each translate physical controls separately.

Unity's Input System should normalize supported devices through action maps, bindings, control schemes, and player/device pairing.

A single production input adapter then projects those actions into the engine-neutral `Game.Input.Api` contract.

## 2. Keep `Game.Input.Api` semantic and engine-neutral

Gameplay should depend on concepts such as:

```text
Move
Look
PrimaryAction
SecondaryAction
Interact
Confirm
Cancel
OpenInventory
OpenJournal
OpenMenu
```

rather than physical controls such as:

```text
WASD
left mouse button
keyboard E
Xbox X
PlayStation Square
touch button index 3
```

The exact semantic action set should grow only from demonstrated gameplay requirements.

Input contexts such as `Exploration`, `Combat`, `Ui`, and `Disabled` remain game-semantic concepts above device binding.

## 3. Device bindings are configuration, not gameplay logic

A semantic action can have different bindings per control scheme, for example:

```text
Move
    KeyboardMouse -> WASD
    Gamepad       -> left stick
    Touch         -> virtual stick

Interact
    KeyboardMouse -> E
    Gamepad       -> face button
    Touch         -> on-screen action control
```

Gameplay behavior does not branch on device type to decide what `Interact` means.

Changing a binding must not require changing combat, WorldObject, character, quest, inventory, or campaign code.

## 4. Mobile controls feed the same semantic actions

Touch/mobile presentation may provide virtual sticks, buttons, gestures, or other controls, but those controls ultimately feed the same semantic actions consumed by the game.

Do not create a separate mobile gameplay controller or mobile-only command protocol merely because the physical input surface differs.

Mobile-specific layout and affordance remain presentation/configuration concerns.

## 5. LocalPlayerId must resolve a real local input user

The public API already accepts `LocalPlayerId`. Production input must make that identity meaningful.

Conceptually:

```text
LocalPlayerId
    -> local Unity input user / player binding
        -> paired device(s)
            -> semantic input actions
```

This is required for multiple local players, multiple controllers, device reconnect/reassignment, and any future split-screen/local-co-op composition.

Do not read one global `Gamepad.current`/keyboard state and pretend that `LocalPlayerId` was honored.

## 6. Current implementation is transitional

The existing `UnityPlayerInputReader` currently reads the legacy `UnityEngine.Input` API directly, including named axes, mouse buttons, keyboard keys, and mouse position.

That implementation is a transitional compatibility adapter, not the target production architecture.

Production input should migrate to the Unity Input System rather than adding more device-specific branches to the legacy reader.

Legacy scene controllers that still read `UnityEngine.Input` directly should be migrated toward the shared semantic input ownership model rather than becoming permanent parallel readers.

## 7. Presentation may know the active binding, gameplay may not

HUD/menu presentation sometimes needs to display the control the local player should press.

For example:

```text
[E] Open
[X] Open
[touch icon] Open
```

Gameplay should still expose only the semantic action (`Open` / `Interact`).

Provide a presentation-facing seam conceptually like:

```text
IInputBindingPresentation
    GetBindingDisplay(LocalPlayerId, SemanticInputAction)
    BindingPresentationChanged
```

or an equivalent read model.

That presentation service may use Unity Input System binding/control-scheme information internally, but gameplay APIs remain device-independent.

## 8. Prompts must never hardcode physical controls

WorldObjects, combat actions, quests, encounters, or other gameplay content must not contain strings such as:

```text
Press E
Press X
Tap here
```

They expose semantic action identity.

HUD/UI combines:

```text
semantic gameplay action
+ active local binding presentation
+ localized action label
```

to produce the final prompt.

## 9. Rebinding belongs to input/settings presentation

When production supports user rebinding, system 23 may expose it through Settings.

Rebinding changes the configured Unity Input System binding for a semantic action. It does not change gameplay semantics.

Binding overrides/preferences are local user/device settings and must not be stored as authoritative system-16 game-session state.

Do not invent a second key-binding database if Unity Input System binding overrides satisfy the requirement.

## 10. Control scheme changes are presentation events

A local user may move between keyboard/mouse and controller, reconnect a gamepad, or otherwise change active controls.

Gameplay semantics remain unchanged.

Presentation can observe an active-control-scheme/binding-display change and refresh prompts/glyphs without mutating authoritative game state.

## 11. UI and gameplay share the same device abstraction

System 23 menus should not have a separate keyboard-only navigation implementation while gameplay uses the shared input layer.

Both gameplay and UI consume semantic actions/contexts over the same local input-user/device pairing.

The existing `Ui` input context remains the ownership mechanism for menus and dedicated UI screens.

## 12. Headless independence

Authoritative/headless gameplay must not depend on Unity Input System, attached physical devices, binding files, controller glyphs, or local input users.

Input is a client/local intent source. Authoritative gameplay validates semantic commands independently of which physical device produced them.

## Acceptance / reuse proof

### Keyboard/mouse

1. Bind a local player through the Unity Input System.
2. Drive movement and one gameplay action through keyboard/mouse bindings.
3. Verify the same `Game.Input.Api` semantic values reach gameplay.

### Gamepad/joystick

1. Pair a gamepad with the same local-player abstraction.
2. Drive the same semantic movement/action through gamepad bindings.
3. Verify gameplay code is unchanged.

### Touch/mobile

1. Feed an on-screen/touch control into the configured semantic actions.
2. Drive the same gameplay behavior.
3. Verify no mobile-specific gameplay controller or command path exists.

### Dynamic prompt

1. Present a semantic `Interact` prompt.
2. Use keyboard/mouse and verify the keyboard binding is displayed.
3. Switch the active local control scheme to gamepad.
4. Verify presentation refreshes to the gamepad binding without changing the WorldObject/gameplay action.

### Multiple local players

1. Create two `LocalPlayerId` values.
2. Pair separate devices/input users.
3. Verify each read resolves only its paired semantic input.

### Headless

Run authoritative gameplay with no Unity Input System/player devices loaded and verify gameplay behavior remains deterministic once semantic commands are supplied by tests/network/session infrastructure.

## Architectural constraints

- Unity Input System owns physical-device normalization and bindings.
- `Game.Input.Api` owns semantic actions and input contexts.
- Gameplay never branches on keyboard/gamepad/touch to determine command meaning.
- Do not create one production input reader per device family.
- `LocalPlayerId` resolves a real local input user/device pairing.
- The legacy `UnityEngine.Input` reader is transitional, not the target architecture.
- Prompts resolve current binding presentation separately from gameplay semantics.
- Rebinding/preferences are local settings, not authoritative game-session state.
- UI and gameplay reuse the same local input-device abstraction.
- Headless authoritative gameplay has no dependency on physical input devices or Unity Input System runtime objects.
