# Experiment 007 — targeted visible-cap CI

**Hypothesis** — After deduplicating the mixed-field oracle additions, the focused SceneIssue
014011 visible-cap material regression compiles, executes a nonzero test count, and passes in the
targeted CI budget.

**What was performed** — Source commit `f394ebc3ab68f8c872272a150a54be1a160526c9` was reset onto
`ci-test/fixes`; request commit `16ad3cff4b5f8d210bb58e7313078a9a0e6e7227` requested
`VoxelEngine.Tests.EditMode.CoarseLodDensityReconstructionTests.Step2LayeredSlopeUsesVisibleTopSurfaceMaterial`.
Evidence is in `verification-targeted-visible-cap-ci.txt` and GitHub Actions run `32746786542`.

**Result** — Passed. `ci/single-test` reached `success`; Unity executed exactly 1 test case in 61
seconds, below the five-minute single-test budget.

**What was learned** — Hypothesis confirmed for the pre-merge repair head. The duplicate removal
restores compilation and the direct visible-cap invariant is green. Because `master` was merged
after this run, a new request from the merged feature head is still required.

**Next** — Reset the same `ci-test/fixes` branch to the merged `fixes` head and issue a new unique
request for the same focused regression.
