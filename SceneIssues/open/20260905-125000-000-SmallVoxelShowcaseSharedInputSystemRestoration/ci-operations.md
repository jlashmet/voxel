# CI operations — SmallVoxelShowcase shared Input System restoration

## 2026-09-05 — first exact-SHA request

- Feature SHA: `b1bcc789d2d9464569b630c71d59fe6e3e2d4335`
- Transport SHA: `da10e03bd1d4d1bb49dbf4331378486732ea6697`
- Workflow run: `33971319798`
- Request: automatic affected-module validation + explicit `VoxelEngine.Structures.Tests.PlayMode.TypedStructuralSocketCompositionSceneTests` + SceneIssue `showcase-input-smoke` replay.
- Result: **failed**. `Run automatically required module validation` failed after about 72 seconds. The later SceneIssue replay step started but the job ended without final status publication or artifacts; GitHub's job-log blob is unavailable, so this run does not provide a trustworthy failing test name.

## Discriminator after completed failure

The automatic plan owns the changed `SceneRuntime` module and its player validation, while the Structures test is an independent explicitly requested regression. Do not change production input from this incomplete signal. Re-run the same feature content with **no explicit extra test** so automatic `SceneRuntime` EditMode/player validation plus Kentridge integration are isolated from the Structures bootstrap regression. If that automatic gate is green, diagnose/fix the independent Structures bootstrap; if it still fails, inspect the shared-input module path first.
