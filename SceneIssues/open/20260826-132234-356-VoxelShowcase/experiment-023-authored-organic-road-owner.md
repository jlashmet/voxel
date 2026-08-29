# Experiment 023 — authored organic-road owner

## Question
Why did earlier route/plot experiments leave the saved-pose Dirt/grass pixels unchanged, and which production path actually creates the jagged road edge in `VoxelShowcase`?

## Runtime/source discrimination
- `ShowcaseWorld` builds `ShowcaseCatalogue`.
- `ShowcaseCatalogue` authors Kentridge through `WorldBuilderTownAuthoring` and `WorldBuilderVoxelCatalogue`.
- `WorldBuilderVoxelCatalogue` binds the authored `SettlementPlan` into `VoxelWorldGenSettings` before calling `KentridgeCombinedVoxelCatalogue`.
- `KentridgeDefinition.Build(1592594996)` produces inferred organic routes, so `KentridgeDirectedTownSurfaceCatalogue` selects `KentridgeOrganicCirculationCatalogue`.
- That backend sampled each route at <= half-width spacing and emitted terrain-following 18–28dm axis-aligned square road-surface boxes. Their corners geometrically produce the metre-scale right-angle Dirt/grass bites visible in the issue capture.
- Workflow `33271533057` independently falsified the competing macro-root hypothesis: the root marker's runtime X range is `1110..1229`, outside the localized upper probe near X `924`.

## Change / discriminator
Keep route samples and terrain following unchanged, but emit vertical cylinders for both the clearance and road surface. The focused regression builds the exact authored Showcase plan and proves the evaluated road primitive retains its centre while excluding the old square corner.

## Blast radius / cost
Only authored organic Kentridge routes change. Stamp count, definitions, instructions, and primitive count are unchanged; radial footprints touch fewer voxels than square footprints.
