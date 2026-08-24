# Experiment 003 — local density regression dispatch

**Hypothesis** — After deduplicating the mixed-field oracle commit, the focused density/material regression runs and passes locally.

**What was performed** — At source commit `ccfff86b393bc4627f2e04e6c5b61639d3e4d690` plus the working-tree deduplication, invoked `VoxelEngine.Tests.EditMode.CoarseLodDensityReconstructionTests` through `tools/unity-run.sh` with both `-quit` and `-runTests`. Output is in `verification-local-density-no-dispatch.txt`.

**Result** — Inconclusive. Unity compiled successfully and exited 0, but `-quit` caused batchmode shutdown before the test runner dispatched. No result XML was created, so zero-test execution is not accepted as a pass.

**What was learned** — The compile blocker is resolved, but the local invocation must omit `-quit` and allow Unity's test runner to terminate the process.

**Next** — Rerun the exact filter without `-quit`; require a result XML with nonzero test count and zero failures.
