# Experiment 001 — current-head exact replay

**Hypothesis** — The diagonal terrain sheet and hidden waterfall still reproduce on current
`fixes`, requiring a new authoring or meshing change.

**What was performed** — Built and ran the production `VoxelShowcase` macOS player through
`tools/unity-run.sh` at source `be2315394e5f000a4093c0c61f71c10b2d1b7630`. An untracked replay
fixture pinned `Showcase Camera` to the captured pose and 70-degree field of view; the player ran
for 50 seconds at the original 1293×718 resolution. The settled frame is
`verification-current-head-replay.png`; run facts are in `verification-current-head-replay.txt`.

**Result** — The hypothesis was disproven. The invalid diagonal sheet is absent from all four
marked regions. A narrow blue cascade is visible descending from the upper stream behind the
central tower into the ravine. The final frame was captured after the visible surface stabilized
at 742 sections with zero missing sections.

**What was learned** — This capture was already repaired by work later applied to the shared
branch. The original sheet matches the class of cross-chunk GPU regular-cell geometry corrected by
commit `9275602c3610079a2966cd022b1a3f2fb13d8b62`; once the invalid sheet no longer occludes the
ravine, the authored waterfall is visible and its voxel-state invariants remain covered by
`CastleAccessTests.CastleLandscapeContainsConnectedWaterLevelsAndSupportedBridge`.

**Next** — Run the focused GPU boundary-ownership and castle waterfall-state regressions on the
current head. If both pass, no new production edit is warranted; resolve this capture against the
existing causal fix and current-head replay evidence.
