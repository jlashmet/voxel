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
- Retried the same ownership regression through CI commit `652a49a780a0c4ed91dd6efab8a669eb42b664d9`, Actions run `32880681029`, and inspected the uploaded artifact to recover compiler diagnostics hidden by the warning-heavy inline log tail.
- Migrated `KentridgeCampaignWorldRealizationTests` through WorldBuilder authoring and added a scene-local Kentridge compatibility bridge so the playable slice's existing unqualified `KentridgeDefinition.Build` entry point now calls `WorldBuilderTownAuthoring.Author` and reuses that authored backend plan for its legacy physical adapters instead of authoring Kentridge independently.
- Added the intentionally narrow `Game.Kentridge.PlayableSlice` friend access needed for the scene bridge to unwrap `AuthoredTownPlan.BackendPlan`, and added `Game.WorldBuilder.Runtime` to the playable-slice assembly reference list.
- Updated the durable plan at source commit `e870d61f480cd6b7c1aae032408976b8470499f5` to document the intended end state: `Game.WorldBuilder` owns semantic game intent, while reusable generic physical generation is a follow-on extraction target for `VoxelEngine.WorldGen`.
- Retried the same ownership regression through CI commit `a5861391395776984581f0127385c05dce83d022`, Actions run `32882316548`, and inspected uploaded artifact `single-test-32882316548` (artifact id `9576332031`).

## Result

Still failing at Unity compilation before the requested ownership assertion executes, but the prior raw-plan/raw-realization caller errors are gone. The uploaded `single.log` contains one compiler error repeated across Unity compile passes:

- `Assets/Scenes/Kentridge/KentridgeOpeningPresentation.cs(244,53)`: `CS0117: 'KentridgeDefinition' does not contain a definition for 'FootprintDm'`.

The scene-local compatibility type intentionally shadows the relocated legacy `KentridgeDefinition` for the whole `Game.Kentridge.PlayableSlice` namespace. `KentridgeOpeningPresentation` therefore resolves the new compatibility facade too, and that facade currently forwards `Id`, `TownCentreDm`, `Theme`, and `Build` but not the non-authoring `FootprintDm` geometry helper. `ci/single-test` correctly reported failure; no ownership assertion has passed yet.

## What learned

The WorldBuilder/integration migration is now compiling past every previously identified raw legacy plan/facts caller. The latest failure is not a second authoring path or a dependency-direction problem; it is an incomplete compatibility facade caused by normal C# namespace/type resolution. Forwarding `FootprintDm` to the relocated physical backend preserves the desired behavior: construction still enters through WorldBuilder, while an existing presentation helper may query backend geometry without independently constructing a town.

The durable architectural destination remains intentionally broader than this capture: semantic authoring stays in `Game.WorldBuilder`, while generic spatial generation, architecture grammars, and voxel realization should later move behind a `VoxelEngine.WorldGen` boundary. The current `Assets/Game/WorldBuilder/Generation` location is an intermediate consolidation, not the final layering goal.

## Next

Add only the missing `FootprintDm` forwarding member to the scene compatibility facade, repin `ci-test/fixes/agent-1` to the resulting source tip, and rerun the exact ownership regression. If Unity exposes another missing non-authoring compatibility member, record it before changing source. After the regression is green, replay the original `20260825-040805-194-VoxelShowcase` capture and inspect the saved verification artifact before terminal bookkeeping.
