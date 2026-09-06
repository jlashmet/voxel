# SmallVoxelShowcase shared Input System restoration

## Defect and acceptance
`SmallVoxelShowcase` used `VoxelEngine.Showcase.VoxelShowcase`, whose interactive path read legacy `UnityEngine.Input` while Player Settings are Input-System-only. Required behavior covers movement, look, cursor recovery, sprint/jump/fly, interact/respawn, brush scroll, edit actions, shared consumers, and the independent Structures SRP debug-UI startup symptom.

## Ownership / hypotheses
1. **Production input owner — supported:** `VoxelShowcase.Update -> HandleKeys/MovePlayer/HandleLook/HandleEdits` was the legacy-input owner. `SmallVoxelShowcase` and `VoxelShowcase` serialize it; Multiplayer attaches it at runtime.
2. **Structures startup — independent and supported:** the reflected SRP `DebugManager.enableRuntimeUI` setter can run before backing runtime UI exists; this is test-harness-owned and must not mask production exceptions.
3. **Regression harness edge-state — resolved:** two materially different EditMode harness fixes left the same `wasPressedThisFrame` assertions red while the built SmallVoxel replay passed. Unity's `InputTestFixture` isolates runtime input state and is supported for PlayMode, so direct edge semantics moved to module-owned PlayMode. See `experiment-002-input-edge-test-mode.md`.

## Selected fix
Use one semantic `Unity.InputSystem` snapshot in SceneRuntime and make `VoxelShowcase` consume it. On cursor/focus reacquire, discard one look-delta frame instead of `Input.ResetInputAxes`. Keep the module-local production validation scene/scenario and actual SceneIssue replay that inject Input System events through the real `VoxelShowcase`. Harden only the known early SRP debug setter failure and require post-load suppression to succeed. Run direct Input System edge tests through `InputTestFixture` in `VoxelEngine.Showcase.Tests.PlayMode`.

## Validation / closure
**Passed.** Exact feature SHA `7e6c609c34dff4768032f9046e891f43cbd935b7`, transport `e60a1c7c6e348d9876b35baa8b4a5898b7043abe`, workflow `33988857330`: repository-derived Showcase EditMode + PlayMode tests passed, explicit Structures PlayMode regression passed, module-local standalone validation and Kentridge integration passed, and actual built `SmallVoxelShowcase` `showcase-input-smoke` replay passed. All assigned acceptance is satisfied; close directly from `open/` to `closed/`, then sync current master and promote by PR + auto-merge.
