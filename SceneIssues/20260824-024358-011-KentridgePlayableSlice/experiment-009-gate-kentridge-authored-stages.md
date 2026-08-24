# Experiment 009 — gate Kentridge-authored stages

## Hypothesis

Hightown stops contaminating Kentridge when the canonical composition retains only stages whose
placements derive from the resolved settlement plan, while Kentridge keeps its full authored stage
sequence.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the uncommitted catalogue
composition fix, gated Kentridge-owned absolute-placement stages on the Kentridge theme and changed
shared town-dressing elevation to use the resolved settlement centre. Ran
`VoxelEngine.Tests.EditMode.TwoTownWorldTests.HightownVoxelCatalogueDoesNotEmitSouthOfTheCountryMidpoint`
locally through `tools/unity-run.sh`.

## Result

The previously red regression passed 1/1 in 0.031 seconds. Evidence is in
`verification-hightown-boundary-fixed-results.xml` and
`verification-hightown-boundary-fixed-unity.log`.

## What was learned

The hypothesis is confirmed at the catalogue boundary: Hightown no longer emits explicit
placements onto Kentridge's side of the country midpoint.

## Next

Run the production catalogue-composition probe at the exact obstructing torso voxel, then validate
the runtime opening line of sight and exact captured view.
