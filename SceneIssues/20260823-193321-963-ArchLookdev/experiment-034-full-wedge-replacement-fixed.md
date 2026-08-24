# Experiment 034 — full-wedge retained-profile replacement fixed

**Hypothesis** — Assigning matching topology by triangle centroid through each raw profile wedge
removes the conservative binary strips, while the retained profile's inset radial sides preserve
the intentional voussoir joints.

**What was performed** — Changed the generic ownership predicate from all-vertices inside the
joint-inset span to centroid inside the raw authored wedge. Kept material, annular, and depth
constraints; faceted topology was untouched. The full focused profile fixture passed 5/5. Rebuilt
the production player and ran the exact 1637x1140 camera for 25 seconds on the working tree based
at `7e5b34d95`, with temporary omission counters still enabled.

**Result** — The reported staircase is absent throughout the upper-left and crown marked regions.
The smooth intrados is continuous, intentional radial voussoir joints remain, and unrelated bay,
pier, veneer, and vegetation geometry remains present. The upper chunks omitted 1537/2843 and
1460/2805 continuous triangles; the lower boundary chunks omitted only 21/6778 and 19/6756.
Evidence is `verification-full-wedge-ownership-green.txt`,
`verification-full-wedge-ownership-green.xml`, `verification-full-wedge-build.txt`,
`verification-full-wedge-fixed-pose.png`, `verification-full-wedge-fixed-marked-region.png`, and
`verification-full-wedge-ownership-counts.txt`.

**What was learned** — The root cause was duplicate presentation ownership, not insufficient curve
samples, a CPU/GPU math difference, or a silhouette offset. The retained primitive already carried
the intended smooth geometry and joints; the binary continuous topology beneath its complete
wedge had to be replaced according to the one-surface/one-primitive authoring rule.

**Next** — Remove temporary counters/logging, run affected profile, architecture, extraction, and
authoring tests, then rebuild and replay a clean final exact camera before review and commit.
