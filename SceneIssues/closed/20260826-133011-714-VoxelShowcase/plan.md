# Plan

## Evidence
- Capture note: “rooms in all buildings need to be larger with higher celings.” The saved VoxelShowcase camera pose is the runtime repro; there are no marked circles, so acceptance is town-wide.
- Production `KentridgeCombinedVoxelCatalogueCanonical` includes `KentridgeSharedStructureVoxelCatalogue`: 13 generated roles compile through the shared house program; church, warehouse, mansion, and well remain bespoke.
- Generated room footprint comes from `StructureForm.WidthDm/DepthDm`; shared storey spacing comes from `KentridgeDefinition.Theme.FloorHeightDm`. Church/warehouse/mansion already exceeded the selected room-scale minimums.

## Hypotheses / discriminators
1. **Authored generated-room/storey scale is too small.** Confirmed by production dimensions and the former 34 dm storeys.
2. **Rounded voxel realization steals usable room space.** Falsified: the interior carve uses width/depth minus wall thickness.
3. **Bespoke landmarks are the systemic constraint.** Falsified: all room-bearing bespoke landmarks already exceeded the target.

## Fix + regression
- Expand every generated Kentridge form by 8 dm in width/depth, with style-owned 4 dm envelope clearance; raise shared floor height to 40 dm.
- Keep bespoke landmark massing unchanged; leave the open well out of the room contract.
- PlayMode regression `VoxelEngine.Tests.PlayMode.KentridgeInteriorScaleTests.ProductionBuildingsMeetExpandedRoomAndCeilingMinimums` covers all 16 room-bearing roles: >=64 dm horizontal carve, >=40 dm vertical carve, 40 dm generated storeys, and X/Z footprint containment.

## Verification
- Tested source: `6d8edbf62512b4495982c5a52704c811d035e67c`.
- Exact request: `5bb99265224e31c195b1556f8cb38c83c7629b14`, run `33040329581`: 1/1 requested test passed; real-player replay reported `Verified standalone frozen pose`; artifact `9633954143` uploaded successfully.
- Visual review of the final replay frame shows an open interior floor plan and raised overhead clearance at the original saved camera pose. Remaining gates: bookkeeping close and non-force master promotion.

## Blast radius / cost
Plot positions/envelopes, rule counts, primitive limits, and generation topology are unchanged. Generated shell volume grows inside existing plots but primitive count does not; other architecture styles retain the conservative 12 dm clearance default.
