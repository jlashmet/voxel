# Experiment 004 — backing ownership visual attempt 1

**Hypothesis** — Restoring exact faceted ownership to the planar backing will close every marked
top/right aperture while retaining the veneer blocks' rounded silhouettes and recessed joints.

**What was performed** — Rebuilt the ordinary production `ArchLookdev` player through
`tools/unity-run.sh` with the three-path ownership change on the working tree based at
`e420fb0e24c58e7fadbc5d27d38552631b4cc92a`. Ran the saved 1637×1140 camera at 34-degree FOV
for 25 seconds and inspected all seven circles in the settled 24-second frame. Evidence is
`verification-attempt1-build.txt`, `verification-attempt1-player-log.txt`, and
`verification-attempt1-pose.png`.

**Result** — Confirmed. All four top-course background apertures and all three right-edge
apertures are closed. The affected seams now show dark recessed backing or stone rather than the
pale sky, while the broader horizontal and vertical masonry joints remain visibly recessed. The
arch opening and block silhouettes are unchanged. The player exited normally.

**What was learned** — Empty-side curved halo metadata stealing the planar backing face was the
dominant full-scene cause. The first genuine visual fix attempt solved the issue; no authoring
expansion, duplicate solid, or half-cell adjustment is needed.

**Next** — Run the new regression with the affected surface extraction, GPU-oracle, authored
boundary, and prior arch ownership suites. Then perform one clean final exact-pose replay.
