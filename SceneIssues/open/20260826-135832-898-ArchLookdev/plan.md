# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: global saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported lower/upper/crown English-ivy masses with visible overlapping leaves/drapes; flowers as layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Automated geometry is not visual acceptance.
- Experiment 019 source `6b162f2b...`, request `83d80b24...`, run `33147763145` passed its readability regression and 45-second replay, but direct final-frame inspection rejected three disconnected left clumps, two stray right blobs, tiny flower patches, and no mass on the actual crown.

## Hypotheses / discriminator
1. Render/camera/lifecycle or leaf/flower count — rejected: world-space meshes render, rebuild deterministically, and required topology/budget is present.
2. Material/readability alone — falsified by experiment 019: stronger leaf depth/value and larger overlapping blooms still land in the wrong architectural composition.
3. Confirmed by minimal repro 020: hero preset springline is `y=6.4 m`, opening crown `y=7.8 m`, but the prior “crown” anchor compressed five clusters around `y=6.91 m`, visibly on the haunch. It also retained four right clusters despite the left/crown-heavy reference.

## Selected fix / regression
Final architectural pass keeps existing topology/readability but derives a semantic growth frame from the hero preset: lower + upper left-pier supports; eight clusters following the masonry arc from left haunch through the crown; one sparse right cluster; four lower/mid/haunch/crown bouquets. `ArchReferenceGrowthArchitecturalPassTests.FinalPassFollowsArchHaunchAndCrownWithSparseRightGrowthAcrossRebuild` must prove crown radial support and height, crown x-span, only one right cluster, bouquet centroids/crown reach, bounded leaf/flower sizes, unchanged meshes/3 draws/<=4,096 vertices, and deterministic rebuild.

## Blast radius / cost
ArchLookdev presentation only. Reuses existing leaf/stem/petal/centre vertices and meshes; one-shot vertex/material rewrite, no new renderers, GameObjects, vertices, or steady-state geometry work.

## Remaining gates
Run exact feature SHA through existing `ci-test/fixes/agent-4` with 45-second replay. Accept only if focused regression is green and direct saved-pose inspection meets the AAA reference bar. Then record verification, pending metadata/move, close, merge latest master, and non-force push exact head to master.
