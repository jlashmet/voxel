# SceneIssue 014011 — GPU LOD2 cutover

## Context
`SceneIssues/20260823-014011-920-VoxelShowcase` captures the high-altitude VoxelShowcase terrain artifact where the near/coarse terrain appears blueish and spiraly.

## Decision
Extend the existing GPU surface-extraction path to exact `sourceStep == 2` (LOD2) in addition to `sourceStep == 1`.

Keep `sourceStep == 4` on the existing exact CPU path for now, and keep `sourceStep == 8`/block HLOD on HLOD. Do not broaden the cutover beyond LOD2 without evidence from the captured view or focused tests.

## Work
1. Verify the GPU extraction context, shader addressing, and seam metadata are source-step aware at `sourceStep == 2`.
2. Widen the cache GPU-eligibility policy only from step 1 to steps 1 and 2.
3. Add a focused regression that proves LOD2 is GPU-eligible while the farther exact/HLOD rings remain on their existing paths.
4. Run the focused EditMode regression through the repository's targeted Unity CI.
5. Replay the saved SceneIssue capture and verify the visual artifact is gone; compare topology/material behavior and watch for an unacceptable performance regression.
6. Only after the captured view passes, mark the SceneIssue fixed in a separate bookkeeping commit.

## Rollback / escalation
If GPU LOD2 produces topology/material regressions or the captured issue remains after three focused attempts, revert the cutover and build a bare-bones reproduction per `SceneIssues/README.md` before continuing production changes.
