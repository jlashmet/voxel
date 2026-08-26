# Experiment 005 — integrated framed-glazing regression

## Hypothesis

The attempt-2 large-window construction remains correct after integrating the current master history: large rectangular glazing should retain masonry perimeter/frame ownership, split into two inset panes, and preserve the original facade normal.

## What was performed

- Product/test source: `f31a9c2c02f25c7e21cc0a0447a9e765947ddeee`.
- Targeted-CI request: `3bc7f76ac452f737f50d1643465cb1df2781936b` on `ci-test/fixes/agent-3`.
- Unity test: `VoxelEngine.Tests.EditMode.ArchitectureVoxelPatternTests.GlazedOpeningFramesAndSubdividesLargeFacadePane`.
- GitHub Actions run: `33003343182`.
- The CI request was created directly on the exact feature source and the persistent CI ref was updated once.

## Result

Passed. The requested Unity test executed successfully, result upload completed successfully, the workflow completed with conclusion `success`, and commit status `ci/single-test` is `success`.

After the run, master advanced only through unrelated SceneIssue bookkeeping and CI/process changes. Those changes were merged into the feature at `e5b106425c263d4466f95d2ec4caaed7a86be8c2`; neither `ArchitectureVoxelPatterns.cs` nor `ArchitectureVoxelPatternTests.cs` changed across that merge.

## What was learned

Hypothesis confirmed. The framed/subdivided glazing contract is green on the integrated product/test state, so the remaining product question is visual: whether the exact reported camera now reads as an intentional architectural window rather than a flat amber slab.

## Next

Run the shared exact SceneIssue replay from the reconciled feature head, persist `verification-final.png`, compare it with `screenshot-001.png`, and obtain explicit human approval before terminal bookkeeping.
