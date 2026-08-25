# Experiment 007 — target-specific saved-view verification

## Hypothesis
The previous post-fix `155`-pixel failure is measurement contamination from other procedural trees in the recorded circle, not a surviving presentation of the shot Willow. A target-specific rooted-presentation assertion plus the saved framebuffer should verify the fix correctly.

## What was performed
Source commit: `eaead8ede86cbf90e36ead8d92ddbc4a34083aa9`.

Updated the capture regression to require the recorded shot to make a level-zero structural cut, require that exact target tree to have no rooted dynamic presentation afterward, and preserve the saved-camera framebuffer plus the all-procedural-tree pixel count as diagnostic evidence. Ran `VoxelEngine.CI.SceneIssue20260825033015TreeRenderingTests.CapturedPlayerShot_ClearsStandingTreeFromMarkedRegion` through `ci/single-test` on request commit `766e212f78c6d8fa16eb142a930ad0924587c4cf` (workflow `32895976717`).

## Result
The workflow passed and executed exactly one test. The target standing presentation was `False`; target bark triangles were `5888 -> 0`, leaves `4200 -> 0`. The marked circle still contained `178` pixels attributable to all procedural-tree renderers because another pink-leaf tree stands behind the falling target. Visual inspection of `verification-after.png` confirms the thick rooted stump from the pre-fix frame is gone and the shot tree is a detached falling body.

## What was learned
**Hypothesis confirmed.** The earlier 10%-of-all-tree-pixels threshold was over-broad: it treated unrelated background vegetation as if it belonged to the shot tree. The production fix does retire the target rooted renderer; the durable regression now checks that ownership directly while retaining the original saved view for replay evidence.

## Next
Run the full Showcase destruction lifecycle and batching/query regressions against the same feature source.
