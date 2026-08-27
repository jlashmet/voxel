# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
Capture note: `there is a floating mailbox`; no circles, so the whole saved pose is acceptance. The camera looks almost directly at the east market lamp at `(153.0 m, *, 54.9 m)`. Accept only when the saved-camera replay shows that lamp grounded on its sidewalk and continuously supported through the lantern head, with no nearby streetscape regression.

## Competing hypotheses / results
1. **Wrong elevation / sidewalk ownership. Confirmed after replay.** Attempt-1 evidence from request `6d16727f3651fc041c50f96b459c8c11634765a5` still floats the whole lamp. `(1530,549)` is outside the road and inside the working-yard north terrace shoulder. Showcase seed: macro Y `256`; generated shoulder Y `232`.
2. **A plot sign/mailbox is floating. Falsified.** Nearest market sign is outside the saved view; the east-market lamp is the camera-forward object.
3. **Thin smooth pole collapses. Confirmed secondary defect.** Material 6 is Smooth; the 3×3 pole now explicitly uses `SurfaceStyles.Planar`, preserving occupancy/material while reconstructing exactly.

## Fix / regression / blast radius
Keep the Planar pole fix. For street dressing, use the deterministic working-yard stepped shoulder surface when a placement lies inside that terrace; all other placements retain the existing macro Y. Only the captured `(1530,549)` placement currently intersects that terrace.

`KentridgeStreetLampSupportPlayTests.CapturedEastMarketLampKeepsPlanarSupportUnderLantern` now builds both production catalogues, evaluates the actual working-yard terrace program at the captured column, requires lamp origin Y to equal the generated solid surface, proves the macro Y differs, then verifies the Planar pole overlaps the lantern.

Current source: `ff16ed4b19e7672f0b6267692bac9152dd9ddb9a`.

Blast radius: one Kentridge street-lamp placement plus the existing Planar style on all 24 poles. No road/terrace geometry, materials, renderer-wide behavior, allocations, storage reads, or jobs added; placement work adds only bounded integer terrain/profile queries for the one matching working-yard point.

## Remaining gates
The user-specified old exact request `6d16727f...` remains queued and must not be replaced while queued. Its first artifact is diagnostic only because visual acceptance failed and current source has advanced. After it reaches a terminal state, request the updated focused PlayMode test + 30 s saved-pose replay on the exact current feature SHA; inspect the fresh artifact; commit `verification-final.png`, complete metadata, move `open -> pending -> closed`, merge current master, and advance master non-force.
