# Experiment 032 — perimeter plinth static work cost

## Question
What bounded raster-work reduction is guaranteed by the selected generic-building shell geometry independent of the renderer/publication blocker?

## Source
Feature branch production method `TopDownWorldPhysicalVoxelCatalogue.BuildingProgram` and its independent synthetic regression `GenericBuildingBlockoutUsesBoundedFoundationAndWallShellsInsteadOfSolidVolumes`.

The regression fixture uses `width=100`, `depth=80`, `height=60`, `scale=1`, zero terrain relief. Production constants are foundation inset 6 dm, normal foundation height 8 dm, and wall thickness 4 dm. The selected implementation derives a perimeter foundation thickness of 10 voxels and emits four non-overlapping perimeter boxes around a hollow centre.

## Static calculation
Former solid foundation slab:
- footprint: `(100 + 12) x (80 + 12) = 112 x 92`
- height: `8`
- volume/work bound: `112 * 92 * 8 = 82,432` voxels

Current four-box perimeter plinth:
- front/back: `2 * (112 * 8 * 10) = 17,920`
- left/right between them: `2 * (10 * 8 * 72) = 11,520`
- total: `29,440` voxels
- reduction from former slab: `52,992` voxels = `64.3%`
- current work is `35.7%` of the former solid slab

Former solid timber body:
- `100 * (60 - 8) * 80 = 416,000` voxels

Current four-wall timber shell:
- front/back: `2 * (100 * 52 * 4) = 41,600`
- left/right between them: `2 * (4 * 52 * 72) = 29,952`
- total: `71,552` voxels
- reduction from former solid body: `344,448` voxels = `82.8%`
- current work is `17.2%` of the former solid body

For this independent fixture, foundation + timber authored box volume therefore falls from `498,432` to `100,992` voxels, a reduction of `397,440` voxels (`79.7%`). Roof work is unchanged and excluded from both sides.

## Interpretation
This is deterministic static work-volume evidence for the reusable generic-blockout geometry and supports the demonstrated throughput fix without depending on GPU publication. It does not replace exact-SHA execution of the behavioral regression, actual settlement dimensions/terrain relief, player CPU/GPU/memory telemetry, or final multi-target convergence evidence. Those remain required and blocked by the pre-merge renderer compatibility gate.
