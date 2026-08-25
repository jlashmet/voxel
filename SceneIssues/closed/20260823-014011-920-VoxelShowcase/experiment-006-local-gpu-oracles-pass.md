# Experiment 006 — local GPU oracle rerun

**Hypothesis** — Updating the stale source-text guard to the shader's current nested control flow leaves all behavioral SceneIssue 014011 oracles green.

**What was performed** — At source commit `ccfff86b393bc4627f2e04e6c5b61639d3e4d690` plus the working-tree deduplication and test-guard update, reran the same four filtered fixtures through `tools/unity-run.sh`. Evidence is in `verification-local-oracles.xml` and `verification-local-oracles.txt`.

**Result** — Passed 12/12 tests in 0.190 seconds: two mixed-field density/material/surface/boundary cases, five boundary-ownership/cap-material cases, three GPU cutover/source guards, and two GPU vertex geometry/material/normal parity cases.

**What was learned** — Hypothesis confirmed. No CPU/GPU density, topology ownership, material, or normal mismatch remains in the bare-bones step-1/step-2 fixture.

**Next** — Replay the exact saved VoxelShowcase camera locally and inspect the rendered frame against the original issue evidence.
