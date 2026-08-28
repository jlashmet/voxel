# Experiment 008 — depth and scale hierarchy

## Evidence
Exact-SHA run `33128668933` is green for `VoxelEngine.Tests.PlayMode.ArchLookdevSceneTests.SceneBuildsHeroThroughProductionSurfacePath`, and its original saved-pose RealPlayer replay renders the detached hero growth correctly. The frame is still not acceptable: the left/crown ivy reads as a nearly flat dark cutout and each pink cluster reads as a repeated radial flower stamp.

Code inspection explains both artifacts without reopening the solved lifecycle hypothesis. Every ivy leaf is emitted in essentially one Z band with `Vector3.back` normals and a narrow `0.34–0.50 * cluster.Scale` size range. Every flower head is five petals spaced at 72 degrees around a conspicuous disc centre.

## Discriminator
Hold path coordinates, automatic detached-root lifecycle, semantic ground accents, and the three-draw budget fixed. Change only close-up geometry hierarchy:
- create shallow front/back leaf layering and a wider small/medium/hero leaf size distribution;
- soften the leaf outline so overlaps read as foliage instead of a solid emblem;
- replace five-way radial blossoms with smaller irregular three-bract heads, scatter the heads farther within each cluster, and shrink the pollen centre.

## Pass / fail
Pass only if a fresh saved-pose standalone replay visibly separates individual leaves through overlap/depth/scale and the pink growth reads as delicate clustered blossoms rather than daisies, while the existing regression remains green and hero geometry stays <= 4,096 vertices / 3 draws.
