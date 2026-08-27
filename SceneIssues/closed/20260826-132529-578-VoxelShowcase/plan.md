# Plan

## Observed / acceptance
The capture reported generated Kentridge houses as completely empty. The full saved `VoxelShowcase` pose is the acceptance region because no circles were marked. Acceptance requires deterministic interior furniture in every generated house/shop/hospitality role, unchanged bespoke structures, a clear entrance approach, and no definition exceeding its existing 256-primitive budget.

## Hypotheses / discriminator
1. Furniture existed but was hidden by rendering/culling/placement. Falsifier: the active generated-house program contains no interior-prop instructions.
2. Shared-house realization never authored generic interior props. Falsifier: evaluating the production shared-structure catalogue already yields furniture for non-pub generated roles.

Inspection selected (2): `KentridgeSharedHouseProgram` emitted shell/openings/roof and only a Pub counter; town/plot dressing was exterior-only.

## Fix / regression / blast radius
`KentridgeHouseInteriorPropCatalogue` is composed only by the generated branch of `KentridgeSharedStructureVoxelCatalogue`. It adds a common table plus home bed, shop counter/shelf, or hospitality bench in the rear half; the Pub keeps its bar. Furniture starts behind the gameplay entrance approach. The focused production-path regression verifies all 13 generated roles contain the common furniture signature, all 4 bespoke roles do not, and all roles remain within `MaxPrimitives`. Cost is +55 primitives total and at most +5 to any definition.

## Verification
Exact request `6d8dfcad2ff585b3331a4a70572519273a9d13da` passed `ci/single-test` in run 33027896536. The saved-pose real-player replay completed successfully and produced `verification-final.png`; the replay shows authored interior geometry while the public entrance remains open. Fix source SHA: `5873450cec6907bb5c0d71d69bfa0ace5be20b2b`.
