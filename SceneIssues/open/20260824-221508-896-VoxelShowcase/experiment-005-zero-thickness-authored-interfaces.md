# Experiment 005 — remove zero-thickness authored interface ownership

## Hypothesis

The fresh-bake defect is caused by authored solids meeting on zero-thickness interfaces, not by missing plaza occupancy.

There are two visible manifestations in the assigned screenshot:

1. The long light-blue line through the three lower circles coincides with the hard piazza's north dark-border/light-centre transition at about `Z = 58.5 m`. Attempt 2 gave the piazza continuous backing occupancy, but the decorative DarkMasonry bands were still emitted as coplanar `Fill` boxes over that slab.
2. The remaining marked region surrounds market-stall feet. The vertically adapted stall placement landed at the piazza surface and its stone shoes start at local `y = 0`, so separately authored stall solids only touched the piazza instead of overlapping their support.

The smallest common correction is to remove both zero-thickness junctions: use `PrimitiveMode.PaintSolid` for the four decorative piazza border boxes, and sink market-stall placements one authored decimetre into the shared surface.

## Evidence before attempt 3

- Attempt 2 fresh replay: workflow run `32883086329`, artifact `scene-221508-unobscured-view` / `9576720440`.
- The hard piazza already had one full-footprint FoundationStone backing `Fill`, proving the crack could remain despite occupied support beneath it.
- Exact camera projection placed the long marked line at the authored north-border start (`depth - BorderWidthDm`), not at the outer plaza endpoint.
- `PrimitiveMode.PaintSolid` repaints existing solid voxels without changing occupancy.
- `KentridgeTownDressingCatalogue.MarketStallProgram` starts each stone shoe at local `y = 0`, while `BuildTownDressing` adapted the stall placement to the same vertical piazza surface.

## Focused red regression

Regression: `VoxelEngine.Tests.EditMode.KentridgeMarketPiazzaTests.PiazzaInterfacesPaintBordersAndOverlapMarketStallSupports`.

Red source head: `ed98e7641410a8a96468d5cd752c2d215f9f5349`.
CI request commit: `f01b580f4bf5110f1765406aa4a6edd402d63c16`.
Workflow run: `32928540659`.

Result: **failed as expected**. Unity executed exactly one test. The first failing assertion was the intended border-ownership contract at `KentridgeMarketPiazzaTests.cs:177`: expected `PrimitiveMode.PaintSolid` (`3`) but baseline emitted `PrimitiveMode.Fill` (`0`). Setup and checkout steps succeeded, so this is a valid behavioral red rather than an infrastructure failure.

## Production attempt 3

Production commit: `9c839dcbbe73bb3f325db8d3dd3ef380d22343cf`.

- Kept primitive 0 as the sole full-footprint geometric `Fill` slab.
- Changed the four DarkMasonry border boxes to `PrimitiveMode.PaintSolid`.
- Changed only the four rule-0 market-stall placements to sink one authored decimetre into the shared piazza support.
- No screenshot-coordinate, camera-specific, or extra presentation geometry was introduced.

Production-fix attempt: **3 / 3**.

## Green verification

Focused green request commit: `387a408849a04297bac8f5ab94c1ebb535141d87`.
Focused workflow run: `32929986757`.
Result: **passed**. The same focused regression completed successfully against the attempt-3 production inputs.

The CI branch was then reset to durable feature head `840f34efb29d2ec5c3a744445bce75ba70d9e53d` and the full class was requested.

Class request commit: `6600fcbe17f96aeea8fa8ceaf70640b12dc9b929`.
Class workflow run: `32930568776`.
Result: **passed**. `VoxelEngine.Tests.EditMode.KentridgeMarketPiazzaTests` completed with the requested-test step green.

## What was learned / next

The red→green regression and full class confirm the attempt-3 authored-interface contract structurally. The remaining acceptance gate is the mandatory fresh-bake standalone replay at this issue's exact saved camera. If any marked defect remains in that replay, do not make a fourth production change; keep the issue open and record the exhausted-attempt result.
