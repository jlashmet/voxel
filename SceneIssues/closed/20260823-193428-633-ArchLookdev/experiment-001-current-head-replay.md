# Experiment 001 — current-head exact-pose replay

**Hypothesis** — The seven marked wall slivers still expose the background on current `fixes` and
are stable production geometry rather than transient streaming gaps.

**What was performed** — Built the ordinary production `ArchLookdev` macOS player through
`tools/unity-run.sh` from source `e420fb0e24c58e7fadbc5d27d38552631b4cc92a`, then pinned the
saved 1637×1140 `Hero Arch Camera` pose at 34-degree FOV for 25 seconds. Inspected the settled
24-second frame at all seven saved circles. Evidence is `verification-current-build.txt`,
`verification-current-player-log.txt`, and `verification-current-pose.png`.

**Result** — Confirmed. Four pale background apertures remain along the top masonry course and
three remain down the right shoulder/outer edge. They persist in both settled captures and are
visually distinct from the intentionally dark recessed mortar joints. The player exited normally.

**What was learned** — This is stable published geometry, not convergence or delayed chunk
streaming. The marked holes occur at masonry block boundaries seen obliquely, focusing diagnosis
on side/cap face coverage and cross-path ownership rather than occupancy arrival.

**Next** — Trace the intact ArchBay primitives and emitted faceted/continuous/profile geometry at
the top and right wall boundaries, then build the smallest direct coverage assertion that separates
valid recessed joints from background-visible holes.
