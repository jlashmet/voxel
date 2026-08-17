# Worldbuilding Repository Inventory

This note records the Phase 0 evidence for `worldbuilding-plan.md`. The inventory describes what is actually present on `agent/worldbuilding-structures-caves`; an absent builder is recorded as an absence rather than inferred from a similarly named world-planning type.

## WB001 — house/cottage baseline

There is no production house/cottage builder under `Assets/Game/Structures` on this branch. The current concrete cottage baseline is test-owned feature-authoring content:

- `Assets/Tests/Features/Fixtures/CottageFixture.cs` exposes `CottageFixture.Build(Allocator, int)` and constructs a `FeatureCatalogue` containing one `FeatureDefinition` named `cottage`.
- `Assets/Tests/Features/Fixtures/CottageProgram.cs` exposes `CottageProgram.Build()` and hand-authors the cottage as integer `ShapeProgram` opcodes.
- The fixture's public parameters are width, depth, wall height, and roof pitch. It declares `door` and `hearth` anchors and stone/wood/glass material ids.
- The fixture is intentionally test-owned and is the current compatibility surface to migrate in WB031 rather than evidence of a separate production `HouseBuilder`.

Current cottage compatibility defaults are: width 64, depth 64, wall height 32, wall thickness 4 in the hand-authored program, an 8-voxel stone foundation, a 12 x 20 south doorway, a 16-voxel gable roof, and a declared 96 x 80 x 96 maximum footprint. Catalogue parameter ranges are width/depth 48..88 in quanta of 8, wall height 24..40 in quanta of 4, and roof pitch 4..12 in quanta of 2. The fixture uses deterministic catalogue/instance generation; it contains no independent floating-point house RNG.

No production/showcase house caller exists outside the feature fixture/test path in the branch inventory. The detailed house showcase required by WB040 will therefore be a new migration target over this fixture baseline.

## WB002 — castle baseline

The dedicated game structure module is currently castle-specific.

### Public surface and entry path

- `Assets/Game/Structures/Api/CastlePlan.cs` is the castle plan data surface.
- `Assets/Game/Structures/Runtime/CastlePlanner.cs` exposes `CastlePlanner.Plan(int3 centre, uint seed)` and `EstimateWrites(in CastlePlan)`.
- `Assets/Game/Structures/Runtime/CastleAuthoringBuild.cs` is the bounded incremental authoring entry point. It accepts an `IStructureAuthoringSession`, `CastlePlan`, and terrain seed; `Step()` executes eight semantic stages and refuses plans/builds that exceed the write budget.
- `CastleAuthoringBuild` deliberately preserves legacy write ordering in the keep so migration must not casually reorder overlapping writes.

The direct structure tests are:

- `Assets/Game/Structures/Tests/CastlePlannerTests.cs`
- `Assets/Game/Structures/Tests/CastleAuthoringBuildTests.cs`

The branch tree contains no separate castle showcase builder/definition alongside these entry points. Existing runtime/tests therefore form the castle compatibility surface until WB050 adds explicit preset/showcase coverage.

### Current deterministic dimensions and materials

`CastlePlanner.Plan` seeds `Unity.Mathematics.Random` with `seed | 1u` and preserves a historical RNG draw so refactors do not perturb unrelated dimensions. Current planned ranges/default policy are:

- bailey half extents: 220..279 x 220..279
- plateau radius: bailey diagonal ceiling plus 18..31
- plateau height and cliff drop: 26..43
- curtain-wall height: 82..107
- curtain-wall thickness: 18..24
- corner tower radius: 30..38; height: 125..159
- gate tower radius: 28..35; height: 135..171
- keep half extents: X 92..120, Z 78..100
- floor height: 46; floor count: 5..6; keep height is their product

Castle authorers currently choose game material ids directly. The curtain-wall path demonstrates the compatibility palette currently in use: `Stone`, `DarkStone`, `Wood`, `Slate`, `Gold`, `Tile`, `LitWindow`, `Moss`, `Cloth`, and `Empty` for carving/openings. Other focused castle authorers add their own hard-coded semantic choices; WB013 will centralize those roles without changing the compatibility preset unintentionally.

## WB003 — city/settlement baseline

There is no city/settlement geometry builder or structure definition on the current branch.

`Assets/Game/WorldBuilder` contains campaign/world-hierarchy planning contracts and compilation/runtime orchestration (`CampaignBlueprint`, `CampaignBuilder`, `WorldHierarchy*`, site/story/secret planning, and related handles). Its recursive branch tree contains no `City` or `Settlement` builder/definition and does not own house geometry. `Assets/Game/Structures` likewise contains only castle production authoring. There are consequently no current city structure tests or showcase calls to migrate as a geometry compatibility fixture.

This makes WB088's wording intentionally forward-looking: the future city/settlement composition layer must consume shared structure definitions/presets and bounded placement rules rather than refactor a hidden existing `CityBuilder`. Compatibility for city output is therefore architectural (world-planning contracts and deterministic placement invariants), not a golden geometry snapshot that does not exist.

## WB004 — castle cave reuse verdict

`Assets/Game/Structures/Runtime/CastleCaveAuthoring.cs` is castle-local, not a reusable cave generator.

It owns the cave layout, castle-relative coordinates, material/decorative choices, carving behavior, and its ellipsoid/noise carving routine. It does not delegate to a generic cave core. The authoritative authoring path also uses floating-point trigonometric/math operations, so preserving the implementation as a generic deterministic cave engine would conflict with the project's integer deterministic generation constraint.

**Reuse verdict:** there is no generic cave algorithm here that should remain as the shared cave implementation. Useful intent/defaults may be migrated as a castle cave compatibility preset, but the algorithm itself must move to the generic deterministic cave path described by Phase 4.

**Chosen migration path:**

1. Define the reusable deterministic cave configuration/generation path in WB051-WB062.
2. Preserve relevant castle entrance/layout/material intent as data/configuration rather than private cave code.
3. Route the castle's `Cave` attachment through the same generic path used by standalone caves (WB049/WB062-WB063).
4. Retire/deprecate the duplicate castle-local carving algorithm after compatibility and reachability tests cover the migration.

## WB005 — reusable feature-authoring capabilities

The reusable engine layer lives under `Assets/VoxelEngine/Structures`, not under the game castle module.

### Definition/catalogue contracts

`Assets/VoxelEngine/Structures/Api/FeatureDefinition.cs` already provides the reusable definition boundary: feature kind, base-plane rule, declared integer footprint, slope/precedence policy, ranges into shared parameter/anchor/slot/program/material pools, and a proved maximum primitive count. `FeatureCatalogue`/`FeatureCatalogueBuilder` own the blittable shared pools and placement definitions.

`AnchorSpec` and resolved anchors provide named attachment positions/facings. Slots are represented in catalogue/definition ranges, but current slot execution is not yet a usable composition mechanism (see WB008).

The material surface is presently a compact list of byte material ids. This is efficient and deterministic but has no semantic roles such as `PrimaryWall`, `Roof`, or `Trim`; that semantic layer belongs in the shared authoring model.

### Shape-program/runtime capabilities

`Assets/VoxelEngine/Structures/Runtime/ShapeProgram.cs` is an integer, bounded evaluator. It already supports primitive emission for boxes, cylinders, prisms, capsules, ramps, rounded boxes, ellipsoids, frustums, annuli, and arc wedges. It also supports transform push/pop, bounded repeat/conditionals, deterministic draw ranges, arithmetic, terrain sampling, and anchor output.

The evaluator receives origin, cardinal orientation, terrain seed, and instance seed. It may sample terrain through `TerrainQuery` but does not inspect already-generated voxel state, preserving region-order independence. `FeatureGeneration`/`FeatureRegionBuild` provide the broader deterministic feature generation path, while runtime emitters/rasterization remain the primitive-to-voxel layer.

Terrain adaptation currently consists of definition base-plane policy plus explicit `SampleGround` access. Higher-level foundations, skirts, retaining behavior, and bounded cut/fill semantics are authoring-library concepts unless a proven missing primitive requires an engine extension.

## WB006 — existing helper reuse classification

The castle implementation contains useful behavior but it is organized by castle semantic stage rather than shared architectural component. Classification for refactoring is:

### Generalize into shared components

- `CastleCurtainAuthoring` wall runs, material courses, openings/slits, and parapets -> wall-run/opening/battlement configs.
- `CastleTowerAuthoring` -> tower/turret config and reusable tower authoring.
- `CastleKeepRoomAuthoring` and dungeon room/connective carving -> interior volume/opening configs.
- `CastleKeepRooflineAuthoring` -> roof/parapet/vertical-accent pieces where geometry is archetype-neutral.
- `CastleCourtyardAuthoring` -> courtyard/open-space composition.
- `CastleSiteAuthoring` -> bounded foundation/terrain-adaptation pieces; castle siting policy stays game-owned.
- repeated `Arch`, `Box`, `Cylinder`, `Cone`, ramp/roof-like calls -> shared component emitters over the existing `IStructureAuthoringSession`/shape primitives rather than copied loops.

### Keep as castle composition/policy

- `CastlePlanner` dimension/layout policy and compatibility RNG stream.
- `CastleAuthoringBuild` high-level castle stage orchestration until WB041 replaces stages with shared component composition while retaining compatibility ordering.
- gatehouse, great-hall wing, chapel, keep facade/oriel, and landscape classes where the *semantic arrangement* is castle-specific; their lower-level wall/opening/roof/tower mechanics should delegate to shared components.

### Replace/migrate

- `CastleCaveAuthoring`'s private cave algorithm -> generic cave generator (WB049/WB051-WB063).
- hard-coded material-role choices distributed across castle authorers -> semantic structure palette (WB013), with a castle compatibility palette preserving current ids.

## WB007 — compatibility targets

Compatibility means preserving deterministic authored intent where an output exists, not freezing implementation structure.

- **House/cottage:** preserve the fixture geometry/defaults listed under WB001 until the shared-house tests deliberately approve a migration.
- **Castle:** preserve `CastlePlanner` seed behavior/ranges, write-budget refusal behavior, the legacy keep write ordering called out in `CastleAuthoringBuild`, cardinal placement semantics, and current hard-coded game material choices through an explicit compatibility preset before varying them.
- **City/settlement:** no existing geometry output exists to snapshot. Preserve the existing `WorldBuilder` deterministic world-hierarchy/planning boundary; new city geometry must be introduced as bounded composition rather than changing campaign/world hierarchy semantics.
- **Cave:** preserve useful castle entrance/layout/material intent as a migration fixture, but do not preserve the floating-point private algorithm as the architecture.

## WB008 — engine-extension versus authoring-library gaps

The desired worldbuilding model is mostly missing **semantic authoring composition**, not low-level voxel shapes.

### Authoring-library work; no new engine opcode required initially

- stable structure generation context and semantic child seeds
- semantic material palette roles
- footprint/foundation, wall, floor, opening, roof, stair/ramp, tower, column, buttress, battlement, chimney/spire, room, and courtyard config/builders
- archetype presets and composition policies
- deterministic validation of dimensions/spacing/bounds before emission

Existing integer primitives and shape-program control flow are already sufficient to prove the rectangular house/shared-component path and a large fraction of castle refactoring. New opcodes must therefore be justified by geometry that cannot be expressed boundedly with the current primitive set.

### Actual engine/contract gaps to address deliberately

- `FeatureDefinition` exposes slot ranges, and the opcode vocabulary includes `CallSlot`, but the current `ShapeProgram` evaluator's `CallSlot` case performs no composition. If runtime shape-program slot composition is required by the shared model, WB028 must implement/validate that contract rather than hiding composition in a parallel builder framework.
- materials are raw byte ids without semantic structure roles; the role mapping can remain authoring-side unless catalogue serialization needs to persist it.
- terrain access is intentionally narrow (`BasePlaneRule`/`SampleGround`); reusable bounded terrain adaptation should first be authored from those contracts and only extend the engine if a required deterministic operation is impossible.
- the evaluator already produces resolved anchors, so named architectural attachment semantics should layer over anchors rather than introduce another attachment representation.

## Phase 0 conclusion

WB001-WB009 are now inventoried. The repository has one production structure family (castle), one test-owned cottage feature baseline, no production city/settlement geometry builder, and a castle-local cave algorithm that must be migrated. Phase 1 should therefore add the missing shared deterministic authoring model directly over `VoxelEngine.Structures` contracts, while game-specific castle/house/city semantics remain outside the engine.