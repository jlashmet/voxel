# Experiment 004 — Integration realization handoff

## Hypothesis

If the backend-facing WorldBuilderWorldGen integration assembly wraps the two legacy realization-fact interfaces behind a Game/integration-owned public type, then Kentridge composition can remain free of `MountingForce` assembly references while `CreateSession` exposes only Game-owned types. Strengthening the regression to inspect every public bootstrap method should prove the leak is closed rather than merely compiling by restoring a legacy reference.

## What performed + source commit

- Started from feature tip `83274f4845aa090c39f41f85f7718be281b63cd6`.
- Strengthened `WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` so every public Kentridge bootstrap parameter and every public generation-plan property must be free of `MountingForce.WorldGen` types.
- Added `KentridgeCampaignRealizationFacts` and `KentridgeCampaignWorldRealizationBoundary` in the explicit `Game.Composition.WorldBuilderWorldGen.Runtime` integration assembly.
- Migrated `KentridgeCampaignSessionBootstrap.CreateSession` to accept the integration-owned facts bundle and migrated the confirmed EditMode session tests through `WorldBuilderTownAuthoring` plus that bundle.
- Source tip for the first CI attempt was `558cb2489d6e3f7d35d138744ff4cbc65b0eadd9`; request commit `2bbb748c732ac34343c29c268f56d0688e3b0b65`, Actions run `32879973521`, failed on stale PlayMode callers.
- Migrated `KentridgePubExitPlayTests` and `KentridgeOpeningVerticalSlicePlayTests` through WorldBuilder authoring and the integration-owned realization bundle. Source tip became `cdfcd8d9db95751bbae8c8fe30aee4be0a8277bb`.
- Retried `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` through CI commit `0e12f4a8ba5e4e50a9c417302fa208f7811e682c`, GitHub Actions run `32880386099`.

## Result

The second CI attempt still failed at Unity compilation before the requested ownership assertion executed. The previously reported production and PlayMode caller errors are gone. The remaining errors are confined to `Assets/Tests/EditMode/KentridgeCampaignSecretBootstrapTests.cs`:

- its campaign plan still passes raw `SettlementPlan` instead of the WorldBuilder-authored `AuthoredTownPlan`;
- its missing-host path passes `KentridgeVoxelSiteRealizationFacts` directly where `KentridgeCampaignRealizationFacts` is now required and passes hidden facts in the old secret-host argument position;
- its success path still uses the removed seven-argument `CreateSession` shape instead of bundling site and hidden-space realization facts before supplying the `IKentridgeCampaignSecretHost`.

`ci/single-test` correctly reported failure. No ownership assertion has passed yet.

## What learned

Experiment 003’s second compile failure was an architectural signal: commit `d6191178d3b242eeac747700204845eb87dcfe01` deliberately removed legacy worldgen references from Kentridge runtime. Re-adding them would regress the intended boundary. The integration-owned handoff is now compiling through production plus the migrated PlayMode callers; the only remaining compile fallout reported by Unity is a stale EditMode secret-bootstrap caller. This confirms the production boundary itself is no longer the source of the compile failures.

## Next

Migrate `KentridgeCampaignSecretBootstrapTests` through `WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed)` and one `KentridgeCampaignRealizationFacts` bundle containing both the concrete site and hidden-space facts. Then repin `ci-test/fixes/agent-1` to the new source tip and rerun the exact ownership regression. After green CI, replay the original VoxelShowcase capture and inspect the verification artifact before terminal bookkeeping.
