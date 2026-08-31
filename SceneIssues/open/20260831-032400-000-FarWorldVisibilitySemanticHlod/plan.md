# Far-World Visibility Implementation Plan

## Acceptance

Build a reusable far-world capability for a real game, not a Showcase-specific solution. The system must keep important never-visited world features visible through 12 km without resident voxels, aggregate dense populations such as forests, cull ordinary scatter by projected significance, guarantee terrain coverage, preserve near/far visual continuity, and meet the authoritative device budgets. Required built-player evidence remains 0.5/1/3/6/10/12 km terrain views plus 8/10/12 km landmark views across cardinal/diagonal directions and snap phases.

## Architectural reset

Current branch head before this reset is `fa26c6d1b8087818b1277ee0ade07ec133a2eb35`. The branch contains useful generic work, but the recent `ShowcaseCastleFarPresentation` / `ShowcaseCastleVisibilityManifest` direction is rejected as a production architecture: adding a bespoke descriptor/manifest adapter for every large object does not scale.

The stronger requirement is **zero per-object far-visibility integration by default**. Creating a new castle, tower, bridge, statue, giant rock, procedural building, or other generated feature must not require an engineer to also implement a far descriptor, register a visibility adapter, or author a special HLOD recipe merely for the object to exist at long range.

Far representation should be a generic derived/baked product of the same canonical world-generation input that creates the object. The preferred source is the existing deterministic authoring/generation representation before resident voxel materialization: geometry operations, structure plan/form/site geometry, authored feature graph, or equivalent canonical generation recipe. A generic far-bake stage derives stable identity, bounds, conservative silhouette/massing, material/style family, revision, and one or more coarse presentation payloads automatically. It may replay the canonical generator into a coarse occupancy/geometry target or simplify a generator-owned geometry representation; it must not require full distant voxel regions to become resident.

This creates one pipeline:

`canonical world feature/generation recipe -> generic far bake -> spatial visibility index -> projected significance/readiness -> generic render representation`

The bake is infrastructure, not object-specific code. Default visibility importance can be derived from bounds/projected significance. Semantic tags or explicit importance are optional overrides for unusual gameplay/landmark requirements, not mandatory boilerplate for every object. Likewise, a custom far presentation recipe is an escape hatch for demonstrated visual defects, not the normal path.

For authored/static assets the same concept may run at import/build time. For procedurally generated features it runs deterministically from the procedural plan/recipe before detailed residency. For runtime-created/destructible content that did not exist in macro planning, a generic runtime HLOD bake may derive a compact far representation from the authoritative created voxel/surface state when the object is created or leaves the near field. These are different bake timings behind the same visibility/render contract, not separate object-type systems.

High-volume populations remain separate. Ordinary trees, rocks, shrubs, and similar scatter are queried deterministically by sector/cell and represented by individual instances only while significant; farther out they collapse into cluster/canopy/statistical HLOD. Exceptional members can enter the generic bake/index path automatically when their derived bounds/projected significance exceed configured promotion thresholds; explicit semantic importance remains an override.

Rendering remains downstream and Game-agnostic. Renderers must not know `CastlePlan`, Kentridge, Showcase coordinates, or world-generation implementation types.

## Hypotheses / discriminators

- **H1 selected:** a generic far-bake pipeline over canonical feature-generation inputs can produce useful distant geometry/metadata for unrelated feature types without feature-specific visibility code. Discriminator: add two structurally different generated features and obtain far representations with no changes to the visibility API/index/renderer and no new per-object adapter.
- **H2 selected for mass populations:** deterministic sector/cell queries plus aggregate HLOD scale better than baking/registering every ordinary tree/rock/shrub individually.
- **H3 rejected:** per-feature adapters/descriptors are acceptable because they are thin. The castle spike demonstrated that this creates producer-specific visibility plumbing and invites one subsystem per object type.

## Ownership and blast radius

World authoring/generation owns canonical feature truth. A reusable far-bake capability derives presentation metadata/geometry from that truth automatically; it does not become another authority. WorldBuilder Runtime owns deterministic spatial indexing only if an index is needed. Vegetation/scatter modules own mass-population queries. Rendering API owns render-ready contracts and tier policy inputs; Rendering Runtime owns disposable proxy/HLOD caches. Game composition supplies global quality/threshold policy and exceptional semantic overrides only.

Reuse existing terrain coverage/detail work, deterministic vegetation/canopy work, projected-significance math, batching, and generic spatial-index code only where they conform to those boundaries. Remove or fold castle-specific visibility code before adding another feature type.

## Remaining gates

Audit canonical generation representation and prove generic bake feasibility -> automatic bake for two unrelated producers -> generic index/render handoff -> renderer readiness/hysteresis -> module-local built-player distance scene -> visual transition and landmark evidence -> device CPU/GPU/memory measurements -> exact-SHA module/Kentridge gates -> cleanup/docs/closure.