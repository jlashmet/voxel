# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: global saved 1928×836 `Hero Arch` pose. Ivy must read as asymmetric masonry-supported lower/upper/crown English-ivy masses with visible individual overlapping leaves; flowers as integrated layered bouquets; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Automated geometry is necessary but not visual acceptance.
- Runs `33147763145`, `33148437517`, and `33149348379` were mechanically green but rejected by direct final-frame inspection. The latest frame materially improved the foliage mass/bouquets, but a long diagonal/vertical legacy stem still crossed the opening.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected: world-space hero meshes render and rebuild deterministically with required topology/budget.
2. Architectural placement — corrected by minimal repro 020: springline `y=6.4 m`, opening crown `y=7.8 m`; prior false crown was near `y=6.91 m`.
3. Leaf/bouquet readability — improved by experiment 021; remaining artifact is not placement.
4. Confirmed by experiment 022: color-based stem discovery is invalid after earlier passes mutate vertex colors. `BuildIvyMesh` has deterministic topology: 2,488 ivy vertices with exactly 78 four-vertex stem quads.

## Selected fix / regression
Keep the AAA leaf/bouquet composition, then use `ArchReferenceGrowthTopologyCleanupPass` to derive all 128 leaf starts and 78 stem starts directly from the production mesh layout and collapse every stem quad independent of color. `ArchReferenceGrowthTopologyCleanupPassTests.FinalTopologyCleanupRemovesAllStemQuadsWithoutRegressingAaaMassAcrossRebuild` proves all 78 stem spans are effectively zero, AAA support/crown/right distribution survives, counts/3 draws/<=4,096 vertices are unchanged, and rebuild is deterministic.

## Blast radius / cost
ArchLookdev presentation only. Existing ivy vertices are rewritten once; no shared vegetation/world truth, new renderers, vertices, per-leaf GameObjects, or steady-state geometry work.

## Remaining gates
Run the exact feature SHA through existing `ci-test/fixes/agent-4` with 45-second replay. Accept only if the focused regression is green and direct saved-pose inspection meets the AAA reference bar. Then commit the accepted verification frame, promote to pending with complete metadata, move pending→closed with `fixed`/`resolvedUtc`, merge latest master, and non-force push exact head to master.
