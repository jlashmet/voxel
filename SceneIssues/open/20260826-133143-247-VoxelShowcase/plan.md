# Plan — 20260826-133143-247-VoxelShowcase

## Observed defect / acceptance
The saved VoxelShowcase pose at camera `(158.649, 82.725, 74.282)` has no circle annotations; the whole frame is the evidence region. The note says the houses remain very cramped. Acceptance: production Kentridge anonymous frontage envelopes must no longer overlap and adjacent houses on the same frontage must leave at least 20 dm (2 m) of lateral clearance at scale 1, while named-plot reservations remain intact.

## Competing hypotheses
1. **Anonymous frontage packing is too dense — supported.** Production `KentridgeUrbanFabricCatalogue` packs 72-dm safety envelopes with an 80-dm module pitch and `coverage + 14`. Replaying the exact block/run math yields 42 anonymous sites and a worst same-segment envelope clearance of **-26 dm** (overlap) on `civic-west-block-south`.
2. **The `+14` coverage uplift alone is the cause — rejected.** Removing only the uplift does not change the packed counts/centres on the key residential/working runs and still leaves overlapping/tight runs elsewhere.
3. **Named gameplay plots are causing the crowding — rejected as primary.** Production applies `KentridgeNamedPlotReservationCatalogue` with the settlement density policy's 12-dm reservation around every named plot; removing anonymous sites can only increase clearance.

## Selected fix
Keep the shared `SettlementPlotLayout.PackFrontage` semantics unchanged and make the Kentridge-specific anonymous module pitch 160 dm instead of 80 dm. With the unchanged authored blocks, gaps and coverage, the conservative pre-reservation fabric drops from 42 to 26 sites and the worst adjacent 72-dm envelope clearance becomes **25 dm**. This is localized to Kentridge anonymous infill; named structures, frontage topology, building grammar and other settlements are unchanged.

## Regression / blast radius / cost
Add a PlayMode regression through `KentridgeFrontageAlignedUrbanFabricCatalogue.Build`, the exact production stage used by the combined Kentridge catalogue. It compares actual explicit placements and production definition footprints for same-facing, depth-overlapping frontage neighbors and requires >=20 dm lateral clearance. The final named-plot reservation may only remove sites, so this is the conservative stage.

Cost improves: anonymous definitions/placements fall 42→26 (~38% fewer), with unchanged per-building primitive budgets. Remaining gates: targeted exact-SHA PlayMode CI, saved-pose replay/artifact inspection, `verification-final.png`, final issue bookkeeping, closure, and non-force master integration.