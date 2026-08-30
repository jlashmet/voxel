# Experiment 001 — registry and reusable composition

## Hypothesis
The reopened extensibility failure is caused by three independent central dispatch points rather than by the gallery scene: one-to-one semantic form validation, named catalogue resolution/seed switches, and silhouette-to-town voxel dispatch.

## Runtime/source discrimination
All three were present in the accepted baseline. `TownArchitectureProgram` gated silhouette/roof pairs, `WorldBuilderTownArchitecture` listed/resolved/seeded exactly six names, and `WorldBuilderTownArchitectureVoxelAuthoring.Author` selected six `Author<Town>` methods from `program.Silhouette`. The gallery only supplied placement/palette/evidence and therefore was not the correct layer for a geometry workaround.

## Action
Replaced style-name dispatch with public `TownArchitectureDefinition` registry data and exact-four-role `TownArchitectureComposition`. Each `TownArchitectureRoleRecipe` composes reusable massing, roof, opening and orthogonal detail capabilities. The voxel backend now dispatches only on those reusable capabilities and retains the existing deterministic four role anchors and public district bounds.

The exact gallery adds `river-trade-proof` only as a registered definition, palette and placement. Its top-level semantic `PastoralTimberFrame + TwinGable + OrderedStone` combination was impossible under the old one-to-one gate, while its four roles mix existing gabled frame, stone gable and parapet massing with timber framing, masonry courses, balconies, market awnings, civic arches, chimney and buttress capabilities. No proof-town backend method or style-id switch was added.

## Regression
`TownArchitectureExtensibilityTests.RegisteredSeventhStyleComposesExistingCapabilitiesWithoutCentralDispatch` independently registers another arbitrary river-trade id, proves canonical/custom seeded resolution and all four roles, runs the production authorer twice for deterministic physical composition, and exercises all six baseline programs through the same production backend.

## Blast radius / cost expectation
Blast radius is the shared town-program API/catalogue/voxel authorer and gallery composition. No scene voxel-writing was introduced. Registry lookup happens once per district. Each district remains inside the 164x132x78 contract. The stale-bake gallery repair budget is provisionally raised from 18M to 22M for the seventh bounded district; exact built-player `TOWNARCH_AUTHORING` and `TOWNARCH_COST` logs determine the measured verdict.

## Gate
Do not promote from open until the one exact-SHA CI request compiles/passes the focused regression, builds the canonical `WorldbuildingGalleryShowcase`, produces all 21 town audit frames without runtime exceptions, and direct frame inspection confirms seven physically distinct, grounded, non-intersecting styles with the six accepted identities intact.
