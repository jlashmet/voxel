# Experiment 001 — current-head two-pose replay

**Hypothesis** — The terrain-like mound still appears inside the first marked circle and disappears
after the saved 9.6-metre movement to the second pose.

**What was performed** — Built and ran the production `VoxelShowcase` macOS player twice through
`tools/unity-run.sh` at source `4813b91dd`. A temporary untracked camera fixture pinned each saved
pose independently at the original 1293×718 resolution and 70-degree field of view. Each run lasted
50 seconds; the selected frames were captured after visible surface convergence. Evidence is in
`verification-current-pose1.png`, `verification-current-pose2.png`, their marked crops, and
`verification-current-two-pose-replay.txt`.

**Result** — The hypothesis was disproven. At pose 1, the triangular terrain-like silhouette inside
the original circle is absent and the castle frontage has a rectangular structural silhouette. At
pose 2, the same structure remains consistent. Pose 1 settled at 514 visible surfaces and pose 2 at
557, both with zero missing surfaces.

**What was learned** — The reported transient mound was already repaired by later rendering work on
the shared branch. It was not an initial-streaming artifact, because both independent runs were
stable before the selected frames.

**Next** — Move the LOD handoffs without changing world state to determine whether the clean result
depends on an accidental finer ring, then identify the source step covering the marked content.
