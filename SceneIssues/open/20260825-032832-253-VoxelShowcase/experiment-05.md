# Experiment 05 — mapped exact-view standalone replay

## Hypothesis

The hierarchy-exclusive solid draw staging validated in experiment 03 removes the mixed-resolution terrain overlap at the saved `VoxelShowcase` viewpoint.

## What was performed

Requested `VoxelEngine.Tests.PlayMode.ShowcaseSceneIssue032832ReplayTests.SavedFixtureIsConfiguredForExactReplay` through `ci-test/fixes/agent-4` from source commit `542843fe901769990bd7f0ebf38093858854d8c4`, using request commit `7c3899aed7cc2e3e5df96f8c2149e64140e4e3cb`. GitHub Actions run: `32892693260`.

The shared standalone-player path built and ran `Assets/Scenes/VoxelShowcase.unity` with the saved scene-issue fixture at the original 1364x836 framing. Artifact `single-test-32892693260` contains five real-player screenshots through the stationary 54.7-second frame.

## Result

Falsified. The standalone player reached the recorded camera and produced the expected screenshots, but the final stationary frame still shows dense striped/contour-like terrain artifacts in the reported view. The visual defect therefore survives the hierarchy-exclusive draw-staging fix and the capture is not ready to close.

The PlayMode test itself also failed because `WaitForEndOfFrame` is not invoked in Unity batch mode. Its direct `Camera.Render` diagnostic is not the closure gate; the real standalone-player screenshots are the authoritative visual evidence for this experiment. The workflow was eventually cancelled by its five-minute limit after the player evidence had already been written.

## What was learned

Suppressing a visible finer descendant whenever its coarse hierarchy ancestor is visible is not the complete overlap path. The remaining real-player artifact is consistent with another terrain representation or a non-hierarchical overlap still being submitted. The next implementation direction must therefore trace every terrain submission visible in the standalone showcase, including the analytic/far field and LOD transition paths, before changing ownership again.

## Next

Trace the standalone terrain render sources and determine whether the analytic/far-terrain representation is drawn inside the voxel-ring coverage or whether another non-hierarchical duplicate submission remains. Also make the replay fixture batchmode-safe before the next targeted replay request.
