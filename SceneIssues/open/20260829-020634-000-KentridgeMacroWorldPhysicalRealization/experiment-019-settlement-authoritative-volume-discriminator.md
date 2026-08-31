# Experiment 019 — settlement authoritative-volume discriminator

## Trigger

Exact feature source `9d51fb9a947af76d0b8005c35288a7007dd6d9e6` passed the corrected presentation-readiness regression through CI run `33354287850`, yet the same run's full-resolution built-player captures remain closure-red:

- `macro-rossdam.png` still exposes essentially one unmistakable complete/readable building rather than the four generic settlement blockouts required by acceptance.
- `macro-moordell.png` likewise has one clear gabled structure plus low/partial-looking forms rather than four convincingly complete structures.
- `macro-rossdam-lake-detour.png` still reads as a thin distant water strip rather than a substantial lake.
- Fairy/Orc/ridge/network captures are still absent before the unchanged 60 s cutoff.

The readiness race isolated in experiment 018 is now proven fixed, so another presentation/readiness change would be an unsupported third attempt at the surviving settlement symptom.

## Established production intent

The shared `TopDownWorldBuildingBlockoutPlan` authors four generic buildings around a settlement centre with distinct heights and real foundation/timber/gable programs. The production Kentridge path uses `KentridgeCombinedVoxelCatalogue`; this is not intended to be a one-building settlement or flat placeholder.

## Competing hypotheses

1. **Authoritative volumes are correct.** All four production building volumes contain the expected authored non-terrain structure voxels with meaningful vertical extent, so the defect is downstream in render publication/culling/framing.
2. **Voxelization/catalogue is incomplete.** Fewer than four expected building volumes receive structure voxels, indicating a planner/program/catalogue/rasterization defect.
3. **Grounding/placement is wrong.** Four features are authored but their occupied vertical ranges are materially buried, floating, or displaced relative to the semantic settlement/building bounds, so only one reads above terrain.

## Next discriminator

Add the smallest PlayMode regression that uses the real production semantic settlement/catalogue data rather than synthetic building geometry. For a production settlement such as Rossdam, enumerate the four expected semantic generic building definitions/placements, drive normal production generation for their occupied regions, and inspect authoritative voxel storage inside each expected building volume.

For every expected building, record/count:

- authored non-terrain structure-material voxels,
- occupied minimum/maximum Y and resulting vertical span,
- semantic placement/footprint bounds,
- relationship of occupied lower/upper Y to the intended grounded placement.

The test should fail distinctly when an expected building volume is empty or lacks meaningful above-ground vertical occupancy. If all four authoritative volumes are healthy, stop modifying voxelization/readiness and investigate render publication/framing. If the volumes are absent or vertically misplaced, fix only that identified production owner.

## Guardrails

- No hardcoded fake settlement geometry.
- No widened streaming radius/budget, prestreaming, or replay extension.
- No camera/framing change until authoritative volume correctness is known.
- No World Building Gallery dependency; final evidence remains the production `KentridgePlayableSlice`/Kentridge module validation path.
