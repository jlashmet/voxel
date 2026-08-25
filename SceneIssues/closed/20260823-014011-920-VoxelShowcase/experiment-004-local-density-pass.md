# Experiment 004 — local coarse density/material regression

**Hypothesis** — With the duplicated oracle additions removed, the existing coarse phase and visible-top-material fix remains correct at the current `fixes` head.

**What was performed** — At source commit `ccfff86b393bc4627f2e04e6c5b61639d3e4d690` plus the working-tree oracle deduplication, ran `VoxelEngine.Tests.EditMode.CoarseLodDensityReconstructionTests` through `tools/unity-run.sh` without `-quit`. Evidence is in `verification-local-density.xml` and `verification-local-density.txt`.

**Result** — Passed 5/5 tests in 0.068 seconds. This includes `Step2LayeredSlopeUsesVisibleTopSurfaceMaterial`, the direct SceneIssue 014011 regression, plus phase preservation at source steps 2, 4, and 8.

**What was learned** — Hypothesis confirmed. The material-selection fix and the earlier phase correction both remain green once the branch's duplicate verification code is repaired.

**Next** — Run the related GPU mixed-field, boundary ownership, cutover, and vertex-attribute parity oracles before replaying the exact saved camera.
