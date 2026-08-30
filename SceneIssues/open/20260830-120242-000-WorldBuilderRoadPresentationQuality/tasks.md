# Tasks

- [ ] Trace the existing road profile/corridor, road-influence, SmoothSurface material/reconstruction, vegetation, junction, streaming/LOD, and persistence paths; record the demonstrated presentation root cause and affected consumers.
- [ ] Implement the narrowest reusable semantic/config-driven road-presentation extension for coherent curve/edge geometry and bounded terrain-aware cross-section/shoulders/cut-fill without changing route authority.
- [ ] Add deterministic shared terrain-surface wear/variation and topology-aware junction presentation through existing production material/surface contracts; keep non-road consumers unchanged.
- [ ] Prove reuse with an independent non-Kentridge road/trail/profile consumer or fixture where practical.
- [ ] Add focused production-path regressions covering deterministic semantics, representative profiles/sides/junctions, material wear, exposed-top/slope behavior, vegetation, chunk/LOD continuity, persistence/vertex budgets, and non-road behavior.
- [ ] Measure relevant world-build/voxel/vertex/material/primitive/streaming cost against existing budgets; do not weaken budgets.
- [ ] Validate exact built `KentridgePlayableSlice` player evidence: curved/diagonal approach, both uneven/sloped shoulders, real junction/approach, non-flat cross-section, medium/far continuity, vegetation recovery, and CharacterMotor traversal; classify visual quality at the AAA bar and fix demonstrated defects before proceeding.
- [ ] Review final diff and complete required pending metadata (`resolutionSummary`, `regressionTest`, `fixCommit`); move only this assignment from open to pending.
- [ ] Request targeted CI for the exact feature SHA using only `ci-test/fixes/agent-3`; leave queued/running CI untouched and resolve any completed failure before retrying.
- [ ] After green exact-SHA CI, finalize status/resolution metadata, move pending to closed, merge current `origin/master`, revalidate affected work if needed, and push the exact feature head to `origin/master` non-force.
