# Experiment 018 — final focused fixture dependency

## Hypothesis

The two retained PlayMode regressions pass together after the temporary standalone-player camera
resource is removed.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the final uncommitted diff,
ran the production-composition and captured-camera line-of-sight tests together through
`tools/unity-run.sh` with no `SceneIssueCameraPose` resource in `Assets/Resources`.

## Result

The run failed 1/2. The production composition test passed in 47.652 seconds with
`k=0 kh=0 kc=0 khc=0`. The camera test failed before voxel tracing because the unpinned authored
camera was 0.5 metres from the saved issue position. Evidence is in
`verification-final-focused-results.xml` and `verification-final-focused-unity.log`.

## What was learned

The hypothesis is disproven. The earlier focused pass had an undeclared dependency on the
temporary standalone replay resource. A durable SceneIssue regression must explicitly apply its
saved camera fixture inside the test before asserting the pose and tracing line of sight.

## Next

Pin the captured position, quaternion, and FOV in the regression itself and rerun both retained
tests without the temporary resource.
