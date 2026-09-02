# Voxel Renderer Performance Ideas

Status: exploratory notes only. These are optimization candidates, not accepted implementation requirements. Profile first and preserve rendering correctness, edit behavior, LOD transitions, material semantics, and the CPU/GPU oracle contract.

## Current mental model

The current production-oriented GPU path separates world data from generated geometry:

```text
CPU authoritative Storage
        |
        | changed/demanded bricks
        v
shared persistent GPU voxel mirror
        |
        | GPU extraction request with CPU-selected SourceStep / LOD
        v
GPU surface page arena
        |
        | CPU-selected visible chunk handles today
        v
GPU draw compaction / indirect arguments
        |
        v
render
```

Important ownership boundary:

- CPU Storage remains authoritative for persistence, gameplay, collision, edits, and change tracking.
- The shared GPU voxel mirror is a derived, demand-filled copy of world voxel data.
- LOD policy is currently selected before extraction; the GPU receives `SourceStep` rather than independently choosing LOD from camera distance.
- The GPU surface arena stores derived geometry, not authoritative voxel state.

The mirror already performs useful coarse compression:

- Empty brick: no payload; absence is the canonical GPU representation.
- Uniform brick: compact directory/metadata representation without a mixed-voxel payload slot.
- Mixed brick: detailed material/surface/boundary payload.
- Already-resident unchanged bricks should not be re-uploaded for every chunk or LOD change.

This means the same detailed mixed-brick payload can support multiple extraction resolutions over time without maintaining separate per-LOD voxel copies.

## 1. Planar / greedy surface merging

### Opportunity

A long perfectly flat terrain strip can still produce many triangles because the current density extraction emits topology at the requested LOD grid resolution. Coarser `SourceStep` reduces tessellation, but does not globally collapse a large coplanar region into a few triangles.

For example, a large flat patch is conceptually emitted as many adjacent surface cells:

```text
+---+---+---+---+---+---+
| / | / | / | / | / | / |
+---+---+---+---+---+---+
```

A planar-aware representation could potentially reduce a compatible region to a rectangle:

```text
+-----------------------+
|                      /|
|                    /  |
|                  /    |
+-----------------------+
```

That is four vertices and two triangles for the patch rather than cell-by-cell tessellation.

### Candidate approach

For reconstruction modes whose semantics permit planar merging:

1. Identify surface cells that lie on a compatible plane.
2. Form horizontal runs of compatible cells.
3. Merge adjacent equal runs vertically into rectangles.
4. Emit one quad / two triangles for each maximal rectangle.

This is closely related to greedy meshing; RLE can be a convenient intermediate representation for the runs.

### Compatibility constraints

A merge must stop at any boundary that affects visible or edit semantics, including:

- material changes;
- surface-style/reconstruction changes;
- coating/detail changes that alter geometry or shading requirements;
- authored sharp edges or boundary samples;
- holes and destruction boundaries;
- LOD transition seams;
- topology that is not truly planar;
- any boundary needed for correct normals/tangents or semantic joins.

The optimization is much more promising for planar/cubic reconstruction than for arbitrary smooth Transvoxel terrain.

### Why this may matter more than input compression

Compressing a flat region's voxel bytes can reduce upload cost, but still leaves the renderer generating and drawing a large redundant mesh. Avoiding the redundant geometry attacks:

- extraction work;
- page-arena memory;
- index/vertex bandwidth;
- draw vertex processing;
- potentially rasterization overhead.

Profile triangle count and GPU time before and after any prototype.

## 2. Selective RLE / compressed brick transfer

### Where RLE is probably not appropriate

Do not make the persistent GPU mirror a simple RLE stream by default. The density shader performs many coordinate-addressed neighboring taps. The current packed array representation gives effectively O(1) address calculation; naive RLE would require locating the run containing each sample and would make random GPU access more expensive.

The existing empty/uniform/mixed brick model already captures much of the easy compression benefit at brick granularity.

### Where RLE may help

RLE or another lightweight codec could be tested for CPU -> GPU transfer of mixed-brick payloads:

```text
CPU authoritative mixed brick
        |
        v
compress / RLE
        |
        | smaller transfer
        v
GPU decompression compute
        |
        v
normal random-access shared mirror layout
```

This preserves the fast mesher representation while potentially reducing transfer bandwidth.

Only pursue this if profiling shows mixed-brick upload bandwidth or upload CPU cost is significant. Benefits may differ substantially between discrete GPUs and unified-memory systems.

Useful measurements:

- raw bytes of dirty mixed-brick payload per frame;
- encoded bytes per frame;
- compression CPU time;
- GPU decompression time;
- upload/driver time;
- worst-case incompressible bricks;
- latency from Storage edit to mirror-ready extraction.

## 3. GPU frustum and Hi-Z occlusion after semantic LOD selection

Keep semantic LOD/readiness decisions authoritative, but move final raster visibility suppression toward the GPU.

Candidate pipeline:

```text
CPU semantic LOD/readiness selection
        |
        v
candidate chunk handles / instances
        |
        v
GPU frustum culling
        |
        v
GPU Hi-Z occlusion
        |
        v
GPU visible-handle compaction
        |
        v
indirect draw generation
```

This preserves CPU correctness responsibilities such as LOD ownership, readiness, transition coverage, and hole prevention while avoiding CPU work for purely visual occlusion.

Recommended visibility-funnel metrics:

```text
resident candidates
 -> LOD candidates
 -> CPU semantic/readiness candidates
 -> GPU frustum survivors
 -> Hi-Z survivors
 -> chunks drawn
 -> vertices submitted
 -> pixels shaded
```

### Reversed-Z requirement

Unity already uses reversed-Z on important modern backends such as Metal and DirectX. Treat reversed-Z as a foundation to verify and preserve rather than a new optimization to invent.

Any custom depth or Hi-Z code must use the active platform convention (`SystemInfo.usesReversedZBuffer` / `UNITY_REVERSED_Z`) rather than assuming near=0 and far=1. For reversed-Z, near depth is approximately 1 and far depth approaches 0, so hierarchical reduction and occlusion comparisons must be chosen accordingly.

This should be explicitly tested because a wrong Hi-Z comparison can create false occlusion and missing terrain. Also investigate whether an infinite-far reversed-Z projection is useful for the project's extreme viewing distances, without weakening semantic culling or far-HLOD bounds.

## 4. Eliminate fixed empty indirect submissions

The current paged GPU render path prepares indirect draw buckets, but when any paged geometry is visible the render pass can issue the fixed bucket count of indirect submissions even when many buckets have zero work.

Do not solve this by reading bucket occupancy back to the CPU every frame; GPU -> CPU synchronization may cost more than the saved empty calls.

Candidates to investigate:

- GPU-compacted indirect command list;
- multi-draw indirect / indirect-count APIs supported by the project's Unity version and target graphics backends;
- fewer/coarser buckets if profiling shows bucket fragmentation without enough benefit;
- retaining fixed zero-count commands only if profiling proves their submission cost is negligible.

Measure:

- occupied buckets per frame;
- empty indirect submissions per frame;
- CPU render-thread time spent submitting them;
- GPU command-processing cost;
- effect of different bucket counts.

## 5. Remove CPU visible-handle upload from the final visibility stage

Today the draw dispatcher consumes CPU-visible handles. If GPU frustum/occlusion is implemented, a later step could keep candidate generation and final visible-handle compaction GPU-resident rather than uploading the final list each frame.

Do not move semantic world ownership to the GPU merely to achieve this. A good boundary is:

- CPU: which semantic chunks/LODs are eligible and must exist for correctness;
- GPU: which eligible representations actually need rasterization for this camera.

## 6. Arena and geometry efficiency

Additional candidates to profile after higher-value visibility/geometry reductions:

- improve page-arena eviction using actual recency/visibility pressure;
- reduce paged vertex/index indirection if address translation is measurable;
- tune page sizes based on real chunk geometry distributions;
- retain previous published geometry until replacement generation is complete to preserve no-hole behavior;
- avoid storing multiple equivalent LOD meshes when regeneration is cheaper than residency, but only after measuring extraction cost versus memory pressure.

Track geometry distributions rather than guessing:

- vertices/indices per chunk by LOD;
- pages allocated versus pages actually used;
- fragmentation/waste per page size;
- geometry residency lifetime;
- regeneration frequency after camera movement;
- peak and steady-state arena memory.

## 7. Shader / lighting cost

Once geometry and visibility are under control, profile shading separately. Potential later candidates:

- clustered/Forward+ local-light evaluation instead of broad per-pixel local-light loops;
- distance-based shader simplification where it cannot change required semantics;
- lower-cost far material paths;
- reduce unnecessary material-state changes and repeated bindings.

Do not trade geometry correctness for shader savings without captures and GPU timing evidence.

## 8. Water batching

Water remains a likely independent submission/batching target. Profile water draw count and CPU submission time, then investigate whether instances can share GPU-driven batching/indirect infrastructure without coupling water semantics to solid-surface extraction.

## 9. True meshlets: later, not first

The current fixed-page arena should not be mistaken for a complete meshlet renderer. True meshlets could eventually provide finer GPU culling and better command generation, but they add complexity in clustering, bounds/cones, transitions, edits, and regeneration.

Only consider this after measuring the simpler wins above. Large planar merging plus GPU frustum/Hi-Z plus compact indirect draws may remove enough work that meshlets are unnecessary.

## 10. Screen-space-driven LOD refinement

The current CPU-selected source-step model is a good semantic boundary. It can still be improved by basing thresholds on projected error / projected size rather than distance alone, with hysteresis to avoid churn.

Any refinement must continue to guarantee:

- neighboring LOD transition compatibility;
- no temporary holes while replacement geometry is generated;
- deterministic behavior around thresholds;
- bounded regeneration/upload churn.

## 11. Integrate per-camera GPU work with Unity RenderGraph

The renderer already enters URP through `VoxelRenderFeature` / `VoxelRenderPass`, but much of the current GPU work is still orchestrated outside explicit RenderGraph dependencies, and the final surface draw uses one `AddUnsafePass` that manually sets render targets and emits procedural draws.

The target architecture should distinguish persistent world/presentation work from per-camera rendering work.

### Keep outside per-camera RenderGraph

These systems are not fundamentally camera-render operations and should remain independently owned:

- authoritative Storage and persistence;
- edits and change journal;
- semantic LOD/readiness policy;
- brick demand and mirror residency policy;
- world generation;
- persistent GPU voxel mirror state;
- persistent surface page arena lifetime;
- potentially surface extraction itself, because generated geometry can survive for many frames and cameras.

Surface extraction is a gray area: it may eventually benefit from graph/async-compute scheduling, but should not be moved into a camera render pass merely for organizational consistency.

### Move per-camera GPU work into explicit graph passes

A better long-term pipeline is:

```text
CPU / persistent world work
---------------------------
Storage + changes
semantic LOD/readiness
mirror residency
persistent GPU extraction / surface arena

                |
                v

URP RenderGraph per camera
--------------------------
GPU frustum compute
        |
        v
Hi-Z build
        |
        v
occlusion + visible compaction
        |
        v
indirect command generation
        |
        v
voxel surface raster
        |
        v
water raster (if useful as a separate dependency)
```

Specific candidates:

- **GPU frustum culling:** `AddComputePass`; read candidate bounds/handles and write survivor handles.
- **Hi-Z pyramid generation:** explicit graph pass(es) reading camera depth and writing the hierarchy.
- **Occlusion + visible compaction:** `AddComputePass`; read Hi-Z and candidates, write visible handles/counts.
- **Indirect argument / draw metadata generation:** graph compute pass instead of standalone `ComputeShader.Dispatch` orchestration in `GpuSurfaceDrawDispatcher`.
- **Voxel surface drawing:** migrate from `AddUnsafePass` toward a proper raster pass with declared color/depth attachments and buffer dependencies where Unity APIs permit.
- **Water:** consider a separate raster pass if its resources/order differ enough to benefit; do not split it merely for code organization.

### Why this matters

Explicit RenderGraph dependencies let URP understand which passes read/write depth, color, visibility buffers, draw metadata, and indirect arguments. This gives Unity more freedom to manage resource transitions, pass ordering, attachment lifetime, tile-memory behavior, and possible pass merging instead of treating the renderer as an opaque command stream.

This is especially relevant on tile-based GPUs such as Apple Silicon, where unnecessary attachment transitions/store-load behavior can be expensive.

Do not create many `ScriptableRendererFeature` objects merely to mirror these stages. One renderer feature / integration point can record multiple graph passes.

### Hi-Z sequencing and voxel self-occlusion

The current voxel surface pass defaults near `BeforeRenderingTransparents`, so the existing opaque depth can be consumed before drawing voxels. That is enough to occlude voxels behind normal URP opaque geometry, but it does not include the current frame's voxel terrain itself.

Potential self-occlusion strategies:

1. voxel depth prepass -> Hi-Z -> main voxel draw;
2. previous-frame voxel depth / Hi-Z with conservative temporal rules;
3. initially use only existing scene depth, then add voxel self-occlusion after basic GPU frustum/occlusion infrastructure is proven.

Do not assume a full voxel depth prepass is automatically worthwhile; it can duplicate substantial vertex/geometry work. Prototype the simpler options and measure.

### Target ownership boundary

The intended architecture is:

> CPU owns the persistent voxel world and semantic correctness decisions; Unity RenderGraph owns the per-camera GPU visibility -> command generation -> raster pipeline.

Avoid adding future frustum/Hi-Z work as more ad-hoc standalone `ComputeShader.Dispatch()` calls if those operations consume camera resources or feed same-frame rasterization. Express those dependencies through RenderGraph instead.

## Suggested priority

Profile and prototype roughly in this order:

1. **Instrumentation first**: triangle/page/bucket/visibility/upload funnel metrics; also verify depth convention/reversed-Z on supported backends.
2. **Planar/greedy merging prototype** for a controlled flat/cubic terrain case.
3. **RenderGraph integration foundation** for per-camera compute/raster dependencies; avoid building the next GPU visibility stages as ad-hoc dispatches.
4. **GPU frustum culling** as an explicit compute pass.
5. **Hi-Z occlusion + GPU visible compaction**, explicitly correct for reversed-Z.
6. **Compact/multi indirect command generation/submission**, keeping command creation GPU-resident where possible.
7. **Migrate/reduce the unsafe raster path** when supported by the required indirect/procedural APIs and verify benefit on Metal/tile GPUs.
8. **Transfer compression/RLE experiment** only if uploads are measured as a bottleneck.
9. **Arena/page tuning, water batching, and shader/lighting work** based on remaining bottlenecks.
10. **True meshlets / async compute** only with evidence that simpler measures are insufficient.

## Benchmark scenes / acceptance measurements for experiments

Use repeatable synthetic cases in addition to real scenes:

### Flat strip

A long, single-material flat terrain strip. Measure whether planar merging can approach a handful of rectangles instead of grid-resolution triangle growth.

### Material-boundary strip

Flat terrain with regular material/style boundaries. Ensures merging stops at semantically required seams.

### Destruction strip

Flat terrain with holes/edited cells. Ensures edits invalidate/split only necessary regions and do not produce stale merged geometry.

### Smooth hill field

A surface where planar merging should largely decline to act. Guards against damaging smooth reconstruction.

### Occlusion city/canyon

Many semantically resident chunks with most geometry hidden from the camera. Measures frustum + Hi-Z effectiveness.

For every optimization, record at minimum:

- CPU frame/render-thread time;
- GPU extraction time;
- GPU render time;
- dirty bytes uploaded;
- resident mirror bytes;
- resident geometry bytes;
- candidate and survivor counts through the visibility funnel;
- generated vertices/indices/triangles;
- draw/indirect submission count;
- visual/oracle correctness.

For RenderGraph experiments, additionally record:

- pass count and graph order;
- whether passes remain unsafe or become compute/raster passes;
- attachment load/store behavior where observable;
- GPU resource/barrier cost where observable;
- Metal/tile-GPU render time before and after restructuring.

## Guiding rule

Prefer eliminating work over compressing work that should never have been generated.

For the flat-terrain example, reducing thousands of redundant triangles to a few compatible planar patches is potentially more valuable than merely compressing the voxel input that would have generated those triangles.