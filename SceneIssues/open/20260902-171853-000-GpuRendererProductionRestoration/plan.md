# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced draw distance, or permanent CPU renderer fallback.

## Exact state

GPU launch source `9684ff509d65...`, request `fb4a7a92...`, run `34007154618`, artifact `9982119472` completed success. Rendering-owned minimal and multi-chunk GPU players are visually correct with zero fallback/unsupported/context/count/write failures. Full VoxelShowcase genuinely uses GPU extraction (`~731 requests / 730 publications`) and converges to `missingVisible=0`, but its final image remains severely fragmented with large missing terrain/castle masses. Late 1600x900 diagnostic throughput is ~194–199 FPS (~5.3–5.5 ms p95); this predates trusted frame-timing samples and is not the locked 1080p benchmark. See [gpu-resumption-evidence.md](gpu-resumption-evidence.md).

CPU-only removal inventory is [cpu-render-backend-removal-ledger.md](cpu-render-backend-removal-ledger.md). `CpuTransvoxelChunkCache` is mixed legacy CPU mesher + current GPU host orchestration and must be split before deletion; steps 4/8 and water still require GPU migration.

## Current discriminator and selected repair

External-review P0 completion/publication findings match current code. Immutable fail-before source `51a9f344...`, request `c1f72490...`, run `34010085590` is queued; preserve it until terminal. Its real page-arena test requires allocation/write finalization to leave geometry pending until explicit publication approval.

Descendant feature head `902689f171c1a33d5c45d06c8bd1bfe1918fc163` now:
- retains submitted batch lanes/resources until a tiny async status/identity readback finishes;
- distinguishes Ready/Exhausted/Stale/TooLarge/Unsupported and never reads generated geometry;
- leaves successful writes pending and provides generation-checked Commit/Abort kernels;
- prevents unsupported semantics from allocating a false empty candidate;
- uses an end-of-frame generation gate as an **intermediate** publication bridge so status-aware GPU output remains renderable;
- adds real compute transaction tests plus a 600-visible-handle draw-compaction regression.

The frame-boundary bridge is not the final identity contract: renderer slot/build/config/world identity still must move out of the mixed legacy cache and approve Commit before G10 closes. Fixed-frame page retirement/multi-buffer lifetime and mixed-LOD compatibility also remain open.

## Next gate

After fail-before terminates, exact-SHA validate the descendant with Rendering module tests/players plus a full VoxelShowcase replay and frame timing. Inspect status outcomes and images. If geometry remains missing with all candidates genuinely Ready/live, move the discriminator upstream/downstream to full-scale prepared geometry or draw consumption rather than restarting already-proven basic shader math. Final CPU-backend-free validation, locked workloads, closure, current-master merge, PR + auto-merge remain mandatory.
