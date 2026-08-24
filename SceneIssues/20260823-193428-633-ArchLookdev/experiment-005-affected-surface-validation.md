# Experiment 005 — affected surface validation

**Hypothesis** — Restoring planar faces based on occupied-cell ownership closes the veneer/backing
gap without regressing authored boundaries, continuous/faceted division, chunk edges, retained
profiles, or the GPU extraction oracle.

**What was performed** — Ran eight affected EditMode fixtures through `tools/unity-run.sh` on the
working tree based at `e420fb0e24c58e7fadbc5d27d38552631b4cc92a`:
`FacetedBoundaryOwnershipTests`, `GpuSurfaceExtractorOracleTests`,
`TransvoxelChunkBoundaryOwnershipTests`, `VoxelSurfaceArchitectureTests`,
`GeometryPipelineArchitectureTests`, `ArchProfileStitchTests`, `ArchCrossingStabilityTests`, and
`ArchCapLayerDiagnosticTests`. Evidence is `verification-affected-editmode.{txt,xml}`.

**Result** — Confirmed: 104/104 tests passed (0 failed, 0 skipped). The focused regression and all
adjacent geometry, architecture, and prior arch ownership checks are green.

**What was learned** — The corrected faceted rule is compatible with the existing authored
boundary contract and retained-profile fix. It does not change GPU eligibility; the arch's planar
masonry remains on its intended CPU extraction path while GPU oracle behavior stays valid.

**Next** — Rebuild the final production player from the clean implementation, replay all seven
circles once more, remove the temporary camera fixture, and review the final diff.
