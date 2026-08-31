# Far-World Visibility Implementation Tasks

**Architecture reset:** 2026-08-31  
**Feature head when reset began:** `fa26c6d1b8087818b1277ee0ade07ec133a2eb35`  
**Rule:** work the next unchecked non-blocked task. Existing green tests remain useful evidence only where the implementation survives this reset; they do not grandfather rejected castle-specific architecture.

## Phase 0 — Reset the architecture before more implementation

- [x] **T000 — Replace the SceneIssue plan with the scalable macro-feature direction.**
  - Reject one descriptor/manifest subsystem per large object type.
  - Preserve acceptance: never-resident landmarks, 12 km visibility, terrain coherence, HLOD, stable handoff, and device budgets.

- [ ] **T001 — Audit the current branch and classify every far-world addition as retain, migrate, or delete.**
  - Inventory `StructureFarPresentation`, `IWorldVisibilitySource`, `WorldVisibilityManifest`, `ShowcaseCastleFarPresentation`, `ShowcaseCastleVisibilityManifest`, `ShowcaseFarStructureSource`, `ShowcaseFarWorldRendering`, far render API/runtime, cluster code, natural-scatter visibility, vegetation/tree visibility, canopy rendering, terrain coverage/detail changes, and their tests.
  - **Retain** only code whose abstraction is generic and has one clear owner.
  - **Migrate** structure-only contracts that are actually generic macro-feature/representation concepts.
  - **Delete** Showcase/castle-specific visibility ownership and tests that merely prove the rejected design.
  - Check API/runtime assembly direction while auditing; other modules may depend only on module API assemblies.
  - Record the resulting keep/migrate/delete table in this file before changing production code.

- [ ] **T002 — Locate the canonical world-generation semantic boundary and define one generic sparse macro-feature contract there.**
  - First inspect existing World Feature Authoring / WorldBuilder API contracts so this does not create a parallel notion of world feature.
  - Add or extend the narrowest API-owned semantic record for individually significant features. Required concepts: stable identity, deterministic world bounds/anchor/orientation, semantic kind/tags, importance/visibility inputs, coarse presentation recipe/style key, optional cluster/relationship identity, persistent presentation-relevant state summary where required, and deterministic revision.
  - The contract describes **what exists**, not how far rendering works. It must not contain `Mesh`, `Material`, `GameObject`, camera state, HLOD tier, `CastlePlan`, Showcase/Kentridge types, or resident-voxel handles.
  - Do not encode a closed enum that requires shared API edits for every future game object if semantic tags/keys can express the requirement safely.
  - **Regression:** deterministic equality/revision/bounds semantics and API assembly-boundary test.

- [ ] **T003 — Generalize the sparse spatial visibility source around macro features.**
  - Adapt or replace `IWorldVisibilitySource` / `WorldVisibilityManifest` so the index stores/query generic macro features, not `StructureFarPresentation`.
  - Keep it metadata-only: no voxel bricks, interiors, renderer objects, AI, collision, or physics.
  - Deterministic sector query, de-duplication for cross-sector bounds, stable ordering, replacement/removal by stable identity.
  - **Regression:** query requires no region generation/residency and supports unrelated semantic kinds through the same API.

## Phase 1 — Prove producers scale without per-object visibility subsystems

- [ ] **T004 — Integrate a planned structure through the generic producer path.**
  - The existing structure/castle planning owner publishes a generic macro feature when planning establishes the semantic fact, before physical voxel realization.
  - Mapping from structure planning facts to the generic contract may be a small producer-owned mapper; it may not create a castle-owned visibility manifest or renderer adapter.
  - **Regression:** a never-visited planned castle is queryable before any intersecting voxel region is generated.

- [ ] **T005 — Integrate an independent non-castle producer through the exact same macro-feature API.**
  - Use a genuinely different producer category already required by acceptance, preferably an exceptional natural feature (giant rock/monolith or promoted landmark tree) rather than another castle-shaped structure.
  - No changes to the macro-feature API, visibility index, selection policy, or renderer query interface are allowed merely because this producer is different.
  - **Regression:** both producer types coexist, query deterministically, retain independent identities/revisions, and require no residency.

- [ ] **T006 — Keep high-volume populations out of the sparse macro-feature index and formalize promotion.**
  - Ordinary trees/rocks/shrubs remain deterministic sector/cell queries owned by their population module; do not register millions of sparse records.
  - Preserve/rework existing vegetation, canopy, and natural-scatter query code where it already follows this model.
  - Add a semantic/config-driven promotion rule for exceptional members whose scale/importance requires landmark treatment; promotion yields a normal macro feature.
  - **Regression:** ordinary forest/rock populations do not grow sparse-index cardinality; an exceptional member is promoted exactly once with stable identity.

## Phase 2 — Make rendering generic downstream of world truth

- [ ] **T007 — Replace structure-specific render input with a generic far-feature render contract.**
  - Audit `FarWorldRendering.cs`; rename/generalize structure-only types where necessary.
  - Rendering API may contain render-ready bounds/transform, stable ID, presentation recipe/style key, selected representation tier, and visual-state flags.
  - Rendering API/runtime must not reference WorldBuilder/Game planning types or special-case castles, Kentridge, or Showcase.
  - **Regression:** rendering contract can represent the T004 structure and T005 natural landmark without API changes.

- [ ] **T008 — Generalize composition adaptation and projected-significance selection.**
  - One composition adapter queries generic macro features, applies configured projected-size + semantic-importance + hysteresis/readiness policy, and emits generic render-ready instances.
  - Concrete thresholds/caps stay in game composition/config; shared renderer/policy contains no scene coordinates or named-content rules.
  - Reuse existing projected-significance math if it satisfies this boundary.
  - **Regression:** ordinary small feature culls while large/important feature persists; back/forth motion around thresholds is stable.

- [ ] **T009 — Make proxy/HLOD recipes extensible data/registry behavior rather than object-specific renderer code.**
  - Reuse cached/instanced proxy infrastructure, but select a registered presentation recipe from the generic feature.
  - A castle recipe may preserve walls/keep/tower silhouette; a monolith/giant natural feature recipe may use another massing strategy. Adding the second recipe must not add another visibility subsystem.
  - No persistent GameObject per distant feature; immutable proxy resources are cached/batched.
  - **Regression:** two unrelated recipe keys render through the same renderer path with stable batching and no per-instance object ownership.

- [ ] **T010 — Preserve aggregate HLOD for dense groups without changing semantic truth.**
  - Settlement/building clustering and forest canopy clustering are disposable presentation aggregation over generic feature/population truth.
  - Cluster activation must not duplicate constituent draws; landmark members remain independent when policy requires.
  - Reuse existing deterministic cluster/canopy builders only if ownership and input contracts remain generic.
  - **Regression:** cluster/member handoff is deterministic and hysteretic; state changes invalidate only affected aggregate revisions.

- [ ] **T011 — Implement readiness-aware near/far representation handoff.**
  - Far representation stays until the authoritative near surface covering the feature is actually ready, not merely resident/queued.
  - On retreat/eviction, far representation becomes ready before near presentation disappears.
  - Keep overlap bounded; no missing frame and no permanent double rendering.
  - **Regression:** approach/retreat across the resident boundary for both a sparse macro feature and forest/tree representation.

## Phase 3 — Remove the rejected castle-specific path and duplicate authority

- [ ] **T012 — Delete/fold castle-specific visibility plumbing after generic parity exists.**
  - Remove `ShowcaseCastleFarPresentation` and `ShowcaseCastleVisibilityManifest` unless a remaining type has a producer-local responsibility unrelated to visibility ownership.
  - Remove castle-only global event/plumbing in `ShowcaseWorld.FarVisibility.cs` if generic publication supersedes it.
  - Replace `ShowcaseFarStructureSource` / `ShowcaseFarWorldRendering` structure assumptions with the generic composition path rather than layering the new system beside the old one.
  - Remove obsolete castle-specific tests and replace them with producer-independent regressions from T004/T005.

- [ ] **T013 — Remove or migrate `StructureFarPresentation` so there is one semantic feature concept.**
  - If its fields belong on the generic macro-feature contract, migrate them and delete the parallel type.
  - If structure generation needs a private intermediate, keep that internal to the owning module runtime; it must not become the cross-module far-visibility API.
  - Prove there is one owner for stable feature identity/bounds/importance and no duplicate manifest/state store for the same fact.

- [ ] **T014 — Keep `FarFieldStructureStore` only for nonsemantic authored surface deviation/fallback.**
  - Semantic features with generic proxies must not depend on far-terrain vertices landing inside their footprint.
  - Preserve useful terrain lowering/raising and anonymous/legacy voxel-silhouette fallback for things that genuinely have no semantic producer.
  - **Regression:** macro landmark remains visible when no far-terrain sample hits its footprint; anonymous terrain alteration still affects far terrain.

## Phase 4 — Complete terrain correctness and visual continuity

- [ ] **T015 — Finish geometric coverage diagnostics and guarantee configured far radius.**
  - Keep the existing reusable `FarTerrainCoverageMath` if correct.
  - Fix the current diagnostics draft so clipmap resolution is supplied from real configuration, not hard-coded.
  - Expose requested radius, guaranteed radius, ring count/spacings, and fallback state on the existing diagnostic surface.
  - **Regression:** 409.6 m -> 12 km is guaranteed across worst snap phases and fallback retirement never shrinks coverage.

- [ ] **T016 — Validate/fix whole-range far-terrain material and detail coherence.**
  - Reuse current world-space macro/detail shader work only if built-player evidence confirms it derives compatible terrain families/lighting language from the same world facts as near terrain.
  - Surface detail may be finer than clipmap vertices but must be distance-filtered and stable in absolute world space.
  - Add a denser bounded inner far-geometry tier only if visual evidence still proves silhouette loss after shading/material continuity is fixed.

- [ ] **T017 — Complete the ~350–600 m resident/far terrain transition.**
  - Multiple headings/elevations and snap phases.
  - No hard geometry, material-family, normal, color, fog, lighting, or coverage seam.
  - Do not extend near voxel residency to solve the problem.

## Phase 5 — Production-faithful module validation and acceptance evidence

- [ ] **T018 — Add the module-local far-world built-player validation consumer.**
  - Follow repository `*.module-validation.json` discovery; do not hand-register every test.
  - Validation scene/scenario must invoke production terrain, macro-feature producers/index, far renderer, vegetation/tree/canopy, materials, atmosphere/lighting, and production composition boundaries.
  - Test code may control deterministic seed, cameras, instrumentation, and assertions only. No fake planes/cubes/trees/materials or parallel renderer.

- [ ] **T019 — Capture the required fixed-distance terrain comparison.**
  - Durable built-player captures at approximately **0.5, 1, 3, 6, 10, and 12 km** from comparable terrain-dominant views.
  - Inspect material family, broad terrain character, slope/rock/soil relationships, lighting/fog, surface-frequency stability, silhouette loss, and shimmer.
  - Produce a comparison sheet/artifact if the harness supports it; these captures replace the unrelated Kentridge opening screenshots as visual evidence.

- [ ] **T020 — Prove never-resident landmark visibility at 8/10/12 km.**
  - Cardinal and diagonal views, representative/worst snap phases.
  - Assert target landmark voxel regions are not resident/generated while the generic macro-feature path remains visible.
  - Run for at least one planned structure and the independent producer from T005.

- [ ] **T021 — Prove population HLOD and promotion behavior visually.**
  - Horizon mountains read as forested without individual horizon trees.
  - Ordinary small scatter disappears by projected significance.
  - Exceptional natural feature remains as a landmark through the generic macro-feature path.
  - Verify no obvious tree/canopy or cluster/member holes/double representation during travel.

- [ ] **T022 — Measure blast radius and authoritative device budgets.**
  - Record CPU query/build/update cost, render-thread/draw cost, GPU cost where available, allocation/GC behavior, proxy/cache memory, canopy/cluster counts, and far-terrain vertex/build cost against the repository device matrix.
  - Compare steady camera, moving camera, dense forest/settlement, and 12 km landmark cases.
  - Do not weaken budgets or acceptance to fit results.

## Phase 6 — Exact-head validation and cleanup

- [ ] **T023 — Run focused behavioral CI on the final architecture.**
  - Smallest regression proving generic producer -> query -> representation plus required affected-module automatic validation.
  - Use only `ci-test/fixes/agent-7`; never replace queued/running CI.

- [ ] **T024 — Run exact-SHA module-local built-player evidence and canonical Kentridge integration.**
  - Required module scene/scenario and `KentridgePlayableSlice` must pass on the exact feature SHA.
  - Inspect artifacts directly; green automation without production-quality visual evidence is insufficient.

- [ ] **T025 — Final architecture/ownership cleanup.**
  - Search for castle-/Showcase-specific far-visibility ownership, duplicate feature descriptors, renderer references to Game/WorldBuilder runtime types, and dead migration adapters.
  - Confirm API/runtime module boundaries and independent-producer reuse.
  - Update `architecture-proposal.md` to the implemented generic macro-feature architecture; remove obsolete per-structure guidance.
  - Update `plan.md` with final measured results and remaining gates only.

- [ ] **T026 — Close only after every acceptance criterion is proven.**
  - Complete SceneIssue resolution fields/evidence.
  - Merge current `master`, revalidate affected exact head if necessary, move only this SceneIssue `open` -> `closed`, then non-force merge/push to `master` per workflow.