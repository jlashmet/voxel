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
- Retried the same ownership regression through CI commit `a5861391395776984581f0127385c05dce83d022`, Actions run `32882316548`, and inspected uploaded artifact `single-test-32882316548` (artifact id `9576332031`). That run exposed the one missing scene compatibility member, `FootprintDm`.
- Added only that non-authoring forwarding member in source commit `433bbe8ed24ce43627d4ff547d46e53930121f9e`.
- Reset `ci-test/fixes/agent-1` to that exact source commit and requested `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` through CI commit `c5243ad758f2e6349fb64268dbd3dbc447893616`.
- GitHub Actions run `32882777952` completed successfully. The job log records the exact requested filter and `Executed 1 test case(s).`; `ci/single-test` is `success`.

## Result

Passed. Source commit `433bbe8ed24ce43627d4ff547d46e53930121f9e` compiles and the focused ownership regression executed exactly one test case successfully in run `32882777952`. The regression now proves the public Kentridge bootstrap/generation-plan boundary is backend-blind, VoxelShowcase no longer consumes the legacy Kentridge voxel catalogue directly, the old package path is absent, and the relocated generation implementation is behind WorldBuilder ownership.

This experiment verifies the structural/API consolidation only. The scene issue remains open until the original `20260825-040805-194-VoxelShowcase` capture is replayed at its saved scene/camera pose and that visual verification evidence is inspected and recorded.

## What learned

The WorldBuilder/integration migration compiles through all known production and test callers without restoring legacy worldgen dependencies to the public Kentridge composition surface. The scene compatibility facade can keep existing presentation/survey code stable while forcing town construction through the one WorldBuilder authoring entry point; non-authoring geometry helpers may still delegate to the relocated physical backend.

The durable architectural destination remains intentionally broader than this capture: semantic authoring stays in `Game.WorldBuilder`, while generic spatial generation, architecture grammars, and voxel realization should later move behind a `VoxelEngine.WorldGen` boundary. The current `Assets/Game/WorldBuilder/Generation` location is an intermediate consolidation, not the final layering goal.

## Next

Replay the original `20260825-040805-194-VoxelShowcase` capture using its saved `Assets/Scenes/VoxelShowcase.unity` pose, inspect the produced frame against `screenshot-001.png`, and save replay evidence beside this experiment. If replay is satisfactory, perform the final diff/spec review, update the plan, then create the separate terminal bookkeeping commit moving this capture from `SceneIssues/open/` to `SceneIssues/closed/` with `issue.json` set to `fixed` and `fixCommit` pointing to the verified source commit.
