# Architecture Proposal: Composable Voxel Surfaces

**Status**: Partially implemented; authored-boundary preservation remains open
**Created**: 2026-08-12
**Related specifications**: [001 Destructible Voxel Engine](../001-destructible-voxel-engine/spec.md), [002 World Feature Authoring](../002-world-feature-authoring/spec.md)
**Motivating scenario**: A curved stone arch with moss whose shape, material behavior, and neighbor transitions remain correct across chunks and regions.

## 1. Decision summary

The codebase should keep multiple render domains, but it should not keep multiple competing interpretations of solid voxel state.

The proposed architecture is:

1. Keep the discrete voxel grid authoritative for simulation, destruction, collision, persistence, and networking.
2. Represent a solid cell with independent base-material, surface-style, and coating semantics.
3. Preserve compact authored boundary constraints at mixed cells; occupancy alone cannot recover
   the sub-voxel boundary of an annulus, wedge, bevel, or other authored primitive.
4. Replace the split smooth-versus-hard solid paths with one feature-aware solid-surface extraction contract.
5. Retain separate specialized render domains for water, vegetation, debris, and other genuinely different phenomena.
6. Define explicit, symmetric rules for how adjacent surface styles join and how curvature propagates across their boundary.
7. Extend the deterministic feature primitive language with curved architectural primitives rather than constructing curved features through floating-point runtime brushes.
8. Route voxel changes through a versioned change journal and a common build scheduler instead of shared mutable upload sets.

This preserves the strengths of the existing architecture—deterministic generation, chunked data, specialized rendering, and progressive GPU migration—while removing the ambiguity that currently makes curved mixed-material structures difficult.

### Implementation record

The implementation deliberately used a direct cutover rather than a compatibility migration:

- `VoxelCell` now stores base material separately from compact surface semantics.
- `MaterialPalette`, `SurfaceCatalogue`, `CoatingCatalogue`, and
  `MaterialAdjacencyCatalogue` own their distinct rule sets.
- `CpuTransvoxelChunkCache` is the sole opaque-solid extractor and consumes per-cell styles,
  coatings, material defaults, and the symmetric surface join table. Its curved-density and
  constrained greedy-facet phases feed one chunk entry, allowing cubic/planar and curved cells
  to coexist without renderer selection. Water remains a separate topology domain.
- `VoxelSurfaceScheduler` owns solid/water derived state, consumes `VoxelChangeJournal`, discovers
  surface bricks directly from authoritative regions, expands invalidation to affected bricks and
  halos, and rejects stale solid builds. No full brick-pool GPU mirror remains.
- The world view supplies the immutable, hashed surface catalogue to extraction. Custom style and
  pairwise join rules are therefore production inputs; changing their version/hash invalidates
  derived solid chunks instead of requiring renderer code changes.
- Curved integer primitives and `ArchFeatureDefinition` provide rounded piers, annular voussoirs,
  typed sockets, and moss as a coating over stone. The current rasterizer retains occupancy and
  surface semantics but discards the primitive's sub-voxel boundary. Consequently, the unified
  extractor can choose smooth versus planar reconstruction, but cannot reproduce the authored
  annulus exactly. That missing boundary contract is not considered implemented.
- Detached-voxel presentation snapshots both base material and coating before clearing the
  authoritative cell. Debris therefore retains moss/snow/soot/wet appearance while destruction,
  mass, and collision behavior continue to come from the base material.
- The hard-brick classifier, GPU solid extractor, raymarch density path, showcase fallback, and
  their obsolete tests and assets were removed. The unused legacy debris pool/renderer was also
  removed; showcase falling-voxel debris is now the sole debris owner.

The structural acceptance suite is recorded in `VoxelSurfaceArchitectureTests`, with existing persistence,
subvolume, storage-growth, and showcase tests extended for the new cell contract. GPU-mirror
lifetime tests were removed with the obsolete mirror they covered. The visual arch criterion is
still failing and must not be inferred from those structural tests.

## 2. Goals

- Render cubic, planar, beveled, rounded, and smoothly curved voxel surfaces in the same chunk.
- Allow materials to define simulation behavior independently from visual geometry.
- Allow coatings such as moss, snow, soot, or wetness without replacing the underlying material.
- Specify deterministic primitives and constraints for authored structures.
- Specify what may be adjacent to what at both the feature and voxel-surface levels.
- Make curvature continuity across neighboring cells intentional and testable.
- Preserve exact subvolume generation and cross-chunk determinism.
- Keep rendering incremental, bounded, and safe against stale asynchronous results.

## 3. Non-goals

- Curved voxel collision is not required initially. Collision may continue to use discrete occupied cells or an independently generated coarse collider.
- All visual phenomena do not need one shader or one draw call.
- The authoritative world does not become an arbitrary triangle mesh or floating-point signed-distance field.
- Materials do not directly select renderer implementation classes.
- This proposal does not require DOTS Entities or Netcode for GameObjects.

## 4. Current architecture assessment

The project has several rendering paths:

- GPU Surface Nets migration through `GpuSurfaceChunkCache`.
- CPU Transvoxel smooth terrain extraction.
- CPU greedy hard-surface meshing.
- A dedicated water-surface path.
- A density-building compute path still carrying the historical `BrickRaymarch` name.
- A showcase `VoxelSurfaceRenderer` fallback.
- Separate procedural vegetation and debris rendering.

Having several paths is not inherently wrong. Water, translucent effects, vegetation, and short-lived debris have different topology, lifetime, and shading requirements from solid terrain. The architectural problem is that solid voxel meaning is divided across incompatible classifiers and renderers.

### 4.1 Brick-level hardness is too coarse

`Region.HardSurfaceWords` classifies an entire 8×8×8 brick as hard. A hard brick is excluded from smooth extraction, and all renderable contents in it are sent to hard-surface meshing.

That cannot correctly express a planar pier beside a curved arch, moss over stone, or smooth terrain touching architectural masonry within the same brick. The classification is both spatially coarse and semantically overloaded.

### 4.2 Material identity is overloaded

Material currently risks controlling several unrelated properties:

- simulation behavior;
- destruction and hardness;
- surface reconstruction;
- shader appearance;
- environmental overlays.

In the arch work, moss replacing stone illustrates the failure: a visual coating can accidentally change structural behavior and surface classification. These concepts require separate identities even if storage later packs them together.

### 4.3 Curvature metadata does not control the final silhouette

The arch branch moves in the right direction by introducing material roundness and density flags, but the arch is tagged as hard at a broader level. It therefore travels through a renderer that produces blocky or planar geometry, so curvature metadata cannot influence the silhouette.

Surface style must be consumed by the extractor that owns the final solid boundary. It cannot be advisory metadata attached downstream.

### 4.4 Feature authoring and runtime rendering disagree

The existing feature intermediate representation is deterministic, integer-based, and capable of exact subvolume evaluation. It supports boxes, cylinders, prisms, capsules, ramps, and an arch profile. The newer arch construction uses useful concepts—typed sockets and joint-first voussoir layout—but relies on a separate floating-point brush pipeline.

This creates two authoring models with different reproducibility and clipping behavior. Curved architectural construction should extend the deterministic feature language instead.

### 4.5 Invalidation is shared mutable state

A shared `RegionsNeedingUpload`-style collection does not fully describe what changed, which data version a build consumed, or which downstream products need rebuilding. As render paths multiply, this produces redundant work and creates opportunities for stale meshes to win races.

## 5. Target architecture

```text
Feature definitions and procedural rules
        |
        v
Deterministic primitive evaluator
        |
        v
Authoritative voxel cells + change journal
        |
        +--------------------+----------------------+------------------+
        |                    |                      |                  |
        v                    v                      v                  v
Solid surface domain    Water domain         Vegetation domain   Debris domain
        |                    |                      |                  |
        v                    v                      v                  v
Unified solid extractor  Specialized build     Instance build      Effect build
        |
        v
Geometry buffers + material/style/coating attributes
        |
        v
Render pass coordination, culling, residency, and draw submission
```

The word *unified* applies to solid-boundary interpretation. It does not mean that water, leaves, particles, and opaque solids must share topology generation.

## 6. Authoritative voxel semantics

At the logical level, a cell should expose the following independent values:

```text
VoxelCell
  occupancy or density
  baseMaterialId
  surfaceStyleId
  coatingId
  optional flags
```

The logical model is more important than its first physical layout. IDs may be bit-packed, palette-indexed, or stored in sparse sidecars after profiling.

### 6.1 Base material

The base material controls simulation semantics:

- solidity;
- durability and destruction class;
- mass or structural category;
- collision category;
- sound and particles;
- gameplay tags;
- default shader material family.

Examples: stone, packed soil, timber, metal, water.

### 6.2 Surface style

The surface style controls boundary reconstruction:

- cubic or faceted;
- planar masonry;
- beveled;
- rounded;
- smooth organic;
- authored profile;
- corner and edge treatment;
- smoothing group and join policy.

Examples: `StoneAshlar`, `StoneRounded`, `TerrainSmooth`, `TimberSquare`.

### 6.3 Coating

A coating is an overlay and must not replace the base material:

- moss;
- snow;
- soot;
- wetness;
- dust or lichen.

Coatings may affect shading, small displacement, decals, particles, and slow gameplay modifiers, but they inherit structural properties from the base material unless an explicit rule says otherwise.

For the motivating arch, a moss-covered stone voxel remains stone for destruction, collision, and structural behavior.

## 7. Catalogues and rule ownership

Use separate data catalogues with stable runtime IDs:

```text
BaseMaterialDefinition
  stableId
  simulation properties
  render material family
  allowed coating classes

SurfaceStyleDefinition
  stableId
  reconstruction mode
  feature-preservation parameters
  curvature parameters
  join group
  fallback policy

CoatingDefinition
  stableId
  shader/displacement properties
  placement constraints
  inheritance and compatibility rules
```

Authoring assets may be ScriptableObjects, but builds should compile them into immutable, deterministic runtime tables. Runtime code should compare stable IDs and compiled flags, not asset references or names.

## 8. Surface adjacency contract

Curvature behavior should be defined by a symmetric pairwise join table, indexed by surface style or join group.

```text
SurfaceJoinRule(A, B)
  compatibility: join | seam | reject | fallback
  continuity: discontinuous | tangent | smooth
  blendWidth
  dominantSide: A | B | neutral
  preserveSharpFeature
  transitionStyleId
```

Required properties:

- Symmetry is explicit: looking up `(A, B)` and `(B, A)` yields equivalent geometry.
- Missing rules select a documented conservative fallback, normally a sharp seam.
- Rules are data, not renderer-specific conditionals.
- Neighbor sampling includes the one-cell halo needed across chunk and region boundaries.
- A rule cannot silently alter the base material.

Example outcomes:

| Pair | Result |
|---|---|
| Smooth terrain + smooth terrain | Continuous smooth surface |
| Rounded stone + rounded stone | Tangent-continuous join where occupancy permits |
| Planar masonry + rounded stone | Preserved seam or short bevel |
| Stone + moss coating | Same stone geometry with coating attributes |
| Solid + water | Separate domain boundary |

## 9. Unified solid-surface extraction

The target is one solid extraction contract that can preserve sharp features and create curved ones according to surface-style data. A feature-aware Surface Nets or dual-contouring implementation with constrained vertex placement is the recommended direction.

The extractor receives:

- occupancy or density samples;
- base-material IDs;
- surface-style IDs;
- coating IDs;
- neighbor halo data;
- immutable catalogue and join-table versions;
- source region/chunk version.

It produces:

- positions, normals, and indices;
- base-material attributes;
- surface-style or shading attributes where required;
- coating masks or IDs;
- bounds and build metadata;
- source version for stale-result rejection.

The extractor should support these local modes without switching the entire brick to another renderer:

- snap to planar or cubic constraints;
- solve a smooth vertex from neighboring planes;
- retain a sharp edge or corner;
- apply a rounded transition;
- emit a deliberate seam between incompatible styles.

This removes `HardSurfaceWords` as the authority for solid geometry. The implementation performs
the direct cutover: no compatibility classifier remains, and mixed styles coexist in one brick.

### 9.1 Authored boundary constraints

Occupancy is not enough input for the modes above. Once an annulus is reduced to solid/empty cell
centres, its exact radius and crossing positions are gone. A reconstruction filter can make the
result rounder, but it is guessing a new shape; a cubic mesher can preserve hard cells, but it
produces a staircase. Neither is an acceptable substitute for the curve the feature authored.

Mixed boundary bricks therefore need a compact geometry sidecar, independent of material and
presentation. The preferred first representation is a quantized signed-distance sample at voxel
sample points, with gradients derived during extraction. If testing shows that sharp intersections
or thin joints cannot survive that representation, promote boundary cells to quantized Hermite
constraints (edge crossing plus normal) rather than adding feature-specific renderer branches.

```text
BoundarySample
  signedDistance     fixed-point, negative inside
  provenanceClass    authored | edited | derived (optional compact bits)
```

Rules for this data:

- deterministic primitive rasterization computes distance with integer/fixed-point predicates;
- boolean fill/carve composition updates occupancy and distance together;
- direct voxel edits generate conservative cell-boundary constraints rather than pretending to
  know the destroyed primitive's original curve;
- the sidecar exists for empty samples adjacent to solids as well as occupied samples;
- uniform interior/exterior bricks do not allocate boundary payload;
- persistence, hashing, replication, and the change journal treat boundary changes as geometry;
- collision may continue to use occupancy, but rendering must not recover authored curvature by
  applying an unrelated smoothing style;
- surface style controls how valid constraints join and how normals are shaded; it does not create
  missing geometric information;
- material and coating catalogues never select an arch, stone, or other feature-specific distance
  function in a shader or extractor.

This makes the extractor genuinely feature-aware without retaining live feature objects in the
renderer. Geometry still derives from authoritative voxel state, and destruction naturally replaces
authored constraints only where cells were edited.

## 10. Primitive and feature-authoring changes

Extend the deterministic primitive IR so that architectural curvature is a first-class authored concept. Candidate primitives are:

- rounded box;
- ellipsoid;
- conical frustum;
- extruded 2D profile;
- annulus and half-annulus;
- arc or voussoir wedge;
- capsule chain.

Each primitive operation should carry:

```text
PrimitiveOperation
  shape and integer/fixed-point parameters
  boolean operation
  baseMaterialId
  surfaceStyleId
  optional coatingId
  priority and conflict policy
```

Rasterization must remain deterministic and exact under subvolume evaluation. Fixed-point or integer predicates are preferred. Floating point may be used in editor previews if the compiled runtime representation is deterministic.

### 10.1 Arch definition

The arch should become a `FeatureDefinition` composed of deterministic primitives:

- piers use box or rounded-box operations;
- the ring uses annulus, half-annulus, or voussoir-wedge operations;
- keystone and voussoirs may retain intentional joints through surface-style or seam metadata;
- moss is applied as a coating rule after stone occupancy is established;
- sockets expose foundation, span endpoints, crown, and optional attachment points.

Typed sockets and joint-first layout from the experimental arch implementation should be retained. Runtime floating-point brush mutation should not be the authoritative construction path.

## 11. Three distinct adjacency systems

The word *adjacency* currently covers different concerns. They should be modeled separately.

### 11.1 Feature composition adjacency

This decides whether authored features can connect before voxelization. It uses typed sockets, orientation, size constraints, tags, and connection rules. Examples include attaching an arch to a wall or requiring both piers to meet valid foundations.

### 11.2 Simulation material adjacency

This decides gameplay consequences after voxelization. Examples include water contacting lava, unsupported stone, fire next to timber, or moss growth eligibility.

### 11.3 Rendered surface adjacency

This decides how neighboring occupied cells form a boundary. It uses the surface join table and never substitutes for simulation rules.

Keeping these systems separate prevents a rendering concern such as smoothing from changing construction validity or destruction behavior.

## 12. Rendering subsystem boundaries

### 12.1 Common scheduler

A common scheduler should own:

- dirty-range discovery;
- dependency expansion for neighbor halos;
- priority and frame budget;
- CPU/GPU job dispatch;
- cancellation or stale-result rejection;
- buffer residency and release;
- handoff to each render domain.

### 12.2 Versioned change journal

Voxel edits append bounded change records rather than mutating a shared set:

```text
VoxelChangeRecord
  region/chunk coordinate
  changed bounds
  change categories
  new data version
```

Change categories distinguish occupancy, material, surface style, coating, water, and feature metadata. Consumers track the last processed version. Builds include their source version and are discarded if newer input exists.

### 12.3 Thin render pass

`VoxelRenderPass` should coordinate visibility and draw submission, not decide voxel meaning or perform feature-specific classification. Extraction policy belongs to domain services operating on the same compiled voxel contracts.

### 12.4 Specialized domains retained

Keep separate domains where topology or lifetime demands it:

- water and other translucent fluid surfaces;
- vegetation instances;
- destruction debris and short-lived effects;
- debug visualization.

These domains still consume the common change journal and catalogue IDs where relevant.

## 13. Implementation sequence

### Phase 0: Record and protect current behavior

- Add this proposal as an architecture decision input to specifications 001 and 002.
- Capture representative scenes for terrain, hard structures, water, destruction, and the target arch.
- Add metrics for mesh builds, stale builds, allocations, upload bytes, and render-path usage.

### Phase 1: Separate cell semantics

- Introduce stable `BaseMaterialId`, `SurfaceStyleId`, and `CoatingId` types.
- Compile independent material, style, coating, and join catalogues.
- Adapt existing voxel data through compatibility accessors.
- Stop encoding moss by replacing stone.

### Phase 2: Introduce journal and scheduler

- Replace shared upload sets with versioned change records.
- Centralize halo expansion and build budgeting.
- Add stale-result rejection and deterministic scheduling tests.

### Phase 3: Establish the unified solid extractor

- Define one extractor input/output contract.
- Bring smooth terrain through it first.
- Add planar and sharp-feature constraints.
- Support different surface styles inside one brick.
- Retire brick-level hardness as an authoritative split.

### Phase 4: Add curved architectural primitives

- Add fixed-point annulus, arch/voussoir, rounded-box, and extruded-profile primitives.
- Port the experimental arch to a deterministic `FeatureDefinition`.
- Preserve typed sockets and structural joint intent.

### Phase 5: Add coatings

- Store or derive sparse coating data.
- Pass coating attributes through extraction and shading.
- Add deterministic environment-based placement rules for moss.

### Phase 6: Add composition constraints

- Formalize socket compatibility and orientation rules.
- Add feature-level validation diagnostics.
- Keep composition, simulation, and surface adjacency APIs distinct.

### Phase 7: Remove obsolete paths

- Remove the legacy hard-brick classifier after parity is proven.
- Remove the Surface Nets migration wrapper once it becomes the production service.
- Remove the showcase fallback from production configuration.
- Rename the density builder to match its responsibility and remove stale raymarch options.
- Consolidate duplicate debris ownership.

The implementation completed these phases as a direct architectural replacement. No legacy
solid-rendering compatibility gate remains; repository history is the rollback mechanism.

## 14. Validation and acceptance criteria

### 14.1 Determinism

- Evaluating a feature as a whole produces the same cells as evaluating every intersecting subvolume separately.
- Primitive output is identical across supported runtime platforms.
- Catalogue ordering does not change stable IDs.

### 14.2 Surface joins

- Join rules are symmetric under neighbor order.
- Chunk and region seams produce the same topology as a monolithic extraction.
- Missing rules use the documented fallback.
- A planar and curved style can coexist within one former 8³ hard brick.

### 14.3 Arch scenario

- The arch silhouette is curved rather than stair-stepped at the target viewing distance.
- Voussoir joints remain intentional where specified.
- The crown, intrados, extrados, and pier transitions remain stable across chunk and region borders.
- Moss follows allowed exposed surfaces without changing stone durability, collision, or destruction behavior.
- Destroying a moss-covered voxel produces stone behavior plus moss presentation effects.

### 14.4 Incremental rebuilding

- Editing one cell invalidates only the affected chunk plus the required extraction halo.
- An older asynchronous result cannot replace a newer result.
- Repeated coating-only edits do not rebuild collision or unrelated simulation products.
- Steady-state rendering performs no avoidable per-frame managed allocation.

### 14.5 Regression coverage

- Existing smooth terrain, planar structures, water, vegetation, and debris remain visually and behaviorally correct.
- The production solid renderer has no competing CPU/GPU fallback interpretation.
- Production capture tests exercise the actual renderer, not only showcase components.

## 15. Architectural invariants

The following rules should be treated as review-time invariants:

1. The voxel grid is the authoritative mutable world representation.
2. Rendering derives geometry from voxel state and immutable catalogues.
3. Base material, surface style, and coating are logically independent.
4. Coatings do not silently change structural behavior.
5. Surface joins are explicit, symmetric, and deterministic.
6. Feature evaluation is invariant under subvolume partitioning.
7. Mixed solid styles do not require brick-wide renderer selection.
8. Every asynchronous build identifies the source data version.
9. Specialized render domains share invalidation infrastructure but may own distinct topology generation.
10. Debug and showcase renderers are not production authorities.
11. Surface metadata does not change primitive membership; geometric recesses are explicit boolean
    operations or boundary constraints.
12. Smoothing may shade or join a valid boundary, but it is not the source of an authored curve.

## 16. Resolved implementation decisions

- The existing CPU Transvoxel implementation is the initial unified extractor. Its current density
  field is occupancy- and style-aware, not feature-aware: it does not retain authored crossing
  positions. Adding the boundary sidecar is required before constrained dual contouring can be
  considered merely an optimization rather than a correctness change.
- Surface style and coating overrides use a compact per-cell sidecar in mixed bricks. Uniform
  bricks represent material-default style and no coating.
- Coatings are per-cell IDs. Moss is shaded from the extracted coating attribute and does not
  occupy the base-material byte.
- Coating definitions are versioned extractor inputs. Their displacement values alter derived
  boundary geometry generically; the extractor and shader never identify moss, snow, or any
  other coating by stable ID.
- Coating definitions may also request deterministic sparse surface decoration by shape, density,
  spacing, footprint, height, and eligible face mask. Decorations are emitted allocation-free into
  the owning chunk mesh and reuse packed material/coating attributes, so clumped growth does not
  require GameObjects, a material-specific shader branch, or a second invalidation authority.
- Coursed walls use a reusable bonded-block veneer over structural backing. Blocks own their
  rounded authored boundaries and piece variation; the backing closes joints and remains the
  load-bearing occupancy, avoiding a material-specific brick-grid shader.
- Join rules expose sharp/discontinuous, tangent, and smooth continuity with a conservative sharp
  seam fallback. Masonry joints use an intentional-seam flag and their own join group.
- Collision remains discrete voxel occupancy, as permitted by the non-goals. Coarse curved
  colliders are deferred until gameplay demonstrates a need.

## 17. Remaining verification

1. Run EditMode and PlayMode tests through `tools/unity-run.sh` with the Unity editor closed.
2. Capture the reference arch and inspect silhouette, voussoir seams, moss coverage, and region
   boundaries against the target image.
3. Profile steady-state allocations and the new one-voxel solid extraction resolution on the
   device tiers before tuning build budgets.
4. Add the boundary sidecar to mixed bricks and carry it through persistence, hashing, replication,
   journaling, rasterization, edits, and extraction.
5. Prove with an arch capture that planar-shaded masonry follows the authored annulus without a
   smoothing filter and without an arch-specific shader or extractor branch.
