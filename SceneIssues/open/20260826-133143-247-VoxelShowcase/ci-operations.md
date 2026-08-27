# CI operations — 20260826-133143-247-VoxelShowcase

- First exact request: source `4bb7a5fc2fa54b4928c21d1b12fb7b66b08e29bf`, request `e309998bd8e4dda6ffb53f7a044a8a6cd3d6e586`, request id `agent3-133143-final-20260827-0532`, run `33072410408`.
- Result: `ci/single-test=failure`. Unity aborted before test execution because `KentridgeUrbanFabricSpacingPlayModeTests.cs` passed `NativeArray` indexer expressions as `in` arguments (`CS8156`, lines 33/42). Replay build failed for the same compile error, so this run is diagnostic only and supplies no visual verification.
- Correction: test-only plumbing changed to copy `FeatureDefinition` / `ExplicitPlacement` values to locals before calling `Bounds`; production spacing code is unchanged.
- Final gate still required: a new exact-SHA PlayMode request running `VoxelEngine.Tests.PlayMode.KentridgeUrbanFabricSpacingPlayModeTests.ProductionAnonymousFrontagesLeavePedestrianClearanceBetweenHouses` with the assigned capture replayed for 45 seconds.
