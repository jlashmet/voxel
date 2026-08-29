# Plan

## Captured evidence
The note is the Dirt/grass join being visibly jagged. I inspected both circles separately at the saved 1928x836 pose. The lower transition is a repeated route-edge staircase; the upper circle contains a larger axis-aligned grass tongue around the corrected ray envelope `X≈91.0..93.8m, Z≈28.6..30.4m`.

Fresh built-player replay `33215984995` rebuilt `ShowcaseWorld.bytes`, passed focused PlayMode coverage, launched `VoxelShowcase` for 45 s without runtime failure, yet `RealPlayer/verification-final.png` still showed the upper right-angle tongue. Green infrastructure therefore did not satisfy the visual gate.

## Competing hypotheses / discrimination
1. **Square organic-route stamps.** Proven owner of the repeated lower staircase. Replacing live route boxes with equal-width vertical cylinders improved that transition without changing route centers, widths, samples, precedence, placement count, or two-primitive budget.
2. **Parcel-sized plot grading.** Proven contributor to the upper rectangle. Replacing the 12-step outward feather with the real archetype pad removed grading from the MayorHouse parcel west edge, but the built replay still retained a rectangle.
3. **Plot/route precedence overlap.** Current leader. Exact seed `1592594996` places MayorHouse/WideHouse at `(910,250)` dm; the marked envelope intersects both its bounded pad and an organic Dirt route. In the real combined catalogue the pad remains precedence 40 while the route is 20, so Moss wins the shared columns despite corrected shapes. Falsifier: no route/pad overlap inside the saved mark, or a replay still showing the tongue after route precedence wins.
4. **Stale bake/streaming.** Falsified by fresh cache misses and stable saved-camera real-player replays.

## Fix / regression
Keep standalone/legacy plot precedence unchanged. In organic Kentridge composition only, lower plot grading to precedence 10: above generic ground cover (5), below authored public Dirt routes (20). Preserve the bounded plot pad and round route stamps.

Existing regression proves pad geometry and round route programs. New exact-seed regression proves the saved upper envelope intersects both real owners and that the combined production catalogue enforces `route > plot > ground` there.

## Blast radius / cost
Precedence adaptation affects only organic Kentridge combined generation; legacy Kentridge and standalone plot catalogues are unchanged. No geometry, occupancy budget, placements, route samples, or per-frame work are added. Cost is one build-time loop over the small plot-definition array. Remaining gates: focused exact-SHA PlayMode, built-player `VoxelShowcase`, and saved-pose inspection with both original circles clean.
