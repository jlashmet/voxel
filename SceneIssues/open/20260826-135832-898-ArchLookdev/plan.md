# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported left/crown masses with overlapping English-ivy leaves/drapes; blossoms as layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 flower heads, 3 combined draws, <=4,096 vertices, two semantic ground ferns.
- Green geometry is not visual proof. Experiment 016 source `f8d79180...`, exact request `75815c0e...`, run `33145729562` passed its regression and 45-second replay, but direct final-frame inspection rejected a dominant leaf/flower mass floating inside the arch opening plus isolated pier blobs.

## Proven causes / discriminator
1. Missing render data / camera placement — rejected/fixed earlier; authored meshes render and world-space anchoring survives rebuilds.
2. Stem removal / relative path compression — rejected by experiments 014–016; a wrong inherited path centroid can remain compact while still floating off masonry.
3. Experiment 015 parser — rejected as evidence: `Infinity` came from assuming stem spacing rather than positively locating leaves.
4. Experiment 017 — current: retain positive 128-leaf parsing, but recompose the same left clusters around stable world-space lower-pier, upper-pier, and left-crown masonry anchors; place all three bouquet zones on those supports; keep rounded overlapping petals with stronger readable pastel material separation.

## Behavioral regression
`ArchReferenceGrowthMassBreakupPassTests.FinalPassBreaksDiagonalBandIntoMassesAndGathersReadableBouquetsAcrossRebuild` must recover exactly 128 leaves; bound each left foliage/bouquet centroid to its masonry-side world envelope; reject any left cluster drifting into the central opening; require real inter-mass gaps plus rounded/deep flower heads; preserve mesh identity, 3 draws, <=4,096 vertices; and deterministically reapply after production rebuild.

## Blast radius / cost
ArchLookdev presentation only. Same meshes/topology/draws/vertex budget; one-shot vertex/material rewrite only, no per-leaf/flower GameObjects or per-frame geometry work.

## Remaining gates
Run the focused regression on the exact feature SHA through the existing `ci-test/fixes/agent-4` transport with 45-second replay. Accept only if green and direct saved-pose inspection shows foliage and bouquets supported by the masonry with organic overlap and no floating opening mass. Then record verification, open→pending metadata, pending→closed `fixed`/`resolvedUtc`, merge current master, and non-force push exact feature head to master.