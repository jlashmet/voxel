# CI operations — SmallVoxelShowcase shared Input System restoration

## 2026-09-05 — first exact-SHA request

- Feature SHA: `b1bcc789d2d9464569b630c71d59fe6e3e2d4335`
- Transport SHA: `da10e03bd1d4d1bb49dbf4331378486732ea6697`
- Workflow run: `33971319798`
- Request: automatic affected-module validation + explicit `VoxelEngine.Structures.Tests.PlayMode.TypedStructuralSocketCompositionSceneTests` + SceneIssue `showcase-input-smoke` replay.
- Result: **failed**. Automatic module validation failed; later evidence was incomplete.

## 2026-09-05 — isolated automatic-module request

- Feature SHA: `9e7a4374def320b9919ba6221eab5aee526a41c7`
- Transport SHA: `4f01fb4d3c265cd4ed2c5b6bf1a9e689f91321cc`
- Workflow run: `33984299208`, successful execution on attempt 3 after two pre-step infrastructure cancellations.
- Built `SmallVoxelShowcase` replay: **passed**.
- Automatic module validation: **failed** only the three new `ShowcaseInputSystemTests`; Structures was not the cause.

## 2026-09-05 — current-device corrected request

- Feature SHA: `53ef9d4de34bcc42c44405bb74bedddb5952e0eb`
- Transport SHA: `19dc670b0c1cc580b15c77c9a090efb3d8481dd0` (parent exactly feature SHA; request-file-only diff).
- Workflow run: `33986010080`.
- Built `SmallVoxelShowcase` replay: **passed**.
- Automatic module validation: **failed** the same three input-edge assertions despite synthetic devices being current.

## 2026-09-05 — manual-update corrected request

- Feature SHA: `dacd2884f6ed9a399165d7f60fffab7f77acd792`
- Transport SHA: `0623548f8ec91f6c32fe7900d5b5c21f4f803ddc` (parent exactly feature SHA; request-file-only diff).
- Workflow run: `33987455658`.
- Built `SmallVoxelShowcase` replay: **passed**.
- Automatic module validation: **failed** the same three edge assertions after `ProcessEventsManually`.
- Issue-guide stop condition reached after two materially different harness fixes.

## Minimal root cause

Unity Input System's `InputTestFixture` resets/severs runtime input state for deterministic tests and is designed for **PlayMode**; EditMode is generally unsupported. The direct edge regression therefore belongs in module-owned PlayMode rather than `VoxelEngine.Showcase.Tests.EditMode`.

## 2026-09-05 — root-cause exact acceptance request

- Feature SHA: `7e6c609c34dff4768032f9046e891f43cbd935b7`
- Transport SHA: `e60a1c7c6e348d9876b35baa8b4a5898b7043abe` (parent exactly feature SHA; only `.github/test-request.json` differs).
- Workflow run: `33988857330`.
- Correction: moved only `ShowcaseInputSystemTests` to module-owned `VoxelEngine.Showcase.Tests.PlayMode`, using `InputTestFixture`; assertions and production input remained unchanged.
- Automatically required module validation: **passed**, covering repository-derived Showcase EditMode + PlayMode tests and the explicit `VoxelEngine.Structures.Tests.PlayMode.TypedStructuralSocketCompositionSceneTests` regression.
- Repository-owned player validation: **passed**, including the module-local Showcase validation and Kentridge integration targets.
- Actual built `SmallVoxelShowcase` SceneIssue replay `showcase-input-smoke`: **passed**, proving injected supported input changes the real player/camera path.
- Verdict: **all assigned exact-SHA acceptance gates passed**. Proceed directly from `open/` to `closed/`, then sync current master and promote through PR + auto-merge.
