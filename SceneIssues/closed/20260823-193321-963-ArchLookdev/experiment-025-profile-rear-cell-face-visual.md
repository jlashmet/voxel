# Experiment 025 — retained profile rear cell-face visual replay

**Hypothesis** — Extending `BackQ4` from the last occupied sample coordinate to the actual rear
cell face plus the authored projection covers the separate stepped rear opening seen in the exact
crop.

**What was performed** — Changed `ArchFeature` profile authoring from
`(origin.z+Depth-1)*16+projectionQ4` to `(origin.z+Depth)*16+projectionQ4`. The focused endpoint
test passed 1/1 (Unity wrote a valid green XML result, then its Burst compiler bus-faulted during
editor shutdown). Rebuilt the production player successfully and ran the exact 1637x1140 camera
for 25 seconds on the working tree based at `7e5b34d95`.

**Result** — The hypothesis was not valid as a standalone change. The retained voussoir profile
disappeared, exposing a stronger binary staircase across the opening. Evidence is
`verification-profile-rear-face-green.txt`, `verification-profile-rear-face-green.xml`,
`verification-profile-rear-face-build.txt`, `verification-profile-rear-face-pose.png`, and
`verification-profile-rear-face-marked-region.png`.

**What was learned** — `BackQ4` currently has two incompatible responsibilities. It is the
rendered rear endpoint and is also rounded to a voxel coordinate by `TryReadProfileBacking`.
Moving it into projected empty space makes backing validation sample outside the structure, so
every retained segment is skipped. The rendering endpoint cannot be corrected until backing
validation receives an explicitly occupied coordinate.

**Next** — Add an explicit retained-profile backing-depth contract (the last occupied depth
sample), keep geometry endpoints on cell faces plus projection, and prove both values independently
before another visual replay.
