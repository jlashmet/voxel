# Experiment 002 — local density regression after runtime deduplication

**Hypothesis** — Removing the duplicated runtime oracle declarations is sufficient to restore compilation and dispatch the focused coarse-density/material regression.

**What was performed** — With the runtime deduplication applied to source commit `ccfff86b393bc4627f2e04e6c5b61639d3e4d690`, reran `VoxelEngine.Tests.EditMode.CoarseLodDensityReconstructionTests` through `tools/unity-run.sh`. Output is in `verification-local-density-duplicate-test.txt`; Unity again stopped before producing XML.

**Result** — Failed compilation with CS0111 at `GpuSurfaceExtractorOracleTests.cs:593`: `MixedSampleFieldMatchesTheCpuJob` was also added twice by the duplicate oracle commits.

**What was learned** — Hypothesis disproven. The duplicate commit affected both runtime oracle seams and their EditMode regression; all byte-for-byte duplicate additions must be removed as one branch-repair change.

**Next** — Remove the second identical test body, confirm only one declaration of every duplicated symbol remains, and rerun the same focused class.
