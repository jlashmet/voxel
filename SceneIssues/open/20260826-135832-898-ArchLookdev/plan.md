# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: global saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported lower/upper/crown English-ivy masses with visible overlapping leaves; flowers as integrated layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Geometry tests are necessary but the saved player frame is the final visual gate.
- Runs `33147763145`, `33148437517`, and `33149348379` were green but visually rejected. `33149674980` exposed the wrong 2,488/78 topology model; `33150510855` exposed a 0.4398 m exact-cluster displacement plus an opening-spanning sliver. `33151026338` was green after exact leaf/stem repair and removed the sliver, but its player frame was still rejected because the result read as a thin chain of flat ivy cutouts with tiny flower stamps.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected: world-space hero meshes render/rebuild with required counts/budget.
2. Architectural placement — corrected by repro 020: springline `y=6.4 m`, opening crown `y=7.8 m`.
3. Mutable-color indexing — confirmed/fixed by exact topology: production ivy is 2,484 vertices / 77 stems because global cluster 13 / leaf 2 has a <0.01 m stem suppressed by `AddStem`.
4. Remaining visual defect — representation density/depth, not topology: exact frame from `33151026338` had correct placement but insufficient leaf envelope/size/depth and undersized blossoms.

## Selected fix / regression
Keep exact topology authoritative. Final pass rebuilds all 128 real leaf polygons with broader support envelopes, larger overlapping silhouettes, depth/tilt/value variation, collapses all 77 real stems, and scales/repositions the same 30 flower heads into compact masonry-anchored bouquets. No topology/draw increase.

`ArchReferenceGrowthFinalPresentationTests.FinalPresentationMaintainsLayeredIvyAndIntegratedBouquetsAcrossRebuild` is the final behavioral falsifier: exact 2,484/77 topology, zero stem span, max ivy triangle edge <0.30 m, support-centroid fidelity, average leaf radius >0.165 m, average leaf-centre spread >0.30 m, average flower-head radius >0.19 m, bouquet anchor offset <0.22 m, unchanged 128 leaves / 30 heads / 3 draws / <=4,096 vertices, and the same constraints after rebuild.

## Blast radius / cost
ArchLookdev presentation only. Existing ivy/flower vertices are rewritten once; no shared vegetation/world truth, new renderers, vertices, per-leaf GameObjects, or steady-state geometry work.

## Remaining gates
Run the exact feature SHA through the existing `ci-test/fixes/agent-4` transport with 45-second replay. Accept only if the focused presentation regression is green and direct saved-pose inspection meets the tracked reference bar. Then commit the accepted verification frame, promote open→pending with complete metadata, close pending→closed with `fixed`/`resolvedUtc`, merge latest master, and non-force push the exact feature head to master.
