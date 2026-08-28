# Plan — VoxelShowcase dirt/grass seam

## Observed / acceptance
The capture has one pose with two marked Dirt/grass transitions. Direct inspection of the latest exact-pose built-player replay shows the lower mark now has a continuous diagonal edge, while the upper mark still has a large axis-aligned grass tongue intruding into the Dirt route. Acceptance requires both original marks clean in a fresh replay with full residency and no startup/runtime exceptions.

## Hypotheses / discriminators
1. **Stale bake / streaming:** falsified by repeated fresh fully resident replays.
2. **District terrace/civic/correction stages:** falsified because VoxelShowcase seed `1592594996` selects organic Kentridge; those district-only stages are not composed.
3. **Organic route square stamps:** falsified by exact-SHA run `33214166946`: the focused regression found zero live route placements overlapping the corrected upper envelope, while the built-player replay still showed the defect.
4. **Organic plot grading:** supported by the minimal exact-seed reproduction. MayorHouse (`WideHouse`) owns parcel `X=91.0..104.2m, Z=25.0..38.2m`, containing the upper mark at its west edge. Its generic plot program expands 12 progressively higher moss-capped rectangles to the parcel boundary, reaching ~233–234 while natural terrain at `(910,295)` is `223` and the frontage target is `221`.

## Selected fix / regression
Revert the falsified route-shape candidate. Grade each non-well plot only inside its real building-envelope pad: one clearance carve, one Dirt fill, one Moss surface at the existing frontage target. Parcel-edge terrain remains deterministic natural terrain.

`VoxelEngine.Tests.PlayMode.KentridgePlotSurfaceSceneIssueRegressionTests.SceneIssue20260826132234356MayorHousePlotEdgePreservesNaturalTerrain` builds the production plot catalogue with the exact Showcase seed, locates the WideHouse definition and MayorHouse placement without editor-only dependencies, proves the marked parcel edge is outside all plot primitives, preserves target/natural heights, and enforces the 3-primitive pad budget.

## Blast radius / cost
All organic Kentridge non-well plot placements keep the same positions, orientations, frontage target, precedence, and building-envelope dimensions. Only the artificial parcel feather is removed. Per-placement plot work drops from 39 primitives to 3 and vertical definition extent drops by 1.2 m; no per-frame cost is added.

## Gate
Use only `ci-test/fixes/agent-8`. Final exact-SHA PlayMode CI must pass the focused regression plus the built-player 45-second VoxelShowcase replay, then both original marks must be inspected directly.
