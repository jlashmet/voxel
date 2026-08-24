# Experiment 010 — current-head exact saved-camera replay

**Hypothesis** — The merged `fixes` head removes the blue/grey spiral-like material bands from
the original saved view while preserving the intended coarse terrain geometry.

**What was performed** — Built and ran the production `VoxelShowcase` macOS player locally through
`tools/unity-run.sh` at source `315dec0805e45c5bef20a96fb5c921f228563060`. An untracked replay
fixture pinned `Showcase Camera` to the captured position, rotation, and 70-degree field of view;
the player ran at the original 1293×718 resolution for 40 seconds. The final settled frame is
`verification-lod2-material-fix.png`; run facts are in
`verification-current-head-exact-replay.txt`.

**Result** — Passed. The final frame was captured after visible terrain reached a stable 425
sections with zero missing sections. Compared with `screenshot-001.png`, the broad blue/grey
buried-material bands dominating the near field are gone. Remaining green contouring follows the
coarse voxel geometry rather than alternating to the incorrect buried material.

**What was learned** — The exposed-cap material correction fixes the reported blueish spiral
appearance at the exact saved pose. The phase correction and coarse geometry remain intact.

**Next** — Confirm the graphics-dependent CPU/GPU oracle set on the same merged head, then resolve
the issue in a separate bookkeeping commit.
