# CI operations — Kentridge meadow density

- Prior product-failure runs: `33244533044`, `33246401704`, `33246992214`; each workflow was green but failed mandatory built-player grass-motion inspection.
- Final source candidate: `1d1a129a6c5e47aef658575888bb70f232f5c5d7`.
- Final focused filter: `VoxelEngine.Tests.PlayMode.KentridgeMeadowAcceptanceTests.BuiltKentridge_ReportsDenseConnectedGrassOnlyMeadowWithNoExcludedLeakage`, including exposed-root assertions and the workflow-mandated isolated framebuffer deformation repro, plus 60-second issue replay.
- Previous assigned CI request is completed; no queued/running request is being replaced.
