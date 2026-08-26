# Experiment 02 — pre-fix LOD ownership regression

## Hypothesis

The reported mixed-resolution terrain can occur because the renderer has no hierarchy-aware owner for overlapping visible coarse/fine solid chunks. A focused ownership regression should therefore be red before that mechanism exists.

## What was performed

Targeted EditMode CI requested `VoxelEngine.Tests.EditMode.SurfaceLodActiveCoverageTests.VisibleDrawOwnershipKeepsCoarseParentAcrossPartialFinerOverlap` from source commit `1466b27c3508fff09898cff0b21f18c1caa91ac6` via request commit `86d3e4325f3536bb90bf5454081b68d1da6f66fe` (`request_id=agent4-lod-overlap-prefixtest-20260825T1900Z`). GitHub Actions run: `32887385236`.

## Result

`ci/single-test` failed. Unity compilation reported `CS0246` for `SurfaceLodVisibleOwnership` at all three new ownership assertions because the production ownership primitive did not yet exist.

## What was learned

Confirmed as a red-before-fix guard, with an important limitation: the baseline is compile-time red rather than an assertion failure. It proves the desired ownership API/invariant was absent, while the static trace in experiment 01 supplies the behavioral evidence that overlapping chunks were simultaneously publishable.

## Next

Implement the smallest hierarchy ownership primitive and apply it only at final solid draw staging, preserving existing streaming/ring overlap.
