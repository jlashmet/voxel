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

## Current validated implementation state

- **T002 validated:** `FeatureGeneration.EvaluateInstance` + `ShapeProgram.Evaluate` are the canonical pre-residency large-feature representation for catalogue-driven generated content. They emit deterministic bounded primitives/anchors without touching voxel bricks or region residency. Existing production Kentridge structure and WorldBuilder mountain generators both pass through this path.
- **T003 validated:** `FeaturePresentationBake` + `FeaturePresentationBaker` derive stable identity/revision, conservative bounds, style/material family and generic coarse primitive payloads. `FeaturePresentationCatalogueBaker` provides one catalogue-level lifecycle hook and handles ordinary rules plus structural-root expansion without per-object visibility adapters. Exact focused CI run `33473262150` passed on CI child `e21a9b13e46723aa0595bf914f21eaedf25c476e`, parent feature SHA `303cb0b3e5e2b06405f23c1406676ee560b2344a`.
- **T004 validated:** `IFeaturePresentationSource` / `FeaturePresentationManifest` provide a generic metadata-only sparse index with deterministic sector query, cross-sector de-duplication, stable SourceId ordering, replacement and removal. Exact focused CI run `33475203893` passed on CI child `44b4b34d300a142f05984bc2ef62961a737cb442`, parent feature SHA `303cb0b3e5e2b06405f23c1406676ee560b2344a`.
- **T005 validated:** `ShowcaseWorld.FeaturePresentation` uses normal `QueueLandmarks()` planning and replays the canonical `CastleAuthoringBuild` recipe into generic `IStructurePresentationCaptureSession`, so the planned castle is queryable before any detailed region is generated. The production RNG zero-seed defect found by the regression was fixed narrowly. Exact focused CI run `33490275502` passed on CI child `178aa5f7b16cf52c17dc5bb324ce791c309a2129`, parent production feature SHA `c147864826f4a5e90b365548c526b4e2556f8a22`.

## Next task: T006 independent non-building producer

T006 read-only audit confirms the existing production Kentridge structure and WorldBuilder mountain landmark already enter `FeaturePresentationCatalogueBaker` through one normal composed catalogue. The existing regression proves unrelated `Structure`/`Landform` shapes plus repeat-stable source/revision/bounds. The remaining proof is deliberately test-only: compose the already-proven planned castle bake with the normal mountain catalogue bake in the same generic `FeaturePresentationManifest`, assert distinct stable identities and repeat-stable mountain revision/bounds, and assert the Showcase world still generated zero detailed regions. Do not change bake/index/renderer contracts for this producer unless that regression exposes a demonstrated defect.

T005 had two materially different failed validations before its final fix: run `33481618514` exposed a test compile error from reaching through the public Showcase plan wrapper into private derived bounds, and run `33485118484` exposed the production zero-RNG-seed defect in `CastleLandscapeAuthoring`. Both were root-caused before the next fix, satisfying the repeated-symptom rule.

## Remaining gates

Independent T006 producer -> population promotion -> generic render contract/selection/rendering/HLOD/readiness -> remove rejected castle/structure-specific paths -> terrain coverage/material/transition -> production-faithful module built-player validation -> visual/budget evidence -> exact-head gates -> cleanup/docs/closure.
