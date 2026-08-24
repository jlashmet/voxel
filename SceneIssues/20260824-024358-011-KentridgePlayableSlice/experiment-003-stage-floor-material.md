# Experiment 003 — stage floor material

## Hypothesis

The opening stage points have empty actor volumes but were resolved over the moss-covered plot pad
instead of the generated pub floor.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a`, strengthened the uncommitted
`KentridgePubExitPlayTests` regression to require foundation-stone material 1 directly beneath all
four occupied opening-stage points. The same production catalogue/world test was run locally through
`tools/unity-run.sh`.

## Result

Every new empty-volume and foundation-stone-floor assertion passed. The unrelated existing
doorway-walk assertion again failed afterward, leaving the overall test at 0/1. Evidence is in
`verification-stage-floor-material-results.xml` and
`verification-stage-floor-material-unity.log`.

## What was learned

The hypothesis is disproven. Stage resolution, generated pub-floor placement, and authoritative
actor clearance agree at the tested points. The apparent burial in the saved view must be downstream
presentation: actor visual transforms/pivots, stale or incorrectly clipped derived surface geometry,
or camera cutaway composition.

## Next

Measure the runtime actor root and rendered visual bounds, then compare the visible terrain surface
against authoritative top-solid heights and active cutaway bounds at the exact replay pose.
