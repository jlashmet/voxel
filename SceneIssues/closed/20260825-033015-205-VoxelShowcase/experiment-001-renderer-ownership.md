# Experiment 001 — renderer ownership trace

## Hypothesis
The stale main tree is caused by the damage renderer releasing its per-tree override after destruction, allowing the original healthy batched tree/trunk to become visible again while detached branch debris falls.

## What was performed
Source commit: `9986c7db9c799270e2046a6d28e88ae2457b7d37`.

Traced the captured VoxelShowcase shot path from `VoxelShowcase.TryTornadoImpact` / `StepTornadoes` through `ProceduralTreeDamageService`, `TreeWorldState`, `ProceduralTreeRenderer`, `ProceduralTreeBreakPresenter`, and `ProceduralTreeDetachedLimbPresenter`. Compared the current renderer's batch-release and fully-removed paths with the existing `ShowcaseTreeDestructionVisualTests` coverage and the earlier `865f8fba` tree-batch damage optimization.

## Result
The hypothesis is disproven in the current source. `ProceduralTreeRenderer.ReleaseTreeFromBatch` first zeroes the affected tree's bark, leaf, and impostor index ranges through `HideTreeInBatch`, then marks the tree unbatched. `ApplyDamage` subsequently materializes only the surviving rooted geometry, or destroys the dynamic presentation when every branch is resolved removed. Detached falling geometry is independently produced from the same cut subtree by `ProceduralTreeDetachedLimbPresenter`.

The existing showcase destruction regression has a coverage gap relevant to this capture: it first damages an upper branch with `impactRadius=2`, waits for a dynamic per-tree presentation, and only then performs a lower-trunk sever. It therefore never proves the first damaging shot against a still-healthy batched tree. It also uses a 0.2 m tree blast, while normal VoxelShowcase left-click uses the default brush radius 12, producing a 1.2 m tree blast.

## What was learned
**Hypothesis disproven.** The current renderer does not simply fall back to an intact batch after a cut. The untested boundary is a normal player shot as the *first* damage to a still-batched showcase tree, especially through the larger default blast radius.

## Next
Add a focused PlayMode regression that starts from a fresh batched VoxelShowcase tree and exercises the normal player-shot radius/impact path. Use the saved capture camera/marked screen region where practical so the test targets the same presentation path, then run it through `ci/single-test` before changing production behavior.
