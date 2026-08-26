# Experiment 06 — far-terrain boundary ownership trace

## Hypothesis

The saved-view stripes survive hierarchy-exclusive voxel LOD ownership because `VoxelFarTerrain` is a separate analytic representation whose ring-0 boundary quads still overlap the fine voxel-owned footprint. On steep terrain, a coarse far-field quad can sit above the finer voxel surface, so ordinary depth testing exposes the coarse striped surface even though the near terrain is present.

## What was performed

Traced every standalone `VoxelShowcase` terrain submission after the failed exact-view replay in experiment 05, with particular attention to `VoxelShowcase.OnEnable`, `VoxelFarTerrain.HoleRadiusMetres`, `VoxelFarTerrain.LateUpdate`, and `RebuildRingFromCachedHeights`, then compared the behavior with `FarFieldCoverageInvariantTests`.

The showcase configures both the voxel ring and far field from the same streamed radius. `HoleRadiusMetres` already reserves one ring-0 sample-cell diagonal inside proven near coverage. However, ring-0 topology keeps a boundary quad whenever the quad's farthest corner lies outside the circular hole. That means a whole coarse far-field cell is submitted even when part of that cell lies inside the fine-owned hole. Existing coverage tests guard against holes and stale publication, but do not assert exclusivity across this near/far boundary.

## Result

Confirmed a second, non-hierarchical overlap path. The first fix removed duplicate coarse/fine voxel submissions, but ring 0 can still submit coarse analytic terrain into the fine near footprint by up to one cell at the circular boundary. The existing one-cell-diagonal safety margin is sufficient to move the topology boundary outward without exceeding proven near coverage; it is currently consumed only by snap safety, not by an exclusive-cell ownership rule.

## What was learned

This defect is an ownership/topology issue, not a draw-order or shader-depth issue. The safe invariant is: once near coverage is proven, any ring-0 cell intersecting the configured near-owned hole belongs to the voxel representation and must not be submitted by the far terrain. Closed-hole fallback behavior must remain unchanged while near coverage is incomplete.

## Next

Add a focused topology regression that proves ring 0 has no triangle overlap inside its published hole, make the minimal boundary-cell ownership change, and make the existing saved-pose replay batchmode-safe before targeted CI and standalone replay.
