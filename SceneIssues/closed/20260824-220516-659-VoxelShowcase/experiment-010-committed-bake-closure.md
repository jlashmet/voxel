# Experiment 010 — committed bake closure

## Purpose

Experiment 009 proved that the exact VoxelShowcase replay had been judging an August 22 baked startup image instead of current Kentridge world-generation source. This closure experiment verifies the repository state users actually receive: current generator source, a freshly committed `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes`, the normal standalone player build, and the permanent structural regressions.

## Production source retained

The circulation cleanup remains in production source:

- `2bdda6b1380091660c11cabfb26ceb231570de45` — remove the disconnected lower Kentridge stair;
- `c160eaa52d6f3e3f6fb4c458fd9b599c8daf57b6` — stop overlaying dedicated stairs on the already-continuous main-road climb;
- `fdf2406a93a7f799e6ee72ea95b2a4e5a679cf8a` — retire the duplicate lower semantic stair chain while preserving the coherent upper-west route;
- follow-up cleanup removed retired constants/dead builders without changing the intended route topology.

`fdf2406a93a7f799e6ee72ea95b2a4e5a679cf8a` is the primary fix commit recorded by this SceneIssue because it is the last causal production topology change in the fix series.

## Committed startup image

Actions run `32841982576` rebuilt the full VoxelShowcase startup image from the current `fixes` source and committed only the resulting binary as:

- commit `70ef06ec585e79001e8253efd2ceab53d8a696e7`
- message `Refresh VoxelShowcase bake for Kentridge scene fixes`

The same job then built the ordinary standalone `Assets/Scenes/VoxelShowcase.unity` player from that committed file, replayed `issue.json`, verified the frozen saved camera pose, and completed successfully.

Evidence artifact:

- `9561017738` — `scene-220516-committed-bake`
- digest `sha256:3ff7ff110f24f6fdb6ed5bb35f90570109887dfd317960e4e4f2b7b743bc5013`

This closes the validation gap from experiment 009: the improved town view now comes from repository state, not a temporary in-job bake replacement.

## Final structural regression

After removing the temporary spatial-dump test, Actions run `32842583635` ran exactly the permanent `VoxelEngine.Tests.EditMode.KentridgeCirculationCoherenceTests` suite on the cleaned current head and passed all three tests:

1. `SecondaryParallelStairStreetsKeepTownSpacingFromMainSpine`
2. `LowerTownSkeletonDoesNotAdvertiseDuplicateSecondaryStairChain`
3. `VerticalInfrastructureDoesNotOverlayDedicatedStairsOnContinuousMainRoad`

Evidence artifact:

- `9561096056` — `scene-220516-final-regression`
- digest `sha256:5af2d945fa87e17a06f6ed951dee7cc86702ae2b107f1d3a8d462bb13267198f`

## Cleanup

- temporary `Assets/Tests/EditMode/KentridgeScene220516SpatialDiagnosticsTests.cs` removed in `808d22a0233b3ac98abd9df7017bdcfb1cdf71ca`;
- borrowed `.github/workflows/one-shot-sceneissue-014011-core-only-topology.yml` restored byte-for-byte in `889399364bb23790c2db1ff96b7be2772d17af2a`;
- restored workflow blob SHA is `a37f470b5d271c1b2a4c9fda3f37ba608065afd3`, exactly matching the pre-issue blob from commit `a2c8ab9427ed245450099d06f4768b4b1c2cf922`.

The restore commit naturally retriggered that historical 014011 one-shot and that unrelated diagnostic run failed immediately; it made no repository changes and does not affect this SceneIssue's validation.

## Result

The captured view now satisfies the SceneIssue acceptance criteria: the conspicuous overlapping foreground stair ribbons are gone, the lower-town circulation reads as one coherent central climb with separated side access, the visible uphill buildings do not read as floating over those stair forms, and the permanent generator invariants pass.

The SceneIssue is ready to mark `fixed`.
