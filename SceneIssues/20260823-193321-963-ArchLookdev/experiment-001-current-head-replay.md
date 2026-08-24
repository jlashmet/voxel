# Experiment 001 — current-head exact-pose replay

**Hypothesis** — The four marked areas still show an unintended voxel staircase along the inner
arch curve on current `fixes`.

**What was performed** — Built the production `ArchLookdev` macOS player through
`tools/unity-run.sh` at source `7e5b34d95`, then pinned the saved `Hero Arch Camera` pose at the
original 1637×1140 resolution and 34-degree field of view for 25 seconds. Evidence is in
`verification-current-pose.png` and `verification-current-marked-region.png`.

**Result** — The hypothesis was confirmed. The stable 23-second frame retains a regular
horizontal/vertical staircase from the upper-left intrados into the crown in all four marked areas.
The radial voussoir joints are separately visible and are not the reported artifact.

**What was learned** — This is stable production geometry, not streaming or a stale capture. The
staircase follows the voxel lattice while the retained front-face ring is smooth, focusing the next
step on the intrados crossing reconstruction rather than voussoir segmentation.

**Next** — Run the existing `ArchCrossingStabilityTests` diagnostic to measure current radial error,
then inspect the retained profile and boundary-sign path responsible for those crossings.
