# Experiment 006 — final clean exact-pose replay

**Hypothesis** — The occupied-cell faceted ownership fix remains visually correct after affected
validation, with no diagnostic wiring and no loss of intentional masonry depth.

**What was performed** — Rebuilt the ordinary production `ArchLookdev` macOS player through
`tools/unity-run.sh` on the final working tree based at
`e420fb0e24c58e7fadbc5d27d38552631b4cc92a`. Ran the saved 1637×1140 camera at 34-degree FOV
for 25 seconds, inspected every marked region in the settled 24-second frame, and removed the
temporary camera resource afterward. Evidence is `verification-final-build.txt`,
`verification-final-player-log.txt`, and `verification-final-pose.png`.

**Result** — Confirmed. The build succeeded and player exited normally. None of the seven circles
contains pale background through the wall. Top-course and right-edge joints retain dark recessed
backing, all other intentional joints remain readable, and the arch opening, ivy, profiles, and
structural silhouette are unchanged.

**What was learned** — The causal ownership fix is stable in the final production path and closes
the captured holes without adding occupancy, overlapping presentation geometry, or altering
half-cell boundary rules. The issue is ready for final diff review and the required two commits.

**Next** — Review the diff, commit/push production/test/evidence, then resolve `issue.json` with
the resulting fix SHA in a separate commit.
