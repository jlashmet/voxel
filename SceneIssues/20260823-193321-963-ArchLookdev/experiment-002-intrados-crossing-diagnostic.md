# Experiment 002 — intrados crossing diagnostic

**Hypothesis** — The visible staircase comes from inaccurate radial density crossings on the
front/soffit intrados.

**What was performed** — Ran the existing focused EditMode diagnostic
`VoxelEngine.Tests.EditMode.ArchCrossingStabilityTests.IntradosCrossingsFollowTheAnalyticCircle`
through `tools/unity-run.sh` at source `7e5b34d95`.

**Result** — The hypothesis was disproven. One test executed and passed; 65 measured intrados edge
crossings had mean radial error 0.018 voxel and worst absolute error 0.111 voxel. The guarded Unity
wrapper exited 0 after 9 seconds. The test currently logs these measurements but has no threshold.

**What was learned** — The analytic circle crossing is substantially more accurate than the
roughly one-voxel visual steps. In the replay, the smooth proud face and soffit end at a stepped
rear opening boundary. Source inspection explains why: `EmitProfileBlock` emits the projected front
cap and continuous inner/outer/radial sides, but emits no rear cap at `BackQ4`; the binary rear-face
mesh remains visible there. The earlier full-depth stitch changed `BackQ4` but did not close that
surface.

**Next** — Add a regression requiring each retained profile segment to emit its rear annular cap,
observe it fail on current source, then add the missing back-facing quad and replay.
