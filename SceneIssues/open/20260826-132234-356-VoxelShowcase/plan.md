# Plan

## Captured evidence
The note is the Dirt/grass join being visibly jagged. I inspected both circles separately at the saved 1928x836 pose. The lower transition is a route-edge staircase; the upper circle contains the larger axis-aligned grass tongue around the corrected ray envelope `X≈91.0..93.8m, Z≈28.6..30.4m`.

Exact replay `33215984995` after the plot-only change rebuilt `ShowcaseWorld.bytes`, passed the focused PlayMode test, built `VoxelShowcase`, and ran 45 s, but direct inspection of `verification-final.png` still showed the upper right-angle tongue. It is therefore not promotable despite green CI.

## Competing hypotheses / discrimination
1. **Organic-route square stamps.** Production `KentridgeOrganicCirculationCatalogue` emits overlapping axis-aligned box stamps along diagonal routes. This directly predicts the repeated horizontal/vertical staircase visible on both road edges. Earlier rounded-route replay `33214166946` improved the lower transition but left the upper mark.
2. **Plot feather over the route/natural edge.** Exact seed `1592594996` places MayorHouse/WideHouse at `(910,250)` dm; the upper ray crosses its west parcel edge where the old 12-step Moss feather expanded beyond the real building pad. The route-only experiment left this later precedence-40 owner intact. Conversely, the plot-only replay left the precedence-20 square route geometry intact.
3. **Stale bake/streaming.** Falsified by fresh WorldBuilder cache misses and successful saved-camera real-player replays; the marked geometry is stable.

The single-owner experiments were therefore incomplete: the two rectangular owners stack at the captured transition, so removing only one leaves a hard edge.

## Fix / regression
Keep plot grading inside each archetype's real `PadFor` envelope and leave parcel edges natural. Keep organic route centers, widths, height samples, precedence, placement count, and two-primitive budget, but replace square carve/fill stamps with vertical cylinders of the same half-width.

`SceneIssue20260826132234356CapturedDirtGrassEdgesAvoidRectangularOwners` builds both production catalogues at the exact showcase seed, proves the MayorHouse marked parcel edge is outside grading, and proves every live organic route definition emits only the bounded round carve/fill pair.

## Blast radius / cost
Route change is organic Kentridge only; legacy/district roads are untouched and primitive count is unchanged. Plot grading affects non-well Kentridge plots but reduces each program from 39 primitives to 3 and reduces spatial ownership. No per-frame work is added. Final gate is exact-SHA PlayMode + built-player replay with both original circles clean.
