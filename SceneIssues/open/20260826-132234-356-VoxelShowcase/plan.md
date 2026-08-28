# Plan — VoxelShowcase dirt/grass seam

## Observed / acceptance
Two saved-camera circles mark the seam. Fresh post-bake replay shows the lower circle clean, while the upper circle still contains a raised rectangular tongue. Acceptance is continuous terrain/terrace geometry at both circles with no late rectangular shelf.

## Competing hypotheses and discriminators
1. **Stale bake / LOD:** falsified by a fresh rebuild/replay that preserved only the upper defect.
2. **Material-only ownership:** falsified after removing the correction-layer Moss repaint; the upper rectangle remained geometrically raised.
3. **Civic south shoulder:** supported enough to repair the locally sampled outer profile through the full projected upper-circle envelope (`X≈91.0..93.8m`, `Z≈28.6..30.4m`), but fresh replay still retained the rectangle, so terrace-only ownership is falsified.
4. **Late urban court occupancy:** supported. `civic-west-block` owns `900..1110 × 226..326 dm`; its court is `928..1082 × 254..298 dm`, intersecting the upper mark at `X≈928..938, Z≈286..298 dm`. The court runs at precedence 85 and previously emitted a flat `Fill` from the block's summit sample (`Z=150 dm`), after the precedence-16 shoulder repair.

## Selected fix
Keep district terraces/corrections as height owners. Make urban courts late `PaintSurface` treatments over a bounded local vertical volume instead of flat solid `Fill` slabs. This preserves court material/precedence without re-authoring shoulder height. The regression builds the final combined production catalogue, resolves combined explicit-placement remapping through `PlacementRule.ExplicitOffset`, verifies all eight locally sampled civic strips, and proves the overlapping civic-west court paints but does not fill the marked shoulder.

## Blast radius / cost
All eight urban courts change from one `Fill` box to one `PaintSurface` box; definition count, precedence, and max primitive budget stay unchanged (`2`). Paint bounds sample five natural points once during catalogue build; no per-frame work. Named structures/access remain later/higher-precedence owners.

## Gate
Working baseline: `7d0ceb982af6b9b64c70c628d08a356b81eb680b`. Keep open until the exact-SHA targeted PlayMode test `SceneIssue20260826132234356CivicSouthWestShoulderFollowsLocalTerrainProfile` is green and a fresh saved-camera replay confirms both original circles are clean.
