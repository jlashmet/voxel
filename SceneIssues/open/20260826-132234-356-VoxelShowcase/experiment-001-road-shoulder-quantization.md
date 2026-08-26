# Experiment 001 — road shoulder quantization

## Hypothesis
The jagged dirt/grass contact comes from Kentridge road shoulder authoring rather than authoritative terrain data: the road surface program begins the grassy shoulder with a coarse vertical step and then repeats that stair profile outward.

## What was performed
Inspected the captured issue metadata and the Kentridge town/voxel surface authoring at source commit `36c61bf04c0a060a509e2575ff2d59b6d2d5dad1`. Correlated the recorded camera position with the authored Kentridge main spine and inspected `KentridgeTownSurfaceCatalogue.RoadProgram` plus its existing shoulder regression. No Unity process or new capture was started.

## Result
The camera is near the main-spine road corridor. The surface program uses five 6 dm grass bands per side and raises each successive band by 4 dm; therefore the first grass strip begins 4 dm above the Dirt carriageway and the full 3 m shoulder is represented by only five cross-slope levels. The existing regression only requires five bands per side and does not constrain transition granularity.

## What was learned
**Hypothesis confirmed at the authoring level.** The Kentridge road catalogue deterministically creates a coarse stair profile exactly at the Dirt/Moss boundary. The fix can remain local to surface authoring and does not require changing terrain authority or generic rendering.

## Next
Preserve the existing 3 m shoulder width and 2 m total rise, but distribute them across 30 one-decimetre bands with interpolated integer heights so the Dirt-to-grass contact starts flush and subsequent rises are at most one decimetre at the regression scale. Extend the focused regression to fail on the previous five-band behavior.