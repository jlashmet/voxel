# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- One 1928×836 `Hero Arch` pose, no circles. Match `References/arch_reference.png`: lush asymmetric masonry-grown English ivy with overlapping dimensional leaves and delicate integrated warm-white/blush blossoms; right masonry sparse.
- Preserve 128 leaves, 30 heads, 3 draws, <=4,096 vertices, two ground ferns. Saved-player inspection is the final gate.
- Exact replay `33154793211` falsified mass grouping alone: foliage floated inside the opening. Run `33155444806` proved the masonry attachment visually, but its frame remained sparse/pale/flat and its regression caught crown bouquets at 0.819 m (>0.70 m).

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected.
2. Architectural frame/attachment — confirmed: lower/haunch growth is on left stone; crown is on the ring.
3. Topology/stems/slivers — fixed and bounded.
4. Current: **renderer-facing coverage/relief/material contrast** plus the measured crown bouquet spacing. Keep semantic anchors fixed and change only existing leaf/head presentation.

## Selected fix / regression
`ArchReferenceGrowthSemanticMassPass` tightens the two crown bouquet offsets to +/-0.20 m before the existing radial masonry projection. `ArchReferenceGrowthFinalPresentationPass` then preserves every semantic cluster centre while expanding the existing non-right leaves 22-28%, amplifying their existing Z relief/normals, enlarging existing heads 14%, and restoring green/blush material multipliers.

`ArchReferenceGrowthFinalPresentationPassTests.FinalPresentationKeepsMasonryAttachmentAndBuildsLayeredBotanicalReadAcrossRebuild` proves masonry anchors, integrated crown bouquets, bounded leaf radius/depth/triangle size, green/blush material contrast, collapsed-stem topology, unchanged 128/30/3-draw/<=4,096 budget, and rebuild stability.

## Blast radius / cost
ArchLookdev only. One bounded construction-time rewrite of existing hero buffers/materials; no new hero topology, renderers, draws, per-leaf objects, or steady-state work.

## Remaining gates
One final exact-SHA targeted CI request on existing `ci-test/fixes/agent-4`, then direct saved-player frame inspection. Only if both pass: commit accepted verification; open→pending with complete metadata; pending→closed with `fixed`/`resolvedUtc`; merge latest master; non-force push exact feature head to master.
