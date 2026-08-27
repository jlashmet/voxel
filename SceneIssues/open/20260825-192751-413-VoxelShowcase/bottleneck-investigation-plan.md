# Bottleneck Investigation Plan

## Goal

Identify the dominant cause of low frame rate and traversal stalls in `VoxelShowcase` with measurements that distinguish CPU simulation, voxel discovery/streaming, meshing, render submission, and GPU cost. Do not optimize from aggregate FPS alone.

The working fix for initial clipmap discovery remains independently verified. This plan is for finding the next real performance bottleneck and should be executed without weakening existing coverage/performance assertions.

The current evidence changes the expected direction of the work: build-concurrency tuning is useful for reducing hitches, but it is not expected to close the order-of-magnitude gap to the rendering target. The primary rendering direction is now a GPU-resident meshing pipeline fed by changes from the CPU-authoritative voxel world. CPU ownership of collision, game logic, destruction, save/replication state, and voxel queries remains unchanged.

## Measurement protocol

For every experiment:

1. Use the same camera pose and a deterministic traversal path where applicable.
2. Warm until the intended state is reached, then record a fixed sample window.
3. Record at minimum: frame time p50/p95/p99, main-thread time, render-thread time if available, visible chunk/draw counts, resident regions, meshing jobs queued/completed, upload counts/bytes, and any GPU timing available from Unity/ProfilerRecorder.
4. Run the baseline immediately before or after the variant on the same source SHA and hardware.
5. Change one variable at a time. Experimental switches must default off and must not become production behavior without separate evidence.
6. Save the raw test/log artifact and write the observed delta in an `experiment-*.md` file. A result with no measurable delta is still useful evidence.

## Evidence already established

The traversal profiling work has now produced two independent signals in the same subsystem:

- Per-frame worker profiling identified `Voxel.Surface.Snapshot` as the dominant synchronous worker overrun during traversal. Snapshot p95 was about 4.50 ms, and the worst observed worker frame was about 70.86 ms with about 70.83 ms inside snapshot work.
- Reducing the test-only converging-build ceiling from 12 to 8 on an 8-job-worker CI run reduced snapshot p95 to about 3.77 ms, worker p95 by roughly 27%, and the worst snapshot spike to about 14.31 ms. The unchanged traversal gate still missed its p99 target, so concurrency is a real amplifier but not a sufficient architectural fix.

This means scheduler/job-count tuning should remain available as a bounded-hitch fix, but it should not become the main strategy for reaching the rendering target.

## Phase 1 — Cost attribution by subsystem subtraction

Run the normal `VoxelShowcase` after convergence, then repeat with one subsystem removed or frozen. Prefer test-only/debug injection points rather than editing the scene asset.

### A. Freeze voxel discovery/streaming after convergence

Stop new resident-region discovery, clipmap movement work, and streaming mutations after the initial view is fully populated while leaving rendering intact.

**Interpretation:** a large frame-time improvement means continuous discovery/streaming bookkeeping is expensive even when the image is stable. Little change pushes the investigation downstream.

### B. Freeze meshing after convergence

Allow existing meshes to render, but prevent new surface-extraction/meshing work and mesh uploads during the measured window.

**Interpretation:** a large improvement implicates meshing, job completion handling, allocations, or upload churn. Compare queued/completed counts and bytes, not only FPS.

### C. Render already-built voxel meshes with world/gameplay updates minimized

Keep the voxel renderers and camera active while disabling nonessential scene systems that are not required to draw the captured view (AI, procedural updates, decorators, effects, etc.), one category at a time.

**Interpretation:** this separates voxel-rendering cost from unrelated showcase/game systems. If disabling a category has a large effect, drill into that category before touching voxel algorithms.

### D. Voxel rendering disabled, scene systems left running

Suppress voxel draw submission while leaving the scene/game update path active.

**Interpretation:** if frame time remains high, the primary bottleneck is not GPU voxel drawing. If it collapses, continue with render-path/GPU isolation below.

### E. Fallback/HLOD path isolation

Measure near-ring only, fallback/HLOD only, and normal combined rendering where test hooks can do this safely.

**Interpretation:** identifies whether the fallback path, overlap, or duplicate coverage is disproportionately expensive.

## Phase 2 — Simplified scene ladder

Build test-only scene fixtures/harnesses using the same production voxel renderer/scheduler code so complexity can be added in controlled steps:

1. **Empty camera scene:** camera + renderer infrastructure, no voxel surfaces. Establish framework floor.
2. **Single static voxel region:** one already-resident/simple region, no streaming. Measures basic draw/submission cost.
3. **Flat deterministic terrain:** enough chunks to fill the captured frustum, prebuilt/resident if possible. Measures scaling with chunk/draw count without castle/content complexity.
4. **Castle/content only:** deterministic dense geometry without traversal/streaming. Measures geometry-density and material/surface complexity.
5. **Streaming flat terrain:** same simple terrain but move the camera through it. Adds discovery, meshing, and upload churn without complex content.
6. **Full `VoxelShowcase`:** compare against the simplified ladder using the same counters.

At each step sweep one dimension where cheap to automate: visible chunk count, LOD/ring count, or view distance. Plot frame time versus draw count and versus meshing/upload work. A sharp slope change is more informative than a single FPS number.

## Phase 3 — CPU versus GPU discriminator

When rendering is implicated:

- Hold scene state and camera fixed, then reduce render resolution substantially. A large frame-time reduction points toward GPU shading/fill/bandwidth; little change points toward CPU/render submission or geometry processing.
- With geometry fixed, compare normal material/shader against the simplest existing production-compatible material path available in tests. Do not introduce a fake renderer whose behavior cannot map back to production.
- Compare visible mesh/draw count to CPU render-thread time. If cost scales strongly with draws but not resolution, prioritize batching/indirect draw/submission work.
- Capture GPU timing/counters if available; otherwise treat resolution sensitivity only as a discriminator, not proof.

## Phase 4 — Meshing/streaming discriminator

When traversal or background work is implicated:

- Run stationary camera after convergence versus deterministic movement at the same average visible geometry.
- Log per-frame discovery candidates, surface extraction jobs, completed meshes, uploads, allocation/GC events, and queue depth.
- Temporarily cap one work source at a time in test-only experiments (discovery, meshing completions, uploads) and observe whether p95/p99 improves or only delays missing geometry.
- Distinguish work creation from work consumption: high queue creation with low processing means scheduling pressure; high processing/upload volume means actual compute/bandwidth cost.

Any cap that improves frame time by allowing visible holes is not a valid fix; it only identifies the expensive stage.

## Phase 5 — GPU-resident rendering direction

### Architectural boundary

Keep the CPU voxel world authoritative. Rendering must not become the owner of gameplay state.

CPU remains responsible for:

- authoritative voxel occupancy/material/surface state;
- destruction and edits;
- collision and gameplay queries;
- AI/pathing inputs that consume voxel state;
- save/load, networking, replication and versioning.

GPU rendering owns only derived presentation state:

- a persistent mirror/cache of voxel brick data needed for rendering;
- surface classification needed only for rendering;
- density sampling and Transvoxel/transition evaluation;
- geometry sizing/allocation;
- vertex/index emission;
- indirect draw arguments and GPU-resident render geometry.

The desired data flow is:

```text
CPU authoritative voxel world
    |
    +-- collision / game logic / destruction / persistence
    |
    `-- changed brick/version records
             |
             v
       shared persistent GPU voxel mirror
             |
             v
       GPU lookup/classification
             |
             v
       GPU density + Transvoxel/transition work
             |
             v
       GPU allocation/prefix/geometry emission
             |
             v
       GPU indirect draw arguments
```

The GPU mirror is a rendering cache, never the source of truth. CPU collision and gameplay must not depend on a GPU readback.

### Why the existing GPU path is not enough

Preserve the useful existing GPU work, but do not mistake the current cutover for the target architecture. The current path still depends on the CPU exact-snapshot pipeline before compute can begin:

1. CPU pins region metadata.
2. CPU schedules/compacts exact brick metadata.
3. CPU pins mixed payloads and validates coherency.
4. CPU runs render-eligibility/classification.
5. CPU builds a dense per-chunk brick-cache description.
6. Only then does the GPU sample/count/write geometry.
7. CPU still observes per-chunk count/completion data to reserve/publish geometry.

That boundary leaves the measured snapshot/preparation cost on the CPU. The current GPU extractor should therefore be treated as a useful GPU polygonizer/backend, not yet as an end-to-end GPU-resident surface pipeline.

### Components worth retaining

Prefer evolving, not discarding, the existing GPU implementation:

- GPU Transvoxel regular-cell kernels;
- GPU transition-face kernels;
- `GpuVoxelBrickMirror` data layouts and version concepts where they remain useful;
- GPU-resident vertex/index output;
- shared surface geometry arena and indirect drawing;
- asynchronous completion instead of blocking GPU flushes;
- atomic replacement that keeps old geometry visible until new geometry is complete;
- correctness fallback while GPU-v2 coverage is incomplete.

### GPU-v2 prototype

Before a broad production rewrite, build a narrow prototype alongside the current path for plain continuous near-field terrain.

The prototype should:

1. Keep authoritative voxels on CPU.
2. Push changed mixed-brick payloads/version metadata into one persistent rendering mirror, shared across surface workers rather than reconstructed independently for each chunk where practical.
3. Submit chunk coordinate + LOD/transition information as the primary per-build request.
4. Avoid CPU construction of a complete per-chunk immutable brick neighbourhood where the GPU can resolve the mirrored bricks itself.
5. Perform render-only classification on GPU when possible.
6. Count many requested chunks in batches, then use a GPU-side prefix/allocation step or equivalent so CPU does not sit between count and write for every chunk.
7. Emit vertices, indices, transition geometry and indirect args directly into GPU-resident arenas.
8. Do not read generated geometry back to CPU.
9. Target zero blocking GPU waits and ideally zero per-chunk counter readbacks on the player-frame path.

Do not require GPU-v2 to support every authored surface feature in the first prototype. Plain smooth/rounded terrain is sufficient to validate the architectural hypothesis, but unsupported chunks must continue to render correctly through the existing path.

### Collision/gameplay acceptance

GPU-v2 is acceptable only if it preserves CPU authority and gameplay consistency:

- voxel collision/query results must come from CPU-authoritative state, not render triangles;
- a voxel edit must become visible to gameplay immediately according to current CPU semantics;
- render lag may be asynchronous but must be versioned so stale GPU geometry cannot overwrite a newer voxel generation;
- if any gameplay feature currently depends on generated `MeshCollider` data, treat collision meshing as a separate CPU pipeline rather than reading GPU render geometry back;
- explicitly measure/render-test edit-to-visible latency so asynchronous GPU rendering does not create long-lived visual/collision disagreement.

### GPU-v2 success metrics

Do not judge the prototype only by aggregate FPS. Record:

- CPU microseconds per newly requested/render-dirty chunk;
- main-thread p50/p95/p99 during deterministic traversal;
- chunks meshed per second;
- GPU compute time per chunk and per batch;
- changed voxel/brick bytes uploaded CPU -> GPU;
- GPU readbacks per frame and per chunk;
- main-thread synchronization/wait time;
- visible holes/missing coverage and edit-to-visible latency;
- stationary render cost separately from traversal/meshing cost.

The key architectural success criterion is that CPU cost of producing a newly visible render chunk collapses. If CPU snapshot/preparation remains a multi-millisecond frame cost, the experiment has not moved the meshing boundary far enough onto the GPU even if its compute shader itself is fast.

## Decision rules

- **Stable scene still slow + voxel draws off becomes fast:** focus on draw submission/GPU path.
- **Stable scene becomes fast when meshing/streaming freezes:** eliminate unnecessary steady-state background work/churn.
- **Stationary fast, traversal slow:** focus on discovery/meshing pipeline and GPU-v2 rather than attempting to reach the target through job-count tuning alone.
- **Reducing build concurrency materially reduces snapshot spikes but does not make the unchanged traversal gate green:** retain concurrency as a bounded-hitch lever, but do not treat it as the target architecture.
- **GPU-v2 plain-terrain prototype collapses CPU time per new chunk and materially improves traversal while preserving coverage:** expand GPU-resident meshing coverage incrementally.
- **GPU-v2 still spends substantial CPU time constructing per-chunk snapshots/cache descriptions:** move the brick lookup/classification/allocation boundary further onto the GPU before optimizing compute kernels.
- **Resolution reduction helps strongly:** investigate GPU shader/fill/bandwidth before CPU scheduler changes.
- **Resolution reduction barely helps, but draw-count reduction helps:** investigate CPU render submission/batching/indirect rendering.
- **Simplified voxel scenes are fast but full showcase is slow:** identify the first added scene/system category that creates the regression; do not rewrite unrelated renderer pieces first.
- **Single-region/simple scene is already expensive:** profile the core render path before content/streaming work.

## Evidence required before choosing the next optimization

Do not select the next production change until at least one experiment produces a repeatable, material delta (target >=15% frame-time change or a clearly dominant profiler/counter cost) and a second discriminator points to the same subsystem. Record competing hypotheses and falsifying results as well as the winner.

The current snapshot profiling plus build-concurrency A/B satisfies that evidence bar for the conclusion that CPU snapshot/build preparation is a real traversal bottleneck and concurrency pressure amplifies it. It does **not** establish that simply lowering concurrency can reach the target. The next architectural experiment should therefore test whether eliminating the CPU snapshot boundary for a narrow GPU-v2 terrain path produces a substantially larger reduction in CPU cost.

The eventual fix must then pass the existing exact-pose surface regression, continuous traversal coverage/performance regression, and final saved-pose replay without increasing holes, relaxing thresholds, or hiding work outside the measured window.
