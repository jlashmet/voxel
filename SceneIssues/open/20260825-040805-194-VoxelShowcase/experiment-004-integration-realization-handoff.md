# Experiment 004 — Integration realization handoff

## Hypothesis

If the backend-facing WorldBuilderWorldGen integration assembly wraps the two legacy realization-fact interfaces behind a Game/integration-owned public type, then Kentridge composition can remain free of `MountingForce` assembly references while `CreateSession` exposes only Game-owned types. Strengthening the regression to inspect every public bootstrap method should prove the leak is closed rather than merely compiling by restoring a legacy reference.

## What performed + source commit

- Started from feature tip `83274f4845aa090c39f41f85f7718be281b63cd6`.
- Strengthened `WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` so every public Kentridge bootstrap parameter and every public generation-plan property must be free of `MountingForce.WorldGen` types.
- Added `KentridgeCampaignRealizationFacts` and `KentridgeCampaignWorldRealizationBoundary` in the explicit `Game.Composition.WorldBuilderWorldGen.Runtime` integration assembly.
- Migrated `KentridgeCampaignSessionBootstrap.CreateSession` to accept the integration-owned facts bundle and migrated the confirmed EditMode session tests through `WorldBuilderTownAuthoring` plus that bundle.
- Source tip for the CI attempt was `558cb2489d6e3f7d35d138744ff4cbc65b0eadd9`.
- Requested `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` through CI commit `2bbb748c732ac34343c29c268f56d0688e3b0b65`, GitHub Actions run `32879973521`.

## Result

Failed at Unity compilation before the requested regression executed. Production Kentridge composition no longer reports the legacy realization-fact errors; the remaining compiler errors are stale PlayMode callers:

- `Assets/Tests/PlayMode/KentridgePubExitPlayTests.cs`: lines 126 and 221 still pass `SettlementPlan` to `Plan`, and line 143 still passes `KentridgeVoxelSiteRealizationFacts` directly to `CreateSession`.
- `Assets/Tests/PlayMode/KentridgeOpeningVerticalSlicePlayTests.cs`: line 124 still passes `SettlementPlan` to `Plan`, and line 147 still passes `KentridgeVoxelSiteRealizationFacts` directly to `CreateSession`.

`ci/single-test` correctly reported failure. No ownership assertion has passed yet.

## What learned

Experiment 003’s second compile failure was an architectural signal: commit `d6191178d3b242eeac747700204845eb87dcfe01` deliberately removed legacy worldgen references from Kentridge runtime. Re-adding them would regress the intended boundary. The integration-owned handoff compiles far enough to eliminate the production errors, so the design direction is sound; the current failure is caller migration debt in PlayMode tests rather than a new production dependency problem.

## Next

Migrate the two stale PlayMode tests through `WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, seed)` and `KentridgeCampaignRealizationFacts`, preserving their concrete backend fixture only for post-generation test facts. Then repin `ci-test/fixes/agent-1` to the new source tip and rerun the exact ownership regression. After green CI, replay the original VoxelShowcase capture and inspect the verification artifact before terminal bookkeeping.
