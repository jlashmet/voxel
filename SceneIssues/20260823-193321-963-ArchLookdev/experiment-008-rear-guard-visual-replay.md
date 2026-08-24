# Experiment 008 — rear-guard visual replay (fix attempt 2)

**Hypothesis** — A 0.125-voxel rear-only intrados taper covers the measured crossing error and
removes the marked staircase.

**What was performed** — Rebuilt the production `ArchLookdev` player through
`tools/unity-run.sh` with the rear taper, then repeated the exact 1637×1140 saved-camera replay for
20 seconds. Evidence is in `verification-fixed-pose-attempt2.png` and
`verification-fixed-marked-region-attempt2.png`.

**Result** — The hypothesis was disproven. The far soffit edge moves inward slightly, but the
regular staircase remains plainly visible through the same upper-left and crown marked regions.

**What was learned** — The 0.111-voxel measurement came from a 32-span fixture sampled at
mid-depth, not the capture's 28-span bay composition at its rear layer. It is insufficient evidence
for the actual protrusion bound.

**Next** — Measure the exact capture configuration at both mid-depth and the rear voxel layer,
including the composed bay, before choosing a third and final full-scene fix attempt.
