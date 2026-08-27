# Plan — 20260826-133143-247-VoxelShowcase

## Observed defect / acceptance
The saved VoxelShowcase pose at camera `(158.649, 82.725, 74.282)` has no circle annotations; the whole frame is the evidence region. The note says the houses remain very cramped. Acceptance: adjacent anonymous houses on the same Kentridge frontage must leave at least 20 dm (2 m) between their 72-dm production safety envelopes, while named-plot reservations remain intact.

## Competing hypotheses / evidence
1. **Anonymous frontage packing is too dense — supported.** Baseline `57eab9da86a4ea751f8dcd0d18bd659a2951558f` uses an 80-dm Kentridge module pitch. Replaying the exact `SettlementPlotLayout.PackFrontage` integer math gives 42 pre-reservation sites and **-26 dm** envelope overlap on `civic-west-block-south`.
2. **The `+14` coverage uplift alone is causal — rejected.** Removing only the uplift does not materially change the key packed centres and leaves tight/overlapping runs.
3. **Named gameplay plots cause the crowding — rejected.** `KentridgeNamedPlotReservationCatalogue` removes anonymous sites within named-plot reservations; it cannot create anonymous-to-anonymous overlap.

## Selected fix / regression
Keep shared packing semantics unchanged and change only Kentridge anonymous `ModulePitchDm` from 80 to 160. The same authored runs then produce 26 sites and a conservative worst same-frontage envelope clearance of **25 dm**.

The first placement regression was over-broad: after `KentridgeFrontageAlignedUrbanFabricCatalogue` shifts envelopes along the facade-normal axis, it compared every same-orientation pair with overlapping cross-axis bounds, including houses on different parallel frontage runs. The corrected regression builds the production `KentridgeUrbanFabricCatalogue` before that normal-axis-only alignment, groups placements by orientation plus their authored constant cross-axis frontage coordinate, sorts neighbours along the frontage, and requires >=20 dm. This directly exercises the production packing behavior without cross-run false positives; named-plot reservation can only remove sites afterward.

## Blast radius / cost / gates
The production change is Kentridge-local: no shared `SettlementPlotLayout`, named structures, frontage topology, grammar, or other settlements change. Anonymous definitions/placements fall 42→26 (~38% fewer), so generation/render cost decreases with unchanged per-building primitive budgets.

Remaining gates: commit the corrected regression, final exact-SHA targeted PlayMode CI plus saved-pose replay, inspect the final artifact, commit `verification-final.png` and pending metadata, then perform the explicitly authorized close and non-force master integration.
