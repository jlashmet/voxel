# Experiment 005 — rear-cap visual replay (fix attempt 1)

**Hypothesis** — The newly emitted rear annular cap hides the staircase in the four marked regions.

**What was performed** — Rebuilt the production `ArchLookdev` player through
`tools/unity-run.sh` with the rear-cap change, then repeated the exact 1637×1140 saved-camera replay
for 20 seconds. Evidence is in `verification-fixed-pose-attempt1.png` and
`verification-fixed-marked-region-attempt1.png`.

**Result** — The hypothesis was disproven. The stable 17-second crop is visually unchanged: the
same stepped rear outline remains from the upper-left intrados through the crown.

**What was learned** — The missing cap is valid closed topology, but its positive-depth normal
faces away from this front-oblique camera and cannot cover the artifact. The visible protrusions are
the binary inner surface at the far soffit edge. The crossing diagnostic bounds their inward error
at 0.111 voxel, so a two-Q4 (0.125 voxel) rear-only silhouette guard is the smallest representable
offset that can cover them without moving the exact front radius.

**Next** — Taper the retained intrados from its exact front radius to a 0.125-voxel inward rear
radius, assert the crossing error fits inside that guard, and repeat the exact replay.
