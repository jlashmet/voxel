# Plan — 20260826-132234-356 VoxelShowcase

## Observed / acceptance
The exact `VoxelShowcase` capture (seed `1592594996`, saved camera pose in `issue.json`) marks two jagged Dirt/grass contacts. Both regions must be replayed in the built player and no longer show the metre-scale rectangular owner. The scene must reach full residency without runtime exceptions.

## Hypotheses and discriminators
1. **Final generated-structure foundation owns the rectangle.** Kentridge’s generated house compiler emitted its foundation too high. Prediction: sinking the generated foundation below the authored plot surface changes the exact built-player marked pixels.
2. **Route/plot surface geometry owns the rectangle.** Prediction: materially changing those local shapes changes pixels inside the marked circles.
3. **Late selected macro-world instrumentation repaints the detailed town.** `KentridgeCombinedVoxelCatalogue` appends `TopDownWorldVoxelCatalogue` after the local detailed catalogue. Prediction: a macro primitive covers the surviving upper mark even though local route/plot/foundation edits are byte-identical there.

## Results
- Route boxes→cylinders, plot rounding/stadium caps, and precedence variants produced byte-identical rendered-ground pixels in both marked circles. Those hypotheses are falsified; their production experiments were reverted.
- Fresh combined-world bakes remained stable, falsifying stale bake/streaming ownership.
- The generated MayorHouse foundation really did protrude into the authored surface, so it was corrected and the structural regression remains valuable. However the fresh built-player replay still showed the upper marked grass tongue, disproving that foundation as the final visible owner.
- The late macro-world composition explains the earlier byte-identical local experiments. The three macro corridors leaving Kentridge run along X=1170dm or Z=520dm and do not cross the surviving upper-mark probe near X=924dm/Z=295dm. The Kentridge macro **root node marker**, however, is a 1200dm × 1200dm `PaintSurface` square centered at `(1170,520)` and does cover that probe. It is appended after the detailed Kentridge catalogue even though the detailed root settlement is already present.

## Selected fix / regression
Keep the generated-foundation correction: generated Kentridge structures sink by `theme.FoundationHeightDm`; bespoke landmark programs retain their legacy 5dm sink.

For the surviving visual owner, `TopDownWorldVoxelCatalogue.Build` now supports omitting one destination marker while preserving standalone behavior. `KentridgeCombinedVoxelCatalogue` omits only `selection.Layout.RootId` when it combines an already-detailed Kentridge settlement with the selected macro world. All macro routes and every non-root destination marker remain physically realized.

The issue regression now has two behavioral checks:
- production MayorHouse evaluation/rasterization proves Foundation ends below the authored ground surface;
- production standalone macro generation proves the root marker covers the localized upper-mark probe, then the real selected local+macro build proves exactly that one marker is absent while macro routes and non-root markers remain.

Blast radius: only Kentridge builds that explicitly consume a selected macro layout change composition. Standalone `TopDownWorldVoxelCatalogue.Build` is unchanged, other settlements are unchanged, and external routes/destinations remain. Cost decreases by one definition, one explicit placement, and one short `PaintSurface` program in the combined Kentridge catalogue; there is no new per-frame or per-voxel work.

## Remaining gates
Sync current `master`, run one final targeted PlayMode request against the exact feature SHA plus the exact built-player `VoxelShowcase` replay, inspect both original marked regions, then complete pending/closed metadata and the final merge-to-master workflow.
