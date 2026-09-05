# SmallVoxelShowcase shared Input System restoration

## Defect and acceptance
`SmallVoxelShowcase` uses `VoxelEngine.Showcase.VoxelShowcase`, whose per-frame interactive path read legacy `UnityEngine.Input` while Player Settings are Input-System-only. Restore the complete keyboard/mouse interaction path without enabling Legacy/Both or adding scene-local compatibility polling. Acceptance is the binding list in `issue.json`, including actual built-player input proof and focus/cursor recovery.

A separate captured startup exception comes from `RenderDebugUiTestBootstrap.Disable()` reflecting into SRP `DebugManager.enableRuntimeUI` before the backing runtime UI exists. Keep this test-harness symptom independent and do not mask production exceptions.

## Ownership / hypotheses
`VoxelShowcase` is the shared production input owner for `SmallVoxelShowcase` and `VoxelShowcase`; `MultiplayerSceneBootstrap` attaches the same owner to `Multiplayer`. `HouseShowcase` is an independent already-Input-System consumer. The owning repository module is `Assets/Game/Composition/Showcase/SceneRuntime`, so it owns focused EditMode coverage plus a module-local standalone validation scene/scenario.

1. **Supported:** `VoxelShowcase.Update -> HandleKeys/MovePlayer/HandleLook/HandleEdits` reached legacy `Input.*`; the captured `GetKeyDown` failure was only the first read.
2. **Independent and supported:** the Structures SRP bootstrap runs `BeforeSceneLoad`; its reflected setter can dereference uninitialized SRP runtime-UI state before the fixture body. It has no production-input ownership.

`experiment-001-input-owner-and-srp-bootstrap.md` records the initial discriminator. Legacy mouse-axis sensitivity is `0.1`, so Input System delta conversion preserves that scale while wheel behavior stays sign-based.

## Material results
The actual built `SmallVoxelShowcase` `showcase-input-smoke` replay has passed on exact runs `33984299208` and `33986010080`, proving real Input System events change production player/camera state. The latter run still failed only the three new EditMode tests, each on its first `wasPressedThisFrame` edge assertion. `MakeCurrent()` removed native-device ambiguity but did not make manual `InputSystem.Update()` a valid frame-edge test while the fixture remained in automatic update mode.

## Selected fix
Use one semantic `Unity.InputSystem` snapshot in SceneRuntime and make `VoxelShowcase` consume it for movement, look, cursor toggle, sprint, jump/fly vertical, interact, respawn, brush scroll, and mouse edits. On focus/cursor reacquire, discard one Input System look-delta frame instead of calling `Input.ResetInputAxes`.

Use the real production driver in module-local standalone validation and in the SceneIssue built-player smoke replay. Synthetic validation devices are explicitly current. Harden only the known too-early SRP debug setter failure, then require post-load suppression to succeed.

For EditMode semantics, retain every existing edge/held/mouse assertion but put only `ShowcaseInputSystemTests` into `InputSettings.UpdateMode.ProcessEventsManually` while it explicitly queues events and calls `InputSystem.Update()`, restoring the prior update mode in teardown. Production behavior is unchanged.

## Remaining gates
Current feature head includes the manual-update fixture correction. Run repository-derived SceneRuntime EditMode validation, module-local standalone player validation, Kentridge integration, and actual `SmallVoxelShowcase` replay at one exact feature SHA. If green, complete `open/` -> `closed/` bookkeeping, merge current `origin/master` into the feature branch, open the final PR, enable auto-merge immediately, and monitor the required `affected` gate until the PR is merged and the closed issue is visible on master.
