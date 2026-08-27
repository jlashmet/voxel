# Plan

## Observed / acceptance
The capture note says the houses are completely empty and calls for a prop catalogue. There are no circles, so the full saved 1928x836 `VoxelShowcase` frame at frame 96972 / 305.21 s and the persisted `Showcase Camera` pose are the acceptance region. Production composes `KentridgeSharedStructureVoxelCatalogue`: 13 roles are generated houses/shops/hospitality; church, warehouse, mansion, and well are bespoke.

Acceptance: every generated structure deterministically composes interior furniture, the public entrance remains clear, bespoke structures are unchanged, and each definition stays within the existing 256-primitive budget. Replay the saved pose after green exact-SHA CI and retain `verification-final.png`.

## Hypotheses / discriminator
1. Furniture exists but is hidden by rendering/culling/placement. Falsifier: the active generated-house program contains no interior-prop instructions.
2. Shared-house realization never authors generic interior props. Falsifier: evaluating the active shared-structure catalogue already yields deterministic furniture inside non-pub generated roles.

Inspection selects (2): `KentridgeSharedHouseProgram` emits shell/openings/roof and only a Pub counter; town/plot dressing catalogues are exterior stages.

## Fix / regression / blast radius
Compose `KentridgeHouseInteriorPropCatalogue` only in the generated branch of `KentridgeSharedStructureVoxelCatalogue`. It adds a common table plus home bed, shop counter/shelf, or hospitality bench in the rear half; the Pub keeps its existing bar. The nearest furniture starts at least 39 dm behind the generated front plane, leaving the 18 dm gameplay entrance approach untouched.

Behavioral regression builds and evaluates the production shared-structure catalogue: all 13 generated roles must contain the common furniture signature, all 4 bespoke roles must not; every evaluated role must remain <= `MaxPrimitives`; generated doorway probes must end `Carve` after all primitives. Cost is +4 primitives/home, +5/shop, +4/hospitality: 55 new primitives across 13 definitions, at most +5 to any 256-primitive definition. Semantic planning, placement, orientation, and bespoke programs are unchanged.

## Remaining gates
Push implementation/test SHA; one exact EditMode request on `ci-test/fixes/agent-3`; saved-pose replay; commit verification + metadata; close; merge current master into feature and non-force promote the exact feature head.
