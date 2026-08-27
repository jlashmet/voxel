# Plan

## Evidence
- Capture note: “rooms in all buildings need to be larger with higher celings.” The saved VoxelShowcase camera pose is the runtime repro; the capture contains no marked circles, so acceptance is town-wide rather than region-specific.
- Production `KentridgeCombinedVoxelCatalogueCanonical` includes `KentridgeSharedStructureVoxelCatalogue`: 13 generated roles compile through `KentridgeSharedHouseProgram`; church, warehouse, mansion, and well remain bespoke.
- Generated room footprint comes from `StructureForm.WidthDm/DepthDm`; `ArchitectureVoxelPatterns` carves exactly wall thickness from each side, so rounded rendering is not secretly shrinking the cavity. Shared storey spacing comes from `KentridgeDefinition.Theme.FloorHeightDm` (34 dm).
- The current architecture validator reserves 12 dm inside each high-level envelope. This leaves a bounded way to make rooms larger without changing settlement lots: consume 8 dm of that reserve and retain 4 dm clearance.

## Hypotheses / discriminators
1. **Authored room/storey scale is too small.** Confirmed if the active production catalogue reflects `StructureForm` dimensions and 34 dm storeys; it does.
2. **Rounded voxel realization steals usable room space.** Falsified: interior shell carve is a sharp `InteriorCarve` using width/depth minus only wall thickness.
3. **Only props make rooms feel cramped.** Falsified as the systemic cause: the same small dimensions are present before decoration, across roles without shared props.

## Fix + regression
- Expand every generated Kentridge form by 8 dm in width/depth while retaining 4 dm envelope clearance; raise shared floor height to 40 dm.
- Expand room-bearing bespoke church/warehouse/mansion shells by 8 dm and their room height by 6 dm where not theme-driven. Leave the well unchanged.
- Add an EditMode behavioral regression that evaluates `KentridgeSharedStructureVoxelCatalogue` for all 16 room-bearing roles, proves a minimum 64 dm horizontal interior carve, verifies raised storey/landmark height, and checks every emitted primitive remains inside its reserved footprint.

## Blast radius / cost
Settlement plot positions/envelopes, rule counts, primitive limits, and generation topology remain unchanged. Primitive dimensions grow inside already-reserved envelopes, increasing filled voxel volume but not program/primitive count; footprint-containment assertions guard neighboring structures and infrastructure.