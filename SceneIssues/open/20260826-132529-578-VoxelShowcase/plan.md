# Plan

## Observed / acceptance
The capture note says the houses are completely empty and calls for a prop catalogue. There are no circles, so the full saved 1928x836 `VoxelShowcase` frame at frame 96972 / 305.21 s and the persisted `Showcase Camera` pose are the acceptance region. Production composes `KentridgeSharedStructureVoxelCatalogue`: 13 roles are generated houses/shops/hospitality; church, warehouse, mansion, and well are bespoke.

Acceptance: every generated structure deterministically composes interior furniture, the public entrance remains clear by construction, bespoke structures are unchanged, and each definition stays within the existing 256-primitive budget. Replay the saved pose after green exact-SHA CI and retain `verification-final.png`.

## Hypotheses / discriminator
1. Furniture exists but is hidden by rendering/culling/placement. Falsifier: the active generated-house program contains no interior-prop instructions.
2. Shared-house realization never authors generic interior props. Falsifier: evaluating the active shared-structure catalogue already yields deterministic furniture inside non-pub generated roles.

Inspection selects (2): `KentridgeSharedHouseProgram` emits shell/openings/roof and only a Pub counter; town/plot dressing catalogues are exterior stages.

## Fix / regression / blast radius
Compose `KentridgeHouseInteriorPropCatalogue` only in the generated branch of `KentridgeSharedStructureVoxelCatalogue`. It adds a common table plus home bed, shop counter/shelf, or hospitality bench in the rear half; the Pub keeps its existing bar. The nearest furniture starts at least 39 dm behind the generated front plane, beyond the 18 dm gameplay entrance approach.

Behavioral regression `VoxelEngine.Tests.PlayMode.KentridgeHouseInteriorPropPlayModeTests.ProductionSharedStructuresDecorateEveryGeneratedInteriorWithinBudget` builds/evaluates the production catalogue: all 13 generated roles must contain the common furniture signature, all 4 bespoke roles must not, and every role must evaluate within `MaxPrimitives`. Cost is +4 primitives/home, +5/shop, +4/hospitality: 55 new primitives across 13 definitions, at most +5 to any 256-primitive definition. Semantic planning, placement, orientation, and bespoke programs are unchanged.

## Remaining gates
One exact PlayMode request on `ci-test/fixes/agent-3` runs that regression and the saved-pose replay; inspect replay evidence; commit verification + metadata; close; merge current master into feature and non-force promote the exact feature head.
