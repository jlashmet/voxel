# Far-World Visibility Implementation Tasks

**Architecture reset:** 2026-08-31  
**Feature head when reset began:** `fa26c6d1b8087818b1277ee0ade07ec133a2eb35`  
**Rule:** work the next unchecked non-blocked task. Existing green tests remain useful evidence only where the implementation survives this reset; they do not grandfather rejected castle-specific architecture.

## Phase 0 — Reset the architecture before more implementation

- [x] **T000 — Replace the SceneIssue plan with the scalable far-world direction.**
  - Reject one descriptor/manifest subsystem per large object type.
  - Require zero manual far-visibility integration for ordinary new generated features.
  - Preserve acceptance: never-resident landmarks, 12 km visibility, terrain coherence, HLOD, stable handoff, and device budgets.

- [x] **T001 — Audit the current branch and classify every far-world addition as retain, migrate, or delete.**
  - Inventory `StructureFarPresentation`, `IWorldVisibilitySource`, `WorldVisibilityManifest`, `ShowcaseCastleFarPresentation`, `ShowcaseCastleVisibilityManifest`, `ShowcaseFarStructureSource`, `ShowcaseFarWorldRendering`, far render API/runtime, cluster code, natural-scatter visibility, vegetation/tree visibility, canopy rendering, terrain coverage/detail changes, and their tests.
  - **Retain** only code whose abstraction is generic and has one clear owner.
  - **Migrate** structure-only contracts that are actually generic derived-feature/representation concepts.
  - **Delete** Showcase/castle-specific visibility ownership and tests that merely prove the rejected design.
  - Check API/runtime assembly direction while auditing; other modules may depend only on module API assemblies.
  - Record the resulting keep/migrate/delete table in this file before changing production code.

  **T001 audit result — 2026-08-31 (`e005d10d`)**

  | Classification | Current additions | Decision / owner after reset |
  | --- | --- | --- |
  | **Migrate** | `StructureFarPresentation`, `StructureFarPresentationResolver`, `StructureFarPresentationTests` | The deterministic identity/revision, conservative bounds, material/style-family derivation, and pre-residency derivation are useful, but the contract is structure-only and carries structure vocabulary (`Archetype`, `Facing`, `SettlementKey`, `StructureVisibilityClass`). Replace it with one generic baked-feature output owned by the canonical world-feature/generation path; structure generation may use a private intermediate only. |
  | **Migrate** | `IWorldVisibilitySource`, `WorldVisibilityManifest`, `WorldVisibilityManifestTests` | Keep sector membership, de-duplication, stable ordering, replacement/removal, and no-residency query semantics. Generalize key/value/bounds from `StructureFarPresentation` to baked sparse features. `Game.WorldBuilder.Api` must no longer reference the structure-generation assembly merely to expose visibility. |
  | **Migrate** | `WorldVisibilityClusterBuilder`, `WorldVisibilityClusterBuilderTests`, `SettlementFarHlodTests` | Deterministic aggregation is useful presentation infrastructure, but it currently clusters only ordinary structures by settlement. Rebase it on generic baked-feature inputs / explicit aggregation grouping so clusters remain disposable presentation, not structure truth. |
  | **Migrate** | `StructureVisualState`, `IStructureVisualStateSource`, `StructureVisualStateStore` | Preserve the lightweight CPU-side removed/restored-state idea for far presentation, but key it by generic source identity and keep authoritative mutation with the owning world/game system. Do not retain a parallel structure-only state concept. |
  | **Delete after generic parity** | `ShowcaseCastleFarPresentation`, `ShowcaseCastleVisibilityManifest`, `ShowcaseWorld.FarVisibility.cs`, `ShowcaseCastleFarPresentationTests`, `ShowcaseCastleVisibilityManifestTests` | Rejected per-object integration. `CastlePlan` must enter the same automatic bake path as every other normal generated macro feature; no castle descriptor, registry, event, or manifest owner remains. |
  | **Migrate then delete adapter** | `KentridgeFarPresentationPlanner`, `KentridgeFarVisibilityPlanningTests`, related `KentridgeCampaignWorldRealization` hook | This currently re-walks a Kentridge settlement to explicitly manufacture far records. Reuse its proof inputs while implementing the generic bake, then remove the named planner so normal settlement generation emits derived far data automatically. |
  | **Migrate** | `ShowcaseFarStructureSource`, `ShowcaseFarStructureSourceTests` | Retain query-radius conversion, generic adaptation shape, cluster/member suppression, and renderer-ready handoff. Replace structure/settlement types and structure visual-state dependency with the generic baked-feature contract. Ground-height and thresholds stay composition policy. |
  | **Migrate** | `FarWorldVisibilityPolicy`, `FarWorldVisibilityPolicyTests` | Retain projected-significance math and hysteresis. Replace `StructureVisibilityClass`/structure keys with generic derived bounds plus optional semantic-importance override; concrete distance caps remain game composition/config. |
  | **Migrate** | `FarWorldRendering.cs`, `ProceduralFarStructureRenderer`, `FarStructureVisibilityTests` | Retain render-ready instance boundary, immutable mesh caching, batching, `DrawMeshInstanced`, and zero persistent GameObjects per distant instance. Rename/generalize structure-only API. Delete renderer logic that parses `"castle"`/`"keep"`/`"fort"` proxy keys or hand-authors type-specific castle/house meshes; generic baked geometry must be the normal payload. |
  | **Delete after generic composition exists** | `ShowcaseFarWorldRendering` | It owns a castle-specific subscription/manifest and rewrites the castle proxy key. Replace with a generic far-world composition consumer fed by automatic baked-feature publication/indexing. |
  | **Retain / narrow** | `FarFieldStructureStore`, `FarFieldStructureStoreSuppressionTests` | Keep authored terrain lowering/material overrides and anonymous/legacy positive-silhouette fallback. Its semantic-proxy suppression is only a compatibility bridge; automatic baked features must not depend on a far-terrain sample hitting their footprint. |
  | **Migrate ownership** | `NaturalScatterVisibilityIndex`, `NaturalScatterVisibilityIndexTests` | Keep deterministic sector/cell regeneration for ordinary mass scatter. Move/align it with the population owner rather than making WorldBuilder a second scatter authority. Remove explicit exceptional-record parallel semantics once automatic promotion feeds the generic sparse baked-feature path. |
  | **Retain** | `VegetationVisibility`, `VegetationVisibilityTests` | Correct mass-population model: projection/query over existing vegetation/tree truth, stable IDs, sector filtering, no voxel residency or renderer authority. Keep external consumers on the Vegetation API boundary. |
  | **Retain** | `VegetationCanopyClusters`, `ForestCanopyClusterBuilderTests` | Correct disposable HLOD derived from tree truth; stable cluster identity/revision and exclusion of independent landmarks are reusable. Exceptional landmark promotion policy must converge on the generic sparse-feature system rather than create another permanent tree manifest. |
  | **Retain / migrate naming-policy** | `TreeFarPresentation`, `TreeVisibilitySelector`, `TreeVisibilityTierPolicyTests`, `TreeVisibilitySelectorTests`, `VisibleVegetationBatchAdapter`, `VisibleVegetationBatchAdapterTests`, `ProceduralForestCanopyRenderer`, `ProceduralForestCanopyRendererTests` | Preserve population rendering selection, hysteresis, batching, and canopy renderer. Keep semantic importance/promotion outside renderer ownership; rename only where needed to avoid implying a second world-truth model. |
  | **Retain composition** | `ShowcaseTreeVisibilityComposition`, `ShowcaseForestCanopyRendering` | These are scene-level policy/wiring consumers rather than semantic owners. Keep only while they invoke production Vegetation/Rendering APIs; module-local validation must not clone their rendering logic. |
  | **Retain** | engine `Rendering.Runtime/FarTerrainCoverageMath`, `VoxelFarTerrainCoverageTests` | Generic geometric coverage math with explicit inputs and no scene/world ownership. Continue to T016 and make diagnostics use real configured resolution. |
  | **Delete compatibility duplicate** | Showcase `SceneRuntime/FarTerrainCoverageMath` | Thin duplicate wrapper around the engine implementation. Migrate call sites to the engine-owned math and remove the compatibility surface rather than preserving two owners. |
  | **Retain / fix** | `FarTerrainCoverageDiagnostics`, `VoxelFarTerrain` coverage changes | Keep coverage diagnostics/fallback-retirement work, but remove hard-coded resolution and validate the 12 km guarantee from actual configuration. |
  | **Retain pending visual proof** | `FarTerrain.shader`, `FarTerrainMaterialFamilyTests` | World-space macro/detail material work is generic Rendering-owned presentation. Keep only if built-player evidence proves near/far material-family continuity and stable detail frequency. |
  | **Migrate assembly refs** | `Game.WorldBuilder.Api.asmdef`, `Game.WorldBuilder.Runtime.asmdef`, `VoxelEngine.Showcase.asmdef`, `VoxelEngine.Tests.EditMode.asmdef` changes | Current `Game.WorldBuilder.Api` imports `MountingForce.WorldGen.Architecture` solely because visibility exposes `StructureFarPresentation`, violating the desired API/runtime boundary. Generalizing the baked-feature API removes that dependency; other modules must consume only owning-module API assemblies. Test/composition refs are adjusted to the final ownership rather than preserved mechanically. |

  **Audit invariant:** the retained implementation has three owners, not one giant far-world subsystem: (1) canonical generators own feature truth and emit/enable generic bake input, (2) sparse far bake/index + mass-population query layers own derived visibility data, and (3) Rendering owns disposable render resources. Showcase/Kentridge code may configure or demonstrate those APIs but may not own a second descriptor/manifest/renderer path.

- [x] **T002 — Locate the canonical generation representation from which far data can be baked automatically.**
  - Inspect existing World Feature Authoring, Structures, WorldBuilder, vegetation/scatter, and voxel-authoring paths to identify the narrowest canonical representation available **before detailed region residency**: generation operations, geometry/form/site plans, authoring graph, voxel commands, or equivalent.
  - Prefer a representation that all normal generated large objects already pass through so adding a new object automatically participates in far baking.
  - Do not introduce a second required authoring interface whose sole purpose is far visibility.
  - Determine whether the generic baker can replay the canonical generator into a coarse occupancy/geometry target, simplify an existing generator-owned geometry representation, or use another deterministic conservative derivation.
  - **Discriminator:** prove on two existing unrelated generated feature shapes that conservative bounds/silhouette data can be derived without object-specific code and without loading their detailed distant voxel regions.
  - **Validated:** catalogue-driven generated features already converge on `FeatureGeneration.EvaluateInstance` / `ShapeProgram.Evaluate`, which emits deterministic bounded primitives/anchors before voxel-region materialization. Production Kentridge structure and WorldBuilder mountain paths exercise unrelated shapes through this same canonical representation.

- [x] **T003 — Implement one generic far-bake output contract and bake pipeline.**
  - The bake output is derived presentation data, not world truth. It carries stable source identity/revision, conservative bounds, transform/orientation, material/style family as available, and generic coarse geometry/massing payload sufficient for distant rendering.
  - Bake at the natural lifecycle point: import/build for static authored assets, deterministic planning/generation for procedural features, and runtime creation/update for genuinely runtime-created content.
  - Default importance/visibility comes from derived bounds/projected significance. Semantic tags/importance may override exceptional gameplay requirements but are not mandatory per-object metadata.
  - Custom HLOD/far recipes are optional escape hatches only after a demonstrated visual defect; a newly created normal object must work without one.
  - No `Mesh`, `GameObject`, camera state, Showcase/Kentridge type, or resident-region handle may leak into the authoritative generation API merely to support far rendering.
  - **Regression:** two unrelated generated objects added through their normal generation path produce deterministic far-bake outputs with no new far-specific adapter/registration step.
  - **Validated:** `FeaturePresentationBake` / `FeaturePresentationBaker` derive the generic presentation record, and `FeaturePresentationCatalogueBaker` supplies one catalogue lifecycle hook including structural-root expansion. Exact focused CI run `33473262150` passed `VoxelEngine.Tests.EditMode.FeaturePresentationCatalogueBakerTests` on child `e21a9b13e46723aa0595bf914f21eaedf25c476e`, parent feature SHA `303cb0b3e5e2b06405f23c1406676ee560b2344a`.

- [x] **T004 — Generalize the sparse spatial visibility source around baked feature outputs.**
  - Adapt or replace `IWorldVisibilitySource` / `WorldVisibilityManifest` so the index stores/query generic baked sparse features rather than `StructureFarPresentation`.
  - Keep it metadata/presentation-cache only: no voxel bricks, interiors, renderer objects, AI, collision, or physics.
  - Deterministic sector query, de-duplication for cross-sector bounds, stable ordering, replacement/removal by stable source identity/revision.
  - **Regression:** query requires no region generation/residency and supports unrelated feature shapes through the same API.
  - **Validated:** `IFeaturePresentationSource` / `FeaturePresentationManifest` implement a generic metadata-only sparse source with deterministic sector query, cross-sector de-duplication, stable SourceId ordering, replacement and removal. Exact focused CI run `33475203893` passed `VoxelEngine.Tests.EditMode.FeaturePresentationManifestTests` on child `44b4b34d300a142f05984bc2ef62961a737cb442`, parent feature SHA `303cb0b3e5e2b06405f23c1406676ee560b2344a`.

## Phase 1 — Prove new objects require no far-visibility work

- [ ] **T005 — Prove automatic bake for a planned structure with no castle-specific visibility integration.**
  - Use the existing castle/large-structure generation path as one consumer, but do not add a castle descriptor, castle visibility manifest, castle event, or castle renderer adapter.
  - The normal generation/planning path must automatically feed the generic far baker.
  - **Regression:** a never-visited planned castle is queryable/renderable from baked far data before any intersecting detailed voxel region is generated.

- [ ] **T006 — Prove automatic bake for an independent non-castle feature without changing the far system.**
  - Use a genuinely different generated category required by acceptance, preferably a giant rock/monolith or another existing non-building generator.
  - No changes to the bake contract, visibility index, selection policy, or renderer query interface are allowed merely because this producer is different.
  - The test should model the future workflow: create/use the object through its normal generator and obtain far rendering automatically.
  - **Regression:** structure and natural feature coexist with independent stable identity/revision and require no residency or per-object far plumbing.

- [ ] **T007 — Keep high-volume populations out of the sparse baked-feature index and automate promotion.**
  - Ordinary trees/rocks/shrubs remain deterministic sector/cell queries owned by their population module; do not bake/register millions of sparse records.
  - Preserve/rework existing vegetation, canopy, and natural-scatter query code where it already follows this model.
  - Exceptional members whose derived bounds/projected significance exceed configured thresholds automatically promote into the generic sparse far-bake/index path; explicit semantic importance remains an override.
  - **Regression:** ordinary forest/rock populations do not grow sparse-index cardinality; an exceptional member is promoted exactly once with stable identity without object-specific registration.

## Phase 2 — Make rendering generic downstream of baked data

- [ ] **T008 — Replace structure-specific render input with a generic far-feature render contract.**
  - Audit `FarWorldRendering.cs`; rename/generalize structure-only types where necessary.
  - Rendering API may contain render-ready bounds/transform, stable ID, generic baked geometry handle/key, material/style family, selected representation tier, and visual-state flags.
  - Rendering API/runtime must not reference WorldBuilder/Game planning types or special-case castles, Kentridge, or Showcase.
  - **Regression:** rendering contract can represent T005 and T006 outputs without API changes.

- [ ] **T009 — Generalize adaptation and projected-significance selection.**
  - One generic adapter/query path consumes baked sparse features, applies configured projected-size + optional semantic-importance + hysteresis/readiness policy, and emits render-ready instances.
  - Concrete thresholds/caps stay in game composition/config; shared renderer/policy contains no scene coordinates or named-content rules.
  - Reuse existing projected-significance math if it satisfies this boundary.
  - **Regression:** ordinary small feature culls while large/important feature persists; back/forth motion around thresholds is stable.

- [ ] **T010 — Render generic baked geometry before considering custom recipes.**
  - Prefer generic conservative/simplified baked geometry or massing that preserves the source silhouette over a registry requiring a hand-authored recipe per object type.
  - Reuse cached/instanced rendering infrastructure; immutable baked resources are cached/batched and there is no persistent GameObject per distant feature.
  - Only introduce optional presentation overrides when built-player evidence shows the generic bake cannot preserve a required landmark relationship within budget.
  - **Regression:** two unrelated automatically baked feature shapes render through the same renderer path with stable batching and no per-type renderer implementation.

- [ ] **T011 — Preserve aggregate HLOD for dense groups without changing semantic truth.**
  - Settlement/building clustering and forest canopy clustering are disposable presentation aggregation over baked feature/population truth.
  - Cluster activation must not duplicate constituent draws; landmark members remain independent when policy requires.
  - Reuse existing deterministic cluster/canopy builders only if ownership and input contracts remain generic.
  - **Regression:** cluster/member handoff is deterministic and hysteretic; state changes invalidate only affected aggregate revisions.

- [ ] **T012 — Implement readiness-aware near/far representation handoff.**
  - Far representation stays until the authoritative near surface covering the feature is actually ready, not merely resident/queued.
  - On retreat/eviction, far representation becomes ready before near presentation disappears.
  - Keep overlap bounded; no missing frame and no permanent double rendering.
  - **Regression:** approach/retreat across the resident boundary for both a sparse baked feature and forest/tree representation.

## Phase 3 — Remove the rejected castle-specific path and duplicate authority

- [ ] **T013 — Delete/fold castle-specific visibility plumbing after generic parity exists.**
  - Remove `ShowcaseCastleFarPresentation` and `ShowcaseCastleVisibilityManifest`.
  - Remove castle-only global event/plumbing in `ShowcaseWorld.FarVisibility.cs` if generic bake lifecycle supersedes it.
  - Replace `ShowcaseFarStructureSource` / `ShowcaseFarWorldRendering` structure assumptions with the generic baked-feature path rather than layering the new system beside the old one.
  - Remove obsolete castle-specific tests and replace them with automatic-bake regressions from T005/T006.

- [ ] **T014 — Remove or internalize `StructureFarPresentation` so there is one far-derived feature concept.**
  - Migrate any generally useful fields into the generic bake output and delete the cross-module parallel structure concept.
  - If structure generation needs a private intermediate, keep that internal to its owning runtime; it must not become a required far-visibility API for future structures.
  - Prove there is one owner for source identity/revision and no duplicate manifest/state store for the same fact.

- [ ] **T015 — Keep `FarFieldStructureStore` only for nonsemantic authored surface deviation/fallback.**
  - Automatically baked features must not depend on far-terrain vertices landing inside their footprint.
  - Preserve useful terrain lowering/raising and anonymous/legacy voxel-silhouette fallback for content that genuinely lacks a normal feature-generation lifecycle.
  - **Regression:** baked landmark remains visible when no far-terrain sample hits its footprint; anonymous terrain alteration still affects far terrain.

## Phase 4 — Complete terrain correctness and visual continuity

- [ ] **T016 — Finish geometric coverage diagnostics and guarantee configured far radius.**
  - Keep the existing reusable `FarTerrainCoverageMath` if correct.
  - Fix the current diagnostics draft so clipmap resolution is supplied from real configuration, not hard-coded.
  - Expose requested radius, guaranteed radius, ring count/spacings, and fallback state on the existing diagnostic surface.
  - **Regression:** 409.6 m -> 12 km is guaranteed across worst snap phases and fallback retirement never shrinks coverage.

- [ ] **T017 — Validate/fix whole-range far-terrain material and detail coherence.**
  - Reuse current world-space macro/detail shader work only if built-player evidence confirms it derives compatible terrain families/lighting language from the same world facts as near terrain.
  - Surface detail may be finer than clipmap vertices but must be distance-filtered and stable in absolute world space.
  - Add a denser bounded inner far-geometry tier only if visual evidence still proves silhouette loss after shading/material continuity is fixed.

- [ ] **T018 — Complete the ~350–600 m resident/far terrain transition.**
  - Multiple headings/elevations and snap phases.
  - No hard geometry, material-family, normal, color, fog, lighting, or coverage seam.
  - Do not extend near voxel residency to solve the problem.

## Phase 5 — Production-faithful module validation and acceptance evidence

- [ ] **T019 — Add the module-local far-world built-player validation consumer.**
  - Follow repository `*.module-validation.json` discovery; do not hand-register every test.
  - Validation scene/scenario must invoke production terrain, normal feature generators + automatic far bake/index, generic far renderer, vegetation/tree/canopy, materials, atmosphere/lighting, and production composition boundaries.
  - Test code may control deterministic seed, cameras, instrumentation, and assertions only. No fake planes/cubes/trees/materials or parallel renderer.

- [ ] **T020 — Capture the required fixed-distance terrain comparison.**
  - Durable built-player captures at approximately **0.5, 1, 3, 6, 10, and 12 km** from comparable terrain-dominant views.
  - Inspect material family, broad terrain character, slope/rock/soil relationships, lighting/fog, surface-frequency stability, silhouette loss, and shimmer.
  - Produce a comparison sheet/artifact if the harness supports it; these captures replace the unrelated Kentridge opening screenshots as visual evidence.

- [ ] **T021 — Prove never-resident automatically baked feature visibility at 8/10/12 km.**
  - Cardinal and diagonal views, representative/worst snap phases.
  - Assert target detailed voxel regions are not resident/generated while the generic baked-feature path remains visible.
  - Run for both the structure and independent producer from T005/T006.

- [ ] **T022 — Prove population HLOD and automatic promotion behavior visually.**
  - Horizon mountains read as forested without individual horizon trees.
  - Ordinary small scatter disappears by projected significance.
  - Exceptional natural feature automatically promotes/remains visible without custom object-specific far plumbing.
  - Verify no obvious tree/canopy or cluster/member holes/double representation during travel.

- [ ] **T023 — Measure blast radius and authoritative device budgets.**
  - Record bake cost, CPU query/build/update cost, render-thread/draw cost, GPU cost where available, allocation/GC behavior, baked proxy/cache memory, canopy/cluster counts, and far-terrain vertex/build cost against the repository device matrix.
  - Compare initial procedural bake, steady camera, moving camera, dense forest/settlement, runtime-created content if supported, and 12 km landmark cases.
  - Do not weaken budgets or acceptance to fit results.

## Phase 6 — Exact-head validation and cleanup

- [ ] **T024 — Run focused behavioral CI on the final architecture.**
  - Smallest regression proving normal generation -> automatic bake -> query -> representation plus required affected-module automatic validation.
  - Use only `ci-test/fixes/agent-7`; never replace queued/running CI.

- [ ] **T025 — Run exact-SHA module-local built-player evidence and canonical Kentridge integration.**
  - Required module scene/scenario and `KentridgePlayableSlice` must pass on the exact feature SHA.
  - Inspect artifacts directly; green automation without production-quality visual evidence is insufficient.

- [ ] **T026 — Final architecture/ownership cleanup.**
  - Search for castle-/Showcase-specific far-visibility ownership, duplicate feature descriptors, required per-object far adapters/registrations, renderer references to Game/WorldBuilder runtime types, and dead migration adapters.
  - Confirm adding a normal new generated large object through the canonical generation path requires **zero far-system code changes**.
  - Confirm API/runtime module boundaries and independent-producer reuse.
  - Update `architecture-proposal.md` to the implemented automatic far-bake architecture; remove obsolete per-structure guidance.
  - Update `plan.md` with final measured results and remaining gates only.

- [ ] **T027 — Close only after every acceptance criterion is proven.**
  - Complete SceneIssue resolution fields/evidence.
  - Merge current `master`, revalidate affected exact head if necessary, move only this SceneIssue `open` -> `closed`, then non-force merge/push to `master` per workflow.
