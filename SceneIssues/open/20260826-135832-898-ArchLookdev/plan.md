# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported left/crown masses with overlapping English-ivy leaves and a few drapes; blossoms must read as rich clustered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 flower heads, 3 combined draws, <=4,096 vertices, and the two semantic ground ferns.
- Green geometry is not visual proof. Run `33142488637` passed experiment 014, but its inspected real-player `verification-final.png` still shows one continuous diagonal foliage band and repeated tiny five-petal flower icons, so it is rejected.

## Proven causes / discriminator
1. **Missing render data / camera placement — rejected or fixed earlier.** Authored meshes render at the saved pose and world-space lifecycle anchoring survives rebuilds.
2. **Removing visible stems is sufficient — rejected by experiment 014.** The stem-free mesh still reads as a garland because adjacent leaf-cluster centres continue sampling the entire path at nearly uniform intervals.
3. **Ten small flower clusters are sufficient — rejected visually.** Even with depth and tighter three-head clusters, the saved pose reads them as repeated icons rather than bouquets.
4. **Experiment 015 — current.** Contract the existing left clusters into lower-pier, upper-pier, and crown zones around their generated zone centroids, creating measurable negative-space breaks without absolute capture coordinates. Gather the same 30 flower heads into three bouquet zones and increase head screen presence while retaining topology/depth.

## Behavioral regression
`ArchReferenceGrowthMassBreakupPassTests.FinalPassBreaksDiagonalBandIntoMassesAndGathersReadableBouquetsAcrossRebuild` requires materially lower within-zone ivy/flower-cluster spread, >0.20-unit negative-space breaks between the three left foliage zones, >20% larger blossom heads with retained depth, unchanged mesh identities/3 draws/<=4,096 vertices, and deterministic production rebuild reapplication.

## Blast radius / cost
ArchLookdev presentation only; shared vegetation/world truth unchanged. Same meshes, topology, draw count, and vertex budget. One additional one-shot mesh translation/scale pass; no per-frame geometry work or per-leaf/flower GameObjects.

## Remaining gates
Run the focused regression on the new exact feature SHA through the existing `ci-test/fixes/agent-4` transport with the original 45-second replay. Accept only if the test is green and direct saved-pose inspection shows distinct masses/negative space and readable bouquets; then record verification, promote open→pending, close with `fixed`/`resolvedUtc`, merge current master, and non-force push the exact feature head to master.
