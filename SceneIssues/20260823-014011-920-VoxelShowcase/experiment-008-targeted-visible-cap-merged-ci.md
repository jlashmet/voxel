# Experiment 008 — merged-head visible-cap CI

**Hypothesis** — Merging current `master` into `fixes` does not regress the direct SceneIssue
014011 visible-cap material invariant or reintroduce the duplicate-oracle compile failure.

**What was performed** — Merged source commit `8b9026a0e7be5683225b7bcc4c62790cb740d597` was reset onto
the existing `ci-test/fixes`; request commit `90bbbeaffae1f3fb4d132f7c90745bb2b79487f5` requested
`VoxelEngine.Tests.EditMode.CoarseLodDensityReconstructionTests.Step2LayeredSlopeUsesVisibleTopSurfaceMaterial`.
Evidence is in `verification-targeted-visible-cap-merged-ci.txt` and GitHub Actions run
`32747432360`.

**Result** — Passed. `ci/single-test` reached `success`; Unity executed exactly 1 test case in 58
seconds, below the five-minute single-test budget.

**What was learned** — Hypothesis confirmed. The merged branch compiles and the direct regression
still proves exposed top material wins over buried lateral material.

**Next** — Reuse `ci-test/fixes` for the broader mixed-field CPU/GPU oracle that exercises the
retained single-copy snapshot and readback helpers.
