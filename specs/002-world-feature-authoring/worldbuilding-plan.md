# Detailed Plan: Configurable Worldbuilding Structures and Caves

**Branch**: `agent/worldbuilding-structures-caves`  
**Base**: current `master` at branch creation  
**Parent feature**: [World Feature Authoring](./plan.md)  
**Status**: implementation checklist; update this file as tasks complete

## Purpose

Extend the existing deterministic world-feature authoring system from its current primitive/shape-program foundation into a reusable worldbuilding library capable of producing richly varied houses, castles, settlements, churches, cathedrals, temples, sheds, and underground cave systems.

This work must **not** create a second structure-generation architecture. Structure archetypes are authoring/composition layers over the existing `FeatureDefinition` -> integer `ShapeProgram` -> `Primitive` -> authoritative voxel pipeline. Caves likewise remain deterministic CPU-side voxel-generation input, never a separate gameplay representation.

The central design goal is **extreme configurability with sane presets**:

- Low-level dimensions, ratios, counts, materials, placement, facade, roof, opening, ornament, interior, and terrain-integration controls remain available.
- Named presets/archetypes provide useful defaults without hiding or replacing those controls.
- Reusable components are shared between structure types instead of copied into one-off builders.
- Every randomized choice derives from deterministic integer seeds.
- Generated geometry remains valid across region boundaries and independent of generation order.
- Current authored showcase structures should remain reproducible or deliberately migrated with recorded differences.

## Non-negotiable architecture constraints

These are inherited from `CLAUDE.md`, the feature plan, and the project constitution.

1. Authoritative generation is deterministic, integer, CPU-side, and Burst-compatible.
2. The brickmap/voxel cells remain the single gameplay truth; structure/cave definitions and primitives are generation inputs only.
3. No `com.unity.entities` and no Netcode for GameObjects.
4. Structure existence and authoritative geometry may not vary by device tier.
5. Region generation stays bounded by declared footprints, primitive limits, and candidate limits.
6. No hidden global structure registry is required for immutable generated geometry.
7. Existing shape-program and catalogue contracts are extended when needed rather than bypassed.
8. Unity validation, when eventually run, must use `tools/unity-run.sh`; never invoke Unity directly.

## Configuration model

The target public authoring model is layered so simple use stays simple while detailed use remains possible.

### Layer A — deterministic generation context

Every structure/cave instance receives stable identity, world seed, definition id, instance seed, origin, cardinal orientation, terrain sample access, declared footprint/bounds, and resolved attachment anchors. Child components derive sub-seeds from stable semantic keys so adding an unrelated detail does not reshuffle every other random choice.

### Layer B — shared architectural components

Reusable component configs/builders should cover at least:

- footprint/foundation and terrain adaptation
- wall runs, corners, thickness, height, courses, trim, and material bands
- floors/levels and floor slabs
- openings: doors, windows, arches, niches, shutters, frames, lintels
- roofs: flat, shed/lean-to, gable, hip, cross-gable where practical, pitch, eaves, ridge, parapet
- stairs, ramps, landings, porches, balconies, arcades
- vertical features: towers, turrets, chimneys, bell towers, spires, domes where supported
- structural/decorative features: columns, colonnades, buttresses, flying-buttress approximation, crenellations, battlements
- room/interior volume carving and connective openings
- courtyards and enclosed open space
- attachment anchors for entrances, roads, extensions, crypts, caves, and neighboring composed features
- material palettes with semantic roles instead of hard-coded material ids throughout builders

### Layer C — archetype-specific configuration

House, shed, castle, church, cathedral, and temple configs compose Layer B and add only semantics unique to the archetype.

### Layer D — presets

Presets choose coherent values for Layers B/C (for example cottage, farmhouse, chapel, parish church, gothic cathedral, keep castle, walled castle, storage shed, columned temple). Presets are ordinary config factories/data; every chosen value can still be overridden.

### Layer E — settlement/city composition

Cities/settlements consume a weighted palette of structure definitions/presets, lot rules, road/spacing rules, district rules, landmark rules, and deterministic placement logic. They do not own duplicate house/church/castle geometry code.

## Cave model

Caves become a first-class reusable generator with two equivalent entry modes:

1. **Standalone cave network** driven by world/cave seed and one or more surface/underground anchors.
2. **Attached cave network** driven by a structure attachment anchor, such as a castle dungeon passage or cathedral crypt opening.

Both modes must enter the same cave generation path. Castle-specific code may request/configure a cave but must not contain a private cave algorithm after migration.

Target cave controls include entrance count and style, tunnel width/height, segment length, turn tendency, verticality, branch probability/count, chamber probability/size, depth range, floor/ceiling roughness, wall roughness, dead ends, loops where deterministic/local guarantees permit them, material palette, decoration/resource hooks, water hooks, and explicit keep-out/required-connection constraints.

The existing portal/lattice design from the parent world-feature plan remains the starting point for cross-region cave continuity. If the current castle cave predates or bypasses that design, it will be migrated rather than preserved as a competing implementation.

---

# Task Checklist
## Phase 0 — Repository inventory and compatibility baseline

- [x] **WB001** Locate the current house/cottage builder/definition, its public config surface, tests, and showcase usage.
- [x] **WB002** Locate the current castle builder/definition, its public config surface, tests, and showcase usage.
- [x] **WB003** Locate the current city/settlement builder/definition, its public config surface, tests, and showcase usage.
- [x] **WB004** Trace the castle-attached cave end-to-end and determine whether its algorithm is generic, partially reusable, or castle-local.
- [x] **WB005** Inventory current reusable `FeatureDefinition`, shape-program, primitive, anchor/slot, terrain-adaptation, palette, and composition capabilities relevant to structures.
- [x] **WB006** Inventory existing roof, opening, wall, tower, interior, and terrain helper code so the refactor reuses working pieces.
- [x] **WB007** Record the current default dimensions/materials/seed behavior of existing house, castle, and city output that should remain compatible.
- [x] **WB008** Identify gaps between the desired configuration model and current shape opcodes/contracts; explicitly distinguish required engine extensions from authoring-library work.
- [x] **WB009** Record the cave-reuse verdict and the chosen migration path in this document.

Phase 0 evidence and compatibility targets are recorded in [worldbuilding-inventory.md](./worldbuilding-inventory.md).

## Phase 1 — Shared deterministic authoring foundation

- [x] **WB010** Define a stable structure generation context carrying instance identity, seed, origin/orientation, bounds, terrain access, palette, and anchor output.
- [x] **WB011** Add deterministic semantic child-seed derivation so optional details do not perturb unrelated generated details.
- [x] **WB012** Define common dimension/range validation and clamping/rejection policy for authored structure configs.
- [x] **WB013** Define semantic material palette roles (foundation, primary wall, secondary wall, trim, roof, floor, column, accent, underground, etc.) mapped to voxel materials.
- [x] **WB014** Define reusable footprint/foundation configuration, including rectangular footprints first and extension points for composed footprints.
- [x] **WB015** Define reusable wall-run configuration: thickness, height, material bands, corner behavior, and repetition spacing.
- [x] **WB016** Define reusable floor/level configuration: floor count, per-level height, slab thickness, and optional level variation.
- [x] **WB017** Define reusable opening configuration for doors/windows/arches, including size, spacing, margins, frame/lintel options, and deterministic variation.
- [x] **WB018** Define reusable roof configuration for flat, shed/lean-to, gable, and hip roofs using existing integer primitives where possible.
- [x] **WB019** Define reusable stairs/ramps/landing configuration.
- [x] **WB020** Define reusable tower/turret configuration including shape, dimensions, count/placement semantics, roof/top style, and openings.
- [x] **WB021** Define reusable column/colonnade configuration.
- [x] **WB022** Define reusable buttress configuration with an extension point for flying-buttress approximation.
- [x] **WB023** Define reusable battlement/crenellation/parapet configuration.
- [x] **WB024** Define reusable chimney/spire/vertical-accent configuration where shapes overlap; keep archetype-only semantics outside the shared type.
- [x] **WB025** Define interior volume/room carving and connective-opening configuration sufficient for navigable generated interiors.
- [x] **WB026** Define reusable courtyard/open-space composition.
- [x] **WB027** Define named attachment-anchor semantics (`MainEntrance`, `RearEntrance`, `Road`, `Basement`, `Crypt`, `Cave`, `Extension`, etc.) without coupling consumers to structure internals.
- [x] **WB028** Extend catalogue/shape-program contracts only where shared components cannot be expressed with current deterministic ops.
- [x] **WB029** Add validation tests for invalid dimensions, impossible opening spacing, unsupported roof combinations, bounds overflow, and primitive-budget overflow.
- [x] **WB030** Add deterministic tests proving the same config/seed produces identical primitive/voxel output and stable semantic sub-seeds.

## Phase 2 — House/detail pass

- [x] **WB031** Refactor the current house/cottage authoring path to use shared structure components without changing the default result unintentionally.
- [x] **WB032** Expose house dimensions, floor count, floor height, wall thickness, foundation behavior, and material palette.
- [x] **WB033** Expose roof type, pitch, ridge orientation, eaves/overhang, roof material, and optional dormer-capable extension point.
- [x] **WB034** Expose front/rear/side door counts and placement rules, door dimensions, frames, and optional porch/step treatment.
- [x] **WB035** Expose per-facade window layout, dimensions, spacing, sill/head height, frames, and deterministic variation.
- [x] **WB036** Add configurable chimney placement/size/material and optional fireplace/interior hook.
- [x] **WB037** Add configurable porch/awning/balcony hooks using shared components where supported.
- [x] **WB038** Add interior floor/room/doorway hooks sufficient for later richer house layouts without baking one layout into the geometry layer.
- [x] **WB039** Add house presets demonstrating materially different output from one builder/config type.
- [ ] **WB040** Add house invariants/determinism/footprint tests and update showcase usage to exercise at least one detailed configuration.

## Phase 3 — Castle/detail pass

- [ ] **WB041** Refactor the current castle path to shared wall/tower/opening/battlement/foundation components while preserving a compatibility preset.
- [ ] **WB042** Expose keep dimensions, levels, wall thickness, roof/parapet/top style, openings, and material palette.
- [ ] **WB043** Expose curtain-wall polygon/rectangular dimensions, height/thickness, wall segmentation, and battlement controls.
- [ ] **WB044** Expose tower count/placement, corner/intermediate towers, square/round-compatible shape choice, radius/width, height, taper/top, roof, openings, and crenellations.
- [ ] **WB045** Add configurable gatehouse width/depth/height, gate opening, flanking towers, portcullis-ready opening hook, and road anchor.
- [ ] **WB046** Add courtyard configuration and slots/anchors for secondary buildings.
- [ ] **WB047** Add optional moat/ditch terrain-carve configuration if expressible within current bounded terrain adaptation; otherwise record it as a separate follow-up rather than hiding an unbounded carve.
- [ ] **WB048** Add basement/dungeon/crypt-style interior attachment points beneath the keep/gatehouse.
- [ ] **WB049** Replace any castle-private cave generation with a request into the generic cave system and a `Cave` attachment anchor.
- [ ] **WB050** Add castle presets (at minimum keep-only and walled-castle) plus determinism, seam, footprint, and attachment tests.

## Phase 4 — Reusable cave system

- [ ] **WB051** Define `CaveConfig` (or equivalent existing contract extension) as a reusable deterministic authoring configuration independent of castles.
- [ ] **WB052** Define cave entrance/portal configuration with surface, structure-attached, and underground anchor modes.
- [ ] **WB053** Implement/complete reusable tunnel path generation with integer-only deterministic turns and bounded segment counts.
- [ ] **WB054** Expose tunnel width, height, segment length, horizontal turn tendency, and verticality controls.
- [ ] **WB055** Expose branching controls: branch chance/count/depth, minimum separation, and bounded recursion/iteration.
- [ ] **WB056** Expose chamber controls: frequency, dimensions/ranges, shape choice supported by existing primitives, and connection rules.
- [ ] **WB057** Expose floor, ceiling, and wall roughness using deterministic integer noise/variation that cannot break required traversability guarantees.
- [ ] **WB058** Expose overall depth range, bounds/footprint, entrance clearance, and terrain-surface avoidance rules.
- [ ] **WB059** Add loop/reconnection support only if it can remain region-local/deterministic under the existing cave-lattice contract; otherwise document the limitation explicitly.
- [ ] **WB060** Add semantic cave palette roles and decoration/resource/water attachment hooks without putting gameplay loot state into immutable generation.
- [ ] **WB061** Guarantee required attachment connections remain traversable after roughness/chamber passes.
- [ ] **WB062** Make standalone caves and structure-attached caves invoke the same generation code path.
- [ ] **WB063** Migrate the existing castle cave configuration/output to the generic cave path and remove or deprecate duplicate castle-local cave logic.
- [ ] **WB064** Add cave determinism, cross-region continuity, entrance reachability, bounds, branch/chamber, and castle-attachment tests.

## Phase 5 — Shed archetype

- [ ] **WB065** Implement a configurable shed archetype using shared footprint/wall/opening/roof components.
- [ ] **WB066** Expose shed width/depth/height, wall thickness, foundation, material palette, door count/size/placement, optional windows, and lean-to/gable/flat roof controls.
- [ ] **WB067** Add storage/workshop/lean-to presets and deterministic geometry tests.

## Phase 6 — Church archetype

- [ ] **WB068** Define church-specific plan configuration: nave length/width/height, optional aisles, sanctuary/chancel, apse, and entry orientation.
- [ ] **WB069** Implement configurable nave/aisle wall and roof composition using shared components.
- [ ] **WB070** Add configurable apse/sanctuary geometry using available radial/rounded primitives where appropriate.
- [ ] **WB071** Add bell-tower/steeple/spire options with tower placement and height controls.
- [ ] **WB072** Add church facade/opening controls: primary portal, side doors, regular windows, clerestory-ready hook, and trim palette.
- [ ] **WB073** Add chapel/parish-church presets and deterministic/navigation/footprint tests.

## Phase 7 — Cathedral archetype

- [ ] **WB074** Build cathedral configuration as an extension/composition of church semantics rather than a forked implementation.
- [ ] **WB075** Add configurable transept, crossing, expanded choir/apse, and optional side chapels.
- [ ] **WB076** Add configurable multi-aisle proportions and clerestory/window bands.
- [ ] **WB077** Add configurable buttresses and flying-buttress approximation using shared buttress primitives.
- [ ] **WB078** Add configurable west-front towers/spires and alternate central crossing tower/spire.
- [ ] **WB079** Add configurable rose-window/large-facade-opening approximation through existing opening/arch primitives.
- [ ] **WB080** Add optional crypt volume plus `Crypt`/`Cave` attachment anchors so underground content composes cleanly.
- [ ] **WB081** Add gothic-style and simpler cathedral presets plus determinism, seam, footprint, primitive-budget, and navigability tests.

## Phase 8 — Temple archetype

- [ ] **WB082** Define temple plan configuration around sanctuary/cella, platform, approach axis, stairs, courtyard, and colonnade.
- [ ] **WB083** Implement configurable raised platform/foundation and monumental stair approach.
- [ ] **WB084** Implement configurable perimeter/front colonnades using shared column components.
- [ ] **WB085** Add sanctuary/cella dimensions, inner rooms, entry opening, and optional courtyard.
- [ ] **WB086** Add configurable roof/top families that are actually supported by current primitives; expose extension hooks for dome/tower/pagoda families rather than faking unsupported geometry.
- [ ] **WB087** Add classical-columned and courtyard-temple presets plus deterministic/navigation/footprint tests.

## Phase 9 — City/settlement composition

- [ ] **WB088** Refactor current city/settlement generation to consume structure definitions/presets through a weighted structure palette instead of directly owning house geometry.
- [ ] **WB089** Define lot configuration: width/depth ranges, setbacks, orientation, road frontage, spacing, and occupancy constraints.
- [ ] **WB090** Define deterministic road/street-facing placement hooks compatible with the existing placement system.
- [ ] **WB091** Add weighted archetype selection by district/zone while preserving deterministic candidate identity.
- [ ] **WB092** Add landmark rules so churches, cathedrals, temples, castles, or civic-scale structures can be rare intentional placements rather than ordinary repeated lots.
- [ ] **WB093** Add density/spacing controls and explicit open-space/plaza hooks.
- [ ] **WB094** Ensure city composition remains bounded and region-local; reject configurations that would require unbounded global planning.
- [ ] **WB095** Add mixed-archetype city tests for determinism, overlap/spacing, road-facing anchors, landmark rarity rules, and region seams.
- [ ] **WB096** Update the showcase/demo city to exercise multiple structure archetypes and visibly varied configurations.

## Phase 10 — Presets, examples, and authoring ergonomics

- [ ] **WB097** Establish a consistent preset naming/versioning convention and keep presets pure data/config factories.
- [ ] **WB098** Add examples showing both one-line preset use and deep per-component overrides.
- [ ] **WB099** Add examples for standalone cave generation and structure-attached cave generation.
- [ ] **WB100** Add examples showing semantic seed stability: changing a chimney/window option must not reshuffle unrelated structure choices.
- [ ] **WB101** Update world-feature quickstart/contracts where new reusable configuration or anchor semantics become public API.
- [ ] **WB102** Extend authoring preview/inspection hooks so a designer can see chosen preset, resolved parameters, anchors, footprint, primitive count, and validation failures.

## Phase 11 — Validation and completion

- [ ] **WB103** Run non-Unity static/build validation available in the repo without violating the Unity-run rule.
- [ ] **WB104** Run the relevant EditMode tests through `tools/unity-run.sh` only when it is safe/authorized to run Unity in the developer environment.
- [ ] **WB105** Run relevant PlayMode/seam/determinism tests through the same guarded wrapper when safe/authorized.
- [ ] **WB106** Verify no authoritative structure/cave generation path introduced floating-point state or GPU-derived truth.
- [ ] **WB107** Verify generated structures/caves respect declared footprint/bounds and report budget overflow rather than silently truncating.
- [ ] **WB108** Verify all existing house/castle/city call sites are migrated or intentionally retained with compatibility shims documented.
- [ ] **WB109** Reconcile this checklist against actual implementation, checking completed tasks and adding any discovered follow-up tasks explicitly.
- [ ] **WB110** Record final cave-reuse outcome, compatibility notes, test results, known limitations, and next recommended worldbuilding increments.

---

## Planned implementation order

The checklist is intentionally broader than the first coding batch. Implementation should proceed in dependency order:

1. Inventory the exact current builders and cave path.
2. Fill the smallest missing shared configuration/composition primitives required by those builders.
3. Refactor/enrich **house** first as the proving ground for the common structure model.
4. Refactor/enrich **castle**, including its underground attachment contract.
5. Extract/complete the **generic cave** path and migrate the castle cave to it.
6. Implement **shed** as the simplest proof that new archetypes are cheap to add.
7. Build **church**, then **cathedral** on shared church/components.
8. Build **temple** using the same composition layer.
9. Refactor **city/settlement** to consume the expanded structure palette.
10. Finish presets, examples, inspection tooling, compatibility work, and validation.

This order intentionally avoids implementing six independent builders before proving the shared component model. If a new archetype requires copying wall/roof/opening/tower logic, stop and move that behavior into the shared layer first.

## Completion policy

- Check a task only when the code/documentation described by that task is actually present on this branch.
- Keep this file updated in the same implementation commits or in immediately following checklist commits.
- Do not mark Unity validation tasks complete unless the corresponding guarded Unity run actually occurred.