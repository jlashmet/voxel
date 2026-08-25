# Experiment 010 — final boundary regression

## Hypothesis

The permanent cross-settlement ownership regression passes after all diagnostic tests and temporary
camera resources are removed from the source tree.

## What was performed

Against source `36cec6893239e000c9aa875ebe9320a99927d0f4` plus only the uncommitted issue evidence,
ran
`VoxelEngine.Tests.EditMode.TwoTownWorldTests.HightownVoxelCatalogueDoesNotEmitSouthOfTheCountryMidpoint`
locally through `tools/unity-run.sh` in EditMode.

## Result

The run passed exactly 1/1 tests in 0.029 seconds with zero failures, skips, or inconclusive tests.
Evidence is `verification-final-boundary-results.xml` and
`verification-final-boundary-unity.log`.

## What was learned

The hypothesis is confirmed. The retained invariant directly prevents Hightown composition from
emitting the Kentridge-only placements that intersected the magic-shop volume, and it passes on the
clean final source tree.

## Next

Review the complete issue evidence, commit it, then record the causal production commit in
`issue.json` and resolve the issue in a separate commit.
