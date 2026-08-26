# Experiment 004 — marked-region framebuffer proof

## Hypothesis
Even though structural counts collapse after the saved first-hit trunk sever, the circled region may still contain a large standing-tree presentation that explains the reported defect.

## What was performed
Source commit: `9d62f7e771d9ca68e988e0d45064ce45adc8733c`.

Ran `VoxelEngine.CI.SceneIssue20260825033015TreeRenderingTests.CapturedPlayerShot_ClearsStandingTreeFromMarkedRegion` through `ci/single-test` on request commit `d0040b9191b460e0282681ca5416173c88af13ce` (workflow `32891626690`). The fixture reconstructed the saved pose and circle, rendered before and after the first normal-radius trunk hit, and isolated only `ProceduralTreeRenderer` pixels by comparing each frame with the standing-tree renderers disabled. The run wrote `verification-after.png` and `verification-metrics.txt` to the single-test artifact.

## Result
`ci/single-test` succeeded and executed exactly one test. The marked-circle standing-tree contribution fell from `712` pixels before damage to `159` after damage. Bark triangles fell `5888 -> 16`; leaf triangles fell `4200 -> 0`. Inspection of the saved-pose post-hit frame shows the upper trunk/crown falling while a short rooted trunk/stump remains in the recorded circle.

The existing `ShowcaseTreeDestructionVisualTests` contract explicitly expects a lower-trunk hit to leave the stump standing while the crown topples. Relevant tree-destruction behavior was already present before this August 25 capture; no later tree-rendering fix explains the difference.

## What was learned
**The stale-full-tree hypothesis is disproven; the reported visual issue reproduces as the intentional standing-stump behavior.** The capture's note and circle reject an old presentation contract rather than exposing a failed batch release. The regression was too weak because it only required the standing silhouette to shrink, which the undesired stump satisfies.

## Next
Strengthen the saved-pose regression so a trunk sever must clear nearly all standing-tree pixels from the recorded circle. Confirm that it is red on current source, then change the semantic/presentation sever behavior so the falling crown remains but the rooted main-trunk presentation no longer survives the shot.
