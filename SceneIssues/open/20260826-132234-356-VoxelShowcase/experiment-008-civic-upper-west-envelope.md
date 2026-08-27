# Experiment 008 — civic/upper west-envelope overlap

## Hypothesis
The upper marked grass rectangle is the plan-view ownership step where `civic-summit`'s south shoulder overlaps `upper-shoulder`: civic's west expanded edge is 848 dm while upper's is 828 dm, leaving an abrupt 20 dm Dirt/Moss boundary change across the 72 dm civic south-shoulder band.

## Evidence
The failed final-1 artifact for source `f981771949542628daf1c35b32929d4f0d512b9d` is a valid real-player replay despite the workflow timing out after five minutes. Its focused PlayMode test passed, telemetry stayed stable (`missingMax=0`), and direct inspection shows the lower circle clean while the upper circle still contains a hard rectangular grass tongue. Camera-ray localization places the upper mark around world z≈29 m, inside the civic south-shoulder / upper west-edge overlap; the lower mark lies just beyond that overlap. Source geometry independently gives the same discriminator: upper expanded west edge = `900-72=828` dm, civic expanded west edge = `920-72=848` dm, a 20 dm step.

## Competing hypotheses
- Upper west local terrain sampling: falsified as sufficient because its regression passed in the same artifact while the rectangle remained.
- Streaming/residency: falsified by stable capture telemetry.
- Market→upper join: lower mark is already clean after that taper and its seam is south of this localized band.
- Moving either district terrace: rejected as unnecessary blast radius; the defect is surface ownership at an overlap, not the authored core locations.

## Change / expected result
At precedence 16, repaint only `upper-shoulder`'s west 72×72 dm overlap to Moss, then paint Dirt back in 2 dm z bands, tapering the west inset from 20 dm at the civic side to 0 at the upper side. This preserves occupancy and both district footprints while replacing the single rectangle with a sub-voxel-scale visual progression at the configured scale.

## Regression / cost
A PlayMode test evaluates the emitted correction behavior at every band: Dirt at the taper edge, Moss one column outside, monotonic west-inset changes no larger than 1 dm, endpoint ownership 20→0 dm, and `MaxPrimitives=40`. The correction emits 39 primitives total and has no per-frame cost.

## Falsifier
If the exact saved-camera replay still shows a rectangular intrusion in the upper circle, this overlap hypothesis is false/incomplete and the issue must stay open.
