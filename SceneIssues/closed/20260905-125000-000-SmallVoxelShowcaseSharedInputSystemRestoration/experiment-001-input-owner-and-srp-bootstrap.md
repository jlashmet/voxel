# Experiment 001 — shared input owner and SRP bootstrap

## Question
Which runtime owns the Input-System-only failure, which player-visible scenes share it, and is the captured SRP debug-UI null reference part of the same production cause?

## Production input discriminator
`SmallVoxelShowcase.unity` serializes `VoxelEngine.Showcase.VoxelShowcase` (script GUID `12be027be786465c9a6c8be1321251fd`). The current `VoxelShowcase.Update` executes `HandleKeys`, `HandleLook`, `MovePlayer`, and `HandleEdits` every interactive frame. Those methods directly call legacy `UnityEngine.Input.GetKeyDown`, `GetKey`, `GetAxisRaw`, `GetMouseButtonDown`, and `ResetInputAxes`. Under Input-System-only Player Settings, the captured `Input.GetKeyDown` exception is therefore the first reachable legacy call, not a scene-loading or assembly mismatch.

The project legacy Input Manager defines `Mouse X`, `Mouse Y`, and `Mouse ScrollWheel` with sensitivity `0.1`. The replacement must preserve that scale for look; brush scrolling only depends on sign, so Input System wheel magnitude should be normalized to direction.

## Shared consumers
The same production owner is used by:
- `Assets/Scenes/SmallVoxelShowcase.unity` — serialized `VoxelShowcase` component.
- `Assets/Scenes/VoxelShowcase.unity` — serialized same component/script GUID.
- `Assets/Scenes/Multiplayer.unity` — `MultiplayerSceneBootstrap` finds or attaches `VoxelShowcase` to the main camera, then enables multiplayer.

`HouseShowcase` is not a same-owner failure: it has its own runtime and already reads `Keyboard.current` / `Mouse.current` from `Unity.InputSystem`.

## Module validation ownership
`Assets/Game/Composition/Showcase/SceneRuntime` owns `Tests/EditMode/VoxelEngine.Showcase.Tests.EditMode.asmdef`, so it is the nearest repository module root for `VoxelShowcase`. It has no module-local `Validation/` surface. Because this defect changes player-visible runtime input, repo policy requires a focused `SceneRuntime/Validation` scene/scenario that drives the real `VoxelShowcase` through Input System events.

## Independent SRP bootstrap discriminator
`RenderDebugUiTestBootstrap.Disable()` in `TypedStructuralSocketCompositionSceneTests` runs from `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` and reflectively assigns `DebugManager.enableRuntimeUI=false`. The captured null reference occurs inside the SRP property setter before the fixture body. This code has no dependency on `VoxelShowcase` or input polling and is independently test-harness-owned.

Selected harness correction: tolerate only the known too-early SRP null-reference at the `BeforeSceneLoad` suppression attempt, then require the same suppression to succeed from the fixture after scene load. Do not catch arbitrary runtime exceptions, so a production Input System failure remains visible.

## Result / selected fix
Hypothesis 1 is supported: migrate the shared `VoxelShowcase` input owner to one semantic `Unity.InputSystem` snapshot covering movement, look, cursor toggle, sprint, jump/fly vertical, interact, respawn, wheel brush sizing, and mouse edits. Replace legacy axis reset on cursor recapture with one-frame look-delta discard.

Hypothesis 2 is independently supported by ownership and call timing: harden only the test bootstrap timing path and assert post-load suppression succeeds.

Validation will combine a focused EditMode semantic-input regression, a new SceneRuntime-owned standalone validation scene that injects real Input System keyboard/mouse events into production `VoxelShowcase`, and SceneIssue standalone replay of the actual `SmallVoxelShowcase` with an explicit Input System smoke action.