# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported lower/upper/crown masses with overlapping English-ivy leaves and a few drapes; flowers as layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 flower heads, 3 combined draws, <=4,096 vertices, and two semantic ground ferns. Automated geometry is not visual acceptance.
- Experiment 017 source `5fe1c7d8...`, request `2d6cee06...`, run `33146173563` passed the focused regression and 45-second replay, but direct final-frame inspection rejected horizontal green shelves, repeated small flower icons, and an unreadable crown.

## Hypotheses / discriminator
1. Render/camera/stem/lifecycle failures — rejected earlier: authored meshes render, stems are collapsed, and the world-space rewrite survives rebuild.
2. Masonry x/y anchors alone are sufficient — falsified by experiment 017: correct centroids still produced shelves because oversized translated cards remained densely packed.
3. Current: rewrite each existing left leaf into a smaller varied pointed ivy card with vertical/sloped packing and front-layer depth; place each existing flower head independently into asymmetric lower/upper/crown bouquet footprints. Falsified if the regression sees shelf-like spans/opening drift or the saved frame still reads as shelves/icons.

## Behavioral regression
`ArchReferenceGrowthMassBreakupPassTests.FinalPassBreaksDiagonalBandIntoMassesAndGathersReadableBouquetsAcrossRebuild` must recover exactly 128 leaves; enforce masonry envelopes and no opening drift; require inter-zone gaps, vertical/sloped spans, varied leaf radius/depth, and dispersed rounded varied-size flower heads; preserve mesh identity, 3 draws, <=4,096 vertices; and deterministically reapply after rebuild.

## Blast radius / cost
ArchLookdev presentation only. Reuses the same meshes/topology; one-shot vertex/material rewrite, no new renderers, GameObjects, vertices, or per-frame geometry work.

## Remaining gates
Run this exact feature SHA through existing `ci-test/fixes/agent-4` with a 45-second replay. Accept only if the test is green and direct final-frame inspection shows organic masonry-supported ivy/bouquets, a readable crown, sparse right side, and no central floating mass or shelves. Then record verification, complete pending metadata/move, close, merge latest master, and non-force push the exact feature head to master.
