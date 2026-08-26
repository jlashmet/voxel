# Experiment 03 — post-fix LOD ownership regression

## Hypothesis

If final draw staging gives each visible hierarchy overlap one exclusive owner, the focused coarse-parent/partial-child regression will pass without changing streaming coverage or convergence.

## What was performed

Targeted EditMode CI requested `VoxelEngine.Tests.EditMode.SurfaceLodActiveCoverageTests.VisibleDrawOwnershipKeepsCoarseParentAcrossPartialFinerOverlap` from source commit `7a08317eee5eae95de126b5e1b86dc41b14e077f` via request commit `66768a1a93fb92c468590925f3a07603e1dbdec6` (`request_id=agent4-lod-overlap-postfix-20260825T1907Z`). GitHub Actions run: `32887616593`.

## Result

`ci/single-test` succeeded. Unity executed exactly one test case; `VisibleDrawOwnershipKeepsCoarseParentAcrossPartialFinerOverlap` passed (`duration=0.003832s`, run completed 2026-08-25T19:07:08Z).

## What was learned

Confirmed. The publication-time ownership fix enforces the focused no-double-draw invariant while leaving the scheduler's intentional resident/ring overlap intact.

## Next

Replay the original `VoxelShowcase` capture at its saved camera pose and original 1364x836 framing, checking all three marked regions in a real standalone player.
