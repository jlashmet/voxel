# CI operations — Kentridge meadow density

- Prior product-failure runs: `33244533044`, `33246401704`, `33246992214`; each workflow was green but failed mandatory built-player grass-motion inspection.
- Exposed-face request `4cb67bf942b5b4f9ad8834bb8c4ac92780ac84f2` / run `33247434464` failed during Unity compilation before tests or player capture. This was a product branch regression: the grounding full-file edit had accidentally restored an obsolete concrete-renderer `Configure(...)` version of `KentridgeRegionLife`.
- Compile repair restores the exact prior green interface-based `Populate(...)` implementation and changes only `TryGround` Y from `height * VoxelSize` to `(height + 1) * VoxelSize`. Compared with source `08116a0d6676dad0300cc5b44cd13f4c10de91b2`, `KentridgeRegionLife.cs` is now +3/-1 (two comment lines plus the one grounding line).
- Final focused filter remains `VoxelEngine.Tests.PlayMode.KentridgeMeadowAcceptanceTests.BuiltKentridge_ReportsDenseConnectedGrassOnlyMeadowWithNoExcludedLeakage`, including exposed-root assertions and the workflow-mandated isolated framebuffer deformation repro, plus 60-second issue replay.
- Never replace queued/running work; the compile-failure run is completed before the corrected request is issued.
