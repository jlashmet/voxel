# Experiment 005 — Optional nonresident halo admission

## Question
Does the persistent GPU mirror wait forever on blocks that the authoritative CPU exact snapshot intentionally treats as optional empty halo because their Storage region is not resident?

## Competing hypotheses and evidence
- **Frame/CPU/GPU saturation:** rejected. The exact built player remains roughly `205–244 FPS` after the failure plateaus, with prepare/admission in low single-digit milliseconds.
- **Geometry arena exhaustion:** rejected. The failed replay reports `leaseFail=0` and only a small fraction of vertex/index/draw capacity in use.
- **Shared mirror capacity:** rejected by budget/source scale. The shared mirror is budgeted at least 96 MiB and is not near an eight-completion-sized capacity ceiling.
- **Obsolete Storage generation held by pending stages:** rejected by the earlier exact-SHA experiment; refreshing to live Storage generation did not break the plateau.
- **Recovery admission starvation:** confirmed as a contributor because the fairness change increased completions from three to eight, but rejected as the sole cause because exact coverage still collapsed with 12 workers pending.
- **Optional-halo mismatch:** supported by source semantics. CPU exact snapshots require their core regions but clear/skip an unavailable optional halo region as empty. GPU `Covers` previously required every block in the padded brick cache to become mirror-ready. A block in a legitimately nonresident halo-only region therefore cannot be recovered yet prevents GPU admission forever. The GPU persistent directory already represents canonical empty as absence of a lookup entry.

## Runtime discriminator
The existing 210 m production-path regression remains the end-to-end gate. The focused `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork` is strengthened so it:
- renders the exact `VoxelShowcase` into its own 320x180 target, removing the prior render-pass target mismatch;
- traverses 96 m and observes recovery/active-extraction progress;
- requires at least four additional GPU completions; and
- requires `GpuSurfaceMirrorCoordinator.OptionalNonResidentHaloBlocksAccepted > 0`, proving the exact optional-halo admission branch was exercised rather than passing on unrelated covered work.

## Change
`GpuSurfaceMirrorCoordinator.Covers` now receives the chunk's required/core voxel bounds in addition to its padded brick-cache footprint. For a block whose region is not resident:
- if the block intersects the required core, admission remains blocked exactly as before;
- if the block lies only in the optional halo, it is accepted as canonical empty-by-absence and is not queued for impossible recovery.

Resident blocks keep the existing per-region change-generation validation and exact-block recovery. `GpuSurfaceExtractionContext` derives the core extent as `CellsPerAxis * SourceStep` from the same production request used by the GPU extractor. A diagnostic counter records optional nonresident halo acceptance for the regression only.

Production commits:
- `a68cec77566125902619e26a53aa4e8fc32ac056` — `fix: treat absent optional GPU halo as empty`
- `0349b6caeeb5ea396707e3b35e27a7231157ed3a` — `fix: align GPU coverage with exact snapshot core`
- `44d3781ffe564f9d931144babbdc6becca5cf3f0` — focused regression render-target isolation and optional-halo assertion

## Blast radius / cost
Scope is limited to solid step-1/step-2 GPU admission. Core correctness is not weakened: any nonresident block intersecting the chunk core still prevents admission. There are no shader changes, Storage writes, world-generation/content changes, collision changes, water changes, CPU fallback additions, buffer-size changes, new allocations, broader recovery scans, or changed recovery/journal budgets.

The added hot-path work occurs only for an uncovered block in a nonresident region: two integer voxel-bound calculations, one AABB intersection against the chunk core, and a diagnostic counter increment for halo-only acceptance. Existing traversal p95/p99 and stationary p95 gates remain the cost guard.

## Acceptance
Accept this hypothesis only when one exact feature SHA satisfies all of the following on the assigned GPU runner:
1. the focused liveness regression passes and proves `OptionalNonResidentHaloBlocksAccepted > 0` plus sustained GPU completion;
2. `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` completes the full 210 m traversal with zero eligible CPU fallbacks, no visibility loss, final missing coverage of zero, and all frame-time gates;
3. the 45 s built-player replay of exact `VoxelShowcase` visibly restores the near/mid voxel world instead of freezing into the prior holes/disconnected fragments; and
4. exact replay telemetry converges rather than remaining at a persistent missing-chunk plateau.

Until all four hold, this experiment and the SceneIssue remain open.
