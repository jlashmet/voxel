# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported left/crown masses with overlapping English-ivy leaves/drapes; blossoms as layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 flower heads, 3 combined draws, <=4,096 vertices, two semantic ground ferns.
- Green geometry is not visual proof. Experiment 014 run `33142488637` was green but visually rejected (continuous band/tiny star flowers). Experiment 015 run `33144764363` also visually misses: distinct stems are gone, but foliage still bridges into repeated bands and flowers remain five-point icons.

## Proven causes / discriminator
1. Missing render data / camera placement — rejected/fixed earlier; authored meshes render and world-space anchoring survives rebuilds.
2. Stem removal alone — rejected by experiment 014; leaf placement can still imply a garland.
3. Experiment 015 mass test parser — rejected as evidence: exact failure was `Infinity` because it assumed stem spacing instead of positively locating leaves.
4. Experiment 016 — current: find all 128 leaf cards by non-stem color runs, tighten the same 12 left clusters into three semantic masonry zones, and reconstruct each existing seven-vertex petal as a broad overlapping oval lobe so the same 30 heads form three rounded rosette bouquets.

## Behavioral regression
`ArchReferenceGrowthMassBreakupPassTests.FinalPassBreaksDiagonalBandIntoMassesAndGathersReadableBouquetsAcrossRebuild` must recover exactly 128 leaves with finite metrics; materially compact/separate the three left zones; gather flower clusters; require readable bounded head radius, oval-petal roundness and depth; preserve mesh identity, 3 draws, <=4,096 vertices; and deterministically reapply after production rebuild.

## Blast radius / cost
ArchLookdev presentation only. Same meshes/topology/draws/vertex budget; one-shot vertex rewrite only, no per-leaf/flower GameObjects or per-frame geometry work.

## Remaining gates
Run the focused regression on the exact feature SHA through the existing `ci-test/fixes/agent-4` transport with 45-second replay. Accept only if green and direct saved-pose inspection shows separated organic masses plus rounded layered bouquets. Then record verification, open→pending metadata, pending→closed `fixed`/`resolvedUtc`, merge current master, and non-force push exact feature head to master.