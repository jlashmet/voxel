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

- T001/T002 code and behavioral coverage tests are present. `FarTerrainCoverageMath` derives spacing, half extent, snap loss, guaranteed coverage, guarded minimum ring count, and fallback-retirement coverage. `VoxelFarTerrainCoverageTests` covers the shipped 409.6 m -> 12 km configuration, representative/worst snap phases, `MaxRings` failure, an independent configuration, and no-shrink fallback retirement. These tasks remain unchecked until exact-current-SHA validation is recorded.
- Exact-source run `33389073902` against feature SHA `983a53e25bbde0f866db14c21546318ccacb6161` passed all 8 requested `VoxelFarTerrainCoverageTests`, passed automatically derived `kentridge-integration` and `spatial-reservations` module validation, and produced successful real-player captures for KentridgePlayableSlice and SpatialReservationValidation. The run failed only the SceneIssue replay because its default 30-second replay produced one screenshot at ~20.8 s while the harness requires at least two.
- The earlier SceneIssue replay path failure was configuration, not product behavior: the capture harness accepts `SceneIssues/open/<id>/issue.json`, not the bare SceneIssue ID. The corrected request reaches and runs the actual Kentridge scene.
- T003 remains independently actionable through the existing `CompactFpsOverlay`, but implementation is blocked by the available write surface: the connected GitHub API can only replace complete files and `VoxelFarTerrain.cs` is ~43 KB. The execution container has no repository checkout and outbound DNS resolution for GitHub fails, so a safe line-level patch cannot currently be produced. Acceptance is unchanged; do not wholesale-rewrite the file merely to add diagnostics.
- T004 has been rewritten to the tracker contract. `StructureFarPresentation` is now renderer-neutral semantic data keyed by stable structure ID, footprint/height/archetype/material-family/visibility-class/revision; it contains no Unity rendering objects. Its focused regression exists. It remains unchecked until final exact-SHA validation.
- T005 root cause/discriminator is established. `StructuresComposition.PlanCastle(...)` returns the game-owned semantic `CastlePlan` before `BeginCastleBuild(...)` performs physical authoring, and `ShowcaseWorld.QueueLandmarks()` calls `PlanCastle(...)` before it populates `_castleRegions`. By contrast, legacy `FarFieldStructureStore.CaptureRegion(...)` derives coarse surfaces from already-resident voxel storage and therefore cannot satisfy never-visited semantic visibility. The correct fix is plan -> descriptor -> visibility manifest at that pre-residency boundary. Editing the actual call site is currently blocked because it resides in the ~115 KB `ShowcaseWorld.cs` and only whole-file connector replacement is available; acceptance is unchanged.
- T006 has been rewritten to the tracker contract. `IWorldVisibilitySource`/`WorldVisibilityManifest` use deterministic integer-sector indexing, bounds queries with cross-sector deduplication and deterministic output order, and stable replacement/removal keyed by record ID. Focused regressions cover these behaviors and no residency/generation ownership is introduced. It remains unchecked until final exact-SHA validation.
- `VoxelShowcase` already exposes the legacy far-field handoff by assigning `_world.FarField` to `VoxelFarTerrain.Structures`; this is useful only as terrain/anonymous-voxel fallback. Semantic T005/T007 must not promote that voxel-derived store into the new source of truth.
- The full tracker contains T001–T033. Required late gates include the complete behavioral suite (T029), built-player perceptual fixtures/evidence (T030), authoritative device-matrix budget validation (T031), cleanup only after parity (T032), and architecture documentation after measured evidence (T033). Do not close early.

## Current validation / execution state

- Sole targeted-CI transport remains `ci-test/fixes/agent-7`; no custom request has been written to the feature branch.
- Run `33389906556` is queued from a 60-second replay request, but its request commit was accidentally based on the previous CI request commit instead of directly on the feature SHA. Per queue rules it must not be replaced while queued/running, and even a green result cannot count as exact-feature-SHA evidence. After it completes, reset only `ci-test/fixes/agent-7` directly to the then-current feature SHA and create the corrected request as its direct child.
- The feature branch advances whenever this plan is updated, so all final gates must be rerun against the final exact feature SHA; earlier green source evidence is supporting provenance only.
- The execution container has no authenticated Git checkout and no outbound DNS. Repository reads/writes are through the connected GitHub API. Small/medium files can be edited safely as whole-file replacements; large-file changes are blocked unless a safe complete reconstruction can be established.
