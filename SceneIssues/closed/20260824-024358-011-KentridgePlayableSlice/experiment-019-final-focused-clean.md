# Experiment 019 — final focused clean run

## Hypothesis

When the scene regression owns the saved camera fixture explicitly, both retained PlayMode tests
pass on the final tree without the temporary standalone-player resource.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the final uncommitted diff,
confirmed `Assets/Resources/SceneIssueCameraPose.json` was absent, passed `git diff --check`, and ran
the production-composition and captured-camera line-of-sight tests together through
`tools/unity-run.sh`.

## Result

Both 2/2 tests passed: the captured-camera line-of-sight test in 24.780 seconds and the production
composition test in 47.705 seconds. The exact obstruction voxel remained empty in every composition
(`k=0 kh=0 kc=0 khc=0`). Evidence is in
`verification-final-focused-clean-results.xml` and `verification-final-focused-clean-unity.log`.

## What was learned

The hypothesis is confirmed. The final regression is self-contained, the production composition is
clear, and no replay-only asset remains in the shipped resource tree.

## Next

Review the complete diff and issue evidence, update the durable plan, then create the fix commit and
the separate resolution commit.
