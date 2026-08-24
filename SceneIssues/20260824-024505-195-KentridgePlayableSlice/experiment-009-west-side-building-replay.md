# Experiment 009 — west-side building replay

## Hypothesis

A west-side camera at the magic-shop elevation would show whether the corrected shell remains
attached to the ground and integrated with its neighboring court instead of floating above it.

## What was performed

Against source `36cec6893239e000c9aa875ebe9320a99927d0f4`, rebuilt the production
`KentridgePlayableSlice` player with the temporary replay fixture at `(92,31,60)`, facing east and
slightly downward with 58-degree FOV. Ran for 55 seconds and captured five presented frames after
world generation.

## Result

The harness completed with zero assertion failures. The settled frame
`verification-west-side-building.png` shows the magic-shop shell at the left foreground continuing
down into its stone base beside the grass court, with the surrounding market structures, paths,
retaining edges, and buildings also supported. The enormous detached upper shell from the original
capture is absent. Build and runtime details are in `verification-west-build.txt` and
`verification-west-player-log.txt`.

## What was learned

The hypothesis is confirmed. Together with the exact saved-pose replay and ordered-primitive ray,
the exterior view establishes that the current object is a grounded authored building. The prior
Hightown catalogue contamination—not GPU meshing or half-voxel authoring—caused its captured
floating fragmentation.

## Next

Remove all temporary diagnostics and replay resources, then run the permanent cross-settlement
boundary regression on the clean source tree.
