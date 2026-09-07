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

## First discriminator attempt — selector-red, not production-red

Feature source `aa4952b5706a9bf706ff9b955d36aa39f43a6819`, CI wrapper `bcb41fb7edfb13b3ee14878ba232f87d66e609ce`, run `33357649096` completed with the focused test red. The failure was `matchedBuildings = 0` before any authoritative voxel probe ran. This does **not** classify the production volume hypotheses.

Root cause is in the regression selector: it attempted to discover Rossdam by a `FeatureDefinition.Name` prefix. Production catalogue authoring derives definition names from `settlement.Node.Id`, but that fixed-size catalogue name is not the semantic membership API and must not be used as the authoritative settlement lookup surface.

## Corrected discriminator

Use the same semantic/production data path that authors the catalogue:

1. Build the source-backed layout and `KentridgeTopDownWorldPhysicalIntent`.
2. Build the production `TopDownWorldPhysicalPlan` and obtain Rossdam with `TryGetSettlement(KentridgeTopDownWorldLayout.Rossdam, ...)`.
3. Require its four real `TopDownWorldBuildingBlockoutPlan` entries.
4. Build the real combined catalogue and match each planned building to exactly one `FeatureKind.Structure` explicit placement by the exact X/Z centre used by production authoring; do not inspect definition names.
5. Generate every 3D region intersecting that placement's production footprint through normal `FeatureGeneration.GenerateRegion` into authoritative `RegionTable`/`BrickPool` storage.
6. Bounded-scan that footprint for the production foundation/timber/roof materials and record occupied voxel count, min/max Y, and vertical span.
7. Require non-empty structure occupancy, occupied minimum Y equal to the production grounded placement, and vertical span at least the semantic planned wall height.

The corrected test is committed at `74001f9d98041eb56dbce310f8caa1341221ef90`. It remains a discriminator only; no production world generation, rendering, streaming radius/budget, camera, or replay duration changed.

## Classification rule

If all four corrected authoritative volumes are healthy, stop modifying voxelization/readiness and investigate downstream render publication/framing. If any volume is absent or vertically misplaced, fix only that identified production owner. A compile/test-construction failure must be fixed as a regression defect and is not evidence for either production hypothesis.

## Guardrails

- No hardcoded fake settlement geometry.
- No widened streaming radius/budget, prestreaming, or replay extension.
- No camera/framing change until authoritative volume correctness is known.
- No World Building Gallery dependency; final evidence remains the production `KentridgePlayableSlice`/Kentridge module validation path.
