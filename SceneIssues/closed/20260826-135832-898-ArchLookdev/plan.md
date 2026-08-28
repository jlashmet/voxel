# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- One 1928×836 `Hero Arch` pose, no normalized circles. Match `References/arch_reference.png`: asymmetric masonry-grown ivy, overlapping dimensional leaves, integrated warm-white/blush blossoms, sparse right masonry.
- Preserve 128 leaves, 30 heads, 3 foliage draws, <=4,096 vertices, and two ground ferns.
- Replay `33154793211` falsified mass grouping alone: foliage floated in the opening. Run `33155444806` proved masonry attachment but exposed pale/flat coverage and crown bouquets at 0.819 m (>0.70 m).

## Hypotheses / discriminator
1. Camera/lifecycle/count — rejected by captured replay and stable topology.
2. Architectural attachment — confirmed: lower/haunch growth belongs on left stone; crown growth on the ring.
3. Missing geometry — rejected; required topology exists.
4. Renderer-facing coverage/relief/material response plus crown spacing — confirmed by the prior player frame and focused regression.

## Selected fix / regression
`ArchReferenceGrowthSemanticMassPass` tightens the crown bouquet offsets before the existing masonry projection. `ArchReferenceGrowthFinalPresentationPass` preserves semantic centres while increasing overlap/Z relief of existing leaves, slightly enlarging existing flower heads, and restoring saturated green/blush non-emissive material response.

`ArchReferenceGrowthFinalPresentationPassTests.FinalPresentationKeepsMasonryAttachmentAndBuildsLayeredBotanicalReadAcrossRebuild` exercises the production rebuild and proves masonry anchors, crown integration, increased coverage/depth, material contrast, unchanged 128/30 topology, unchanged renderer count, and rebuild stability.

## Blast radius / cost
ArchLookdev only. One bounded construction-time rewrite of existing hero mesh/material buffers; no new leaves, flower heads, renderers, draws, per-leaf objects, or steady-state work.

## Verification
Tested source: `c4314743b035df3d9ae7fb48f072b8bcfbd39cff`. Exact request `be282c9874fc1f8975b03258a32449da63eb26ce` directly parents it and changes only `.github/test-request.json`. Run `33157360491` passed the focused PlayMode regression and standalone saved-pose replay. Direct inspection of `RealPlayer/verification-final.png` confirms green masonry-attached left/crown growth, integrated distributed blossoms, sparse right masonry, and an unobscured arch opening. Closed after exact-SHA verification and final player inspection.
