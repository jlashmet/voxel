# Experiment 002 — CI compile binding

- **Hypothesis:** the first final CI failure is caused by the new regression binding the compatibility `StructuresComposition.PlanCastle` result to the game-layer `CastleLayout`, not by the gate implementation or runner.
- **Action / source:** exact request `66905ed3812838a19c0f46ad207c671434198ba8` ran PlayMode test `CastleFrontGateVisibleOpenRegressionTests.NearbyInteractionRevealsBothOpenLeavesAndKeepsCentrePassageClear`.
- **Result:** Unity stopped before tests with CS1503 at regression line 41: `VoxelEngine.Showcase.CastlePlan` could not be passed to `Game.Structures.Api.CastlePlan`. The real-player build failed for the same compiler error; bake restore and runner setup succeeded.
- **Verdict:** confirmed test-only compile defect. Existing `CastleAccessTests` use the compatibility `VoxelEngine.Structures.Api.CastleLayout` with this plan type.
- **Fix:** bind the regression to the same compatibility `CastleLayout` API. Production gate authoring is unchanged.
- **Next:** exact-SHA targeted PlayMode regression plus original-pose replay.
