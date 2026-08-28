# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: global saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported lower/upper/crown English-ivy masses with visible individual overlapping leaves; flowers as integrated layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Automated geometry is necessary but not visual acceptance.
- Runs `33147763145`, `33148437517`, and `33149348379` were green but rejected by direct frame inspection. Run `33149674980` exposed the wrong 2,488/78 topology model; run `33150510855` exposed a 0.4398 m exact-cluster displacement and its replay still showed a frame-spanning ivy sliver.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected: world-space hero meshes render/rebuild with required counts/budget.
2. Architectural placement — corrected by repro 020: springline `y=6.4 m`, opening crown `y=7.8 m`.
3. Leaf/bouquet readability — materially improved by experiment 021.
4. Confirmed by experiment 022: mutable-color discovery can corrupt real leaf ranges before cleanup. Production ivy is **2,484 vertices / 77 stems** because global cluster 13 / leaf 2 has a <0.01 m stem suppressed by `AddStem`.

## Selected fix / regression
Final topology pass treats exact indices as authority: rebuild all 128 actual leaf polygons at the AAA supports, then collapse all 77 actual stem quads. `ArchReferenceGrowthTopologyCleanupPassTests.FinalTopologyCleanupRemovesAllStemQuadsWithoutRegressingAaaMassAcrossRebuild` proves 2,484/77 topology, zero stem span, support/crown/right distribution, **max ivy triangle edge <0.30 m**, unchanged 128 leaves / 30 heads / 3 draws / <=4,096 vertices, and deterministic rebuild.

## Blast radius / cost
ArchLookdev presentation only. Existing ivy vertices are rewritten once; no shared vegetation/world truth, new renderers, vertices, per-leaf GameObjects, or steady-state geometry work.

## Remaining gates
Run exact feature SHA through existing `ci-test/fixes/agent-4` with 45-second replay. Accept only if focused regression is green and direct saved-pose inspection meets the AAA reference bar. Then commit accepted verification, promote to pending with complete metadata, move pending→closed with `fixed`/`resolvedUtc`, merge latest master, and non-force push exact head to master.
