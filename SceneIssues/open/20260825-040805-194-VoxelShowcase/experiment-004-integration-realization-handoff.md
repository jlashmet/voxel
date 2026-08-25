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
- Retried the ownership regression through CI commit `0e12f4a8ba5e4e50a9c417302fa208f7811e682c`, Actions run `32880386099`; that exposed the stale EditMode secret-bootstrap caller.
- Migrated `KentridgeCampaignSecretBootstrapTests` through `WorldBuilderTownAuthoring` and one integration-owned realization bundle. Source tip became `087fa065da2e823afbaab8ca1a1de5e0f4d181fa`.
- Retried the same ownership regression through CI commit `652a49a780a0c4ed91dd6efab8a669eb42b664d9`, Actions run `32880681029`, and inspected the uploaded `single-test-32880681029` artifact to recover compiler diagnostics hidden by the warning-heavy inline log tail.

## Result

The third CI attempt still failed at Unity compilation before the requested ownership assertion executed. The previously reported secret-bootstrap errors are gone. The remaining compiler errors are:

- `Assets/Tests/EditMode/KentridgeCampaignWorldRealizationTests.cs` lines 61, 143, and 158 still pass raw `SettlementPlan` into `KentridgeCampaignWorldPlanner.Plan` instead of a WorldBuilder-authored `AuthoredTownPlan`.
- `Assets/Scenes/Kentridge/KentridgePlayableSlice.cs` line 140 still passes raw `SettlementPlan` into `KentridgeCampaignSessionBootstrap.Plan`.
- `Assets/Scenes/Kentridge/KentridgePlayableSlice.cs` line 214 still passes `KentridgeVoxelSiteRealizationFacts` directly to `CreateSession` instead of the integration-owned `KentridgeCampaignRealizationFacts` handoff.

`ci/single-test` correctly reported failure. No ownership assertion has passed yet.

## What learned

The integration-owned handoff is compiling through production Kentridge composition and the previously migrated test callers. The remaining production failure is the playable Kentridge scene itself, which still constructs and passes a legacy settlement directly into the campaign path. Merely changing the method argument while continuing to rebuild Kentridge independently for voxel realization would satisfy the compiler but preserve the exact duplicated-authoring risk this capture targets. The playable slice should therefore receive its campaign plan and Kentridge realization facts from the same WorldBuilder-authored town, with any legacy settlement use confined behind an integration adapter.

## Next

Migrate `KentridgeCampaignWorldRealizationTests` through `WorldBuilderTownAuthoring`. For `KentridgePlayableSlice`, author Kentridge once through WorldBuilder and route campaign/voxel/realization consumers through that same authored plan rather than creating a second Kentridge plan for rendering. Then repin `ci-test/fixes/agent-1` and rerun the exact ownership regression. After green CI, replay the original VoxelShowcase capture and inspect the verification artifact before terminal bookkeeping.
