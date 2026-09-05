# SmallVoxelShowcase shared Input System restoration

## Defect and acceptance

`SmallVoxelShowcase` uses `VoxelEngine.Showcase.VoxelShowcase`, whose per-frame interactive path still reads legacy `UnityEngine.Input` while project Player Settings are Input-System-only. The first observed crash is `Input.GetKeyDown` from `VoxelShowcase.HandleKeys`, but the same runtime also uses legacy key, axis, mouse-scroll, look, jump/movement, and reset-axis calls. Restore the complete production interaction path without changing Player Settings to Legacy/Both and without scene-local compatibility shims.

A separate captured startup exception currently comes from `RenderDebugUiTestBootstrap.Disable()` reflecting into SRP `DebugManager.enableRuntimeUI`, which reaches a null reference in SRP Core. Keep this symptom independent until a discriminator proves otherwise; the test harness must become exception-safe without concealing the production input failure.

Acceptance is the binding list in `issue.json`, including real built-player input proof for `SmallVoxelShowcase`, focus/cursor behavior, shared-consumer migration, and safe test bootstrap behavior.

## Ownership / architecture

Primary suspected production owner: `Assets/Game/Composition/Showcase/SceneRuntime/VoxelShowcase.cs` and whichever shared input abstraction should own player/showcase input under the current Input System configuration. `HouseShowcase` already uses `UnityEngine.InputSystem` directly and is a useful current-master compatibility reference. Prefer a reusable semantic/shared input boundary if multiple scenes share the same controls; do not duplicate key maps across each scene unless repository architecture proves those scenes genuinely own independent controls.

The SRP debug bootstrap lives in Structures PlayMode tests and is test-harness ownership, not a reason to alter production rendering/input policy.

## Hypotheses and discriminator

1. **Leading hypothesis:** `SmallVoxelShowcase` fails because `VoxelShowcase.Update -> HandleKeys/MovePlayer/HandleLook/HandleEdits` executes legacy `UnityEngine.Input` calls under Input-System-only settings. **Falsified if:** the production scene can exercise these methods under the current settings without the reported exception, or the captured stack is from a different runtime assembly than current master.
2. **Independent harness hypothesis:** the SRP debug suppression added to `TypedStructuralSocketCompositionSceneTests` runs before `DebugManager` has a valid persistent-runtime-UI backing object; setting `enableRuntimeUI=false` through reflection therefore throws before the fixture body. **Falsified if:** reproducing the same bootstrap against current master never reaches the captured NRE or inspection shows a different test initializer owns the call.

**Next discriminator:** run the smallest repository-supported `SmallVoxelShowcase` player/validation repro with current Input-System-only settings and capture the first exception before modifying code. Separately invoke/isolate the Structures debug bootstrap without `SmallVoxelShowcase`. Then enumerate direct `UnityEngine.Input` reads in the responsible player-visible modules and classify consumers by shared owner before implementing the fix.

## Selected fix

Not selected until the discriminator is recorded in `experiment-001-*.md`. Expected direction is migration to the supported Input System/shared semantic input boundary plus an exception-safe or unnecessary-bootstrap removal for the SRP test harness. Do not switch project input handling to Both/Legacy.

## Current commit / remaining gates

Baseline master at capture: `af61066de669431a6555e737887bd5d4031525b8`.

Remaining gates: reproduce both captured symptoms independently; identify all same-owner consumers; implement focused fix; add/update module-local validation scene/scenario for player-visible input behavior; validate keyboard/mouse state changes in built `SmallVoxelShowcase`; regression-check same-owner scenes; prove focus/cursor recovery; prove Structures PlayMode bootstrap is exception-free; exact-SHA targeted CI; close only after all assigned symptoms are resolved.
