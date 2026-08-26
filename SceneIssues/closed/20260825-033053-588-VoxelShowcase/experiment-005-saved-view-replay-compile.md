# Experiment 005 — saved-view replay compile

## Hypothesis
The capture-specific saved-camera regression compiles and can replay collision plus shooting against an authored tree visible from the original VoxelShowcase pose.

## What was performed
Source commit: `31abc68a185ee1ab7f3146a888a4c52caf890a86`.

Reset `ci-test/fixes/agent-6` to that feature commit and requested `VoxelEngine.CI.SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot`. Request commit: `1ed3d0a33315958ce9d339502d6bd720ca7f8fe3`; workflow: `32928174804`.

## Result
**Failed before test execution.** Unity reported `CS0234` at `SceneIssue20260825033053TreeInteractionTests.cs(10,19)`: `VoxelEngine.Composition` is not referenced by the CI PlayMode assembly. The replay test executed zero cases.

## What was learned
The saved-view fixture belongs in the CI assembly, which already references Vegetation.Runtime but not the higher-level composition assembly. The replay should exercise `TreeDamageService` directly for runtime collision/damage behavior while the separate green Showcase regression continues to assert that `CharacterMotor` and projectile code route through the production composition capability.

## Next
Remove the invalid composition dependency, instantiate `TreeDamageService` inside the replay fixture, and rerun the same saved-view test on the exact repaired feature head.
