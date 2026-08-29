# Plan — reopened town-architecture extensibility

## Baseline / evidence
The prior six-town gallery remains the accepted visual baseline. This reopened issue contains no new captured frames or marked regions; its quality review identifies architecture/extensibility as the defect. The six reference-driven identities, four-role contracts, deterministic seeds, detail vocabulary and wide/player/close evidence path remain required.

## Competing hypotheses and runtime/source evidence
1. **Style-definition coupling:** confirmed. `TownArchitectureProgram` rejected independent silhouette/roof composition through a one-to-one central switch.
2. **Catalogue coupling:** confirmed. `WorldBuilderTownArchitecture` hard-coded six ids in `AllStyleIds`, `Resolve` and `CanonicalSeed` switches.
3. **Voxel-backend coupling:** confirmed. production authoring switched on the six silhouettes and dispatched to six town-named methods.
4. **Gallery-only limitation:** rejected as root cause. The gallery merely exposed the shared API/backend limitation; the fix therefore lives in shared WorldBuilder contracts/registry/voxel realization, while the proof town is gallery composition data.

## Implemented architecture
- `TownArchitectureDefinition` is a public immutable registry definition; style ids are arbitrary strings and the six historic constants are only stable baseline ids.
- `TownArchitectureComposition` contains exactly one `TownArchitectureRoleRecipe` for each required structure role.
- Each role composes reusable `TownArchitectureMassing`, roof/opening data and orthogonal `TownArchitectureDetailFeatures`; there is no silhouette-to-roof validity switch.
- `WorldBuilderTownArchitecture` resolves and seeds dictionary-registered definitions instead of style-name switches.
- `WorldBuilderTownArchitectureVoxelAuthoring` dispatches only on reusable massing/opening/detail capabilities. It contains no town id/name dispatch and preserves the established deterministic four-role anchors/footprint.
- The exact gallery registers a seventh synthetic `river-trade-proof` definition that deliberately combines semantic/form data rejected by the former one-to-one gate, and mixes existing gabled stone/timber, balcony, awning, arch and buttress capabilities. Its material palette and placement are exhibit data only.

## Regression
`VoxelEngine.Tests.PlayMode.TownArchitectureExtensibilityTests.RegisteredSeventhStyleComposesExistingCapabilitiesWithoutCentralDispatch` registers an arbitrary seventh id through the public registry, resolves canonical/custom seeds, verifies all four roles/details, runs the production voxel authorer twice for determinism, proves mixed reusable macro capabilities are physically distinct, and re-runs all six built-in definitions through that same production authoring path.

## Exact built-scene gate
The canonical SceneIssue remains `Assets/Scenes/WorldbuildingGalleryShowcase.unity`. The capture-less production audit now derives its evidence count from `WorldbuildingGalleryTownDistrictCount`; seven districts therefore produce 21 named screenshots (wide/elevated, player facade, close detail for each town). Inspect all 21 for grounding/intersections/detail retention/distinctness and require `TOWNARCH_AUDIT result=PASS` with no startup/runtime exceptions.

## Blast radius / cost
Shared API/runtime/backend files change, so every consumer of town programs is in blast radius; registry search found no direct `new TownArchitectureProgram` callers outside the catalogue. Per-town realization still resolves one immutable program and emits four bounded role recipes inside the existing 164x132x78 public footprint. Registry lookup is once per district, not per voxel. The exact gallery adds one bounded district and increases stale-bake repair budget from 18M to 22M writes; final player logs must report actual `TOWNARCH_AUTHORING` writes/elapsed time plus `TOWNARCH_COST` allocation/residency evidence before closure.

## Remaining gates
Freeze one implementation SHA, run the exact focused PlayMode regression plus exact SceneIssue built-player replay through `ci-test/fixes/agent-7`, inspect NUnit/log/21-frame evidence and cost. Only after green exact-SHA gates update pending metadata, move open -> pending -> closed, merge current master into `fixes/agent-7`, and non-force fast-forward that exact merged head to master.
