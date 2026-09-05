# SmallVoxelShowcase shared Input System restoration

## Defect and acceptance
`SmallVoxelShowcase` uses `VoxelEngine.Showcase.VoxelShowcase`, whose interactive path read legacy `UnityEngine.Input` while Player Settings are Input-System-only. Restore movement, look, cursor recovery, sprint/jump/fly, interact/respawn, brush scroll, and edit actions without enabling legacy/both input. Independently, the Structures SRP debug-UI bootstrap must not throw during scene startup.

## Ownership / hypotheses
1. **Production input owner — supported:** `VoxelShowcase.Update -> HandleKeys/MovePlayer/HandleLook/HandleEdits` was the legacy-input owner. `SmallVoxelShowcase` and `VoxelShowcase` serialize it; Multiplayer attaches it at runtime.
2. **Structures startup — independent and supported:** the reflected SRP `DebugManager.enableRuntimeUI` setter can run before backing runtime UI exists; this is test-harness-owned and must not mask production exceptions.
3. **Regression harness edge-state — resolved:** repeated exact-SHA runs proved the built SmallVoxel input path passes while only the new `wasPressedThisFrame` assertions fail. Making synthetic devices current and then forcing manual update mode both left the same assertions red. Unity's `InputTestFixture` explicitly isolates Input System runtime state and is designed for PlayMode; EditMode is generally unsupported. The edge regression therefore belongs in module-owned PlayMode, not EditMode. See `experiment-002-input-edge-test-mode.md`.

## Selected fix
Use one semantic `Unity.InputSystem` snapshot in SceneRuntime and make `VoxelShowcase` consume it. On cursor/focus reacquire, discard one look-delta frame instead of `Input.ResetInputAxes`. Keep the module-local production validation scene/scenario and actual SceneIssue replay that inject Input System events through the real `VoxelShowcase`. Harden only the known early SRP debug setter failure and require post-load suppression to succeed.

Move only `ShowcaseInputSystemTests` to `Tests/PlayMode/VoxelEngine.Showcase.Tests.PlayMode`, derive from `InputTestFixture`, and leave other Showcase EditMode coverage plus production input unchanged.

## Remaining gates
Run one exact feature SHA with the explicit Structures PlayMode regression plus repository-derived Showcase EditMode + PlayMode tests, module-local standalone player validation, Kentridge integration, and actual built `SmallVoxelShowcase` replay. If green, complete `issue.json`, move `open/` directly to `closed/`, merge current `origin/master` into `fixes/agent-3`, open the final PR, enable auto-merge, and monitor required `affected` through merge.
