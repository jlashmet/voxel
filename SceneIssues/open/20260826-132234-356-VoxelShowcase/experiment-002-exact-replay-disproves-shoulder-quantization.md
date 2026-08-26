# Experiment 002 — exact replay disproves shoulder quantization

## Hypothesis
Replacing the five coarse grassy shoulder bands with thirty one-decimetre bands would remove the captured jagged Dirt/grass boundary because the visible defect was caused primarily by cross-slope shoulder quantization.

## What was performed
Ran the focused regression for `SceneIssue20260826132234356RoadsUseVoxelGranularGrassTransitions` against production fix commit `cba68a478ede735e6bdf6aa85b8a1803bccfd133`; the targeted CI request commit `3f20fb1b865477bf841dce87720dab75416ca976` received `ci/single-test: success` in Actions run `32998642785`. Then, at source commit `9b9a36ed5372a459887ba3722b2f0b096164b2d5`, a one-shot self-hosted workflow freshly baked VoxelShowcase and replayed the exact saved SceneIssue camera/1928x836 fixture for 75 seconds. Actions run `33000900696` completed successfully and uploaded replay artifact `9618929575`. The final replay image was manually inspected against both recorded circles.

## Result
The structural regression passes, and replay telemetry was stable (`visible=714`, `missingMax=0`, roughly 238–280 FPS in the tail), but the visual defect is **not fixed**. The upper circled region still shows a large stepped/notched grass intrusion along the Dirt boundary. The lower circled boundary is cleaner, but the capture acceptance criterion requires both marked regions to be resolved.

## What was learned
**Hypothesis disproven as the complete root cause.** Cross-slope shoulder quantization was real and the new profile removes that coarse 0.4 m stair pattern, but it does not explain the plan-view notch visible in the upper circle. The remaining defect is more consistent with the lateral road envelope, an approach/corridor surface, segment/intersection geometry, or an overlapping grass-surface catalogue.

## Next
Keep the issue open. Recompute the saved camera rays and trace the surface catalogue responsible for the world-space locations under both circles, especially `RegionCorridorCatalogue` and catalogue-composition order. Do not make another Kentridge public-street shoulder change until ownership of the marked geometry is established.
