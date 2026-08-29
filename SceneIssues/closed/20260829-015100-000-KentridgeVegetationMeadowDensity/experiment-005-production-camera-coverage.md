# Experiment 005 — production camera coverage

## Competing hypotheses
1. The packed grass exists and animates, but Kentridge's bounded semantic-grass budget is exhausted outside the required opening camera frustum.
2. Grass roots are inside the required frustum, but normal packed-render submission fails to reach the Kentridge camera.
3. Grass is submitted inside the frustum but remains unreadable because of geometry/depth/material behavior.

## Discriminator and pre-fix result
Load the production `KentridgePlayableSlice`, read generated `_undergrowth`, and test root-cluster bounds against the real `Kentridge Player Camera` frustum. Kentridge begins countryside sampling at Z=122 m. With the former 0.4 m grid, a 90 m strip has 226 X cells per row; the unchanged 12,000-sample cap therefore reaches only about Z=143.2 m. The required replay camera is around Z=150 m looking +Z. Hypothesis 1 is confirmed: the capped meadow was generated behind the required view.

This also explains experiment 004: raising every grass root by one voxel changed zero foreground pixels because there was no grass in front of the captured camera. The exposed-top-face grounding remains the correct surface contract and is independently asserted, but it was not the cause of the visibility failure.

## Causal correction
Keep `MaxUndergrowth=12000` and density `0.96`, but author Kentridge at a still-dense 0.8 m sample grid. The unchanged cap now spans roughly 85 m of countryside and crosses the required opening view without increasing the semantic-instance safety budget or introducing new draw topology.

`KentridgeMeadowPlayerVisibilityTests.OpeningPlayerCamera_FrustumContainsDenseProductionGrass` uses the production scene, production positions, and real camera. Final exact-source run `33249542767` reports 11,322 grass roots in front of the camera, 3,664 root clusters inside its frustum, furthest-forward grass 116.02 m, and max grass Z=218.80 m.

## Final built-player result
Final source `ec92c3002a6b75ca86de7819f4175c5390a1ca2b`, request `d71730e46c2e12bc81e8c6e58cb87c07525904e3`, workflow `33249542767`: focused test, player build, 60-second exact-scene replay, preview generation, artifact upload, and `ci/single-test` all pass. The player reports 11,322 semantic grass instances / 113,490 blades, with 57,752 blades in the primary connected meadow, 16 chunks, and zero excluded-surface leakage.

Direct inspection of stationary t=39.8s, 49.8s, and 59.8s frames shows dense player-height procedural grass and visibly changing blade silhouettes. Grass-band pixel deltas are 42.89% (39.8→49.8s) and 44.08% (49.8→59.8s) at a >5 RGB-channel threshold with sky/dialogue excluded. Hypotheses 2 and 3 are falsified for the final source.

## Cost / blast radius
The production correction is Kentridge WorldBuilder authoring only (0.4→0.8 m spacing); shared placement/rendering budgets remain capped. Final player build is 157 MB / 36.270 s, wrapper peak RSS is 6,136 MB, and ordinary captured play after warmup is about 60–73 FPS before the held stationary phase. The harness does not expose separate CPU-ms/GPU-ms counters, so none are inferred.
