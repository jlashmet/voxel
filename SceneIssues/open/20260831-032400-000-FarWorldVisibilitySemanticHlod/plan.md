# Far-World Visibility Implementation Plan

## Purpose

Define a durable architecture for keeping terrain, settlements, castles, forests, landmark trees, rock formations, and ordinary scatter visually coherent from the resident voxel world through the far horizon without making distant voxel regions resident. The design must preserve deterministic world truth, reuse current terrain/voxel systems, and make representation choice semantic and budget-driven rather than scene-specific.

## Observed baseline

- `VoxelShowcase` currently streams eight 51.2 m regions (~409.6 m) and hands that radius to `VoxelFarTerrain`.
- Near surface extraction already uses progressively coarser source steps; far terrain is a 96-cell geometric clipmap sampled analytically rather than from resident terrain voxels.
- With the current ~409.6 m inner radius, far-ring spacing is approximately 12.8/25.6/51.2/102.4/204.8 m.
- `FarFieldStructureStore` retains authored raised/lowered surfaces in 16x16 columns per region (3.2 m columns) with a 2.4 m minimum raised-feature threshold, but `VoxelFarTerrain` point-samples that store only at clipmap vertices.
- There are no dedicated persistent far representations for ordinary trees, boulders, shrubs, or other scatter.

## Acceptance

1. A declared landmark remains visible at 8, 10, and 12 km from cardinal and diagonal views, including camera snap phases, without requiring its voxel regions to be resident.
2. A never-visited declared landmark can still appear from macro-world metadata.
3. Broad terrain, semantic structures, and deterministic scatter use independent distance representations while sharing one deterministic world definition.
4. Forested mountains remain visibly forested at horizon distance without retaining/drawing individual trees.
5. Small scatter naturally disappears by projected significance while giant natural features are promoted to landmark treatment.
6. Near/far transitions have overlap/hysteresis and do not visibly drop an object during representation handoff.
7. Configured far radius is geometrically guaranteed; tests prove coverage rather than inferring it from ring count.
8. CPU/GPU/memory budgets are measured against the authoritative device matrix before rollout.

## Working hypotheses / discriminators

- **H1:** Existing far-terrain structure sampling is sufficient if made conservative. Test a narrow castle footprint across outer-ring sample phases. If still silhouette-poor, semantic structure HLOD is required.
- **H2:** Deterministic macro-cell scatter can reproduce convincing forests without persistent per-tree records. Compare regenerated cell identity/distribution across sessions and evaluate mid/far visual continuity.

## Selected direction

Keep `VoxelFarTerrain` for broad analytic terrain and `FarFieldStructureStore` as a generic authored-surface fallback. Add a small far-visibility data layer with semantic structure records, deterministic scatter-cell descriptors, projected-size/importance tier policy, structure HLOD, forest/canopy aggregation, and natural-landmark promotion. Full rationale, contracts, phases, tests, and migration details are in `architecture-proposal.md`.

## Validation gates

Implement in independently testable phases: coverage correctness -> visibility manifest -> semantic structure HLOD -> deterministic scatter -> canopy/forest HLOD -> natural landmark promotion -> transition/budget stress validation. Existing SceneIssues remain the implementation units; do not duplicate active macro-world or terrain-streaming work.

## Progress — 2026-08-31 agent-7

- T001/T002 code and behavioral coverage tests are present. `FarTerrainCoverageMath` derives spacing, half extent, snap loss, guaranteed coverage, guarded minimum ring count, and fallback-retirement coverage. `VoxelFarTerrainCoverageTests` covers the shipped 409.6 m -> 12 km configuration, representative/worst snap phases, `MaxRings` failure, an independent configuration, and no-shrink fallback retirement. These tasks remain unchecked until final exact-current-SHA validation is recorded.
- Exact-source run `33389073902` against feature SHA `983a53e25bbde0f866db14c21546318ccacb6161` passed all 8 requested `VoxelFarTerrainCoverageTests`, passed automatically derived `kentridge-integration` and `spatial-reservations` module validation, and produced successful real-player captures for KentridgePlayableSlice and SpatialReservationValidation. The run failed only the SceneIssue replay because its default 30-second replay produced one screenshot at ~20.8 s while the harness requires at least two.
- The earlier SceneIssue replay path failure was configuration, not product behavior: the capture harness accepts `SceneIssues/open/<id>/issue.json`, not the bare SceneIssue ID. The corrected request reaches and runs the actual Kentridge scene.
- T003 remains independently actionable through the existing `CompactFpsOverlay`, but implementation is blocked by the available write surface: the connected GitHub API can only replace complete files and `VoxelFarTerrain.cs` is ~43 KB. The execution container has no repository checkout and outbound DNS resolution for GitHub fails, so a safe line-level patch cannot currently be produced. Acceptance is unchanged; do not wholesale-rewrite the file merely to add diagnostics.
- T004 has been rewritten to the tracker contract. `StructureFarPresentation` is renderer-neutral semantic data keyed by stable structure ID, footprint/height/archetype/material-family/visibility-class/revision; it contains no Unity rendering objects. Its focused regression exists. It remains unchecked until final exact-SHA validation.
- T005 root cause/discriminator is established. `StructuresComposition.PlanCastle(...)` returns the game-owned semantic `CastlePlan` before `BeginCastleBuild(...)` performs physical authoring, and `ShowcaseWorld.QueueLandmarks()` calls `PlanCastle(...)` before it populates `_castleRegions`. By contrast, legacy `FarFieldStructureStore.CaptureRegion(...)` derives coarse surfaces from already-resident voxel storage and therefore cannot satisfy never-visited semantic visibility. The correct fix is plan -> descriptor -> visibility manifest at that pre-residency boundary. Editing the actual call site is currently blocked because it resides in the ~115 KB `ShowcaseWorld.cs` and only whole-file connector replacement is available; acceptance is unchanged.
- T006 uses `IWorldVisibilitySource`/`WorldVisibilityManifest` with deterministic integer-sector indexing, cross-sector deduplication, deterministic output order, and stable replacement/removal keyed by structure ID. Focused regressions cover these behaviors and no residency/generation ownership is introduced. It remains unchecked until final exact-SHA validation.
- T008 campaign planning now carries a read-only `IWorldVisibilitySource` in `KentridgeCampaignGenerationPlan`. `KentridgeFarPresentationPlanner` derives building descriptors from the already-authored Kentridge `SettlementPlan.Plots`, deterministic architecture compiler, existing site geometry, theme, and geometry profile; it skips the well because the existing site-geometry contract treats it as a non-building interaction anchor and fails closed if any actual building lacks site geometry. No voxel catalogue/region generation is invoked. `KentridgeFarVisibilityPlanningTests` verifies the real campaign planning output already contains every planned building exactly once, including ordinary fabric and significant landmarks, before voxel generation. T008 remains unchecked until exact-current-SHA CI passes.
- T009 engine rendering boundary is present in `Assets/VoxelEngine/Rendering/Api/FarWorldRendering.cs`. It defines stable render-ready structure identity/transform/bounds, semantic proxy/style keys, tier/visual flags, and `IFarStructureRenderer` without depending on Game/WorldBuilder types. It remains unchecked until exact-current-SHA compilation/behavioral validation.
- T010 composition adapter is present in `ShowcaseFarStructureSource.cs`. It queries only `IWorldVisibilitySource`, injects camera-aware tier selection and ground-height policy, converts deterministic decimetre bounds to render instances, preserves stable structure IDs, and never requests voxel residency. `ShowcaseFarStructureSourceTests` is an independent fake-source fixture covering stable identity, camera-policy handoff, dimensions/elevation, landmark flags, and cull omission. T010 remains unchecked until exact-current-SHA compilation/behavioral validation.
- T011 runtime proxy renderer is present in `Assets/VoxelEngine/Rendering/Runtime/FarWorld/ProceduralFarStructureRenderer.cs`. It caches immutable proxy meshes by semantic proxy+tier, batches instance matrices for `Graphics.DrawMeshInstanced`, keeps style materials cached, supplies house roof massing and castle wall/keep/tower/roof silhouette masses, and creates no persistent GameObject per structure. `FarStructureVisibilityTests` covers stable batch keys and the no-per-instance-object invariant. T011 remains unchecked until exact-current-SHA compilation/behavioral and later built-player visual validation.
- T012 policy foundation is present in `FarWorldVisibilityPolicy.cs`. Projected significance derives from camera/FOV/viewport and semantic bounds; configurable enter/exit thresholds provide hysteresis; configurable semantic distance caps allow ordinary structures to disappear sooner while anchors/landmarks remain eligible farther out. `FarWorldVisibilityPolicyTests` covers projected size, hysteresis, horizon semantics, and distance caps. Final scene instantiation/configuration remains blocked with the large `VoxelShowcase.cs` integration surface, so T012 stays unchecked.
- T014 now splits known-semantic suppression from the generic positive-silhouette fallback without importing WorldBuilder semantics. `FarFieldStructureStore.SuppressBuiltSilhouette(...)` records conservative coarse-column exclusions in generic world-voxel bounds; `HeightAt` returns no built fallback in excluded columns while lowered terrain/material channels remain independent, anonymous columns outside the bounds remain eligible, suppression is idempotent, and `Clear` resets it. `FarFieldStructureStoreSuppressionTests` covers coarse-column behavior, negative/cross-region indexing, idempotent cache versioning, clear, and invalid bounds. T014 remains unchecked pending exact-head CI and later T016 integration proof.
- `VoxelShowcase` already exposes the legacy far-field handoff by assigning `_world.FarField` to `VoxelFarTerrain.Structures`; this is useful only as terrain/anonymous-voxel fallback. Semantic T005/T007 must not promote that voxel-derived store into the new source of truth.
- The full tracker contains T001–T033. Required late gates include the complete behavioral suite (T029), built-player perceptual fixtures/evidence (T030), authoritative device-matrix budget validation (T031), cleanup only after parity (T032), and architecture documentation after measured evidence (T033). Do not close early.

## Current validation / execution state

- Current feature head before this plan commit is `e03e480f2db675289f18dd7badea2cb5204bd468`.
- Sole targeted-CI transport remains `ci-test/fixes/agent-7`; no custom request has been written to the feature branch.
- Prior lineage-invalid run `33389906556` completed successfully but remains supporting evidence only. Corrected direct-child request run `33407936184` also completed successfully against feature source `1feeb734995653e4424cac69607ff5f77419b782`; it is valid supporting evidence for the earlier coverage/manifest state.
- Direct-child request run `33409771197` targets feature source `15d09d004bc36aad2f8338109c4796eed4934eb3` with `KentridgeFarVisibilityPlanningTests` and is queued. It has not been replaced or mutated. T014 subsequently advanced the feature branch, so this run is now supporting evidence rather than the final exact-head gate even if green.
- T015/T016 remain blocked on the large `VoxelFarTerrain.cs` / `VoxelShowcase.cs` / `ShowcaseWorld.cs` integration surfaces. T017 is the next independent non-blocked task.
- The feature branch advances whenever production or plan work is committed, so all final gates must be rerun against the final exact feature SHA; earlier green source evidence is supporting provenance only.
- The execution container still has no repository checkout and outbound DNS resolution for GitHub fails. Repository reads/writes are therefore through the connected GitHub API. Small/medium files can be edited safely as whole-file replacements; large-file changes remain blocked unless a safe complete reconstruction can be established.
