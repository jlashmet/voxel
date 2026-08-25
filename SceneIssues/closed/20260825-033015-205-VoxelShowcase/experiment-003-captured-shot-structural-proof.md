# Experiment 003 — captured player shot structural proof

## Hypothesis
The saved camera and marked region, using the normal VoxelShowcase player blast radius as the first hit on a healthy batched tree, leave an intact or oversized standing tree presentation.

## What was performed
Source commit: `f08f4b29935769a5295735bf9e0a3a8b540ea425`.

Ran `VoxelEngine.CI.SceneIssue20260825033015TreeRenderingTests.CapturedPlayerShot_ReportsTreeCutAndStandingPresentation` through `ci/single-test` on request commit `54f9df6eced98597aee4cdd1f8ada0ccab4e1c05` (workflow `32891168946`). The test reconstructs the saved camera pose, FOV, aspect, and circle center, then applies the normal 1.2 m tree blast as the first damage to the hit tree.

## Result
`ci/single-test` succeeded and executed exactly one test. Unity telemetry reported tree 12 (Willow), impact local Y 1.266 m on a 5.619 m tree, a new level-0 trunk cut at branch 1, `Severed=true`, one batch release, standing bark triangles `5888 -> 16`, and leaf triangles `4200 -> 0`.

## What was learned
**Hypothesis disproven at the structural level.** The captured first-hit path releases the tree from the healthy batch and reduces rooted geometry to a tiny stump. If a full upright tree remains visible, it must come from framebuffer presentation not represented by those structural counts, or the captured behavior is no longer present in current source.

## Next
Extend the same saved-pose regression with a framebuffer measurement in the recorded circle, save the post-hit verification frame, and assert that the marked-region tree silhouette collapses rather than remaining full-height.
