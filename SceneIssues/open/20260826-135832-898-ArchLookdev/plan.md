# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: global saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported lower/upper/crown English-ivy masses with visible individual overlapping leaves; flowers as integrated layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Automated geometry is necessary but not visual acceptance.
- Runs `33147763145`, `33148437517`, and `33149348379` were mechanically green but rejected by direct final-frame inspection; latest green frame still had one long legacy stem. Exact-topology run `33149674980` then failed before cleanup because the first topology model expected a non-existent stem quad.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected: world-space hero meshes render and rebuild deterministically with required counts/budget.
2. Architectural placement — corrected by repro 020: springline `y=6.4 m`, opening crown `y=7.8 m`; prior false crown was near `y=6.91 m`.
3. Leaf/bouquet readability — materially improved by experiment 021; remaining artifact is a stem-cleanup defect.
4. Confirmed by experiment 022: color discovery is invalid after prior passes mutate colors, and production `AddStem` suppresses one <0.01 m leaf stem. Actual deterministic ivy topology is **2,484 vertices / 77 stem quads**, not 2,488 / 78.

## Selected fix / regression
`ArchReferenceGrowthTopologyCleanupPass` models the real 12-left + 4-right path layout, including the single omitted global-cluster-13/leaf-2 stem, and collapses all 77 authored stem quads by exact index. `ArchReferenceGrowthTopologyCleanupPassTests.FinalTopologyCleanupRemovesAllStemQuadsWithoutRegressingAaaMassAcrossRebuild` proves exact vertex/stem counts, zero authored stem span, preserved AAA supports/crown/right distribution, unchanged 128 leaves / 30 heads / 3 draws / <=4,096 vertices, and deterministic rebuild.

## Blast radius / cost
ArchLookdev presentation only. Existing ivy vertices are rewritten once; no shared vegetation/world truth, new renderers, vertices, per-leaf GameObjects, or steady-state geometry work.

## Remaining gates
Run exact feature SHA through existing `ci-test/fixes/agent-4` with 45-second replay. Accept only if focused regression is green and direct saved-pose inspection meets the AAA reference bar. Then commit accepted verification, promote to pending with complete metadata, move pending→closed with `fixed`/`resolvedUtc`, merge latest master, and non-force push exact head to master.
