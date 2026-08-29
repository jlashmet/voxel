# Plan — 20260826-132234-356 VoxelShowcase

## Observed / acceptance
The exact `VoxelShowcase` capture (seed `1592594996`, saved camera pose in `issue.json`) marks two jagged Dirt/grass contacts. Both marked regions must be replayed in the built player and no longer show metre-scale right-angle bites. The scene must reach full residency without runtime exceptions.

## Hypotheses and discriminators
1. **Local plot/terrace/structure owners.** Route precedence, plot pads, civic terraces/courts, correction passes, and generated foundations were varied independently and compared at the exact saved pose.
2. **Late macro-world owner.** The selected macro-world catalogue is appended after detailed Kentridge, so its routes/root marker were checked against localized world probes.
3. **Actual authored organic-road rasterizer creates the jagged boundary.** `VoxelShowcase -> ShowcaseCatalogue -> WorldBuilderTownAuthoring -> WorldBuilderVoxelCatalogue` injects the authored `SettlementPlan` into `KentridgeCombinedVoxelCatalogue`. For that plan, `KentridgeDirectedTownSurfaceCatalogue` selects `KentridgeOrganicCirculationCatalogue`, whose terrain-following route stamps were axis-aligned square `EmitBox` fills spaced at up to half the road width. A diagonal/curved sequence of 1.8–2.8m squares necessarily creates the right-angle dirt/grass bites visible in the capture.

## Runtime evidence / discrimination
- Earlier local plot/precedence/foundation variants changed zero marked ground pixels or left the visible defect; those production experiments are reverted.
- Fresh bake/replay evidence rejects stale world data.
- Final macro-root CI (`33271533057`) disproved the root-marker ownership assumption: its runtime X bounds are `1110..1229`, outside the localized upper probe near X `924`; the saved-pose player still showed the defect. Macro/foundation production experiments are reverted.
- Exact scene-source tracing corrected an earlier plan-model mismatch: VoxelShowcase does use Kentridge voxel generation, but it passes the authored `KentridgeDefinition.Build(seed)` settlement through `WorldBuilderVoxelCatalogue`. That authored plan has organic routes and therefore executes the square-stamp organic circulation backend.
- The engine already has deterministic integer `EmitCylinder`/`CylinderEmitter` support. Its X/Z membership is radial (`du*du + dv*dv <= radius*radius`), so it removes each square stamp corner without float math or a new pathfinder.

## Selected fix / regression
`KentridgeOrganicCirculationCatalogue` keeps the same sampled route points, terrain height lookup, clear+surface primitive count, route widths, precedence, and entrance connectors, but uses vertical cylinder stamps instead of axis-aligned boxes. The radial stamps overlap at the existing <= half-width spacing, producing a continuous terrain-following road without metre-scale square corners.

The focused regression builds the exact authored Kentridge plan for `VoxelShowcaseSeed`, binds it into `VoxelWorldGenSettings`, builds the production directed-town surface catalogue, evaluates each organic route-width definition, and proves:
- the exact plan exercises organic routes;
- both clearance and road-surface instructions emit cylinders;
- the production road primitive is vertical/radial;
- its centre remains occupied while the old bounding-square corner is excluded.

Blast radius: authored organic Kentridge circulation only. Legacy directed-ramp roads, plots, buildings, macro-world routes/markers, renderer, terrain, and other assignments are unchanged. Cost: no extra route stamps, definitions, instructions, or primitives; circular footprints rasterize fewer cells than the previous squares, so voxel work should be equal or lower.

## Remaining gates
Merge current `master`, run one fresh final targeted PlayMode request from `ci-test/fixes/agent-8` against the exact feature SHA and exact saved-pose `VoxelShowcase` replay, inspect both original marked regions, then complete pending/closed metadata and the final non-force merge-to-master workflow only if both behavioral and visual gates are green.
