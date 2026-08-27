# Plan

## Observed / acceptance
The capture note says the houses are completely empty and calls for a prop catalogue. There are no circles, so the full saved 1928x836 `VoxelShowcase` frame at frame 96972 / 305.21 s and the persisted `Showcase Camera` pose are the acceptance region. The production composition builds `KentridgeSharedStructureVoxelCatalogue`; 13 roles are generated houses/shops/hospitality, while church, warehouse, mansion, and well are bespoke.

Acceptance: every generated Kentridge structure composes deterministic interior furniture through the production shared-house program, while public entrances remain clear, bespoke structures are unchanged, and per-definition primitive cost remains within the existing 256-primitive budget. Replay the saved pose after green targeted CI and retain `verification-final.png`.

## Competing hypotheses / discriminator
1. Furniture exists but is hidden by rendering/culling/placement. Falsifier: the active generated-house program contains no interior-prop instructions.
2. The shared-house realization never authors generic interior props. Falsifier: evaluating the active shared-structure catalogue already yields deterministic furniture inside non-pub generated roles.

Inspection discriminates for (2): `KentridgeSharedHouseProgram` compiles shell/openings/roof and adds only a pub counter; the existing town/plot dressing catalogues place exterior props and cannot decorate house interiors.

## Selected fix
Add a reusable `KentridgeHouseInteriorPropCatalogue` fragment at the voxel-realization boundary and compose it into all 13 generated shared-house programs. Use small role/archetype-appropriate integer box assemblies (home, shop, hospitality), keep the doorway lane and central hearth area clear, and retain the pub counter in the catalogue. This inherits each structure's existing placement/orientation/precedence and does not alter semantic planning or bespoke structures.

Behavioral regression: build/evaluate `KentridgeSharedStructureVoxelCatalogue`, verify every generated role emits catalogue furniture, doorway approach remains unfilled, bespoke roles gain no furniture signature, and evaluated primitive counts remain <= `MaxPrimitives`.

## Remaining gates
Implement + review blast radius/cost; push exact feature SHA; one EditMode targeted-CI request on `ci-test/fixes/agent-3`; saved-pose replay; commit verification + pending metadata; then user-authorized close and non-force master promotion after merging current master.