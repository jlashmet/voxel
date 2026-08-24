# Experiment 032 — live retained-profile ownership counts

**Hypothesis** — The unchanged replay occurred because the pure ownership predicate omitted no
triangles in the live ArchLookdev chunks.

**What was performed** — Temporarily counted continuous triangles tested and omitted during
production append, rebuilt the player, and ran the exact camera for 15 seconds.

**Result** — The hypothesis was disproven. The two upper arch chunks omitted 930/2843 and 885/2805
continuous triangles. The lower chunks tested 6778 and 6756 but omitted zero, consistent with the
profile annulus living above the spring. Evidence is `verification-profile-ownership-counts.txt`
and `verification-profile-ownership-counts-build.txt`.

**What was learned** — The production filter is active and removes the broad profile interior.
The surviving staircase is the conservative boundary strip intentionally retained by the
all-three-vertices and joint-inset rule. The profile-only replay already proved those binary joint
strips are unnecessary for preserving the visible radial joints: the retained wedge side faces do
that presentation work.

**Next** — Make each profile own matching topology through its full authored angular wedge and
assign boundary-crossing triangles by centroid. Keep material, annular, and depth constraints.
Remove temporary counters after the diagnostic.
