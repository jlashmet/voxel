# Experiment 006 — magic-shop final shell ray

## Hypothesis

After applying the magic shop's fill/carve primitives in their real within-instance order, the
saved camera begins in carved interior space and first meets a supported exterior wall rather than
an unexplained floating component.

## What was performed

Against source `36cec6893239e000c9aa875ebe9320a99927d0f4`, extended the temporary Kentridge
catalogue diagnostic to apply every magic-shop primitive in order at each voxel along the exact
saved centre ray. Ran
`VoxelEngine.Tests.EditMode.KentridgeGenerationTests.DiagnosticSavedFloatingStructureRayContributors`
through `tools/unity-run.sh`.

## Result

The test passed 1/1. The first final occupied magic-shop cell is material 1 at
`(1017,268,604)`, 22 voxels (2.2 metres) from the camera, owned by the principal shell primitive.
Evidence is `verification-magicshop-final-ray-results.xml` and
`verification-magicshop-final-ray-unity.log`.

## What was learned

The hypothesis is confirmed. On current `fixes`, the camera is inside the properly carved magic
shop and looks at its west shell wall 2.2 metres away. The original floating fragments were the
same building volume corrupted/obscured by the cross-town authored overlap already removed in
`0459ec9afb6b7deaa7ed38b35d9059b0b0bc4eb4`.

## Next

Inspect the magic shop from a deliberate exterior validation pose to ensure the corrected building
reads as a grounded structure, then remove diagnostics/replay assets and validate the existing
settlement-boundary regression on the clean tree.
