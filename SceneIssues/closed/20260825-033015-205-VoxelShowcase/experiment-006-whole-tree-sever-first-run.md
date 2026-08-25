# Experiment 006 — whole-tree sever first run

## Hypothesis
Retiring all rooted topology after a level-zero direct cut and spawning the entire remaining skeleton as debris around the actual hit point will clear the reported standing trunk from the saved marked region.

## What was performed
Source commit: `302c2ee85022098902830b0349c944f5fde3b2c4`.

The source makes a level-zero direct cut resolve every rooted branch as removed, updates collision/query semantics to match, makes the first structural trunk cut spawn the whole remaining tree as one detached body using the actual hit point as its pivot, and updates the Showcase/tree-batch lifecycle contracts. Ran `VoxelEngine.CI.SceneIssue20260825033015TreeRenderingTests.CapturedPlayerShot_ClearsStandingTreeFromMarkedRegion` through `ci/single-test` on request commit `abca235da6f9d67035c2ef065a609a9c72a805f6` (workflow `32895437301`).

## Result
The workflow failed and executed exactly one test. Standing-tree contribution was still `712 -> 155` pixels; the allowed maximum was `72`.

## What was learned
**Hypothesis disproven for the current renderer handoff.** Changing topology and detached-debris ownership alone does not retire the standing renderer seen by the saved viewpoint. The remaining contribution is essentially unchanged from the pre-fix stump (`159` pixels), so the next investigation belongs in `ProceduralTreeRenderer` damage/cut reconciliation rather than in batch release or detached-body geometry.

## Next
Trace the renderer's damage/branch-cut update path, especially when `ResolvedRemovedBranches` is recomputed and when a fully removed dynamic presentation is destroyed.
