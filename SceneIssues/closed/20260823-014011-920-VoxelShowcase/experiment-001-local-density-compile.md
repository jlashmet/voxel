# Experiment 001 — local density regression compile

**Hypothesis** — The current `fixes` head compiles locally and the focused coarse-density/material regression remains green after the later oracle commits.

**What was performed** — At source commit `ccfff86b393bc4627f2e04e6c5b61639d3e4d690`, ran `VoxelEngine.Tests.EditMode.CoarseLodDensityReconstructionTests` in EditMode through `tools/unity-run.sh`. Output is in `verification-local-density-compile-failure.txt`; Unity stopped before producing the requested XML.

**Result** — Failed before test dispatch with five duplicate-definition compiler errors: `CpuDensityFieldSnapshot`, `SampleMixedNeighbourhood`, `ReadSampleSurfaces`, and `ReadSampleBoundaries` were each added twice by duplicate mixed-field-oracle commits `c07f68ec05` and `c032bf23d8`.

**What was learned** — Hypothesis disproven. The branch head cannot currently compile; the duplicated verification-only oracle additions must be deduplicated before any regression or replay evidence is meaningful.

**Next** — Remove only the byte-for-byte duplicate declarations, preserving one implementation of each oracle seam, then rerun the identical focused test.
