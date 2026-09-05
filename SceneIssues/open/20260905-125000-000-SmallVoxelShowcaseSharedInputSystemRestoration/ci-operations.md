# CI operations — SmallVoxelShowcase shared Input System restoration

## 2026-09-05 — first exact-SHA request

- Feature SHA: `b1bcc789d2d9464569b630c71d59fe6e3e2d4335`
- Transport SHA: `da10e03bd1d4d1bb49dbf4331378486732ea6697`
- Workflow run: `33971319798`
- Request: automatic affected-module validation + explicit `VoxelEngine.Structures.Tests.PlayMode.TypedStructuralSocketCompositionSceneTests` + SceneIssue `showcase-input-smoke` replay.
- Result: **failed**. `Run automatically required module validation` failed after about 72 seconds. The later SceneIssue replay step started but the job ended without final status publication or artifacts; GitHub's job-log blob is unavailable, so this run does not provide a trustworthy failing test name.

## 2026-09-05 — isolated automatic-module request

- Feature SHA: `9e7a4374def320b9919ba6221eab5aee526a41c7`
- Transport SHA: `4f01fb4d3c265cd4ed2c5b6bf1a9e689f91321cc`
- Workflow run: `33984299208`, successful execution on attempt 3 after two pre-step infrastructure cancellations.
- Request: automatic affected-module validation only, plus SceneIssue replay; no explicit Structures test.
- Built `SmallVoxelShowcase` replay: **passed** with two real-player screenshots.
- Automatic module validation: **failed**. `VoxelEngine.Showcase.Tests.EditMode` ran 16 tests: 13 passed, 3 failed, all new `ShowcaseInputSystemTests`.
- Discriminator: Structures is not the cause; production built input works while the synthetic regression does not.

## 2026-09-05 — current-device corrected request

- Feature SHA: `53ef9d4de34bcc42c44405bb74bedddb5952e0eb`
- Transport SHA: `19dc670b0c1cc580b15c77c9a090efb3d8481dd0` (parent exactly feature SHA; request-file-only diff).
- Workflow run: `33986010080`.
- Built `SmallVoxelShowcase` replay: **passed again**.
- Automatic module validation: **failed**. The same three input tests failed first on `ToggleCursor`, `Interact`, and `SecondaryEdit`, all edge-trigger checks, despite synthetic devices being made current.

## 2026-09-05 — manual-update corrected final-acceptance request

- Feature SHA: `dacd2884f6ed9a399165d7f60fffab7f77acd792`
- Transport SHA: `0623548f8ec91f6c32fe7900d5b5c21f4f803ddc` (parent exactly feature SHA; request-file-only diff).
- Workflow run: `33987455658`.
- Request: explicit Structures PlayMode startup regression + repository-derived Showcase module validation + actual SceneIssue replay.
- Built `SmallVoxelShowcase` replay: **passed again**.
- Automatic module validation: **failed** before player validations. The same three `ShowcaseInputSystemTests` edge assertions remained red after the fixture switched to `ProcessEventsManually`.
- Issue-guide stop condition reached: two materially different harness fixes (`MakeCurrent`, then manual update mode) did not change the same assertion symptom. No further timing/device speculation is allowed.

## Minimal root cause

Unity Input System's `InputTestFixture` implementation states that it resets/severs runtime input state for deterministic tests and is designed for **PlayMode**; EditMode is generally unsupported. The package assembly is `Unity.InputSystem.TestFramework`. Our direct edge regression is in `VoxelEngine.Showcase.Tests.EditMode`, so it is testing `wasPressedThisFrame` semantics in the unsupported mode.

Selected correction: move only `ShowcaseInputSystemTests` to a module-owned PlayMode assembly deriving from `InputTestFixture`; preserve all assertions and production code. Then request one exact-SHA run containing the explicit Structures regression plus repository-derived Showcase EditMode + PlayMode tests, module-local player validation, Kentridge integration, and actual `SmallVoxelShowcase` replay.
