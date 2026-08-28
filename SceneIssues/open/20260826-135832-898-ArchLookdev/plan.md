# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: one global 1928×836 `Hero Arch` pose. Match `References/arch_reference.png`: asymmetric masonry-supported English-ivy masses with overlapping depth and delicate integrated warm-white/blush blossoms; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Saved-player inspection is the final gate.
- Prior green frames repeatedly falsified metric-only fixes. `33149674980` exposed wrong topology; `33150510855` exposed corrupted leaf ranges/sliver; `33151026338` removed the sliver but remained thin/stamp-like. `33152121570` passed stronger density/flower metrics, but direct inspection still showed repeated angular leaf cards and oversized lavender radial rosettes.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected: world-space meshes render/rebuild with required counts/budget.
2. Architectural placement — corrected: springline `y=6.4 m`, crown `y=7.8 m`.
3. Mutable-color indexing — fixed: exact production ivy is 2,484 vertices / 77 authored stems.
4. More size/density alone — rejected by `33152121570`; remaining defect is silhouette/depth/color language.

## Selected fix / regression
Keep exact topology authoritative, then run `ArchReferenceGrowthOrganicFinishPass` once after topology cleanup. It repacks each support into a compact overlapping core, rewrites the same 17-vertex leaves as softer seed-varied asymmetric English ivy with depth tilt, and reconstructs each existing five-petal head as irregular overlapping warm-white/blush petals at smaller bouquet scale.

`ArchReferenceGrowthOrganicFinishPassTests.OrganicFinishBuildsOverlappingIvyAndDelicateBouquetsAcrossRebuild` proves exact topology/no slivers, support fidelity, bounded leaf radius/spread with overlap ratio >0.80, delicate flower radius/anchor bounds, warm petal bias, unchanged 128/30/3-draw/<=4,096 budget, and rebuild stability.

## Blast radius / cost
ArchLookdev only. One extra construction-time component/coroutine mutates existing ivy/flower buffers once; no shared vegetation truth, vertices, renderers, draw calls, per-leaf objects, or steady-state work.

## Remaining gates
Exact-SHA targeted CI on existing `ci-test/fixes/agent-4`, then inspect the saved player frame. Only after both pass: commit accepted verification, open→pending with complete metadata, pending→closed with `fixed`/`resolvedUtc`, merge latest master, and non-force push exact feature head to master.
