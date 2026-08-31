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

- T001 implementation complete pending CI: `FarTerrainCoverageMath` now computes spacing, half extent, snap loss, guaranteed coverage, and minimum guarded ring count; `VoxelFarTerrain` uses that result and emits an explicit one-time diagnostic when `MaxRings` cannot cover the requested range. The shipped 409.6 m -> 12 km case requires six rings by worst-case snap math.
- T002 implementation complete pending CI: startup fallback retirement now requires a gap-free current authoritative ring prefix that covers the configured radius. The fallback mesh slot is protected from authoritative rebuild/presentation refresh until that invariant is satisfied.
- Focused EditMode regression coverage exists in `VoxelFarTerrainCoverageTests.cs` for 12 km cardinal snap phases, the `MaxRings` guard, renderer ring-count integration, and no-shrink fallback retirement.

## Active tooling blocker

- The assigned environment has no local repository worktree and cannot resolve GitHub over the container network. Repository access is through the connected GitHub API only.
- That connector can safely create small new files and can replace known blobs, but editing an existing file is whole-file replacement rather than a line patch.
- T003's remaining integration belongs in the existing `VoxelShowcase.DescribeFarTerrain()` structured measurement surface. `VoxelShowcase.cs` is substantially larger than the far-terrain renderer; a whole-file reconstruction would create unjustified blast-radius risk after a trial renderer reconstruction demonstrated formatting churn. The trial was fully restored, and the current feature diff contains no accidental `noop` file or broad renderer formatting changes.
- T003 acceptance is unchanged. Independent T004+ semantic-data work can proceed while a patch-capable worktree/editor is unavailable; T003 must be completed before closure.
