# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- One global 1928×836 `Hero Arch` pose, no circles. Match `References/arch_reference.png`: lush asymmetric masonry-grown English ivy with readable overlapping leaves and delicate integrated warm-white/blush blossoms; right masonry stays sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, and two ground ferns. Saved-player inspection is the final gate.
- Green runs repeatedly falsified metric-only fixes. `33151026338` removed corrupt slivers but stayed thin; `33152121570` became denser but icon-like; `33152767543` improved organic detail but stayed a garland; `33153293557` separated leaves/heads yet direct replay still showed a sparse diagonal chain.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected.
2. Architectural frame — corrected: springline `y=6.4 m`, crown `y=7.8 m`.
3. Mutable-color indexing — fixed: exact ivy is 2,484 vertices / 77 authored stems.
4. Leaf/head size, hue, and local spacing — improved but insufficient; exact replay proves the remaining defect is **macro composition** caused by one cluster per sequential arch support.

## Selected fix / regression
`ArchReferenceGrowthSemanticMassPass` preserves the accepted exact leaf/flower geometry and only translates existing groups. It derives three semantic centres from authored supports: lower pier (0–4), upper haunch (5–9), crown (10–14); packs five clusters into each broad mass; keeps cluster 15 as the one sparse right accent; and embeds two existing five-head bouquets in each left mass.

`ArchReferenceGrowthSemanticMassPassTests.SemanticMassPassBuildsThreeMasonryMassesAcrossRebuild` proves the three mass envelopes and deliberate inter-mass gaps, wide crown sweep, integrated bouquets, exact 2,484/77 no-stem/no-sliver topology, unchanged 128/30/3-draw/<=4,096 budget, and rebuild stability.

## Blast radius / cost
ArchLookdev only. One bounded construction-time translation pass over existing mesh buffers; no new topology, renderers, draws, material ownership, per-leaf objects, or steady-state work.

## Remaining gates
Run exact feature SHA on existing `ci-test/fixes/agent-4` with saved-pose replay; accept only if focused regression is green **and** direct frame inspection clears the AAA reference bar. Then commit verification, open→pending with complete metadata, pending→closed with `fixed`/`resolvedUtc`, merge latest master, and non-force push exact head to master.
