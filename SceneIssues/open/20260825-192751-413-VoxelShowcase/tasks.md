# GPU migration tasks — SceneIssue 20260825-192751-413

This file is the persistent execution checklist for the performance fix. Add new findings here as actionable tasks when they are discovered; mark them done or rejected with evidence. CPU-only tuning is not a valid final direction.

## Production migration

- [ ] **Restore production GPU cutover for supported near rings.** The compute mesher, GPU brick mirror, transition path, shared geometry arena, and async count/write stages already exist, but `CpuTransvoxelChunkCache.GpuCutoverDisabled = true` currently prevents production dispatch. Restore cutover only with explicit fallback for unsupported/device-unavailable cases.
- [ ] **Move GPU eligibility/classification off the CPU for GPU-candidate chunks.** `ExactSnapshotClassificationJob` currently scans the exact snapshot before GPU routing. Fold supported/unsupported classification into the existing GPU sample/count pass and return only bounded flags with the already-permitted counter readback; do not make CPU classification a prerequisite for GPU meshing.
- [ ] **Broaden GPU eligibility to reconstruction modes the shader already implements.** `VoxelBrickMesher.compute` already contains planar/sharp handling, while the CPU classifier currently marks `!continuous` (including planar/sharp) GPU-unsupported. Validate CPU/GPU parity and route those represented cases to GPU. Keep CPU fallback for semantics the shader does not yet reproduce exactly (for example cubic/faceted-only, decoration/profile behavior, or boundary ownership cases until implemented and proven).
- [ ] **Remove the per-chunk CPU brick-cache staging walk from the GPU critical path.** `GpuSurfaceExtractionContext.TryPin` currently loops the dense brick neighbourhood on CPU, publishes/pins mixed bricks, fills `_brickCacheStaging`, then uploads it. Rework this boundary so Storage changes publish compact brick deltas/version data once and GPU extraction consumes resident mirror/indirection data without rebuilding the full cache on CPU per chunk.
- [ ] **Keep generated geometry GPU-resident through draw.** Preserve count/reserve/write and indirect/shared-arena drawing; do not add geometry readback or CPU mesh reconstruction as part of the migration.
- [ ] **Move additional per-frame visibility/submission work GPU-side if profiling shows it remains material after meshing cutover.** Prefer GPU visibility/indirect command generation over thousands of CPU per-chunk probes; measure after the meshing/classification migration rather than guessing first.

## Correctness / regressions

- [ ] **Add a behavioral regression proving the GPU path is actually used.** During VoxelShowcase traversal, assert eligible near-ring chunks are staged/written by `GpuSurfaceExtractionContext` and are not silently falling back to CPU meshing.
- [ ] **Preserve CPU fallback correctness.** Unsupported styles/features, missing compute support, mirror-slot exhaustion, count/write disagreement, and allocation refusal must leave the prior valid representation standing rather than opening a hole.
- [ ] **Keep CPU/GPU parity coverage for density, material, normals, ownership, planar/sharp reconstruction, and transition seams.** Extend the existing oracle suite for every newly migrated semantic before enabling it in production.
- [ ] **Keep the existing moving traversal regression unchanged.** It must maintain visible solids every frame, zero blocking completions, streaming progress, near/far gap <= 5 cm, p95 < 18 ms, and p99 < 25 ms.

## Performance / acceptance

- [ ] **Instrument CPU→GPU work removal.** Record per-frame/chunk counts for CPU classification, CPU meshing, GPU staged/written/fallback chunks, mirror publication, and visibility so each experiment can state what CPU work actually disappeared.
- [ ] **Benchmark while moving, not only stationary.** Capture traversal frame percentiles and saved-pose replay telemetry; stationary ~400–450 FPS is not sufficient evidence.
- [ ] **Pursue the ~1000 FPS goal through GPU migration.** At ~452 FPS the demonstrated steady-state frame is ~2.2 ms; ~1000 FPS requires ~1.0 ms. Treat remaining CPU voxel rendering/meshing work as migration candidates rather than repeatedly tuning CPU thresholds.
- [ ] **Do not issue final targeted CI for a CPU-only candidate.** Final CI is allowed only after the production diff materially moves/removes CPU voxel rendering/meshing work and the behavioral regression proves that GPU path is exercised.

## Rejected / historical

- [x] **Reject static visible-convergence 12→8 as the fix.** Exact traversal lost all visible voxel draw on frame 5; later replay recovery does not satisfy no-hole movement acceptance.
- [x] **Reject moving visibility reuse as the final fix.** Exact run remained red (23.40 ms p95) and did not satisfy the behavioral gate.
- [x] **Reject CPU snapshot scheduling as the final direction.** Snapshot scheduling experiments are diagnostic/supporting work only; they do not satisfy the required CPU→GPU migration direction.
