# Experiment 011 — captured-camera authoritative line of sight

## Hypothesis

With Hightown's misplaced Kentridge stages removed, the production scene at the saved first-line
camera pose has no authoritative solid outside the bounded cutaway between the camera and the three
initial actors' torsos.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the uncommitted fix, added and
ran the focused PlayMode regression
`VoxelEngine.Tests.PlayMode.KentridgeOpeningCameraReadabilityTests.CapturedOpeningCameraHasAuthoritativeLineOfSightToInitialCast`
through `tools/unity-run.sh`. It verifies the saved position, quaternion, 58-degree FOV, first
dialogue state, and voxel rays to Weldon, Madeline, and Steven while excluding only the production
cutaway volume.

## Result

The test passed 1/1 in 24.890 seconds. Evidence is in
`verification-captured-line-of-sight-fixed-results.xml` and
`verification-captured-line-of-sight-fixed-unity.log`.

## What was learned

The hypothesis is confirmed in the production scene. The GPU renderer receives authoritative empty
space along each captured-camera torso ray; the prior obstruction is gone without broadening the
presentation cutaway.

## Next

Run affected catalogue/opening tests and build the production player for an exact-pose visual replay.
