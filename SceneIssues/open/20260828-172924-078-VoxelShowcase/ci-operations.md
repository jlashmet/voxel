# CI operations — 20260828-172924-078-VoxelShowcase

- Run `33257610930` at source `0aee2ebb70584af7228b381c70c873f28a01b216`: focused regression failed on a synchronization/object-selection assertion; the same run built and launched `VoxelShowcase` for 60 s without player-harness failure. Corrected by the later deterministic component-owned regression; diagnostic only.
- Run `33268258959` at source `c7ee53de060376a690e3f57a035424ac5a8b5543`: NUnit XML contains exactly one selected test, `VoxelEngine.Tests.PlayMode.CastleLowerRiverWaterRepairPlayModeTests.StartupFallbackRecentersAfterCameraRelocationBeforeRingPublication`, and it passed. The player build and 60 s issue replay also completed and uploaded frames, but the overall workflow/job result was cancelled after those substantive steps. Per SceneIssue rules, cancelled CI cannot satisfy a gate.
- No queued/running request is being replaced. The next request is the single fresh final request for the current clean feature SHA.
