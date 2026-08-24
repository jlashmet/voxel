# Experiment 026 — explicit retained-profile backing depth red

**Hypothesis** — Retained profile authoring must carry the last occupied depth sample separately
from the projected rear geometry endpoint, so presentation can extend beyond the cell face without
validating against empty space.

**What was performed** — Added a documented `ProfileBlock.BackingDepthVoxel` contract and extended
the existing profile stitch test to require `Depth-1` independently from the geometry endpoint
`Depth*16+projectionQ4`. No producer or consumer behavior was changed. Ran the single EditMode test
through `tools/unity-run.sh` on the working tree based at `7e5b34d95`.

**Result** — The test executed 1 case and failed: expected backing depth 11, actual 0. Evidence is
`verification-profile-backing-depth-red.txt` and `verification-profile-backing-depth-red.xml`.

**What was learned** — The missing datum is reproduced directly. The old representation cannot
distinguish an occupied backing voxel from projected profile geometry, which caused experiment 025
to reject otherwise valid profile segments.

**Next** — Populate `BackingDepthVoxel` from the arch's last occupied depth sample and make profile
validation use it. Rerun the focused contract and exact camera.
