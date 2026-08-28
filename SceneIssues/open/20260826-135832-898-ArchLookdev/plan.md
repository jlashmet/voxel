# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- One 1928×836 `Hero Arch` pose, no circles. Match `References/arch_reference.png`: lush asymmetric masonry-grown English ivy with overlapping leaves and delicate integrated warm-white/blush blossoms; right masonry sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, two ground ferns. Saved-player inspection is the final gate.
- Green exact runs repeatedly falsified metric-only fixes. `33153293557` still read as a garland. `33154793211` proved three semantic masses, but direct replay showed lower/haunch growth spilling into the opening and the crown mass floating below the ring.

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected.
2. Architectural frame — springline `y=6.4 m`, crown `y=7.8 m` confirmed.
3. Mutable-color indexing — fixed: exact ivy is 2,484 vertices / 77 stems.
4. Local shape/spacing and macro grouping — improved/proven but insufficient.
5. Current: **surface attachment**. Inner-edge semantic supports must be projected outward onto masonry before the reference composition can read physically grounded.

## Selected fix / regression
`ArchReferenceGrowthSemanticMassPass` keeps the three derived left masses (lower pier 0–4, upper haunch 5–9, crown 10–14), one sparse right accent, and two bouquets per left mass. Lower/haunch targets shift 0.34 m left onto the stone face. Crown targets project 0.34 m radially outward from the authored opening springline onto the ring.

`ArchReferenceGrowthSemanticMassPassTests.SemanticMassPassBuildsThreeMasonryMassesAcrossRebuild` proves mass envelopes/gaps/crown sweep, lower+haunch `x < -1.45`, crown foliage/blossoms `y > 7.90`, exact 2,484/77 no-stem/no-sliver topology, unchanged 128/30/3-draw/<=4,096 budget, and rebuild stability.

## Blast radius / cost
ArchLookdev only. One construction-time translation over existing buffers; no new topology, renderers, draws, materials, per-leaf objects, or steady-state work.

## Remaining gates
Exact-SHA CI on existing `ci-test/fixes/agent-4`, then direct saved-frame inspection. Only if both pass: commit verification; open→pending with complete metadata; pending→closed with `fixed`/`resolvedUtc`; merge latest master; non-force push exact head to master.
