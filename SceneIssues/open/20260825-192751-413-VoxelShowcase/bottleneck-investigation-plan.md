# Bottleneck Investigation Plan

## Goal

Identify the dominant cause of low frame rate and traversal stalls in `VoxelShowcase` with measurements that distinguish CPU simulation, voxel discovery/streaming, meshing, render submission, and GPU cost. Do not optimize from aggregate FPS alone.

The working fix for initial clipmap discovery remains independently verified. This plan is for finding the next real performance bottleneck and should be executed without weakening existing coverage/performance assertions.

## Measurement protocol

For every experiment:

1. Use the same camera pose and a deterministic traversal path where applicable.
2. Warm until the intended state is reached, then record a fixed sample window.
3. Record at minimum: frame time p50/p95/p99, main-thread time, render-thread time if available, visible chunk/draw counts, resident regions, meshing jobs queued/completed, upload counts/bytes, and any GPU timing available from Unity/ProfilerRecorder.
4. Run the baseline immediately before or after the variant on the same source SHA and hardware.
5. Change one variable at a time. Experimental switches must default off and must not become production behavior without separate evidence.
6. Save the raw test/log artifact and write the observed delta in an `experiment-*.md` file. A result with no measurable delta is still useful evidence.

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

## Decision rules

- **Stable scene still slow + voxel draws off becomes fast:** focus on draw submission/GPU path.
- **Stable scene becomes fast when meshing/streaming freezes:** eliminate unnecessary steady-state background work/churn.
- **Stationary fast, traversal slow:** focus on discovery/meshing/upload pipeline and burst control.
- **Resolution reduction helps strongly:** investigate GPU shader/fill/bandwidth before CPU scheduler changes.
- **Resolution reduction barely helps, but draw-count reduction helps:** investigate CPU render submission/batching/indirect rendering.
- **Simplified voxel scenes are fast but full showcase is slow:** identify the first added scene/system category that creates the regression; do not rewrite the renderer first.
- **Single-region/simple scene is already expensive:** profile the core render path before content/streaming work.

## Evidence required before choosing the next optimization

Do not select the next production change until at least one experiment produces a repeatable, material delta (target >=15% frame-time change or a clearly dominant profiler/counter cost) and a second discriminator points to the same subsystem. Record competing hypotheses and falsifying results as well as the winner.

The eventual fix must then pass the existing exact-pose surface regression, continuous traversal coverage/performance regression, and final saved-pose replay without increasing holes, relaxing thresholds, or hiding work outside the measured window.
