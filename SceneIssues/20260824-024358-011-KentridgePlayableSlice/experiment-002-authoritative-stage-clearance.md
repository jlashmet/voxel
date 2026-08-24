# Experiment 002 — authoritative stage clearance

## Hypothesis

The grass surface visible across the opening cast is authoritative solid terrain occupying the
resolved actor volumes.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a`, with an uncommitted focused
regression in `KentridgePubExitPlayTests`, generated the production Kentridge catalogue and asserted
that the four occupied opening stage points each have a 6×18×6-voxel empty actor volume plus solid
support below. Unity was run locally through `tools/unity-run.sh`.

## Result

All four new clearance/support assertions passed. The existing doorway-walk assertion later failed,
so the overall test result was 0/1 rather than green. Evidence is in
`verification-authoritative-clearance-results.xml` and
`verification-authoritative-clearance-unity.log`.

## What was learned

The hypothesis is disproven: authoritative solid terrain does not occupy the actor capsules at the
resolved stage points. The support assertion was deliberately material-agnostic, however, so this
run did not prove that the points stand on the pub foundation rather than another valid solid
surface.

## Next

Classify the support material under each stage point. If it is plot moss rather than foundation
stone, the defect is stage/building alignment; if it is foundation stone, inspect actor visual
pivots and rendered-surface/cutaway state.
