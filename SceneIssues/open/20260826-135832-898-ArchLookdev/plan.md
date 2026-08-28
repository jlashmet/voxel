# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- One global 1928×836 `Hero Arch` pose, no circles. Match `References/arch_reference.png`: asymmetric masonry-supported English-ivy masses with individually readable overlapping leaves and delicate integrated warm-white/blush blossoms; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Saved-player inspection is the final gate.
- Green frames have repeatedly falsified metric-only fixes. `33149674980` exposed wrong topology; `33150510855` exposed corrupt leaf ranges/sliver; `33151026338` stayed thin/stamp-like; `33152121570` became denser but still angular/rosette-like. Organic run `33152767543` fixed hue/irregularity metrics but direct inspection still read as a narrow garland with merged pointed leaves and pale bouquet blobs.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected.
2. Architectural placement — corrected: springline `y=6.4 m`, crown `y=7.8 m`.
3. Mutable-color indexing — fixed: exact production ivy is 2,484 vertices / 77 authored stems.
4. More scale/density or hue alone — rejected by exact replay. Remaining defect is size-to-spacing ratio: leaves/heads merge before their authored count becomes visually legible.

## Selected fix / regression
After the exact and organic passes, `ArchReferenceGrowthReferenceFinishPass` rewrites the same buffers once: smaller softer leaves over a wider irregular support footprint, stronger dark/light green variation with near-neutral material multiplication, and smaller five-petal heads spread so all five blossoms remain distinct around each bouquet anchor.

`ArchReferenceGrowthReferenceFinishPassTests.ReferenceFinishSeparatesLeavesAndBlossomsAcrossRebuild` proves exact topology/no stems/slivers, bounded smaller leaf radius plus bushy spread/nearest-neighbor separation/green variance, small distinct bouquet heads, warm petals, unchanged 128/30/3-draw/<=4,096 budget, and rebuild stability.

## Blast radius / cost
ArchLookdev only. One bounded construction-time component/coroutine mutates existing mesh buffers; no new vertices, renderers, draws, per-leaf objects, shared vegetation truth, or steady-state work.

## Remaining gates
Exact-SHA targeted CI on existing `ci-test/fixes/agent-4`, then inspect the saved player frame. Only after both pass: commit accepted verification, open→pending with complete metadata, pending→closed with `fixed`/`resolvedUtc`, merge latest master, and non-force push exact feature head to master.
