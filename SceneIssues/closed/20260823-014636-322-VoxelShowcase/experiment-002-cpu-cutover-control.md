# Experiment 002 — CPU cutover control

**Hypothesis** — The transient brown patches are emitted specifically by the near-ring GPU
extraction path.

**What was performed** — Re-ran the exact 1293×718 production-player replay from source
`87bfc27d7` with `VOXEL_DISABLE_GPU_CUTOVER=1`, keeping the saved pose, field of view, duration,
and one-second screenshot cadence unchanged. Evidence is in `verification-cpu-transient.png`,
`verification-cpu-settled.png`, and this experiment's metrics below.

**Result** — The hypothesis was disproven. With all rings on the CPU, the same large brown regions
remain at 18.7 seconds while 434 surfaces are drawn and 24 are missing. They disappear after the
view reaches 458 drawn and zero missing surfaces. CPU extraction converged faster than GPU in this
run, but the artifact's shape and handoff remained.

**What was learned** — GPU vertex/material generation is not required for the defect. The brown
pixels are exposed wherever current near coverage is incomplete. Source inspection then found that
the fallback and authoritative ground-cover bindings disagree: `GameShowcaseMaterials.Default`
still assigns dirt below the height split, while `GameTerrainMaterials.Default` was changed to
continuous grass in `f5809b4bf`. The existing cross-binding regression still expects the obsolete
dirt value and should fail against this state.

**Next** — Run the narrow cross-binding material test as a red regression, then align the far
fallback's low-surface role with the authoritative terrain binding and rerun it green.

## Metrics

- `t=17.7`: drawn 313, missing 145
- `t=18.7`: drawn 434, missing 24, broad brown patches reproduced
- `t=23.7`: drawn 458, missing 0
- `t=24.7`: drawn 458, missing 0, broad brown patches absent
- transient SHA-256: `7ca5639676f0fda683030f20d8f02bd845a1309446c5e1cb62b850887f3995f5`
- settled SHA-256: `93af6628a5659dd4d0d3075cd8c566ae9a19adb358adbf1a4c5ed74dfe6fba2e`
