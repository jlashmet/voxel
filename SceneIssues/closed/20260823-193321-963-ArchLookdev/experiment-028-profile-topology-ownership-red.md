# Experiment 028 — retained-profile topology ownership red

**Hypothesis** — A retained profile is the sole presentation owner for matching continuous
topology inside its material, annular, depth, and inset angular span; it must not suppress other
materials, unrelated geometry, or intentional joint beds.

**What was performed** — Added a pure generic ownership fixture around a quarter-annulus profile.
It requires a matching intrados triangle to be owned, while rejecting the same triangle with a
different material, a triangle outside the annulus, and a triangle inside the authored joint gap.
Added a behavior-neutral stub for the missing ownership predicate and ran only this test through
`tools/unity-run.sh` on the clean behavior baseline at `7e5b34d95`.

**Result** — The test executed 1 case and failed on the first covered intrados assertion. Evidence
is `verification-profile-ownership-red.txt` and `verification-profile-ownership-red.xml`.

**What was learned** — The extractor has no representation of the one-surface/one-primitive
ownership rule today. Profile emission only appends geometry after all voxel-derived topology has
already been retained.

**Next** — Implement the generic predicate and use it to omit only covered continuous triangles;
close and extend the retained profile so it is a complete replacement. Keep faceted and unrelated
geometry untouched.
