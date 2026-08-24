# Experiment 017 — final boundary regression

## Hypothesis

The retained geographic regression passes after removing all diagnostic-only code and the temporary
camera fixture from the final source tree.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the final uncommitted
production/test diff, ran
`VoxelEngine.Tests.EditMode.TwoTownWorldTests.HightownVoxelCatalogueDoesNotEmitSouthOfTheCountryMidpoint`
locally through `tools/unity-run.sh` after `git diff --check` passed.

## Result

The test passed 1/1 in 0.032 seconds. Evidence is in `verification-final-boundary-results.xml` and
`verification-final-boundary-unity.log`.

## What was learned

The hypothesis is confirmed. The final retained catalogue regression is green without replay or
diagnostic fixtures present.

## Next

Run the two retained PlayMode regressions against the same clean final source.
