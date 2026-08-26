# Experiment 005 — captured-pose replay assembly boundary

## Hypothesis
A scene-specific `VoxelEngine.CI` PlayMode regression can replay the saved camera pose while calling the same high-level composition facade used by Showcase gameplay.

## What was performed
Added `SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot`, reset `ci-test/fixes/agent-6` to feature commit `31abc68a185ee1ab7f3146a888a4c52caf890a86`, and requested the exact test. Request commit: `1ed3d0a33315958ce9d339502d6bd720ca7f8fe3`; workflow: `32928174804`.

## Result
**Failed at compilation before test execution.** `VoxelEngine.CI` cannot reference the higher-level `VoxelEngine.Composition` namespace (`CS0234`). The workflow correctly skipped the generic Showcase pre-bake and reached the requested test compilation path.

## What was learned
The replay belongs below the Composition assembly boundary. This does not invalidate the gameplay wiring: experiment 004 already proves `CharacterMotor` references the composition facade. The scene replay should exercise the public `TreeDamageService` runtime adapter directly, which is the implementation behind that facade and is accessible from the CI assembly.

## Next
Replace the replay's composition calls with a `TreeDamageService` instance and rerun the same saved-pose test from the new exact feature head.
