# Experiment 005 — strengthened stump rejection

## Hypothesis
The saved-pose regression can distinguish the reported rooted trunk from an acceptable post-sever frame by requiring at least a 90% reduction in standing-tree contribution inside the recorded circle.

## What was performed
Source commit: `5005295c9d4cd39263840a3ecdaf788103ad75a3`.

Ran `VoxelEngine.CI.SceneIssue20260825033015TreeRenderingTests.CapturedPlayerShot_ClearsStandingTreeFromMarkedRegion` through `ci/single-test` on request commit `43aab0630933d7ff3ac13f358f9d7024d7c1049f` (workflow `32892353422`). The assertion allows at most 10% of the pre-hit standing-tree pixels, with a 32-pixel noise floor.

## Result
The workflow failed as intended and executed exactly one test. Standing-tree contribution in the marked circle was `712 -> 159` pixels; the allowed post-sever maximum was `72`.

## What was learned
**Hypothesis confirmed.** The strengthened captured-view regression is red on the undesired standing-stump behavior and gives a deterministic acceptance threshold for the reported region.

## Next
Apply the semantic/presentation whole-tree sever change, then rerun this exact regression against the production fix.
