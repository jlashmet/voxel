# Plan

## Evidence
- Capture note: “rooms in all buildings need to be larger with higher celings.” The saved VoxelShowcase camera pose is the runtime repro; the capture contains no marked circles, so acceptance is town-wide rather than region-specific.
- Production `KentridgeCombinedVoxelCatalogueCanonical` includes `KentridgeSharedStructureVoxelCatalogue`: 13 generated roles compile through `KentridgeSharedHouseProgram`; church, warehouse, mansion, and well remain bespoke.
- Generated room footprint comes from `StructureForm.WidthDm/DepthDm`; the shell carve removes only wall thickness, so rounded rendering is not shrinking the cavity. Shared storey spacing is `KentridgeDefinition.Theme.FloorHeightDm` (34 dm).
- Generated forms retain 12 dm envelope clearance today. Church/warehouse/mansion already have clear short spans of 110/132/178 dm and vertical carves of 62/55/102 dm, so they already exceed the new town-wide minimum and do not need massing changes.

## Hypotheses / discriminators
1. **Authored generated-room/storey scale is too small.** Confirmed by the active production dimensions and 34 dm storeys.
2. **Rounded voxel realization steals usable room space.** Falsified: the interior is a sharp `InteriorCarve` using width/depth minus wall thickness only.
3. **Props or bespoke landmarks are the systemic constraint.** Falsified: cramped dimensions exist before decoration, while all three room-bearing bespoke landmarks already exceed the target.

## Fix + regression
- Expand every generated Kentridge form by 8 dm in width/depth while retaining 4 dm envelope clearance; raise shared floor height to 40 dm.
- Leave bespoke landmark massing unchanged; mansion inherits the taller shared theme, while church/warehouse already exceed the ceiling target. Leave the open well out of the room contract.
- Add a PlayMode behavioral regression through `KentridgeSharedStructureVoxelCatalogue` for all 16 room-bearing roles: minimum 64 dm horizontal interior carve, minimum 40 dm vertical carve, raised 40 dm generated storeys, and X/Z footprint containment for every emitted primitive. PlayMode is required so the same exact-SHA request can perform the saved-camera scene replay.

## Blast radius / cost
Settlement plot positions/envelopes, rule counts, primitive limits, and generation topology remain unchanged. Generated shell volume grows inside existing plots but primitive count does not; style-owned clearance preserves the conservative 12 dm default for other architecture styles.
