# Experiment 004 — prior Hightown stages on the centre ray

## Hypothesis

One of the Kentridge-authored stages formerly emitted by Hightown directly filled the saved
camera's centre ray and was itself the floating silhouette in the original screenshot.

## What was performed

Against source `36cec6893239e000c9aa875ebe9320a99927d0f4`, reconstructed the fifteen Kentridge-only
stages that the pre-fix Hightown composer used to run with Hightown settings. A temporary EditMode
diagnostic evaluated every fill primitive along the exact saved centre ray. Ran
`VoxelEngine.Tests.EditMode.KentridgeGenerationTests.DiagnosticPriorHightownStagesAtSavedRay`
through `tools/unity-run.sh`.

## Result

The test passed 1/1 but reported no `PRIOR_HIGHTOWN_HIT` entries. Evidence is
`verification-prior-hightown-ray-results.xml` and `verification-prior-hightown-ray-unity.log`.

## What was learned

The hypothesis is disproven for direct centre-ray occupancy. The malformed original silhouette is
the magic-shop shell itself or results from another stage overlapping/carving its broader volume,
not a separate old-Hightown fill directly in the centre pixel.

## Next

Enumerate every reconstructed old-Hightown primitive whose bounds overlap the magic-shop envelope,
including carve and terrain modes, then determine which can mutilate or obscure the structure.
