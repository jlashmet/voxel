# Experiment 003 — live district terrace owner

## Hypothesis
The remaining jagged Dirt/grass geometry comes from the live district-terrace shoulders, not the road-shoulder catalogue changed in the first attempt.

## Action and source
At feature head after syncing current master, traced VoxelShowcase composition through `ShowcaseCatalogue` → `WorldBuilderVoxelCatalogue` → `KentridgeCombinedVoxelCatalogue`, then inspected the active terrain programs and the prior exact replay. `KentridgeDistrictTerraceCatalogue.AddShoulder` emits six Box slices across every non-flat shoulder; urban shoulders span 72 dm. `KentridgeTownSurfaceCatalogue` and `RegionCorridorCatalogue` are not part of this scene's live combined path.

## Result
The live terrace owner produces fixed-width authored plateaus (about 12 dm per urban shoulder step) that match the scale and rectangular character of the upper marked notch. The prior replay was residency-stable, so streaming/LOD churn does not explain the geometry.

## Verdict
Supported. Replace the six shoulder Fill boxes with the engine's existing reversible `EmitRamp`, preserving the same endpoints/materials and leaving retaining tiers and paint passes unchanged. Restore the inactive first-attempt production change and replace its current-issue structural test with a behavioral regression through the live terrace catalogue plus `BoxEmitter.RampContains`.

## Falsifier / next gate
If exact-SHA targeted CI is green but the exact saved camera still shows either marked notch/step, this ownership hypothesis is incomplete and the issue remains open for another discriminator rather than being promoted.
