# Experiment 009 — exact captured-bay rear crossings

**Hypothesis** — The earlier 0.111-voxel bound understated the staircase because it measured a
32-span standalone fixture at mid-depth rather than the captured 28-span composed bay at its rear
structural layer.

**What was performed** — On the working tree based at `7e5b34d95`, changed
`ArchCrossingStabilityTests.IntradosCrossingsFollowTheAnalyticCircle` to rasterise the exact
`ArchLookdev` hero settings (`ClearSpan=28`, `PierHeight=64`, `Depth=12`, 13 voussoirs, shoulder
4, top margin 4, face recess 1) and measure in-plane intrados crossings at mid-depth `z=7` and the
rear structural layer `z=12`. Ran the one EditMode test through `tools/unity-run.sh`.

**Result** — The test executed 1 case and failed the 0.125-voxel guard assertion. Mid-depth had 57
crossings, mean error 0.007 voxel, and worst absolute error 0.111 voxel. The rear layer had 57
crossings, mean error -0.014 voxel, and worst inward error 0.200 voxel; the crown samples at 85.9
and 94.1 degrees were each 0.164 voxel inward. Evidence is
`verification-exact-bay-crossings.txt` and `verification-exact-bay-crossings.xml`.

**What was learned** — The hypothesis was confirmed. The composed rear layer protrudes beyond the
attempt-2 retained profile guard in the exact marked crown region. The smallest Q4-representable
guard above the measured 0.200-voxel bound is 0.25 voxel.

**Next** — Use a 0.25-voxel rear-only guard as fix attempt 3, rerun the focused contracts, then
rebuild and replay the exact camera before accepting or rejecting it.
