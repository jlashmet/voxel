# Experiment 005 — validate the shared-house frontage constraint

## Hypothesis

The live-path fix in `KentridgeSharedHouseProgram` should preserve Medrare House's two authored asymmetric frontage windows while keeping each physical front-wall opening at least 3 dm clear of the same public door identified by the published semantic anchor.

## What was performed

Ran the focused Unity EditMode regression through the repository-standard `ci-test/fixes` request mechanism.

- Feature source: `a06305ce14b9c251ba94d89549626840b3bf68e1`
- Production/test fix: `cd4480b134461b1eddb33a05c78735e4489bf4f5`
- CI request commit: `2e3d0d40c15abf6b23d31285427b04ad0a673c3d`
- GitHub Actions run: `32819100852`
- Job: `97713307604`
- Test: `VoxelEngine.Tests.EditMode.KentridgeGeneratedEntranceAlignmentTests.MedrareHouseKeepsBothFrontageWindowsClearOfDoor`

## Result

**Passed.** `ci/single-test` completed successfully. Unity executed exactly 1 test case, returned status 0, and completed the Unity invocation in 63 seconds with a 5237 MB peak RSS.

## What was learned

**Hypothesis confirmed structurally.** The regression now observes the actual shared-house bytecode representation: front-wall `ShapeOp.EmitBox` carve operations. The generated Medrare program contains both asymmetric frontage windows, the physical entrance agrees with the published door anchor, and both windows satisfy the 3 dm entrance-clearance invariant.

This establishes that the active shared-house compilation path enforces the intended geometry constraint. It is not yet visual completion evidence for the original `VoxelShowcase` camera pose.

## Next

Run the full `KentridgeGeneratedEntranceAlignmentTests` EditMode fixture as the smallest broader affected set, then produce a fresh replay/render from the original `VoxelShowcase` capture pose and inspect the circled facade before resolving the issue.
