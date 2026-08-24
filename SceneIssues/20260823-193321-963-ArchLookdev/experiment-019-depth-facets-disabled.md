# Experiment 019 — depth facets disabled

**Hypothesis** — The staircase is the structural arc wedge's faceted depth-axis cap overlapping
the retained intrados.

**What was performed** — In the structural-arch-only visual reproduction, temporarily disabled
all axis-2 faceted mask output in both exact-snapshot and density-fed faceted jobs. Retained
profiles and continuous Transvoxel radial topology remained enabled. Rebuilt through
`tools/unity-run.sh` and ran the exact 1637x1140 camera for 25 seconds on the working tree based at
`7e5b34d95`. Evidence is `verification-no-depth-facets-pose.png`,
`verification-no-depth-facets-marked-region.png`, and `verification-no-depth-facets-build.txt`.

**Result** — The hypothesis was disproven. Despite removal of every structural front/rear faceted
plane, the same upper-left/crown staircase remains.

**What was learned** — The surviving binary owner is the continuous Transvoxel radial surface of
the arc wedges, not their faceted depth cap. The earlier crossing test measured radial accuracy but
did not account for the oblique view exposing the coarse continuous surface behind an overlaid
retained soffit.

**Next** — Restore ordinary faceted extraction and bay authoring. Render the structural retained
profiles with both binary topology streams skipped to prove they form a complete smooth
replacement, then design narrowly scoped suppression of only profile-covered binary radial
triangles.
