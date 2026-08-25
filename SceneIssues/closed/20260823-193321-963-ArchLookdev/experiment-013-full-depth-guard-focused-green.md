# Experiment 013 — full-depth intrados coverage green

**Hypothesis** — Starting the smooth retained side with the smallest Q4-aligned 0.125-voxel near
guard, then interpolating to the measured 0.25-voxel rear guard, covers every backing layer while
leaving the exact front face unchanged.

**What was performed** — On the working tree based at `7e5b34d95`, introduced separate near and
rear silhouette bounds. The retained intrados now reaches the 0.125 near guard at its existing
front bevel shoulder and grows to 0.25 at the rear; the front annular face coordinates are
unchanged. Updated the isolated depth regression and exact-bay/profile contracts, then ran
`CsgFieldCoherenceTests`, `ArchProfileStitchTests`, and `ArchCrossingStabilityTests` through
`tools/unity-run.sh` in EditMode.

**Result** — All 8 tests passed in 0.242 seconds; the wrapper exited 0 after 12 seconds. In the
bare-bones fixture, `z=2` now has 0.138 voxel cover against 0.075 inward error, and every layer
through `z=12` has a nonpositive deficit. The exact bay remains bounded at 0.111 mid-depth and
0.200 rear. Evidence is `verification-full-depth-guard-focused.txt` and
`verification-full-depth-guard-focused.xml`.

**What was learned** — The isolated cause is corrected without changing the authored front face,
and both the minimum reproduction and composed-bay numerical contracts are green. Visual proof is
still required because the prior endpoint-only fixes passed narrower numeric tests but failed the
capture.

**Next** — Rebuild the production player and replay the exact 1637x1140 camera, inspecting all
four marked regions before accepting the fix.
