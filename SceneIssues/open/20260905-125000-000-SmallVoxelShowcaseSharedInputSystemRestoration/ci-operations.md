# CI operations — SmallVoxelShowcase shared Input System restoration

## 2026-09-05 — first exact-SHA request

- Feature SHA: `b1bcc789d2d9464569b630c71d59fe6e3e2d4335`
- Transport SHA: `da10e03bd1d4d1bb49dbf4331378486732ea6697`
- Workflow run: `33971319798`
- Request: automatic affected-module validation + explicit `VoxelEngine.Structures.Tests.PlayMode.TypedStructuralSocketCompositionSceneTests` + SceneIssue `showcase-input-smoke` replay.
- Result: **failed**. `Run automatically required module validation` failed after about 72 seconds. The later SceneIssue replay step started but the job ended without final status publication or artifacts; GitHub's job-log blob is unavailable, so this run does not provide a trustworthy failing test name.

## Discriminator after completed failure

The automatic plan owns the changed `SceneRuntime` module and its player validation, while the Structures test is an independent explicitly requested regression. Do not change production input from this incomplete signal. Re-run the same feature content with **no explicit extra test** so automatic `SceneRuntime` EditMode/player validation plus Kentridge integration are isolated from the Structures bootstrap regression. If that automatic gate is green, diagnose/fix the independent Structures bootstrap; if it still fails, inspect the shared-input module path first.

## 2026-09-05 — isolated automatic-module request

- Feature SHA: `9e7a4374def320b9919ba6221eab5aee526a41c7`
- Transport SHA: `4f01fb4d3c265cd4ed2c5b6bf1a9e689f91321cc`
- Workflow run: `33984299208`, successful execution on attempt 3 after two pre-step infrastructure cancellations.
- Request: automatic affected-module validation only, plus SceneIssue `showcase-input-smoke` replay; no explicit Structures test.
- Built production-scene replay: **passed**. `SmallVoxelShowcase` built successfully and ran for 20 seconds; the replay step completed successfully and produced two real-player screenshots.
- Automatic module validation: **failed**. The selected module was only `Assets/Game/Composition/Showcase/SceneRuntime`; its persistent EditMode assembly ran 16 tests, with 13 passed and 3 failed. All failures were the newly added `ShowcaseInputSystemTests` and occurred on expected pressed/held state being false.
- Discriminator result: the independent Structures bootstrap is not the cause of this gate. The editor already owns native `Keyboard.current`/`Mouse.current`; the tests queued state into added synthetic devices without making those devices current. Production `ShowcaseInputSystem` correctly reads the current devices, as independently demonstrated by the successful built-player replay.

## Follow-up fix / next exact discriminator

Keep production input unchanged. Make synthetic keyboard/mouse devices current explicitly in the EditMode fixture and both built-player input harnesses before queuing events. Re-run the same repository-driven SceneRuntime EditMode + module-local player + Kentridge integration gates and the actual `SmallVoxelShowcase` replay at the new exact feature SHA.

## 2026-09-05 — current-device corrected request

- Feature SHA: `53ef9d4de34bcc42c44405bb74bedddb5952e0eb`
- Transport SHA: `19dc670b0c1cc580b15c77c9a090efb3d8481dd0` (verified parent exactly the feature SHA; only `.github/test-request.json` differs).
- Workflow run: `33986010080`.
- Built production-scene replay: **passed again**. `SmallVoxelShowcase` built and the 20-second `showcase-input-smoke` replay completed successfully with real-player captures.
- Automatic module validation: **failed** after 77 seconds. The persistent `VoxelEngine.Showcase.Tests.EditMode` assembly ran 16 tests: 13 passed, 3 failed, all in `ShowcaseInputSystemTests`.
- Exact assertions: `ReadCurrent_MapsCompleteKeyboardAndMouseSemanticFrame` failed first on `ToggleCursor`; `ReadCurrent_PressedActionsAreEdgeTriggered_HeldMovementPersists` failed first on `Interact`; `ReadCurrent_NormalizesWheelDirectionAndMapsSecondaryEdit` failed first on `SecondaryEdit`. These are all `wasPressedThisFrame` edge checks.
- Root cause: `MakeCurrent()` corrected device ownership, but this fixture still manually calls `InputSystem.Update()` while the project remains in automatic update mode. Unity's Input System contract reserves manual updates for tests using its isolated fixture or for `ProcessEventsManually`; editor update state makes frame-edge properties nondeterministic otherwise.

## Follow-up fix / next exact discriminator

Keep production and built-player harness behavior unchanged. In `ShowcaseInputSystemTests` only, save the prior `InputSystem.settings.updateMode`, set `ProcessEventsManually` before driving queued synthetic events, and restore the prior mode during teardown. Keep every existing edge/held/mouse assertion intact. Re-run the full repository-derived SceneRuntime module tests, module-local standalone validation, Kentridge integration, and actual `SmallVoxelShowcase` replay at the resulting exact feature SHA.
