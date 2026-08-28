# Experiment 012 — geometric civic south-west shoulder

## Discriminator
The prior exact targeted run (`f46b971f8901f9e0dab801017ae5372d85fe2456`, workflow `33102166380`) passed its material-ownership regression, yet its fresh real-player replay still shows the upper marked region as a hard rectangular green shelf while the lower mark is clean. Therefore the surviving defect is not fixed by repainting the 20 dm plan mismatch Dirt.

The saved upper ray reaches the visible surface around x=85–92 m, z≈29.5 m, inside the civic summit south-west shoulder. Production samples the civic south edge once at its centreline, whereas the neighbouring upper west edge is locally sampled. This predicts a broad flat civic corner even when the surface material is already Dirt.

## Change
Remove the redundant upper-patch Dirt repaint. At correction precedence 16, rebuild only civic-summit's western 72 dm of its 72 dm south shoulder as six 12 dm-wide ramps. Each ramp samples `TerrainQuery.HeightAt` at its own outer-edge midpoint and joins that height to the unchanged civic core.

## Regression / blast radius
`SceneIssue20260826132234356CivicSouthWestShoulderFollowsLocalTerrainProfile` parses the production correction program and requires all six outer ramp elevations to equal the corresponding terrain samples, verifies the obsolete upper repaint is absent, preserves civic core paving, and caps this patch at 16 primitives. The change is generation-time only and scoped to a 7.2 m × 7.2 m civic corner.

## Result
Pending exact-SHA targeted CI and fresh saved-camera replay. The issue remains open until both marked regions pass direct visual inspection.
