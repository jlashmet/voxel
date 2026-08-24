# Experiment 027 — separated backing-depth visual replay

**Hypothesis** — Separating the occupied backing sample from the projected geometry endpoint lets
the retained profile extend past the rear structural face, remain emitted, and cover the stepped
rear opening.

**What was performed** — Added `ProfileBlock.BackingDepthVoxel`, authored it as the last occupied
depth sample, and made `TryReadProfileBacking` use it while `BackQ4` extended to the rear cell face
plus projection. The focused contract passed 1/1. Rebuilt the production player and ran the exact
1637x1140 camera for 25 seconds on the working tree based at `7e5b34d95`.

**Result** — The retained voussoir faces returned, proving the separated backing validation works,
but the marked staircase remained. Evidence is `verification-profile-backing-depth-green.txt`,
`verification-profile-backing-depth-green.xml`, `verification-profile-backing-depth-build.txt`,
`verification-profile-backing-depth-pose.png`, and
`verification-profile-backing-depth-marked-region.png`.

**What was learned** — Rear endpoint accounting is not the causal scene fix. The duplicate binary
intrados remains visible even when retained geometry spans beyond the structural rear face. This
reinforces experiment 020 and the authoring contract's one-surface/one-primitive rule: overlap is
the defect, and offsets cannot establish unique presentation ownership.

**Next** — Revert the non-causal endpoint/backing schema. Implement generic covered-topology
ownership for retained profiles, scoped by the profile volume and material so unrelated geometry
and intentional joint beds remain.
