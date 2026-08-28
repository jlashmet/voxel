# Experiment 011 — deterministic capture-pose traversal diagnostic

**Hypothesis** — The prior diagnostic's zero-frustum setup was initial-view variability. Starting from the exact saved SceneIssue camera pose should reliably establish visible coverage; any subsequent zero-draw frame can then classify the real movement discontinuity.

**Action / source** — From `35a79439afaeb7c4170dc355978c9e10541d9859`, changed only the diagnostic test. It now sets fly mode, disables mouse look, pins the recorded capture position `(77.953941, 24.550051, -3.345814)` and quaternion `(-0.01155361, -0.28760582, -0.00346975, 0.95767289)` before warmup, and includes camera pose plus far-hole state in failure telemetry. Production code is unchanged.

**Result** — Pending exact-SHA targeted CI for `VoxelEngine.Tests.PlayMode.ShowcaseTraversalCoverageDiagnosticsTests.ShortFlyTraversalKeepsAtLeastOneDrawableSurface`.

**Verdict** — Pending. A visible setup followed by zero draws will discriminate routing/publication state. Failure to establish visibility even at the verified capture pose would instead invalidate this synthetic traversal harness and require a smaller renderer-state repro before production changes.

**Next** — Run the exact diagnostic through `ci-test/fixes/agent-2`; inspect the first failure telemetry before changing production.