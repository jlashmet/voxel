# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Direction constraint — non-negotiable
This is a GPU-migration/performance issue. The primary objective is to move voxel rendering/meshing workload from CPU to GPU and pursue ~1000 FPS while preserving moving-player streaming correctness.

- CPU profiling is diagnostic only: use it to identify work that should move to the GPU.
- Do not pursue CPU-only renderer optimizations as the final solution unless the user explicitly approves a direction change.
- Do not spend final targeted CI on another CPU-only baseline/candidate; the CPU path has already been benchmarked.
- Every implementation experiment must answer: **what CPU work does this remove or move to the GPU?** If the answer is "none", do not pursue it as the solution.
- Start from the existing GPU brick mirror / compute mesher / shared GPU arena implementation; extend it rather than rebuilding those pieces or falling back to CPU extraction.
- Before final CI, the source diff must make it possible to point to the specific workload moved CPU → GPU. If it cannot, the branch is not ready for final verification.

## Defect / acceptance
The only capture is `screenshot-001.png`; its only marked circle covers the top-left FPS/surface telemetry at the saved `Showcase Camera` pose. Replay ties the complaint to moving-player rendering/streaming cost and visible chunk convergence.

Acceptance is the unchanged 420-frame production traversal plus 45 s saved-pose replay: p95 < 18 ms, p99 < 25 ms, max < 80 ms, zero frame-path blocking completions, streamed movement proven, visible solids every moving frame, and near/far gap <= 5 cm. Performance direction is ~1000 FPS (~1 ms/frame) once converged; stationary FPS alone is insufficient if moving traversal opens holes or stalls.

## Existing evidence / rejected directions
**CPU snapshot/admission/visibility tuning — diagnostic, not the solution.** Profiling found real CPU costs and the bounded step-1/2 metadata helper reduced observed snapshot p95 to ~1.76 ms, but repeated CPU-only admission/visibility experiments either failed the frame percentile gate or opened visible holes. These results identify CPU work to eliminate; they do not justify another CPU-only final candidate.

**Static/ramped convergence caps — rejected.** Completion-count and monotonic ramps either lost all visible geometry by frame 5 or still failed the percentile gate. Experiment 020's static 12→8 cap also lost every visible voxel draw on frame 5 in exact run `33123159073`; production concurrency is restored to 12.

**Moving visibility reuse / same-frame reuse — rejected.** Bounded moving-visibility reuse regressed to p95 23.40 ms. The provisional same-frame guard did not match the coroutine's measured player-frame cadence. Do not return to visibility caching unless new GPU architecture requires it.

**Existing GPU path — starting point, not architectural follow-up.** The repository already contains GPU brick mirroring, compute density/Transvoxel extraction, shared GPU geometry arenas, indirect drawing, and historical CPU/GPU parity tests. Production currently hard-disables cutover. The task is to finish/extend that GPU path so materially more of the current CPU rendering pipeline disappears.

## GPU migration checklist
Track the remaining CPU-side responsibilities explicitly and move them where practical without changing authoritative gameplay/collision semantics:

- [ ] Authoritative Storage/version changes produce compact GPU update work rather than per-chunk CPU reconstruction work.
- [ ] GPU brick mirror is the primary voxel input for near-ring rendering.
- [ ] Snapshot/classification work required only for rendering is moved to GPU or eliminated from the per-frame CPU hot path.
- [ ] Density reconstruction stays on GPU.
- [ ] Topology/count/prefix/allocation/geometry emission stays on GPU.
- [ ] Transition/LOD seam generation needed by the near rings is GPU-backed or otherwise avoids CPU meshing fallback.
- [ ] Geometry remains resident in shared GPU arenas and is published/drawn indirectly without CPU vertex/index generation or readback.
- [ ] Visibility/draw submission is reduced toward GPU-driven culling/indirect submission where it materially removes the measured CPU traversal cost.
- [ ] CPU remains responsible primarily for authoritative world changes, compact version/dirty notifications, and GPU orchestration—not walking thousands of chunks to prepare meshes.

## Regression / blast radius / cost
`VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` moves ~0.5 m per rendered frame for 420 frames across streamed regions and asserts visible solids, <=5 cm fallback coverage while near geometry is incomplete, zero blocking completions, streaming progress, p95 <18 ms, p99 <25 ms, max <80 ms, then a low-cost stationary tail.

GPU work must not change Storage format, gameplay/collision authority, voxel meaning, world generation, material/topology semantics, LOD visual ownership, or acceptance thresholds. CPU/GPU parity and replay evidence must police geometry correctness. Avoid CPU readbacks on the frame path; keep transfers bounded to changed voxel/version data and GPU-resident geometry.

## Final verification gate
Do not call a candidate "final" merely because the regression is green. Before the final targeted-CI request, all of the following must be true:

- [x] Inspect the sole marked region and tie it to saved-pose/runtime telemetry.
- [x] Retain the moving traversal behavioral regression and unchanged performance/coverage gates.
- [x] Use CPU profiling to identify the work to migrate; reject CPU-only tuning as the final direction.
- [ ] Implement a material CPU → GPU workload reduction using the existing GPU renderer as the base.
- [ ] Document exactly which CPU stages were eliminated/moved and the remaining CPU responsibilities.
- [ ] Benchmark the GPU implementation during movement and at the saved pose against the ~1000 FPS direction.
- [ ] Green exact-SHA targeted CI plus 45 s saved-pose replay with no holes/regressions.
- [ ] Commit `verification-final.png`, complete pending metadata, close, merge latest master, and non-force advance master.
