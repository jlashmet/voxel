# Plan — 20260826-132234-356 VoxelShowcase

## Observed / acceptance
The exact `VoxelShowcase` capture (seed `1592594996`, saved camera pose in `issue.json`) marks two jagged Dirt/grass contacts. Both regions must be replayed in the built player and no longer show the metre-scale rectangular owner. The scene must reach full residency without runtime exceptions.

## Hypotheses and discriminators
1. **Final generated-structure foundation owns the rectangle.** Kentridge’s generated house compiler emits a 7 dm foundation, but `KentridgeSharedStructureVoxelCatalogue` translated every structure only 5 dm below the intended plot surface. Prediction: the final generated foundation protrudes 2 dm into the visible ground band; rasterized storage above the target surface contains Foundation.
2. **Route/plot surface geometry owns the rectangle.** Prediction: materially changing those shapes changes pixels inside the marked circles.

## Results
- Route boxes→cylinders, plot rounding/stadium caps, and precedence variants produced byte-identical rendered-ground pixels in both marked circles. Those hypotheses are falsified; their production experiments are reverted.
- Fresh combined-world bakes remained stable, falsifying stale bake/streaming ownership.
- Final-stage source/runtime ordering shows generated MayorHouse is emitted after plot surfaces. Its theme foundation depth is 7 dm while the shared placement sink was 5 dm: a deterministic 2 dm rectangular protrusion.

## Selected fix / regression
Generated Kentridge structures now sink by `theme.FoundationHeightDm`; bespoke landmark programs retain their legacy 5 dm sink. The focused regression evaluates the production MayorHouse for the exact seed, rasterizes its region through `FeatureGeneration.GenerateRegion`, asserts Foundation remains below the authored surface, and asserts Foundation no longer owns the voxel immediately above it.

Blast radius: Kentridge generated shared structures only; shared compiler, bespoke landmarks, routes, plot surfaces, other settlements, renderer, and terrain are unchanged. Cost: one stored integer per 17 compiled programs and one placement-time multiply; no additional primitives or per-voxel work.

Current production/test fix: `53f76db8b6629e1de7aa3a89750ab46403b4a3d1`.

## Remaining gates
Final targeted PlayMode regression + exact built-player `VoxelShowcase` replay; inspect both original marked regions; then pending/closed metadata and merge workflow.
