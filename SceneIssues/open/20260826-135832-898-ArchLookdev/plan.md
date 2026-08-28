# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: global saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported lower/upper/crown English-ivy masses with visible overlapping leaves/drapes; flowers as layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Automated geometry is not visual acceptance.
- Experiment 018 source `292b9e62...`, request `6431ec56...`, run `33147305207` passed its regression and 45-second replay, but direct final-frame inspection rejected flat dark leaf blobs, repeated pink/orange flower disks, and a weak crown.

## Hypotheses / discriminator
1. Render/camera/stem/lifecycle failures — rejected earlier; authored meshes render and world-space composition survives rebuild.
2. Position/count is still primary — falsified by experiment 018: masonry envelopes, opening clearance, vertical/sloped spans, 128 leaves and 30 heads all pass while the frame remains visually flat.
3. Current: preserve placement/topology but expose layers—slightly shrink individual leaf cards, increase per-leaf value/depth separation, reuse only short local stem quads as vine cues, enlarge/compact flower heads into overlapping bouquets, shrink centre dots, and vary blossom value/palette. Falsified if the saved frame still merges into cutouts/icons.

## Behavioral regression
`ArchReferenceGrowthReadabilityPassTests.FinalReadabilityPassSeparatesLeafLayersAndBuildsOverlappingBouquetsAcrossRebuild` must keep 128 leaves and the same meshes/budget; prove smaller leaves, meaningful leaf value/depth range, >=10 bounded local vines with no long garland, larger overlapping heads, small flower centres, blossom value variation, 3 draws, <=4,096 vertices, and deterministic rebuild.

## Blast radius / cost
ArchLookdev presentation only. Reuses existing leaf/stem/petal/centre vertices and meshes; one-shot vertex/material rewrite, no new renderers, GameObjects, vertices, or per-frame geometry.

## Remaining gates
Run the exact feature SHA through existing `ci-test/fixes/agent-4` with 45-second replay. Accept only if the focused regression is green and direct final-frame inspection meets the AAA foliage/bouquet bar with a readable crown, sparse right side, and no shelves/central floating mass. Then record verification, complete pending metadata/move, close, merge latest master, and non-force push exact head to master.
