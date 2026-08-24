# Experiment 007 — rear-guard focused validation green

**Hypothesis** — The 0.125-voxel rear-only taper covers the full measured binary crossing error
while preserving the retained-profile structural contracts.

**What was performed** — Added the missing test namespace import and reran
`ArchProfileStitchTests` plus `ArchCrossingStabilityTests` through `tools/unity-run.sh` on the
working tree based at `7e5b34d95`.

**Result** — The hypothesis was confirmed. Six tests executed and all six passed with zero failures
in 0.288 seconds; the wrapper exited 0 after 12 seconds. The measured worst crossing remains 0.111
voxel, below the 0.125-voxel rear silhouette guard.

**What was learned** — The guard is numerically sufficient and remains localized to the far edge;
front `InnerRadiusQ4`, full-depth continuity, opening ownership, and profile topology stay covered.

**Next** — Rebuild the production player and replay the exact marked view for fix attempt 2.
