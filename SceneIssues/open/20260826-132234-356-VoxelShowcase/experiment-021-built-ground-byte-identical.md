# Experiment 021 — built ground is byte-identical across rejected Kentridge hypotheses

## Question
After repeated source-level fixes passed focused regressions but failed the exact `VoxelShowcase` screenshot, do the organic-route or generated-plot primitives actually own either annotated Dirt/grass contact in the built player?

## Minimal reproduction
Use retained full-resolution `1928×836` real-player artifacts from forced-bake exact-camera replays, then compare pixels only on rendered ground and inside the two immutable issue circles.

Compared sources/runs include:

- workflow `33206033751`, source `636f6120…`: production `KentridgeOrganicCirculationCatalogue` still emitted square `EmitBox` route carve/fill stamps.
- workflow `33214166946`, source `57006268…`: route-only hypothesis with round cylinder stamps.
- workflow `33225240544`, source `36eb66c5…`: precedence/plot hypothesis.
- workflow `33231174953`, source `ed933466…`: fixed `1.2m` rounded visible plot cap.
- workflow `33233041050`, source `29786792…`, CI transport `8cedf0ea…`: stadium visible plot cap. The focused test, fresh bake, real-player build, saved-camera replay, artifact upload, and `ci/single-test=success` publication all ran, but the Actions job concluded `cancelled`; it is diagnostic only.

The last artifact also proves the bake was fresh: `ShowcaseWorld.bytes` was rebuilt/imported for seed `0x5EED1234`, and the player reported all 199 generated regions at stable residency under the saved camera.

## Result
The rendered ground is byte-for-byte identical across these materially different source variants. In particular:

- square-route source vs cylinder-route source: **0 changed ground pixels** and **0 changed pixels in either original circle**;
- route-only vs precedence/plot: **0 changed ground pixels** and **0 changed circle pixels**;
- precedence/plot vs rounded-cap: **0 changed ground pixels**;
- rounded-cap vs stadium-cap: differences occur only in sky/background above the rendered ground; **0 changed ground pixels** and **0 changed circle pixels**.

Direct inspection of the latest stadium artifact still shows both original defects: the upper contact remains stair-stepped and the lower/right contact retains a metre-scale axis-aligned Dirt/grass corner.

## Discrimination
This falsifies the earlier claims that the lower circle visibly improved when organic route stamps became cylinders and that generated-house Moss-cap ownership explained the upper circle. Those source primitives can change analytically while leaving the exact built-player contacts completely unchanged.

The existing focused regression is therefore at the wrong ownership seam: it evaluates `KentridgePlotSurfaceCatalogue`/route primitives in isolation rather than the final combined Kentridge world consumed by `VoxelShowcase`. `KentridgeCombinedVoxelCatalogueCanonical` actually inserts plot surfaces through `KentridgeVerticalPlacementAdapter.BuildPlotSurfaces(...)`, and later catalogues may overwrite the same columns.

## Next discriminator
Before another production geometry change, build a behavioral ownership regression at the **combined catalogue/final rasterized voxel** seam for the captured camera probes. Approximate authored-surface probes reconstructed from the immutable camera are `(X≈922,Z≈295)` and `(X≈957,Z≈306)` voxels around `Y≈220`. The regression must identify the final winning Dirt/grass writers after ordered combined generation, then a production fix may target only that owner.
