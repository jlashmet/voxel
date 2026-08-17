# Worldbuilding Repository Inventory

This note records the Phase 0 evidence for `worldbuilding-plan.md`. The inventory describes what is actually present on `agent/worldbuilding-structures-caves`; an absent builder is recorded as an absence rather than inferred from a similarly named type.

## WB001 — house/cottage baseline

There are two relevant house baselines on this branch: a small test-owned cottage feature and the production Kentridge architecture pipeline.

### Test-owned cottage fixture

- `Assets/Tests/Features/Fixtures/CottageFixture.cs` exposes `CottageFixture.Build(Allocator, int)` and constructs a `FeatureCatalogue` containing a `FeatureDefinition` named `cottage`.
- `Assets/Tests/Features/Fixtures/CottageProgram.cs` hand-authors the cottage as integer shape-program opcodes.
- The fixture exposes width, depth, wall height, and roof pitch; declares `door` and `hearth` anchors; and uses stone/wood/glass material ids.
- Compatibility defaults are width 64, depth 64, wall height 32, wall thickness 4, an 8-voxel stone foundation, a 12 x 20 south doorway, a 16-voxel gable roof, and a declared 96 x 80 x 96 maximum footprint. Catalogue parameter ranges are width/depth 48..88 in quanta of 8, wall height 24..40 in quanta of 4, and roof pitch 4..12 in quanta of 2.

This fixture remains the narrow `FeatureDefinition`/shape-program compatibility target for WB031.

### Production Kentridge houses

Production house and town content lives in the in-repository package `Packages/com.mountingforce.worldgen`, not in `Assets/Game/Structures`.

- `Runtime/Architecture/Kentridge/KentridgeBuildingGrammar.cs` defines the renderer-independent `StructureForm` and the Kentridge style compiler.
- Generated forms expose footprint form, roof form, frontage rhythm, window treatment, width/depth, storeys, door offset, upper overhang, roof height, optional wing dimensions/side, and chimney side.
- Ordinary generated house grammar covers `Townhouse`, `WideHouse`, `Shop`, and `Inn`. Normal houses are deterministically 66..76 dm wide and 64..74 dm deep; wide houses are 84..96 dm wide and 74..86 dm deep. They are normally two storeys, with deterministic three-storey variation in civic/market districts. Roof, frontage, overhang, door offset, wing, and chimney choices derive from a stable integer hash of seed/role/archetype/district.
- Named house-like roles preserve authored compatibility values. For example, `MayorHouse` resolves to 90 x 78 dm, three storeys, rear wing, twin gable, and warm windows; `AbandonedHouse` resolves to 66 x 66 dm, two storeys, side wing, gable-with-lean-to, and open windows.
- `Runtime/Content/Kentridge/KentridgeDefinition.cs` supplies semantic material defaults through `ArchitectureTheme`: foundation stone, masonry wall, timber frame, glass window, roof tile, dark-masonry accent; with 7 dm foundation height, 4 dm wall thickness, 34 dm floor height, 24 dm door height, 20 dm window base, 12 dm window height, 3 dm beam width, and 4 dm roof overhang.
- Maximum structure envelopes are 104 x 120 x 104 dm for townhouses, 132 x 120 x 132 for wide houses, 124 x 120 x 124 for shops, and 184 x 120 x 184 for inns.

The live showcase call site is `Assets/Game/Composition/Showcase/ShowcaseCatalogue.cs`, which delegates catalogue construction to `MountingForce.WorldGen.Voxel.KentridgeCombinedVoxelCatalogue`.

**Migration seam:** WB031 should reuse the semantic house controls already represented by `StructureForm`/`ArchitectureTheme` while moving reusable wall/opening/roof/foundation mechanics onto the shared authoring components. It must not create a third independent house generator.

## WB002 — castle baseline

The dedicated game structure module is castle-specific.

- `Assets/Game/Structures/Api/CastlePlan.cs` is the plan surface.
- `Assets/Game/Structures/Runtime/CastlePlanner.cs` owns deterministic layout policy.
- `Assets/Game/Structures/Runtime/CastleAuthoringBuild.cs` is the bounded incremental authoring entry point and deliberately preserves legacy keep write ordering.
- `Assets/Game/Composition/Showcase/ShowcaseStructureComposition.cs` is the showcase/application facade: `PlanCastle` terminates at `CastlePlanner.Plan`, while `BeginCastleBuild` creates `AsyncCastleBuildSession` backed by game-owned castle content.
- Authoring is split into focused keep, curtain-wall, courtyard, tower, gatehouse, chapel, dungeon, cave, roofline, room, and site helpers rather than one monolithic geometry method.

`CastlePlanner` uses deterministic seeded choices. Current compatibility policy includes bailey half extents 220..279, plateau height/cliff drop 26..43, curtain-wall height 82..107 and thickness 18..24, corner-tower radius 30..38 and height 125..159, gate-tower radius 28..35 and height 135..171, keep half extents X 92..120 and Z 78..100, floor height 46, and 5..6 floors.

Castle authorers currently choose game material ids directly. The current palette includes stone, dark stone, wood, slate, gold, tile, lit-window, moss, cloth, and empty/carve semantics. WB013 should centralize semantic roles without unintentionally changing the compatibility preset.

Direct castle tests include planner, feature-pipeline, authoring, seed-variation, and structure-property coverage under `Assets/Game/Structures/Tests`.

**Migration seam:** preserve `CastlePlanner`/`CastlePlan` policy and compatibility sequencing while replacing lower-level wall/tower/opening/roof/foundation mechanics with shared components in WB041.

## WB003 — city/settlement baseline

Production settlement generation exists in `Packages/com.mountingforce.worldgen`; `Assets/Game/WorldBuilder` is a higher-level campaign/world hierarchy and is not the geometry builder.

### Settlement planning

- `Runtime/Content/Kentridge/KentridgeTownPlanner.cs` builds the deterministic `SettlementPlan`.
- It authors four named streets, one market plaza, and 17 named plots, then delegates frontage placement/jitter/envelope work to reusable `SettlementPlotLayout` helpers.
- Current road baselines are: main spine X=1170 dm with width 56 dm, market street Z=520 dm with width 48 dm, residential street Z=900 dm with width 44 dm, and east service lane X=1490 dm with width 36 dm.
- Plot identities include church, mayor house, inn, shops, named residential houses, warehouse, mansion, and well. Residential positional jitter is deterministic and bounded.
- `KentridgeDefinition.Build(seed)` is the semantic entry point and supplies the shared architecture theme and per-archetype maximum envelopes.

### Architecture and voxel realization

The settlement planner does **not** directly own house geometry. It emits `BuildingPlot`/`SettlementPlan` intent. `KentridgeDefinition.StructureIntent` hands plots to the lower architecture layer, `ArchitectureCompiler` resolves detailed `StructureForm` data, and the voxel catalogues compile those forms into engine `FeatureDefinition`/shape-program content.

`Runtime/Voxel/KentridgeCombinedVoxelCatalogue.cs` is the aggregate voxel entry point. It can build from a seed/settings pair, semantic hidden-space requests, or an exact `SettlementPlan` plus realized hidden-space geometry. This boundary explicitly prevents campaign/world-builder types from crossing into voxel realization.

The live showcase uses this production path through `Assets/Game/Composition/Showcase/ShowcaseCatalogue.cs`.

**Migration seam:** WB088 should preserve `SettlementPlan`/plot/road/district semantics and change structure selection/realization to consume the shared structure definitions/presets. City composition remains responsible for lots, roads, districts, landmarks, and deterministic placement; it must not absorb duplicate house/church/castle geometry.

## WB004 — castle cave reuse verdict

`Assets/Game/Structures/Runtime/CastleCaveAuthoring.cs` is castle-local, not a reusable cave generator.

It owns the cave layout, castle-relative coordinates, material/decorative choices, carving behavior, and its ellipsoid/noise carving routine. It does not delegate to a generic cave core. The authoritative path also uses floating-point trigonometric/math operations, conflicting with the project's integer deterministic generation constraint.

**Reuse verdict:** preserve useful entrance/layout/material intent as compatibility data, but migrate the algorithm to the generic deterministic cave path in WB051-WB063. Standalone and castle-attached caves must eventually enter the same generator.

## WB005 — reusable feature-authoring capabilities

The reusable engine layer is `Assets/VoxelEngine/Structures`.

- `FeatureDefinition` is the reusable definition boundary and references shared parameter, anchor, slot, program, and material pools.
- `AnchorSpec`/resolved anchors provide named attachment positions and facings.
- `FeatureCatalogue`/`FeatureCatalogueBuilder` own bounded blittable definition data.
- `ShapeOps` and the runtime shape program already support integer box, cylinder, prism, capsule, ramp, rounded-box, ellipsoid, frustum, annulus, and arc-wedge placement/carving plus transforms, bounded repeat/conditionals, deterministic choices, ground sampling, anchors, and slot opcodes.
- `FeatureHash` provides deterministic integer hashing/seed derivation.
- `IStructureAuthoringSession` already exposes reusable immediate helpers including fill/carve/hollow box, gable, crenellation, arch, stairs, and spiral stairs.

Most Phase 1 geometry can therefore be expressed by composition over existing bounded integer primitives. The missing layer is semantic structure configuration, not another voxel engine.

## WB006 — existing helper reuse classification

### Generalize into shared components

- castle curtain-wall runs/courses/openings/parapets -> wall/opening/battlement configs
- castle towers -> tower/turret config
- keep/dungeon room carving -> interior volume/connective-opening config
- keep roofline -> roof/parapet/vertical-accent pieces
- courtyard -> reusable courtyard/open-space composition
- `CastleSiteAuthoring` -> bounded foundation/terrain-adaptation pieces while castle siting policy stays game-owned
- existing `IStructureAuthoringSession` arch/stairs/gable/crenellation helpers -> component implementation primitives rather than duplicated loops
- Kentridge `StructureForm`/`ArchitectureTheme` semantics -> input/migration source for shared house components, not a parallel long-term structure framework

### Keep as archetype/composition policy

- `CastlePlanner` dimension/layout policy and compatibility seed stream
- `CastleAuthoringBuild` orchestration until WB041 replaces its lower-level mechanics
- Kentridge settlement road/plot/district planning (`SettlementPlan`, `KentridgeTownPlanner`) while WB088 changes how plots select/realize structures

### Replace/migrate

- `CastleCaveAuthoring` private cave algorithm -> generic cave generator
- hard-coded material ids distributed through builders -> semantic structure palette with compatibility mappings

## WB007 — compatibility targets

- **House/cottage fixture:** preserve the fixture defaults and integer shape-program behavior until WB031 deliberately migrates it.
- **Kentridge houses:** preserve deterministic `StructureForm` choices, named-role authored dimensions, `ArchitectureTheme` material/dimension defaults, plot envelopes, and seed/role/archetype/district variation behavior unless a migration test records an intentional difference.
- **Castle:** preserve `CastlePlanner` seed behavior/ranges, write-budget refusal, legacy keep write ordering, cardinal placement semantics, and current material intent through an explicit compatibility preset.
- **City/settlement:** preserve Kentridge street/plaza/plot topology, deterministic bounded jitter, role identities, district/frontage assignments, and the `SettlementPlan -> architecture -> voxel catalogue` separation while moving geometry mechanics to shared definitions/presets.
- **Cave:** preserve useful castle entrance/layout/material intent as a migration fixture, but not the floating-point private algorithm as architecture.

## WB008 — engine-extension versus authoring-library gaps

The desired model is primarily missing **semantic authoring composition**, not low-level voxel shapes.

### Authoring-library work; no new engine opcode required initially

- stable structure generation context and semantic child seeds
- semantic material palette roles
- reusable footprint/foundation, wall, floor, opening, roof, stair/ramp, tower, column, buttress, battlement, chimney/spire, room, and courtyard configs/builders
- archetype presets and composition policies
- deterministic dimension/spacing/bounds validation before emission
- adapters from current Kentridge `StructureForm`/`ArchitectureTheme` and castle plans into the shared components during migration

### Existing archetype controls versus gaps

- **House:** Kentridge already models footprint/roof/frontage/window families, dimensions, storeys, door offset, overhang, wings, chimney side, and semantic theme materials. It lacks the target shared component schemas, richer per-facade opening/porch/balcony/interior controls, and a unified engine-facing structure context/palette.
- **Castle:** rich geometry exists, but most dimensions/material choices are planner- or authorer-specific rather than a reusable public config graph.
- **Cave:** castle-local layout/material/decorative controls exist; no reusable integer `CaveConfig` or generic algorithm exists.
- **Shed/cathedral/temple:** no dedicated target schemas exist.
- **Church:** Kentridge has a church archetype/plot and castle has chapel-specific geometry, but neither is the reusable church configuration required by Phase 6.
- **City:** Kentridge already has deterministic roads, plaza, plots, districts, role identities, architecture handoff, and voxel catalogues. It lacks the target weighted palette of reusable structure presets and generalized lot/district/landmark config.

### Contract gaps to address deliberately

- `FeatureDefinition` exposes slot ranges and the opcode vocabulary includes `CallSlot`, but current runtime slot execution is not yet a complete shared composition mechanism. WB028 should extend it only if Phase 1 components truly require runtime slot calls.
- Engine materials are compact ids without shared architectural semantic roles; map roles authoring-side unless serialization proves a lower-level contract is needed.
- Terrain access is intentionally narrow (`BasePlaneRule`/ground sampling). Implement bounded terrain adaptation over those contracts first.
- Existing resolved anchors should carry named architectural attachment semantics rather than introducing a second attachment representation.

## WB009 — cave migration verdict

The castle cave is not the shared cave core. Phase 4 will create one deterministic integer cave path, adapt the castle `Cave` attachment to it, and then remove/deprecate the duplicate castle-local carving algorithm after compatibility/reachability coverage exists.

## Phase 0 conclusion

WB001-WB009 are inventoried. The repository currently has three relevant structure-generation surfaces: the reusable `VoxelEngine.Structures` pipeline, game-owned castle composition, and the in-repository MountingForce/Kentridge settlement + architecture package. The refactor must converge their reusable geometry mechanics on the existing `FeatureDefinition -> ShapeProgram -> Primitive -> voxel` pipeline rather than add another structure framework.