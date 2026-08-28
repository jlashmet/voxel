# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: global saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported lower/upper/crown English-ivy masses with visible individual overlapping leaves; flowers as integrated layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Automated geometry is necessary but not visual acceptance.
- Runs `33147763145` and `33148437517` were mechanically green but rejected by direct final-frame inspection: first for disconnected dark clumps/tiny flower stamps, then for a surviving long diagonal legacy stem plus still-overcompressed foliage.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected: world-space hero meshes render and rebuild deterministically with required topology/budget.
2. Architectural placement — corrected by minimal repro 020: springline `y=6.4 m`, opening crown `y=7.8 m`; prior false crown was near `y=6.91 m`.
3. Remaining confirmed defect from run `33148437517`: stale stem quads and excessive local overlap/material darkening destroy the reference read even with correct crown placement.

## Selected fix / regression
Final one-shot `ArchReferenceGrowthAaaPass` reuses the existing three meshes: collapse every stem-colored quad, distribute smaller broad English-ivy leaves continuously from left pier through haunch/crown with one sparse right accent, and recompose 30 heads as six five-flower bouquets with small centres and readable color variation. `ArchReferenceGrowthAaaPassTests.FinalAaaPassRemovesStemArtifactsAndBuildsContinuousReferenceMassAcrossRebuild` proves zero-span stem quads, support/crown/right-side distribution, bounded leaf/head sizes, bouquet integration, unchanged draw/vertex budget, and deterministic rebuild.

## Blast radius / cost
ArchLookdev presentation only. Existing mesh/material buffers are rewritten once; no shared vegetation/world truth, new renderers, vertices, per-leaf GameObjects, or steady-state geometry work.

## Remaining gates
Run the exact feature SHA through existing `ci-test/fixes/agent-4` with 45-second replay. Accept only if the focused regression is green and direct saved-pose inspection meets the AAA reference bar. Then record verification, pending metadata/move, close, merge latest master, and non-force push exact head to master.
