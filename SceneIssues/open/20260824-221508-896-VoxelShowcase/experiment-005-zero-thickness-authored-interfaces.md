# Experiment 005 — remove zero-thickness authored interface ownership

## Hypothesis

The fresh-bake defect is caused by authored solids meeting on zero-thickness interfaces, not by missing plaza occupancy.

There are two visible manifestations in the assigned screenshot:

1. The long light-blue line through the three lower circles coincides with the hard piazza's north dark-border/light-centre transition at about `Z = 58.5 m`. Attempt 2 gave the piazza continuous backing occupancy, but the decorative DarkMasonry bands are still emitted as coplanar `Fill` boxes over that slab. Those material-only overlays therefore still contribute independent geometric boundary samples where the backing slab should be the sole surface owner.
2. The remaining marked region surrounds market-stall feet. The vertically adapted stall placement lands at the piazza surface and its stone shoes start at local `y = 0`, so separately authored stall solids only touch the piazza instead of overlapping their support.

The smallest common correction is to remove both zero-thickness junctions: use `PrimitiveMode.PaintSolid` for the four decorative piazza border boxes, and sink market-stall placements one authored decimetre into the shared surface.

## Evidence before attempt 3

- Attempt 2 fresh replay: workflow run `32883086329`, artifact `scene-221508-unobscured-view` / `9576720440`.
- Fresh bake completed successfully and regenerated `ShowcaseWorld.bytes`; the replay still showed the original long crack and stall-foot exposure.
- The hard piazza now has one full-footprint FoundationStone backing `Fill`, proving that the crack can remain despite occupied support beneath it.
- Exact camera projection places the long marked line at the authored north-border start (`depth - BorderWidthDm`), not at the outer plaza endpoint.
- `PrimitiveMode.PaintSolid` is explicitly defined as repainting existing solid voxels without changing occupancy.
- The structure authoring boundary contract states that paint is not geometry; material-only edits should not overwrite the real surface's boundary geometry.
- `KentridgeTownDressingCatalogue.MarketStallProgram` starts each stone shoe at local `y = 0`, while `BuildTownDressing` adapts the stall placement to the same vertical piazza surface. The feet therefore have zero penetration into the floor.

## Planned red regression

Add one focused `KentridgeMarketPiazzaTests` regression that requires both halves of the final interface contract:

- primitive 0 remains the sole full-footprint `Fill` slab and primitives 1–4 are `PaintSolid` border boxes;
- the four market-stall explicit placements produced by the vertical town-dressing adapter sit one authored decimetre below the piazza surface, while their shape program remains unchanged.

The current branch should fail that regression because its border primitives are still `Fill` and its market stalls still sit exactly at the piazza surface.

## Attempt budget

Production attempts completed before this experiment: **2 / 3**. This hypothesis is for **attempt 3**, the final allowed production attempt. If its focused tests become green but the mandatory fresh exact-pose replay still shows any assigned marked defect, stop changing production code and record terminal blocked bookkeeping rather than making a fourth attempt.
