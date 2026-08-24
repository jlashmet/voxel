# Experiment 010 — quarter-voxel rear guard focused validation

**Hypothesis** — A 0.25-voxel rear-only intrados taper is the smallest Q4-aligned guard that
covers the exact captured bay's measured 0.200-voxel rear protrusion while preserving the front
profile and retained topology contracts.

**What was performed** — On the working tree based at `7e5b34d95`, changed
`ProfileRearSilhouetteGuardVoxels` from 0.125 to 0.25 and ran `ArchProfileStitchTests` plus
`ArchCrossingStabilityTests` through `tools/unity-run.sh` in EditMode.

**Result** — Six tests executed and all six passed with zero failures; the wrapper exited 0 after
11 seconds. The exact-bay diagnostic still measured 0.111 voxel worst at mid-depth and 0.200 voxel
worst at the rear layer, both within the new guard. Evidence is
`verification-quarter-guard-focused.txt` and `verification-quarter-guard-focused.xml`.

**What was learned** — The geometric bound and retained-profile contracts are green for fix
attempt 3. This is necessary but not sufficient: only the exact marked replay can show whether the
rear taper actually hides the staircase.

**Next** — Rebuild the production player and replay the saved 1637x1140 camera, inspecting every
marked upper-left and crown region.
