# Experiment 008 — southwest oblique overview

## Hypothesis

A high, distant southwest camera aimed at the magic-shop bounds would avoid foreground terrain and
show whether the prior catalogue-ownership fix left any large floating structure in the district.

## What was performed

Against source `36cec6893239e000c9aa875ebe9320a99927d0f4`, rebuilt the production
`KentridgePlayableSlice` player with the temporary replay fixture at `(90,42,40)`, aimed northeast
and downward at the magic-shop area with 58-degree FOV. Ran the player for 55 seconds and captured
five presented frames after world generation.

## Result

The harness completed with zero assertion failures. The settled frame
`verification-southwest-oblique-overview.png` shows the authored district as grounded buildings,
roads, courts, and walls; the enormous floating silhouette is absent. The magic shop is partly
cropped at the lower-right edge. Build and runtime details are in `verification-oblique-build.txt`
and `verification-oblique-player-log.txt`.

## What was learned

The hypothesis is confirmed at district scale: current occupancy no longer produces the reported
floating shell. The view is weaker than desired for judging the specific magic-shop facade because
that building is only partially framed.

## Next

Capture a closer west-side view centered directly on the known magic-shop shell to prove that the
specific building is continuous and supported.
