# GPU-v2 Immediate Next Step

## Objective

Execute one narrow architectural experiment before further scheduler/job-count tuning: prove or reject that removing the current CPU exact-snapshot boundary from near-field render meshing produces a much larger reduction in traversal CPU cost than concurrency tuning.

CPU voxel storage remains authoritative for collision, game logic, destruction, voxel queries, save/load, and networking. This experiment changes only derived rendering work.

## First implementation slice

Build a test-only/experimental GPU-v2 path for **plain continuous source-step-1 terrain only**. Do not attempt castle/profile/decorated/faceted coverage yet.

The slice will reuse the existing GPU Transvoxel kernels and shared GPU geometry arena, but change the work that happens before them:

1. Introduce or adapt **one persistent GPU voxel-brick mirror shared by the experimental renderer path**, rather than constructing an independent per-worker rendering snapshot for each chunk.
2. Feed that mirror from CPU-authoritative brick/version changes. Upload changed mixed-brick payloads and the minimal metadata required to locate empty/uniform/mixed bricks. Do not move gameplay authority to the GPU.
3. Submit a compact render-build request consisting primarily of chunk coordinate, source step/LOD, generation/version, and transition-face information.
4. For the experimental step-1 path, **do not call the normal CPU exact-snapshot preparation chain** (`ScheduleExactMetadataSnapshot` -> mixed-brick compaction/pinning -> `ExactSnapshotClassificationJob`) merely to prepare GPU rendering input.
5. Let GPU code resolve the chunk's brick neighborhood from the persistent mirror and perform the render-only sampling/classification needed by the existing Transvoxel compute path.
6. Initially keep the existing geometry arena/publication model if necessary to minimize the experiment, but instrument every CPU-GPU synchronization and readback. The experiment is not considered the final architecture if CPU still performs a per-chunk blocking count/allocate/write round trip.
7. Unsupported or unavailable chunks must fall back to the existing production path. No visible hole is an acceptable performance optimization.

## Why this is the next step

Current profiling has already established that the player-frame problem is not primarily geometry upload or GC:

- `Voxel.Surface.Snapshot` is the dominant synchronous worker overrun during traversal.
- Snapshot p95 was about 4.50 ms; one observed worker frame spent about 70.83 ms of 70.86 ms in snapshot work.
- Reducing converging builds from 12 to 8 reduced snapshot/worker spikes materially, but the unchanged traversal performance gate still failed. Concurrency therefore amplifies the problem but does not remove the architectural CPU preparation cost.
- The current GPU cutover begins only **after** CPU region metadata pinning, exact metadata jobs, mixed-payload pinning, coherency validation, CPU render classification, and construction of a dense per-chunk brick-cache description. It accelerates polygonization, but it does not eliminate the measured preparation boundary.

This experiment directly tests whether that boundary is the reason the prior GPU implementation failed to produce the expected order-of-magnitude improvement.

## Required discriminator harness

Create a deterministic A/B benchmark using the same plain continuous terrain and traversal in both variants:

- **A — current GPU cutover:** existing CPU exact snapshot + current GPU sampling/count/write backend.
- **B — GPU-v2 prototype:** persistent shared mirror + direct GPU brick lookup, bypassing CPU exact-snapshot preparation for supported step-1 terrain.

Both variants must run on the same source SHA/hardware configuration and preserve the same visible coverage, view distance, LOD, material rules, and traversal path.

Record at minimum:

- CPU microseconds per render-dirty/newly requested chunk;
- player-loop/frame p50, p95, p99;
- `Voxel.Surface.Snapshot` and total surface-worker wall time;
- chunks requested, successfully meshed, and published per second;
- bytes uploaded CPU -> GPU for changed voxel/brick data;
- number and wall time of CPU -> GPU buffer update calls;
- GPU count/write/compute time where measurable;
- GPU readbacks per chunk/frame and any main-thread waits;
- visible/missing chunk counts and near/far coverage;
- edit/version rejection behavior so stale GPU work cannot publish over newer CPU voxel state.

## Success / stop criteria

Treat the architectural hypothesis as supported only if the GPU-v2 variant:

1. preserves visible coverage and version correctness;
2. removes or nearly eliminates `Voxel.Surface.Snapshot` cost for GPU-v2-supported chunks;
3. reduces CPU cost per newly meshed chunk by a **large** amount, not just another small concurrency-scale gain; target at least a 2x reduction for the prototype and preferably much more;
4. materially lowers traversal p95/p99 or increases sustainable meshed-chunk throughput without deferring work into visible holes;
5. introduces no geometry readback and no blocking GPU wait on the player-frame path.

If direct GPU brick lookup does **not** materially reduce CPU cost, stop before a broad rewrite and profile the prototype to identify whether the remaining cost is brick-mirror upload, draw submission, GPU compute, or another CPU subsystem.

If it succeeds, the next expansion is to remove per-chunk CPU count/allocation coordination by batching chunk counts and moving allocation/prefix/indirect-argument generation onto the GPU, then incrementally expand feature coverage beyond plain step-1 terrain.

## Explicit non-goals for this first slice

- Do not move collision or gameplay authority to the GPU.
- Do not read render geometry back for physics.
- Do not rewrite CPU storage or destruction logic.
- Do not support every castle/profile/coating/faceted case before measuring the architectural hypothesis.
- Do not tune job counts as a substitute for this experiment.
- Do not relax the existing traversal, coverage, or replay assertions.
