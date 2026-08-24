# Experiment 007 — south exterior pose

## Hypothesis

A camera placed south of the magic-shop frontage at `(105,27,48)` would show the corrected shell
as a complete, grounded building from outside.

## What was performed

Against source `36cec6893239e000c9aa875ebe9320a99927d0f4`, built the production
`KentridgePlayableSlice` player with the temporary replay fixture at that pose, 58-degree FOV, and
a 14-degree upward pitch. Ran it for 55 seconds through the standalone capture harness without
automatic dialogue advancement.

## Result

The player completed with zero harness assertion failures, but every stable frame was occluded by
a close tan voxel surface. The final frame was
`Artifacts/SceneIssue024505Exterior/Screenshots/showcase-004-t052.5s-stationary.png`; build and
player output are retained as `verification-exterior-build.txt` and
`verification-exterior-player-log.txt`.

## What was learned

The hypothesis is inconclusive because the chosen pose is not a usable exterior vantage point.
The image neither proves nor disproves the corrected building's exterior coherence.

## Next

Move to a substantially higher, farther southwest oblique pose aimed at the known magic-shop
bounds so foreground terrain cannot occlude the structure.
