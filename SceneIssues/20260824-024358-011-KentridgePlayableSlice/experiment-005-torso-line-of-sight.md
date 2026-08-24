# Experiment 005 — torso line of sight

## Hypothesis

Voxel geometry below the roof-cutaway threshold lies between the exact opening camera and the
otherwise correctly placed actor bodies.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a`, extended the focused production
camera test to sample the authoritative `ShowcaseWorld` from the camera to each line-1 actor's
0.9 m torso point. Samples inside the active renderer cutaway bounds were ignored, matching the
shader's discard policy. Ran locally through `tools/unity-run.sh`.

## Result

The test failed before later camera assertions: Weldon's torso ray hit authoritative material 6 at
world voxel `(1339,231,757)`, outside/below the active cutaway. Camera and torso target were
`(137.200,29.500,74.800)` and `(133.700,22.800,75.800)`. Evidence is in
`verification-torso-line-of-sight-results.xml` and `verification-torso-line-of-sight-unity.log`.

## What was learned

The hypothesis is confirmed. This is not a GPU extraction choice or a character-pivot problem: the
renderer is correctly drawing authoritative solid occupancy below the presentation cutaway. The
earlier Kentridge-only clearance test did not reproduce the production scene's complete world
composition and therefore was insufficient to rule out authoring.

## Next

Reproduce the occupied torso voxel with the production seed/settings and isolate which combined
catalogue stage authors material 6 there before choosing between site-preparation and camera
composition fixes.
