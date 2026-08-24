# Experiment 004 — runtime character bounds

## Hypothesis

The placeholder humanoids are centred below their roots despite the scene treating each root as a
feet position, leaving only their heads above a correct pub floor.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a`, instrumented the production
`KentridgeOpeningCameraReadabilityTests` to log each actor root, combined renderer bounds, and camera
position at opening line 1 and Logan line 11. Ran the focused PlayMode test locally through
`tools/unity-run.sh`.

## Result

At line 1, all roots were at Y 21.900 m and renderer minima were 21.868–21.897 m, within 3.2 cm of
their roots; renderer maxima were 23.716–23.745 m. The camera was exactly
`(137.200,29.500,74.800)`, matching the saved issue pose. The line-1 viewport envelope passed. The
test later failed its pre-existing line-11 minimum-height threshold (0/1 overall). Evidence is in
`verification-actor-bounds-results.xml` and `verification-actor-bounds-unity.log`.

## What was learned

The visual-pivot hypothesis is disproven. The actors are foot-aligned and full-height in world
space. Their viewport bounds being on-screen does not prove they are unoccluded by voxel geometry.

## Next

Ray-test the camera-to-torso segments against the production scene's authoritative voxel world,
ignoring fragments that the active bounded cutaway would discard.
