# Experiment 005 — production camera coverage

## Competing hypotheses
1. The packed grass exists and animates, but Kentridge's bounded semantic-grass budget is exhausted outside the required opening camera frustum.
2. Grass roots are inside the required frustum, but normal `ProceduralVegetationBatchRenderer` submission fails to reach the Kentridge camera.
3. Grass is submitted inside the frustum but remains unreadable because of geometry/depth/material behavior.

## Smallest discriminator
Trace the exact production sampling bounds and cap, then add a production-scene regression that loads `KentridgePlayableSlice`, reads the actual generated `_undergrowth`, uses the real `Kentridge Player Camera`, and counts grass-root cluster bounds intersecting that camera's frustum. This uses production positions and the real replay camera; global blade counts are not accepted as visibility evidence.

## Pre-fix result — hypothesis 1 confirmed
Kentridge calls `Populate` from `(TownCentreDm.Y + 700) * 0.1 = 122 m` toward Hightown. `BuildUndergrowth` samples a 90 m-wide strip (`coverHalfWidth=45`) and stops when `_samples.Count == MaxUndergrowth` (12,000). With the former authored 0.4 m spacing, the X row contains `floor(90 / 0.4) + 1 = 226` cells. The cap therefore permits only 53 complete rows plus 22 cells of the next row, so the furthest sampled Z is about `122 + 53 * 0.4 = 143.2 m`.

The required opening replay camera is at about Z=150 m and looks toward positive Z. Thus the entire capped dense meadow was generated behind the camera. This also explains the previous built-player evidence: moving every root upward by one voxel changed zero foreground pixels because no packed grass roots occupied the captured forward view in the first place.

## Causal correction
Keep the existing 12,000 semantic-instance safety budget and 0.96 occupancy density, but author Kentridge at a still-dense 0.8 m sample grid. The same X row is now 113 cells, so the unchanged cap reaches roughly `122 + 106 * 0.8 = 206.8 m`, extending more than 50 m into the required opening view without multiplying semantic instances, grass blades, per-blade GameObjects, or draw topology.

A new `KentridgeMeadowPlayerVisibilityTests.OpeningPlayerCamera_FrustumContainsDenseProductionGrass` regression requires the production scene to place hundreds of grass roots in front of the real camera and at least 128 packed-root cluster bounds in its frustum. The existing isolated production-shader repro still proves time-varying deformation; the final built-player replay remains the authority for visible meadow density and wind.

## Cost / blast radius
The production change is Kentridge WorldBuilder authoring only: sample spacing 0.4 m -> 0.8 m. `MaxUndergrowth=12000`, `VegetationDensity=0.96`, grass blade expansion (5–15), packed renderer/shader, exclusions, and ambient/tree allowlists are unchanged. The expected semantic/blade budget therefore remains approximately the prior ~12k semantic instances / ~115k rendered blades while covering a larger contiguous Z span. Final CI/player evidence must confirm actual counts, chunk count, FPS/memory, zero exclusion leakage, in-frustum coverage, and visible wind before closure.
