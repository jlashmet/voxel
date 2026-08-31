# Far-World Visibility, HLOD, and Scatter Architecture Proposal

**Status:** Draft architecture proposal  
**Scope:** Visual continuity from resident voxels to the far horizon  
**Primary integration points:** `VoxelSurfaceScheduler`, `VoxelFarTerrain`, `FarFieldStructureStore`, world-feature authoring, macro-world generation  
**Non-goal:** Replace the authoritative voxel world, terrain sampler, or existing world-feature authoring contracts

---

## 1. Problem statement

The engine already has a strong split between the resident voxel world and distant analytic terrain. That split works well for broad mountains and terrain because `TerrainSampler.HeightAt` can be evaluated anywhere without loading distant voxel regions. It is weaker for authored and procedural objects whose identity and silhouette matter at long range.

A castle on a mountain, a settlement skyline, a giant tree, or a major rock spire should remain recognizable kilometers away. An ordinary tree, shrub, grass clump, or two-metre boulder should not require a globally persistent object record or an individual far draw call. Dense forests should read as forests at horizon range without drawing tens of thousands of tree proxies.

The architecture therefore needs three distinct but coordinated representation families:

```text
                  deterministic macro-world definition
                              |
             +----------------+----------------+
             |                |                |
             v                v                v
          terrain      semantic structures   scatter fields
             |                |                |
             v                v                v
      terrain clipmap    structure HLOD      scatter HLOD
             |                |                |
             +----------------+----------------+
                              |
                            view
```

The authoritative world definition must remain deterministic and CPU-derived. Far representations are presentation only. They may simplify geometry aggressively, but may not invent a different world.

---

## 2. Current implementation baseline

### 2.1 Resident voxel range

`VoxelShowcase` currently uses eight loaded regions and a 51.2 m region size, yielding a nominal resident/render-data radius of:

```text
8 regions * 51.2 m = 409.6 m
```

The showcase passes that radius to `RenderingComposition.SetVoxelRingRadiusMetres(...)` and uses it as the inner radius of `VoxelFarTerrain`.

The near surface scheduler already presents progressively coarser source data. Its shipped ring layout is based on source steps 1, 2, 4, and 8; the showcase currently scales the inner handoffs with `m_DetailBandScale = 0.6`. At current settings the rough handoffs are therefore approximately:

| Distance | Source step | Approximate voxel stride |
|---|---:|---:|
| 0–57.6 m | 1 | 0.1 m |
| 57.6–115.2 m | 2 | 0.2 m |
| 115.2–172.8 m | 4 | 0.4 m |
| 172.8–409.6 m | 8 | 0.8 m |

This remains the high-fidelity representation used for collision, destructibility, interiors, overhangs, and other details that cannot be represented by a height surface.

### 2.2 Distant terrain

`VoxelFarTerrain` is a camera-centered geometric clipmap. It samples the deterministic analytic terrain function directly rather than retaining distant terrain regions. Its current resolution is 96 cells per axis per ring. Ring sample spacing doubles outward.

With a ~409.6 m inner radius, the current spacing calculation produces approximately:

| Far ring | Sample spacing |
|---:|---:|
| 0 | 12.8 m |
| 1 | 25.6 m |
| 2 | 51.2 m |
| 3 | 102.4 m |
| 4 | 204.8 m |

This is the correct architectural mechanism for broad terrain. A mountain several kilometres wide still has enough low-frequency shape to remain legible when sampled at hundreds of metres.

### 2.3 Current far authored-content fallback

`FarFieldStructureStore` is a coarse permanent record of authored surface deviations. Each 51.2 m region is divided into 16 by 16 columns, so the source record has ~3.2 m horizontal columns. Raised content must exceed the analytic terrain by 24 voxels, or 2.4 m at the current 0.1 m voxel size, before being retained as a raised far-field feature.

The store is intentionally captured at authored-content boundaries rather than acting as a listener for arbitrary runtime destruction. It can preserve raised silhouettes and authored terrain lowering without retaining the entire voxel region.

This store is useful, but it is not a semantic structure proxy. `VoxelFarTerrain` currently samples it at the terrain clipmap's vertex positions. Consequently the *stored* 3.2 m detail does not imply 3.2 m *rendered* far detail. At the outer ring, a query may occur only every ~204.8 m.

### 2.4 Existing limitations that this proposal addresses

1. **Important objects can be point-sampled away.** A 100–150 m castle may be well represented in `FarFieldStructureStore` yet receive no outer-ring vertex inside its footprint.
2. **Far-radius intent is not yet proven by geometry.** At 204.8 m spacing, 96 cells span 19,660.8 m, or 9,830.4 m from grid center to an edge before camera-to-grid snap offset. That does not obviously guarantee a configured 12,000 m cardinal radius. Ring-count logic and startup fallback intent should be replaced by an exact coverage invariant and regression test.
3. **No dedicated scatter representation exists.** Ordinary trees, shrubs, rocks, and similar features should not be forced through the semantic-structure path.
4. **Unvisited landmark availability must not depend on voxel residency.** If a landmark is part of macro-world truth, enough metadata to represent it from afar must exist before its detailed voxel region is streamed.
5. **One height-surface mechanism cannot express every object class well.** Castles, forests, trees, rock formations, and grass have different visual persistence and aggregation behavior.

---

## 3. Goals

### 3.1 Functional goals

- Guarantee broad terrain coverage to the configured far radius from every view direction and clipmap snap phase.
- Keep declared semantic landmarks visible at distances where their projected silhouette is still meaningful.
- Allow a never-visited landmark to appear from far-world metadata without generating/residing its full voxel regions.
- Reproduce ordinary natural scatter deterministically from compact macro-cell descriptors instead of storing every instance globally.
- Make forests read as forests at long range using aggregation, canopy, material, or skyline representations rather than individual tree draws.
- Promote exceptional natural features such as giant trees, massive boulders, arches, and rock spires into the landmark path when their visual significance warrants it.
- Transition between representations without disappearance, obvious double-drawing, or unstable popping.
- Maintain stable world identity across representation changes.

### 3.2 Architectural goals

- Preserve deterministic integer/CPU world truth; GPU output remains presentation only.
- Keep shared systems semantic and configuration-driven. Scene composition chooses content and policy values; reusable systems do not encode Kentridge- or showcase-specific coordinates.
- Keep far metadata far cheaper than resident voxel data.
- Derive all distance representations from the same world definition.
- Make visual importance a combination of projected size, semantic importance, and representation budget rather than a single hardcoded category distance.
- Retain `VoxelFarTerrain` and `FarFieldStructureStore` where they are already the correct abstraction.

### 3.3 Non-goals

- Simulate physics, AI, collision, interiors, or arbitrary voxel destruction at horizon distance.
- Preserve every leaf, branch, loose stone, or small prop beyond the distance at which it contributes meaningful pixels.
- Store one permanent database record for every generated tree or boulder.
- Make device tiers alter authoritative world content. Lower tiers may choose cheaper *presentation* of the same visible semantic world.
- Solve unlimited planetary-scale rendering in the first implementation. The architecture should extend beyond 12 km later, but 12 km correctness is the immediate acceptance target.

---

## 4. Core design principle: one world, multiple representations

An object or region should not become a different world object merely because the camera moved farther away.

For a castle, the representation chain may be:

```text
macro structure record
        |
        +--> horizon silhouette / landmark proxy
        |
        +--> low-poly or clustered structure HLOD
        |
        +--> generated voxel structure
        |
        +--> resident voxel surface + collision + destruction
```

For a forest cell:

```text
macro scatter cell + deterministic seed
        |
        +--> biome/forest terrain appearance
        |
        +--> canopy/forest cluster HLOD
        |
        +--> simplified tree instances
        |
        +--> detailed nearby tree instances / voxel features
```

The source definition is stable. Render representations are disposable caches.

---

## 5. World visibility data model

Names below are architectural placeholders; implementation should follow existing module naming and ownership conventions rather than treating these exact class names as API commitments.

### 5.1 `WorldVisibilityManifest`

A compact, deterministic query surface for far-visible world facts. It should answer spatial queries without requiring detailed voxel region residency.

Conceptually:

```text
Query(bounds) ->
    semantic structure descriptors
    scatter-cell descriptors
    biome / terrain presentation metadata
```

The manifest may be generated procedurally, baked, streamed, or composed from both, but queries must be deterministic for the same world seed and persistent state.

The manifest does **not** own resident voxel data. It is metadata sufficient to decide what far representations should exist.

### 5.2 Semantic structure descriptor

A castle, bridge, tower, major house cluster, giant tree, rock arch, or similar visually persistent feature should have a stable semantic descriptor. Candidate fields:

```text
StableId
WorldBounds / footprint
Anchor / orientation
SemanticKind
ImportanceClass
GenerationSeed or authoring reference
Material / palette family
Far-representation recipe
Near materialization recipe
StateSummary (optional; initially static)
```

The descriptor should define *what exists*, not a cached Unity `Mesh` or GameObject.

A structure can be generated into full voxels only when required by near residency while still having enough descriptor data to build or retrieve a far HLOD beforehand.

### 5.3 Scatter-cell descriptor

Ordinary vegetation and rocks should be represented by deterministic fields over macro cells, for example:

```text
CellCoord
CellSeed
BiomeId
TreeSet / density / size distribution
ShrubSet / density
RockSet / density / scale distribution
Ground-detail layers
Exclusion masks / reservations
Optional canopy summary cache
```

Individual scatter positions are reproducible functions of cell descriptor + world seed + placement constraints. They do not require permanent global object records unless gameplay promotes an instance into persistent state.

This preserves deterministic placement while allowing each renderer tier to generate only the amount of data it needs.

### 5.4 Landmark promotion

A natural or authored feature can participate in scatter generation initially but be promoted to semantic landmark treatment when it exceeds configured significance criteria.

Promotion inputs should include:

- world-space bounds or estimated silhouette size;
- expected projected size over meaningful viewing distances;
- semantic importance override;
- uniqueness/rarity;
- skyline contribution;
- gameplay discoverability requirements.

Examples:

- 1.5 m rock: ordinary scatter.
- 8 m boulder: scatter with longer mid-distance lifetime.
- 60 m monolith: semantic natural landmark.
- normal pine: scatter.
- 120 m world tree: semantic landmark.

Promotion should be configuration-driven, not a hardcoded list of scene object names.

---

## 6. Representation tiers

The following tiers describe responsibilities, not mandatory fixed metre boundaries. Actual switching should use projected size, importance, hysteresis, and device presentation budget.

### Tier A — resident authoritative presentation

- Resident voxel regions.
- Highest applicable voxel surface source step.
- Collision and destructibility.
- Full structure interiors and overhangs.
- Detailed nearby scatter or detailed renderer-specific instances.

### Tier B — simplified near/mid presentation

- Coarser resident voxel surface rings where applicable.
- Simplified GPU instances for trees and significant rocks.
- Optional low-poly structure mesh/HLOD before full detail is worth drawing.
- Small ground detail fades out.

### Tier C — far semantic HLOD

- Structure-specific HLOD or silhouette proxies.
- Settlement/building clusters.
- Simplified vegetation clusters.
- Forest canopy representation.
- Large natural-feature proxies.
- No collision or full voxel residency required.

### Tier D — horizon representation

- `VoxelFarTerrain` for broad ground form.
- Biome/forest appearance integrated into distant terrain presentation.
- Major skyline landmarks retained as separate semantic proxies or conservative envelopes.
- Ordinary scatter represented only statistically/aggregately.

The current 12 km target primarily spans Tier D at its outer range. A future extension beyond 12 km can add another terrain/horizon tier without changing world-definition contracts.

---

## 7. Representation selection policy

Distance alone is an insufficient rule. A 300 m castle and a 2 m boulder at the same distance should not have the same lifetime.

The selector should estimate projected significance from conservative bounds, camera projection, and importance.

Conceptually:

```text
projected_pixels = Project(bounds, camera)
importance = SemanticImportance(descriptor)
quality = DevicePresentationTier

representation = Select(
    projected_pixels,
    importance,
    quality,
    previous_representation,
    available_budget)
```

Key properties:

1. **Projected-size thresholds** determine when geometric detail is no longer useful.
2. **Semantic importance** can retain critical landmarks beyond normal thresholds.
3. **Hysteresis** prevents repeated switching around a boundary.
4. **Overlap windows** allow old/new representations to coexist briefly for a controlled crossfade/dither.
5. **Budget pressure** may select a cheaper representation, but cannot make a required semantic landmark disappear while it is still above its minimum visibility threshold.
6. **Stable ordering** ensures deterministic selection for equivalent inputs where authoritative metadata is involved. Camera-dependent presentation need not be network authoritative.

Fixed distance bands remain useful as coarse culling bounds and scheduling heuristics, but should not be the only policy.

---

## 8. Terrain clipmap changes

### 8.1 Keep `VoxelFarTerrain`

The current analytic geometric clipmap is the right representation for distant ground. Do not replace it with resident voxel streaming or a separate duplicate terrain database.

### 8.2 Make outer-radius coverage an invariant

The configured outer radius must mean what it says.

For each ring, tests should calculate the actual covered world-space polygon after camera snapping. The final ring must conservatively contain the requested visibility disk (or the explicitly documented target shape) for all legal snap offsets.

The existing current-setting math is a warning case:

```text
outer spacing = 204.8 m
cells per axis = 96
full width = 96 * 204.8 = 19,660.8 m
half width = 9,830.4 m
requested outer radius = 12,000 m
```

Ring count alone must not be accepted as proof. Fix either ring count, resolution/spacing selection, outer-ring sizing, or topology so the geometric invariant is true.

### 8.3 Separate broad terrain sampling from semantic landmark sampling

Terrain vertices should continue to sample analytic height. A critical structure should **not** rely on one of those same terrain vertices happening to land inside its footprint.

`FarFieldStructureStore` can continue to influence the far terrain surface for generic raised/lowered authored terrain, but semantic landmarks require their own visibility query and representation.

### 8.4 Improve generic structure fallback sampling

For non-semantic authored surfaces that remain in `FarFieldStructureStore`, replace fragile point-only behavior where practical with a conservative footprint query appropriate to each far cell. Candidate approaches:

- max-height/min-height envelope over the cell footprint;
- hierarchical min/max summaries;
- conservative structure occupancy bitmask;
- prefiltered levels aligned to far terrain sample spacing.

This prevents generic raised features from vanishing merely because the clipmap lattice phase changes. It does **not** replace semantic HLOD for recognizable buildings.

---

## 9. Semantic structure HLOD

### 9.1 Why structures need a separate path

A recognizable castle silhouette contains information that a sampled heightfield cannot reliably preserve:

- towers and crenellated skyline;
- separated masses;
- bridges and arches;
- vertical facades;
- overhangs;
- material groups;
- stable identity as the player approaches.

At long range we do not need full interiors, but we do need the silhouette and massing that tell the player “that is the castle.”

### 9.2 HLOD forms

The structure system should support more than one proxy recipe:

- conservative silhouette mesh;
- low-poly generated structure mesh;
- clustered building mass mesh;
- landmark billboard/impostor where visually acceptable;
- footprint + height-envelope proxy for less important structures;
- settlement skyline cluster assembled from constituent semantic records.

The recipe is selected by semantic descriptor and presentation tier, not by scene-specific code.

### 9.3 Generation source

Prefer generating HLOD from semantic/world-builder inputs when possible rather than downsampling a transient resident mesh. This allows a never-visited structure to be visible.

For procedural structures:

```text
same stable structure seed/grammar
      +--> coarse HLOD recipe
      +--> full voxel materialization recipe
```

The two representations must agree on bounds, anchor, major masses, and semantic identity.

For arbitrary authored voxel edits with no semantic descriptor, `FarFieldStructureStore` remains the fallback.

### 9.4 Transition to resident voxels

A structure HLOD should remain active until the near voxel representation is known ready, not merely until the camera crosses a distance threshold.

Recommended state flow:

```text
FAR_ONLY
  -> REQUEST_NEAR
  -> OVERLAP (far + ready near)
  -> NEAR_ONLY
```

Reverse transitions follow the same principle. Readiness drives handoff; distance drives requests.

A short dither/crossfade window can hide geometric mismatch, but the invariant is that at least one valid representation exists throughout the transition.

---

## 10. Scatter architecture

### 10.1 Ordinary scatter is a field, not a global object list

Trees, shrubs, grass, flowers, pebbles, and ordinary boulders can number in the millions. The macro world should retain the deterministic recipe for a cell, not all instantiated objects.

A renderer requesting a cell can regenerate the same candidate positions from:

```text
world seed
macro cell coordinate
scatter layer id
biome/config
placement/reservation constraints
```

Generation order and random stream partitioning must be stable so adding a new scatter layer does not reshuffle existing trees.

### 10.2 Layer-specific lifetimes

Suggested behavior:

| Feature | Near | Mid | Far | Horizon |
|---|---|---|---|---|
| Grass/flowers | detailed instances | sparse/fade | none | biome material only |
| Shrubs | detailed | cheap clusters | fade | none |
| Normal trees | detailed | simplified GPU instances | canopy/cluster | forest mass/skyline |
| Landmark tree | detailed | low-poly | landmark HLOD | skyline landmark |
| Small rocks | detailed | fade | none | none |
| Boulders | detailed | simplified instance | usually cull | none |
| Huge rock/spire | detailed | HLOD | landmark HLOD | skyline landmark |

These are policy defaults, not hardcoded world distances.

### 10.3 Reservations and authored composition

Scatter regeneration must honor the same deterministic exclusion/reservation facts as near generation so far forests do not grow through roads, settlements, plazas, or authored landmarks and then pop away on approach.

The scatter descriptor/query layer therefore needs access to compact reservation masks or equivalent semantic exclusion data at macro-cell scale.

### 10.4 Gameplay promotion

If gameplay turns an ordinary generated instance into persistent state—for example a uniquely modified tree or destructible resource whose state must survive—the world-state layer may promote that instance to a persistent override keyed by stable generated identity.

That is separate from *visual landmark promotion*. A giant tree may be visually promoted even if its gameplay state is static.

---

## 11. Forest and canopy representation

Dense forest is the largest scatter case and should be optimized as an aggregate.

### 11.1 Mid distance

Use simplified tree geometry/instances with aggressive material and mesh batching. The deterministic scatter cell can regenerate a subset or all significant trunks/crowns depending on projected density.

### 11.2 Far distance

Convert many trees into a small number of cell or sub-cell canopy representations. Candidate forms:

- canopy surface mesh derived from deterministic tree heights;
- clustered crown cards/meshes;
- low-frequency skyline envelope;
- density-driven forest material plus sparse silhouette clusters.

The exact art technique can vary by device tier while preserving the same forest extent and major skyline facts.

### 11.3 Horizon distance

At extreme distance, forest identity should primarily come from:

- terrain/biome color and roughness variation;
- forest coverage masks;
- conservative canopy-height contribution where it affects skyline;
- exceptional trees or groves promoted to semantic landmarks.

A forested mountain at 10 km must not become visually identical to bare terrain simply because individual trees are culled.

---

## 12. Natural landmarks

Natural features use the same semantic visibility infrastructure as buildings when their visual persistence justifies it.

Examples:

- giant world tree;
- 50–100 m rock spire;
- large arch;
- distinctive boulder cluster used for navigation;
- enormous fallen tree/bridge;
- singular cliff formation authored as a feature rather than broad terrain.

Natural landmarks should retain material category and silhouette. They do not need interiors or high-frequency voxel detail at distance.

The architecture should avoid parallel `CastleProxy`, `TreeProxy`, and `BoulderProxy` subsystems. The shared abstraction is a semantic far-visible feature with a configurable HLOD recipe.

---

## 13. Persistence and runtime destruction

### 13.1 Initial policy

Maintain the current useful rule: arbitrary nearby destruction does not force the far world to mirror every voxel edit immediately. Full voxel state remains authoritative where simulated; far representation is a compact presentation cache.

For initial implementation:

- terrain and generic `FarFieldStructureStore` behavior remain static after authored capture unless existing contracts require otherwise;
- ordinary scatter far representations derive from deterministic generation state;
- semantic landmark proxies represent authored/generated baseline state.

### 13.2 Future landmark-state summaries

Some gameplay events are too significant to ignore at distance—for example destroying an entire bridge tower or felling a giant world tree. Support a future compact semantic state summary:

```text
StableId
PresentationStateVersion
DestroyedMajorParts / state enum
Optional bounds change
```

That summary invalidates/regenerates the landmark proxy without streaming its full voxel state to horizon distance.

This should be introduced only when gameplay acceptance requires it; do not make phase one depend on a generalized far-destruction replication system.

---

## 14. Streaming and scheduling

### 14.1 Metadata horizon is larger than voxel horizon

Visibility metadata must be cheap enough to query/load to at least the far render radius, while resident voxel data remains limited to the near streaming radius.

```text
camera
  |
  +-- resident voxel regions: hundreds of metres
  |
  +-- mid representation cells: kilometres as needed
  |
  +-- visibility manifest / far proxies: horizon radius
```

### 14.2 Scheduling priorities

Prioritize by visual urgency:

1. representation needed for an upcoming handoff;
2. high-importance landmark entering view;
3. near/mid scatter cell entering meaningful projected density;
4. forest/canopy aggregate;
5. low-significance optional detail.

Do not rebuild a far proxy every frame because the camera moved slightly. Spatial cells, snapped origins, cached descriptors, and hysteresis should make updates coarse and stable.

### 14.3 Cache ownership

Separate deterministic descriptors from disposable render caches:

- manifest/descriptor cache: CPU metadata;
- generated proxy mesh cache: presentation;
- GPU instance buffers: presentation;
- resident voxel storage: authoritative near world.

Evicting presentation caches must never erase world truth.

---

## 15. Proposed module boundaries

Exact assemblies/names should follow existing architecture rules, but responsibilities should remain separated.

### Shared semantic/query layer

- visibility manifest spatial query;
- semantic feature descriptor;
- scatter-cell descriptor;
- stable generated identity;
- visibility importance classification;
- reservation/exclusion summary.

This layer should not know about Kentridge, a particular castle, or a particular showcase camera.

### Presentation policy layer

- projected-size calculation;
- representation tier selection;
- hysteresis and overlap policy;
- device-tier presentation configuration;
- budget admission.

### Terrain presentation

- existing `VoxelFarTerrain`;
- exact outer-coverage logic/tests;
- generic conservative authored-surface sampling.

### Structure presentation

Candidate responsibility names:

- `FarStructureScheduler`;
- `FarStructureRenderer`;
- HLOD recipe/builder interface;
- semantic structure proxy cache.

### Scatter presentation

Candidate responsibility names:

- `WorldScatterField` query/generation;
- `FarScatterScheduler`;
- tree/rock instance batches;
- forest-canopy builder/cache.

### Composition

Scene/game composition chooses:

- biome/scatter configuration;
- landmark importance configuration;
- art assets/material families;
- visibility quality policy defaults;
- debug visualization enablement.

Reusable modules own the mechanics.

---

## 16. Debugging and instrumentation

A far-world system will be difficult to tune without explicit visibility diagnostics. Add a debug overlay/view capable of showing:

- current resident voxel radius;
- far-terrain ring boundaries and actual world-space extents;
- ring sample spacing;
- requested vs guaranteed far radius;
- semantic visibility cells queried;
- active semantic landmarks and selected tier;
- proxy readiness / handoff state;
- scatter cells and selected representation;
- forest aggregate bounds;
- per-frame proxy/instance counts;
- structure fallback sample hits/misses;
- CPU generation/scheduling time;
- GPU draw/vertex/instance counts where available;
- cache memory and churn.

Tests prove invariants; the overlay makes scene-quality failures diagnosable.

---

## 17. Validation fixtures and acceptance tests

### 17.1 Far terrain coverage fixture

For representative camera positions spanning every relevant snap phase:

- calculate/inspect final ring bounds;
- test cardinal and diagonal ray intersections;
- prove continuous coverage from near-far handoff through the configured 12 km target;
- include positions near integer/negative-coordinate boundaries.

The test must fail with the current undercoverage math if the suspected discrepancy is real.

### 17.2 Narrow landmark sampling fixture

Place a recognizable narrow/tall proxy footprint at outer-range distances and vary camera clipmap phase.

Acceptance:

- semantic landmark remains present regardless of terrain sample lattice;
- generic fallback behavior is stable if the feature is not semantic;
- visibility does not depend on one terrain vertex intersecting the structure.

### 17.3 Castle-on-mountain fixture

Validate at 8 km, 10 km, and 12 km:

- broad mountain form visible;
- castle skyline visible and recognizable;
- cardinal and diagonal views;
- multiple camera snap phases;
- never-visited state where detailed castle voxel regions have not been resident;
- approach through far HLOD -> near HLOD -> resident voxel handoff;
- retreat back through the same states.

No frame/window should contain neither representation.

### 17.4 Forested mountain fixture

At near, mid, and far ranges:

- deterministic tree layout remains spatially consistent;
- individual trees simplify/cull progressively;
- forest mass remains visually present at horizon distance;
- roads/settlements/reservations remain clear of regenerated far forest;
- approach does not reveal a completely different forest footprint.

### 17.5 Natural-feature fixture

Include at minimum:

- ordinary small rock that legitimately disappears;
- ordinary tree that joins forest aggregation;
- large boulder with extended mid representation;
- giant rock formation promoted to landmark;
- giant tree promoted to landmark.

Acceptance is based on projected/semantic significance, not arbitrary class-name special cases.

### 17.6 Determinism tests

Given identical seed/config/cell:

- scatter candidate identity/order is stable across runs;
- generating one layer does not perturb another layer's random sequence;
- proxy bounds/major semantic mass are stable;
- near materialization agrees with macro descriptor anchor/bounds.

### 17.7 Performance tests

Measure, do not guess:

- visibility-query CPU cost;
- proxy generation time;
- scatter generation time;
- cache memory;
- far draw calls/vertices/instances;
- update churn during continuous camera movement;
- peak work during near/far handoffs.

Numeric acceptance budgets come from the authoritative device matrix and measured baseline. Do not weaken existing budgets to admit the feature.

---

## 18. Implementation phases

Each phase should be independently useful and independently testable. SceneIssues should be scoped around these acceptance slices rather than attempting the entire architecture in one branch.

### Phase 0 — establish baselines and diagnostics

- Add deterministic coverage math tests around current clipmap.
- Add landmark sample-phase repro.
- Record current CPU/GPU/memory baseline in representative showcase/stress scene.
- Add enough far-ring/representation diagnostics to discriminate later regressions.

**Exit:** suspected weaknesses are reproduced or falsified with behavioral evidence.

### Phase 1 — far terrain correctness and generic fallback

- Guarantee configured far radius geometrically.
- Correct ring sizing/count/coverage math without increasing work blindly.
- Add conservative far-cell sampling for generic `FarFieldStructureStore` content where appropriate.
- Keep existing analytic terrain ownership.

**Exit:** broad terrain and generic authored surfaces no longer disappear due solely to ring geometry or lattice phase.

### Phase 2 — visibility manifest foundation

- Introduce semantic far-visible feature descriptors and spatial query.
- Integrate with macro-world/world-feature generation so declared landmarks exist before detailed voxel residency.
- Keep APIs semantic/config-driven.
- Add independent fixture consumer proving the manifest is not showcase-specific.

**Exit:** an unvisited semantic landmark can be queried by bounds with stable identity and generation metadata.

### Phase 3 — semantic structure HLOD

- Implement one reusable far-structure HLOD/proxy recipe.
- Drive it from semantic descriptors.
- Add readiness-based overlap/hysteresis with resident voxel representation.
- Prove castle-on-mountain acceptance at 8/10/12 km.

**Exit:** important structures do not depend on far terrain point sampling for visibility.

### Phase 4 — deterministic scatter field

- Define macro scatter-cell descriptors and stable random stream partitioning.
- Regenerate ordinary trees/rocks from seed and constraints.
- Integrate reservations/exclusions.
- Add near/mid simplified instance path using existing rendering architecture where practical.

**Exit:** ordinary scatter is reproducible without global per-instance persistence.

### Phase 5 — forest/canopy HLOD

- Aggregate dense tree fields into canopy/cluster representations.
- Carry forest/biome appearance into horizon presentation.
- Tune transitions with objective counts plus built-player visual evidence.

**Exit:** forested mountains remain forested at long range without individual-tree explosion.

### Phase 6 — natural landmark promotion

- Add generic semantic promotion rules/overrides.
- Support giant tree / rock formation through the same landmark renderer used for structures.
- Avoid type-specific parallel proxy subsystems.

**Exit:** large natural silhouettes persist while ordinary scatter still culls cheaply.

### Phase 7 — transition and budget hardening

- Stress continuous traversal, fast camera movement, teleport, and direction changes.
- Validate cache churn and bounded work.
- Tune projected-size thresholds/hysteresis per presentation tier without altering world truth.
- Validate supported device tiers from the device matrix.

**Exit:** stable production-ready behavior under representative traversal and load.

---

## 19. Proposed SceneIssue decomposition

Names are proposals only; reuse or extend existing related SceneIssues when scope already exists.

1. `FarTerrainCoverageCorrectness`
   - exact configured-radius invariant;
   - snap-phase/cardinal/diagonal regression.

2. `FarFieldStructureConservativeSampling`
   - generic authored-surface fallback stability;
   - no semantic HLOD scope.

3. `WorldVisibilityManifestFoundation`
   - spatial semantic descriptors;
   - never-visited landmark query;
   - independent consumer fixture.

4. `FarStructureHlodPresentation`
   - reusable structure proxy/HLOD;
   - castle 8/10/12 km acceptance;
   - near/far handoff.

5. `WorldScatterFieldFoundation`
   - deterministic macro-cell scatter descriptors;
   - reservation-aware regeneration;
   - stable random stream partitioning.

6. `ForestCanopyFarRepresentation`
   - tree aggregation/canopy;
   - forested mountain horizon quality.

7. `NaturalLandmarkPromotion`
   - giant tree/rock spire treatment through shared semantic HLOD.

8. `FarVisibilityTransitionContinuity`
   - readiness/hysteresis/crossfade behavior across representation families.

9. `FarWorldVisibilityStressValidation`
   - traversal, cache churn, memory, CPU/GPU budgets, built-player quality.

Before opening any of these, inspect current `SceneIssues/open` and `SceneIssues/closed` for overlapping macro-world, terrain-streaming, terrain-tiering, landmark, or resource-budget work. Existing acceptance should be extended rather than duplicated.

---

## 20. Migration strategy

### Keep

- resident voxel storage and surface extraction as authoritative near representation;
- analytic `TerrainSampler` sampling for broad distant terrain;
- `VoxelFarTerrain` geometric clipmap concept;
- `FarFieldStructureStore` for generic authored raised/lowered surface fallback;
- existing world-feature authoring semantics and deterministic generation contracts.

### Change incrementally

- make far-radius coverage exact and tested;
- stop relying on terrain vertex point samples for critical semantic structures;
- expose enough macro-world metadata for never-resident landmark presentation;
- add deterministic scatter descriptors rather than persistent per-tree/per-rock records;
- add aggregate forest representation;
- make representation selection projected-size/importance aware.

### Do not do

- do not increase far terrain resolution globally merely to catch castles;
- do not retain distant voxel regions to solve visibility;
- do not add a unique proxy subsystem for each object type;
- do not make every tree a persistent semantic entity;
- do not tie reusable visibility code to showcase/Kentridge coordinates;
- do not derive authoritative placement/state from GPU-generated proxies.

---

## 21. Risks and mitigations

### Risk: representation mismatch on approach

A semantic HLOD generated independently from full voxels may disagree visibly with the near structure.

**Mitigation:** derive both from the same stable semantic/generation inputs; require matching anchor, bounds, dominant masses/material families; use readiness overlap and visual acceptance captures.

### Risk: scatter regeneration changes existing layouts

Adding layers or changing random-call order can reshuffle trees.

**Mitigation:** independent deterministic random streams by cell/layer/version; stable generated IDs; version migration policy for intentional world-generation changes.

### Risk: far metadata grows into another world database

Storing too much structure/scatter detail would recreate voxel-residency cost in another form.

**Mitigation:** compact descriptors, procedural regeneration, spatial paging, aggregate summaries, explicit memory instrumentation.

### Risk: HLOD generation spikes during fast movement

Rapid camera travel can request many proxies.

**Mitigation:** priority scheduler, cached reusable proxies, bounded work queues, prefetch from projected movement, fallback representation always available.

### Risk: forests look flat or synthetic at horizon range

A pure biome tint may not preserve skyline/volume.

**Mitigation:** combine material coverage with low-frequency canopy/skyline geometry; preserve exceptional trees separately; validate from built-player captures.

### Risk: semantic importance becomes arbitrary content-specific code

Per-object hacks would erode reuse.

**Mitigation:** configurable importance classes and measurable projected-size rules; content selects configuration, shared renderer owns mechanics.

### Risk: far state disagrees after major destruction

A destroyed landmark may remain visible in baseline form.

**Mitigation:** document the initial static-far policy; add compact landmark state summaries only when gameplay acceptance requires it.

---

## 22. Decisions this proposal intentionally locks

1. Distant terrain remains analytic/geometric rather than resident voxels.
2. Critical semantic landmarks get an independent far-visibility path; they do not depend on far terrain sample coincidence.
3. Ordinary natural scatter is deterministic field data, not a global persistent object list.
4. Forests become aggregate representations with distance.
5. Giant natural features share the semantic landmark/HLOD mechanism with man-made structures.
6. Representation selection combines projected size and semantic importance, with hysteresis/readiness.
7. The same deterministic world definition feeds every representation.
8. Device tiers may reduce presentation quality/cost, never alter authoritative world truth.
9. `FarFieldStructureStore` remains useful as a generic fallback but is not the universal far-object solution.
10. Configured far range becomes an explicit tested geometric guarantee.

---

## 23. Deferred choices

These should be decided by small prototypes and measured evidence rather than architecture preference:

- exact structure HLOD mesh algorithm;
- billboard/impostor usage for specific art families;
- canopy mesh versus clustered cards versus hybrid forest representation;
- exact projected-pixel thresholds;
- exact macro scatter cell size;
- cache sizes and eviction policy;
- whether generic `FarFieldStructureStore` acceleration uses min/max hierarchy, prefiltered levels, or another conservative query structure;
- whether semantic HLOD is generated at build time, world generation time, asynchronously at runtime, or some mixture;
- extension beyond the current ~12 km world-view target.

Each deferred choice needs a representative fixture and device-matrix measurement before becoming a shared API contract.

---

## 24. End-state example

Consider a camera looking toward a forested mountain with a castle 10 km away.

The engine should resolve it approximately as follows:

```text
Terrain:
  TerrainSampler -> VoxelFarTerrain outer clipmap
  broad mountain silhouette survives at ~hundreds-of-metres sample spacing

Forest:
  macro scatter/biome cell says pine forest exists on those slopes
  horizon representation contributes forest color + canopy/skyline mass
  no requirement to instantiate individual 10 km trees

Castle:
  visibility manifest returns stable castle descriptor
  projected size + landmark importance selects horizon structure HLOD
  renderer draws recognizable castle silhouette independently of terrain lattice

Approach:
  horizon HLOD -> richer structure/scatter HLOD -> simplified instances
  -> resident voxel structure and detailed vegetation
  handoff waits for replacement readiness and uses overlap/hysteresis
```

A small boulder beside the castle does nothing at 10 km because it contributes no meaningful pixels. A 70 m stone spire beside it is promoted to the same semantic landmark mechanism and remains visible. The distant world therefore preserves what matters without pretending every far object deserves full geometry.

That is the intended architecture: **broad terrain by clipmap, important objects by semantic HLOD, dense ordinary detail by deterministic aggregation, and full voxels only where simulation and close visual detail require them.**
