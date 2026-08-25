# Experiment 010 — production composition after stage gating

## Hypothesis

Removing Kentridge-authored stages from Hightown clears the exact authoritative voxel that blocked
the saved camera's view of Weldon's torso in the full Kentridge + Hightown + corridor composition.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the uncommitted catalogue
composition fix, ran
`VoxelEngine.Tests.PlayMode.KentridgePubExitPlayTests.ProductionCatalogueCompositionLeavesOpeningTorsoVoxelClear`
locally through `tools/unity-run.sh` with the production plans and material maps.

## Result

The test passed 1/1 in 47.812 seconds. Voxel `(1339,231,757)` sampled empty for Kentridge alone,
Kentridge + Hightown, Kentridge + corridor, and the complete composition (`k=0 kh=0 kc=0 khc=0`),
with no Hightown primitive contributor. Evidence is in
`verification-composition-fixed-results.xml` and `verification-composition-fixed-unity.log`.

## What was learned

The hypothesis is confirmed. The production catalogue no longer authors the known obstruction;
the result is not an artifact of the geographic boundary assertion.

## Next

Make the saved-camera line-of-sight assertion independently runnable, validate it in the production
scene, and replay the exact capture visually.
