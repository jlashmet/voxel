# Experiment 005 — local GPU density and vertex oracles

**Hypothesis** — The GPU implementation matches the CPU density field, boundary ownership, and rendered vertex attributes for the SceneIssue 014011 coarse terrain fixture.

**What was performed** — At source commit `ccfff86b393bc4627f2e04e6c5b61639d3e4d690` plus the working-tree deduplication, ran the mixed-field GPU oracle, `TransvoxelChunkBoundaryOwnershipTests`, `GpuLod2CutoverPolicyTests`, and `SceneIssue014011GpuVertexAttributeParityTests` locally through `tools/unity-run.sh`. Evidence is in `verification-local-oracles-stale-assertion.xml` and `verification-local-oracles-stale-assertion.txt`.

**Result** — 11/12 tests passed. GPU mixed-field density/material/surface/boundary parity passed at source steps 1 and 2; vertex geometry/material/normal parity passed at both steps; all five boundary-ownership tests passed. The only failure was `GpuLod2PortsCoarseExposedMaterialCorrection`, whose source-text assertion expected the obsolete single line `if (centreSolid && sourceStep > 1)`. The current shader has equivalent nested `if (sourceStep > 1)` then `if (centreSolid)` control flow introduced by the later phase correction.

**What was learned** — The GPU parity hypothesis is confirmed by behavioral oracles. The failure is stale test syntax, not a production mismatch.

**Next** — Update the source guard to assert the current nested structure, then rerun the identical 12-test selection and require all green.
