# Experiment 030 — retained-profile ownership predicate green

**Hypothesis** — Material equality plus complete triangle containment in the profile's padded
annular/depth volume and joint-inset angular span expresses the generic replacement boundary.

**What was performed** — Corrected the compile-only issue from experiment 029 by copying the
`in ProfileBlock` inputs before the local point test, then reran the single ownership fixture
through `tools/unity-run.sh`.

**Result** — The test executed 1 case and passed. A covered matching intrados triangle is owned;
different-material, outside-annulus, and intentional-joint triangles are retained. Evidence is
`verification-profile-ownership-green-final.txt` and
`verification-profile-ownership-green-final.xml`.

**What was learned** — The missing ownership rule can be expressed generically without identifying
arches or renderer backends. Requiring all three vertices to be covered preserves triangles that
cross a profile/joint boundary, avoiding holes at presentation limits.

**Next** — Apply this predicate only to continuous topology append, complete the retained profile's
rear surface and depth contract, then run the exact visual replay.
