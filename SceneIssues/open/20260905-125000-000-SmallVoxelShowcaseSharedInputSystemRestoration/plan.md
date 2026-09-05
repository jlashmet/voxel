# SmallVoxelShowcase shared Input System restoration

## Defect and acceptance
`SmallVoxelShowcase` uses `VoxelEngine.Showcase.VoxelShowcase`, whose per-frame interactive path reads legacy `UnityEngine.Input` while Player Settings are Input-System-only. Restore the complete keyboard/mouse interaction path without changing Player Settings to Legacy/Both or adding scene-local compatibility polling. Acceptance is the binding list in `issue.json`, including actual built-player input proof and focus/cursor recovery.

A separate captured startup exception comes from `RenderDebugUiTestBootstrap.Disable()` reflecting into SRP `DebugManager.enableRuntimeUI` before the SRP backing runtime UI exists. Keep this test-harness symptom independent and do not mask production exceptions.

## Ownership / architecture
Implementation base is current master `513ae04ca89b6d3448246349f1ed040e4b48a7ef`. `VoxelShowcase` is the shared production input owner for `SmallVoxelShowcase` and `VoxelShowcase`; `MultiplayerSceneBootstrap` attaches the same owner to `Multiplayer`. `HouseShowcase` is an independent already-Input-System consumer.

The nearest repository module root is `Assets/Game/Composition/Showcase/SceneRuntime` because it owns `Tests/EditMode/VoxelEngine.Showcase.Tests.EditMode.asmdef`. It lacks a module-local `Validation/`, so this player-visible change requires one using the real production driver and Input System events.

## Hypotheses / discriminator result
1. **Supported:** `VoxelShowcase.Update -> HandleKeys/MovePlayer/HandleLook/HandleEdits` reaches legacy `Input.*`; the captured `GetKeyDown` failure is only the first such read. `SmallVoxelShowcase` and `VoxelShowcase` serialize the same script; Multiplayer attaches it at runtime.
2. **Independent and supported:** the Structures SRP bootstrap runs `BeforeSceneLoad`; its reflected setter can dereference uninitialized SRP runtime-UI state before the fixture body. It has no production-input ownership.

`experiment-001-input-owner-and-srp-bootstrap.md` records the discriminator. Legacy `Mouse X/Y/ScrollWheel` sensitivity is `0.1`, so Input System delta conversion must preserve look scaling while wheel behavior remains sign-based.

The isolated exact-SHA run `33984299208` removed the explicit Structures test. Its actual built `SmallVoxelShowcase` replay passed, while the automatically owned SceneRuntime EditMode batch failed only the three new input tests. Those tests queued events into synthetic devices without making them `Keyboard.current` / `Mouse.current`; this is a test-orchestration defect rather than evidence against the production mapping.

## Selected fix
Add one semantic `Unity.InputSystem` snapshot in SceneRuntime and make `VoxelShowcase` consume it for movement, look, cursor toggle, sprint, jump/fly vertical, interact, respawn, scroll brush sizing, and mouse edits. On focus/cursor reacquire, discard one Input System look-delta frame instead of calling `Input.ResetInputAxes`.

Add focused EditMode input semantics coverage and a new `SceneRuntime/Validation` standalone scene/scenario that injects keyboard/mouse events through Input System into the real `VoxelShowcase`. Add an explicit SceneIssue input-smoke replay action so the actual built `SmallVoxelShowcase` proves input changes player/camera state. Synthetic validation devices are explicitly made current before injection so editor/native current devices cannot invalidate the fixture. Harden only the known too-early SRP debug setter failure, then require post-load suppression to succeed.

## Remaining gates
Run the corrected automatically owned SceneRuntime EditMode validation, module-local standalone player validation, Kentridge integration, and actual `SmallVoxelShowcase` SceneIssue replay at one exact feature SHA. If green, complete closure bookkeeping directly from `open/` to `closed/`, merge current `origin/master` into the feature branch, open the final PR, enable auto-merge, and monitor the required `affected` gate through merge.
