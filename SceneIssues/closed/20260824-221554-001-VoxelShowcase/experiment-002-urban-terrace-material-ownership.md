# Experiment 002 — Urban terrace material ownership

## Hypothesis

The captured staircase is not receiving grass from a renderer-side surface-material override. It is an authored Kentridge urban terrace shoulder whose authoritative voxel material is being overwritten by the later terrace surface-correction catalogue.

## What was performed

1. Exported the original `screenshot-001.png` through a CI artifact and inspected it directly.
2. Replayed the saved `Showcase Camera` pose at the recorded 1364×836 framing. The replay reproduced the broad green-tread / masonry-riser staircase.
3. Converted the saved camera position through `ShowcaseWorld.VoxelSize` (0.1 m) and localized it to the east shoulder of the `market-main` district terrace.
4. Ran a diagnostic player probe from an exact disabled camera matching the captured transform, FOV, and aspect ratio. Rays sampled authoritative `ShowcaseWorld.SurfaceQuery` cells rather than rendered colours.
5. Traced `KentridgeDistrictTerraceCatalogue` and `KentridgeTerraceSurfaceCorrectionCatalogue` side by side.

## Result

**Root cause confirmed.**

- The exact captured-ray probe reports material ID `14` (`Moss`) on nearly every foreground stair tread, with occasional material ID `1` masonry seams.
- `market-main` is an Urban terrace. Its base district-terrace program deliberately authors the core as `DarkMasonry` and its six-step shoulder as `RoadSurface`.
- `KentridgeTerraceSurfaceCorrectionCatalogue` subsequently paints the *entire* terrace footprint with `Moss`, then restores `DarkMasonry` only inside the Urban core. The broad shoulder remains Moss.
- The renderer is therefore faithfully displaying an incorrect authoritative material; this is not a material-sampling/render-order defect.

The earlier `proceduralBakeSurfaceMaterialId` hypothesis was disproven: the current `VoxelShowcase` scene does not serialize those fields. The first probe that used `Camera.main` was also discarded because it sampled a different active camera near spawn; the corrected probe used the exact captured pose explicitly.

## Intended invariant

An Urban terrace surface correction must preserve the same material split as its base terrace authoring: `RoadSurface` over the broad stepped shoulder and `DarkMasonry` over the Urban core. Non-Urban correction behaviour is outside this capture's scope.

## Regression

Added `VoxelEngine.Tests.EditMode.KentridgeTerraceSurfaceCorrectionTests.MarketMainUrbanShoulderUsesRoadSurfaceInsteadOfMoss`. It inspects the deterministic correction program for `market-main` and requires the full-footprint `PaintSurface` material to be `RoadSurface`, followed by the existing `DarkMasonry` core paint.

## Next

Run the focused regression against the unfixed commit to confirm it fails for the captured ownership violation. Then change only the Urban full-footprint correction material from Moss to RoadSurface, rerun targeted CI, and replay the saved viewpoint.
