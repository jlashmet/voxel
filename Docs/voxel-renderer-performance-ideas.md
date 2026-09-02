# Voxel Renderer Performance Ideas

Status: exploratory research and optimization backlog, not accepted implementation requirements.

Research snapshot: 2026-09-02.

Profile first. Preserve rendering correctness, edits, material/surface semantics, LOD transitions, no-hole publication, persistent-world ownership, and the CPU/GPU oracle contract.

## Current architecture

```text
CPU authoritative Storage
        |
        | changed/demanded bricks
        v
shared persistent GPU voxel mirror
        |
        | extraction request with CPU-selected SourceStep / LOD
        v
GPU surface page arena
        |
        | CPU-selected visible chunk handles today
        v
GPU draw bucket/indirect preparation
        |
        v
URP voxel raster pass
```

Ownership boundary:

- CPU Storage is authoritative for persistence, gameplay, collision, edits, and change tracking.
- The shared GPU voxel mirror is a derived, persistent, demand-filled world-data cache.
- Empty bricks require no payload; uniform bricks use compact metadata; mixed bricks carry detailed voxel payload.
- The same mixed-brick payload can support several extraction resolutions; do not create per-LOD voxel copies by default.
- The GPU surface arena owns derived geometry, not authoritative voxel state.
- Semantic LOD/readiness remains a correctness concern; final per-camera visibility is a rendering concern.

## Guiding principles

1. **Eliminate work before compressing work.** Do not optimize transfer of geometry that should never have been generated.
2. **Keep persistent world work separate from per-camera rendering.** Storage/mirror/extraction lifetime is not the same as visibility/raster lifetime.
3. **Prefer screen-space error over raw distance.** Detail should track perceptual impact, FOV, and resolution.
4. **Exploit temporal coherence.** Camera visibility and residency usually change incrementally.
5. **Make every GPU stage measurable.** Candidate counts, survivor counts, page pressure, triangle counts, and submission counts need explicit telemetry.
6. **Specialize representations by topology.** Flat heightfield-safe terrain should not necessarily pay the same representation cost as caves, destruction, overhangs, and structures.
7. **Partition dynamic derived geometry so edits have bounded rebuild radius.** A small edit should not force rebuild of a huge terrain section.
8. **Benchmark steady-state rendering separately from edit maintenance and retained representation memory.** The fastest renderer can still be the wrong choice if edits make its derived data prohibitively expensive.
9. **Never trade edit correctness for a static-mesh optimization without a bounded invalidation/rebuild model.**

# Priority

This is the current research-informed order. Re-rank after measurements.

| Priority | Idea | Expected value | Risk / scope |
| --- | --- | --- | --- |
| P0 | Instrument the full visibility/geometry/upload/edit-maintenance funnel | Required for every other decision | Low |
| P0 | Verify reversed-Z/depth conventions on every supported backend | Correctness foundation for Hi-Z | Low |
| P1 | Prototype Unity multi-command indirect instead of 128 indirect submissions | Potentially large CPU/render-thread win | Low-medium |
| P1 | Planar/greedy surface merging | Potentially enormous geometry reduction | Medium |
| P1 | Audit and compress GPU surface vertex/index representation | Large memory/bandwidth/page-pressure win | Medium |
| P1 | Move per-camera GPU stages into explicit RenderGraph compute/raster dependencies | Foundation for GPU-driven visibility | Medium |
| P1 | GPU frustum culling | Large and relatively simple | Medium |
| P1 | Two-pass / temporal Hi-Z occlusion + visible compaction | Large in dense/occluded scenes | Medium-high |
| P2 | Hybrid heightfield-safe + general mesh terrain representation | Potentially enormous for open terrain | Medium-high |
| P2 | Partitioned, variable-resolution terrain sections with local rebuilds | Strong fit for destructible/editable terrain | Medium-high |
| P2 | Runtime cluster/meshlet hierarchy with screen-space error LOD | Biggest long-term geometry architecture | High |
| P2 | GPU-resident candidate/visibility/indirect pipeline | Removes CPU visible-handle upload and submission pressure | Medium-high |
| P2 | Cluster cone/backface culling | Strong once clusters exist | Medium |
| P2 | Clustered/Forward+ local lighting | Likely practical shading win | Medium |
| P3 | VRS on supported platforms if fragment-bound | Strong but platform-dependent | Medium |
| P3 | RLE/compressed mixed-brick transfer | Useful only if transfer is measured bottleneck | Medium |
| P3 | Page-arena tuning, eviction, fragmentation work | Profile-driven | Medium |
| Research | Voxel SVDAG/ray traversal path for selected far/complex regions | Could avoid meshes entirely | High |
| Research | Visibility-buffer shading | Attractive if overdraw/material cost dominates | High |
| Watch | End-to-end compressed meshlets / mesh shaders | Very promising, Unity portability/API dependency | High |
| Watch | RTX Mega Geometry / cluster acceleration structures | Cutting edge, hardware/API dependent | High |
| Watch | Stochastic direct lighting similar to Unreal MegaLights | Potentially major with many lights, RT/denoising complexity | Very high |
| Avoid for now | Work Graphs / bespoke native bindless path | Too much engine/platform coupling today | Very high |

# Near-term ideas

## 1. Replace fixed indirect submissions with multi-command indirect

The current paged path can issue the entire fixed bucket count when any paged geometry is visible. Unity's modern procedural indirect API supports a command buffer plus `commandCount`, while `DrawProceduralIndirect` is obsolete in favor of `RenderPrimitivesIndirect` / indexed equivalents.

Prototype a representation where GPU compaction produces the final command array directly:

```text
visible geometry
      |
      v
GPU bucket / command compaction
      |
      v
N occupied IndirectDrawArgs
      |
      v
one multi-command indirect submission where supported
```

Do not read bucket occupancy back to the CPU each frame merely to skip empty commands.

Measure occupied buckets, old/new submission count, render-thread CPU time, GPU command-processing time, and backend behavior on Metal, Vulkan, and DX12 targets.

## 2. Planar / greedy surface merging

A long flat region currently remains tessellated at the extraction grid resolution. LOD makes the grid coarser but does not globally collapse a plane.

For compatible reconstruction modes:

```text
surface cells
    |
    v
compatible horizontal runs
    |
    v
merge equal neighboring runs
    |
    v
maximal planar rectangles
    |
    v
one quad / two triangles per rectangle
```

A merge must stop at material/style/coating boundaries, authored sharp edges, holes, destruction, transition seams, topology changes, and any boundary required for correct shading semantics.

This is much more promising for planar/cubic/faceted regions than arbitrary smooth Transvoxel terrain.

## 3. Compress the GPU surface representation

The surface arena currently stores structured extracted vertices and 32-bit indices. Voxels give us more structure than an arbitrary imported mesh, so generic `float3`-heavy representations may be wasteful.

Investigate a voxel-specific encoded vertex such as:

- chunk-local/cell-local position rather than world float position;
- compact edge identifier + interpolation parameter where possible;
- quantized local coordinates;
- octahedral or otherwise packed normals;
- packed material/surface/coating identifiers;
- 16-bit indices where a cluster/page can guarantee the range.

The vertex shader can reconstruct world position from chunk/page metadata.

Targets to test: 16-byte, 12-byte, and 8-byte encoded vertices, but correctness determines the floor.

Benefits can compound across arena capacity, page count, geometry write bandwidth, vertex fetch bandwidth, cache behavior, and residency lifetime.

## 4. Selective RLE / compressed brick transfer

Do **not** make the persistent random-access GPU mirror a naive RLE stream. Density extraction performs coordinate-addressed neighboring taps and should retain effectively O(1) lookup.

RLE or another lightweight codec is only interesting as a transfer format:

```text
CPU dirty mixed brick
      -> encode
      -> smaller transfer
      -> GPU decode
      -> normal random-access mirror layout
```

Only pursue if dirty mixed-brick upload bandwidth/driver cost is measured as important. Unified-memory systems may show little benefit.

# Per-camera RenderGraph architecture

## 5. Use RenderGraph for per-camera GPU visibility and raster work

The renderer already enters URP through `VoxelRenderFeature` / `VoxelRenderPass`, but substantial GPU work is orchestrated through direct compute dispatches and the final surface path uses an unsafe graph pass.

Target split:

```text
PERSISTENT WORLD/PRESENTATION
-----------------------------
Storage + edits + journal
semantic LOD/readiness
mirror residency
persistent voxel mirror
surface extraction
persistent surface pages

                |
                v

URP RENDERGRAPH PER CAMERA
--------------------------
candidate bounds/handles
        |
        v
GPU frustum compute pass
        |
        v
Hi-Z / temporal occlusion passes
        |
        v
visible cluster/handle compaction
        |
        v
indirect command generation
        |
        v
voxel raster pass
        |
        v
water / specialized passes where justified
```

Keep Storage, edit journal, semantic policy, world generation, mirror residency, and persistent arena lifetime outside the per-camera graph.

Surface extraction is a gray area: it may later benefit from async-compute scheduling, but it should not become a camera pass solely for code organization.

## 6. Reversed-Z is a required Hi-Z invariant

Modern Unity backends use reversed-Z. Custom depth code must honor `SystemInfo.usesReversedZBuffer` / `UNITY_REVERSED_Z` rather than assuming conventional depth.

A wrong hierarchical reduction or comparison can incorrectly cull visible terrain.

Test the actual backend convention and include an automated occlusion correctness case. Infinite-far reversed-Z projection can be investigated for extreme view distances, but it does not replace semantic far-world bounds/HLOD.

## 7. Two-pass / temporal Hi-Z instead of blindly adding a full voxel depth prepass

A full depth prepass can duplicate large amounts of geometry work. Prefer exploiting temporal coherence.

Candidate strategy:

```text
previous-frame depth / Hi-Z
        |
        v
cull likely-visible candidates
        |
        v
main voxel draw
        |
        v
current depth / updated pyramid
        |
        v
re-test uncertain / previously occluded candidates
        |
        v
small disocclusion/post draw
```

Start conservatively. False-visible work is acceptable; false-occluded geometry is not.

# Representation specialization

## 8. Heightfield-safe terrain path

Do not force all terrain through a general 3D surface mesh when a region can prove a simpler topology.

A region may be heightfield-safe when each X/Z column has one relevant surface and contains no visible caves, overhangs, vertical architectural topology, or destructive topology requiring the general representation.

Possible path:

```text
simple ground region
    -> compact height/material field
    -> reusable GPU grid/clipmap
    -> vertex reconstruction

complex region
    -> full voxel mirror
    -> extracted mesh / cluster hierarchy
```

This attacks meshing, geometry residency, page pressure, and triangle count at once.

Transitions and promotion back to full voxel topology are the hard part. Treat the authoritative voxel world as truth and the heightfield as a derived representation.

## 9. Partitioned non-uniform mesh terrain

Unreal Engine 5.8's experimental Mesh Terrain is a useful 2026 signal: Epic is moving beyond a heightfield-only terrain representation toward spatially partitioned mesh terrain that supports arbitrary topology such as overhangs, tunnels, and sheer cliffs while allowing non-uniform resolution.

The most transferable idea is not the authoring tool; it is the **partition/rebuild boundary**. Epic explicitly warns that very large terrain sections make even a small modifier edit expensive because the entire section is recalculated.

For this renderer, test a derived terrain layout where:

- sections/patches are spatially bounded;
- resolution can vary by semantic importance/projected error;
- local topology changes invalidate only nearby sections;
- section publication is generation-based and atomic;
- the authoritative voxel data remains independent of the derived mesh partition;
- neighboring sections maintain crack-free transition contracts.

This complements rather than replaces the heightfield-safe path. A useful hierarchy could be:

```text
heightfield-safe region
    -> cheapest compact terrain representation

non-heightfield but coherent terrain section
    -> variable-resolution partitioned mesh

highly dynamic / cave / structure / arbitrary topology
    -> general voxel surface clusters
```

Sources:

- https://dev.epicgames.com/documentation/unreal-engine/mesh-terrain-in-unreal-engine
- https://dev.epicgames.com/documentation/unreal-engine/crafting-mesh-terrain-in-unreal-engine
- https://dev.epicgames.com/documentation/unreal-engine/unreal-engine-5-8-release-notes

## 10. Runtime virtual geometry / cluster hierarchy

Long term, the surface arena can evolve from "one selected chunk mesh at one SourceStep" toward a local hierarchy of small triangle clusters.

Target:

```text
voxel changes
    |
    v
GPU surface extraction
    |
    v
small clusters / meshlets
    |
    v
local simplification hierarchy + errors
    |
    v
persistent virtual-geometry pages

per camera:
projected-error LOD selection
frustum
cone/backface
Hi-Z
visible cluster compaction
indirect draws
```

This changes camera movement from "regenerate another LOD mesh" toward "select another already-derived hierarchy level". Edits still invalidate only affected local hierarchy regions.

Do not build this until simpler geometry reduction, compressed vertices, and GPU visibility are measured; however, design new page metadata so it does not make this evolution unnecessarily hard.

## 11. Cluster cone/backface culling

Once geometry is clustered, store conservative spatial bounds plus a normal cone. If every triangle in a cluster must face away from the camera, kill the whole cluster before rasterization.

Useful for cliffs, walls, caves, structures, and other directional surfaces.

# Lighting and shading

## 12. Clustered/Forward+ lighting first

The current voxel shader can evaluate many local lights per pixel. A practical next architecture is screen/frustum clustering:

```text
lights
  -> compute cluster assignment
  -> per-pixel cluster lookup
  -> evaluate only affecting lights
```

Godot's Forward+ renderer demonstrates this conservative, production-friendly approach. Prefer it before a much more complex stochastic ray-traced lighting architecture.

## 13. Stochastic direct lighting is a future option

Unreal's current MegaLights path uses importance sampling and a fixed number of rays per pixel so cost is much less dependent on the number of lights. It combines sample generation, ray tracing, and denoising and can run parts on async compute.

This is interesting if the game eventually needs very large numbers of dynamic shadowed lights. It is not a near-term replacement for clustered lighting because it introduces ray-tracing scene maintenance, denoising/temporal stability, platform constraints, another simplified geometry representation for RT, and substantial RenderGraph complexity.

## 14. Variable Rate Shading

If profiling becomes fragment/shading bound, use VRS on supported platforms to reduce shading frequency for distant/low-detail screen regions while retaining full depth/geometry coverage.

Potential shading-rate inputs include distance, motion, semantic importance, depth discontinuity, and material frequency. This is platform-dependent and should not shape the core geometry architecture.

## 15. Visibility-buffer shading

Aokana and modern GPU-driven renderers reinforce the idea of separating visibility determination from expensive shading. A visibility buffer can record compact primitive/surface identity first and perform material/light work only for final visible pixels.

Research this only if overdraw and material/light cost remain dominant after geometry reduction, early depth, and GPU occlusion. Tile-GPU bandwidth must be measured carefully on Metal.

# Research survey

## Unity Virtual Mesh (0.2.0-preview, Dec 2025)

Unity's experimental `com.unity.virtualmesh` package is highly relevant as reference code. The current package targets Unity 6000.3+, URP, Vulkan, and RenderGraph and explicitly describes itself as non-production reference code.

Key implementation choices:

- GPU-driven LOD and culling directly before rendering;
- clusters/meshlets of up to 64 triangles;
- hierarchical cluster groups;
- screen-space simplification error for LOD;
- two-pass occlusion culling and a depth pyramid;
- memory pages requested by the GPU;
- async GPU readback of page requests followed by jobified I/O/upload buffers;
- placeholder geometry when requested pages are unavailable;
- half-precision vertex positions to reduce streaming/GPU handling cost;
- persistent buffers owned separately from custom render passes.

The package's architecture strongly supports our proposed boundary: persistent manager-owned GPU buffers plus RenderGraph passes for visibility and drawing.

**Recommendation:** use it as reference implementation and testbed, not as a drop-in renderer today. Its current static-opaque/baked assumptions do not match destructible runtime-generated voxel geometry, and its own roadmap still calls out backend work.

Sources:

- https://github.com/Unity-Technologies/com.unity.virtualmesh
- https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/Documentation~/implementation.md
- https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/CHANGELOG.md

## Unreal Engine 5.8 / Nanite

Nanite remains the strongest production example of virtualized triangle geometry.

Relevant ideas:

- quantized/compressed specialized geometry representation;
- fine-grained streaming with always-resident root geometry;
- screen/perceptual error rather than traditional hand-authored distance LODs;
- candidate and visible cluster buffers with explicit capacity telemetry;
- two-pass occlusion;
- persistent streaming pool where undersizing causes thrash;
- HLOD/World Partition above the fine virtual-geometry layer;
- separation between source/authoritative representations and derived Nanite geometry where necessary.

Nanite Landscapes are particularly instructive. Unreal keeps the ordinary Landscape representation in addition to Nanite data because other systems still need it; edits can make the Nanite representation stale, and the normal representation is used until Nanite data is rebuilt. Nanite landscape seams may use skirts where independent simplification causes boundary mismatch.

That maps closely to our desired correctness model:

```text
authoritative voxel world remains valid
        |
        +--> old/alternate presentation remains usable
        |
        v
local derived representation rebuilds
        |
        v
atomic publication when ready
```

Do **not** blindly copy Nanite's duplicated landscape memory cost. Our goal should be to retain one authoritative voxel representation plus derived caches with bounded residency.

Sources:

- https://dev.epicgames.com/documentation/en-us/unreal-engine/nanite-in-unreal-engine
- https://dev.epicgames.com/documentation/en-us/unreal-engine/nanite-technical-details
- https://dev.epicgames.com/documentation/unreal-engine/using-nanite-with-landscapes-in-unreal-engine
- https://dev.epicgames.com/documentation/en-us/unreal-engine/world-partition---hierarchical-level-of-detail-in-unreal-engine

## Unreal Engine 5.8 Mesh Terrain

This is a significant new 2026 direction. Mesh Terrain is an experimental, next-generation mesh-based terrain system intended to remove the constraints of heightfield-only landscapes. Epic documents arbitrary 3D terrain shapes, non-uniform resolution, spatial mesh partitions, non-destructive modifiers, and Nanite integration.

The section-size guidance is especially relevant: edits are rebuilt at section granularity, so oversized sections increase the cost of small local changes.

**Recommendation:** copy the architectural lesson, not the editor workflow. Derived voxel terrain should use bounded spatial partitions with topology/resolution chosen per region, and edit cost should scale with the affected partition footprint rather than the size of the visible world.

Sources:

- https://dev.epicgames.com/documentation/unreal-engine/mesh-terrain-in-unreal-engine
- https://dev.epicgames.com/documentation/unreal-engine/crafting-mesh-terrain-in-unreal-engine
- https://dev.epicgames.com/documentation/unreal-engine/unreal-engine-5-8-release-notes

## Unreal Virtual Shadow Maps and MegaLights

Virtual Shadow Maps demonstrate the same virtual-memory/page idea applied to shadow depth: allocate high resolution only where needed and cache/reuse pages.

MegaLights is a newer, more radical lesson: replace cost that scales roughly with light count by a fixed stochastic sample budget per pixel, guided toward important lights, followed by denoising. Unreal also overlaps several related dispatches using async compute.

**Recommendation:** virtualized shadow/page concepts are worth remembering if voxel shadow rendering becomes expensive. MegaLights-style stochastic lighting is a research-stage option for us; clustered lighting is the sensible nearer step.

Sources:

- https://dev.epicgames.com/documentation/unreal-engine/virtual-shadow-maps-in-unreal-engine
- https://dev.epicgames.com/documentation/en-us/unreal-engine/megalights-in-unreal-engine

## Godot current rendering architecture

Godot provides a useful contrast because it applies simpler, broadly portable techniques:

- reverse-Z across renderers;
- clustered Forward+ lighting on desktop;
- automatic mesh LOD generated with meshoptimizer;
- screen-space metric for LOD selection;
- explicit HLOD/visibility ranges;
- depth prepass in Forward+;
- instancing/MultiMesh for submission reduction;
- occlusion culling using simplified occluders and a low-resolution CPU representation.

Lessons for this renderer:

- screen-space LOD + HLOD layering is mature and worth adopting;
- clustered lighting is a practical first lighting architecture;
- Godot's CPU occlusion design is **not** the model to copy for our dynamic GPU-resident voxel surfaces; GPU Hi-Z better matches our ownership/data path;
- large instance batches need finer spatial grouping or they lose per-instance culling efficiency.

Sources:

- https://docs.godotengine.org/en/4.7/engine_details/architecture/internal_rendering_architecture.html
- https://docs.godotengine.org/en/latest/tutorials/3d/occlusion_culling.html
- https://docs.godotengine.org/en/latest/tutorials/3d/mesh_lod.html
- https://docs.godotengine.org/en/latest/tutorials/3d/visibility_ranges.html

# Research papers and transferable ideas

## Aokana: GPU-Driven Voxel Rendering for Open World Games (2025)

This is the most directly relevant voxel-rendering paper found.

Aokana uses multiple relatively shallow SVDAG chunks rather than one extremely deep DAG, separates geometry/color compression, adds LOD and streaming, uses previous-frame Hi-Z, screen-tile/chunk candidate pairs, a compact visibility buffer, and GPU voxel traversal. The authors implemented it in Unity and designed it to coexist with mesh-based rendering.

**Transferable idea:** our renderer does not have to choose "mesh everything" or "ray trace everything." A hybrid may eventually use:

```text
near / frequently edited / feature-rich surfaces
        -> extracted geometry

selected far or topology-heavy voxel regions
        -> compact voxel hierarchy + GPU traversal
```

Only prototype this after the current GPU mesher is correct and measured. SVDAG compression can be poor for highly unique/noisy attributes and dynamic rebuilding is the core challenge.

Source: https://doi.org/10.1145/3728299

## Six Ways to Draw Vangers with WebGPU (August 2026)

This new benchmark is unusually relevant because it compares six rendering approaches over the **same editable multi-layer game terrain data path** rather than using conventional single-valued DEM terrain.

The compared methods include heightfield ray marching, voxel-accelerated ray marching, sliced proxy geometry, per-sample bar rasterization, compute scattering, and a fitted triangle mesh. Every method had to preserve two vertical solid intervals per ground sample and reflect local terrain destruction without reloading the level.

At the selected quality settings, the greedy triangulated irregular network (TIN) mesh had the lowest mean frame time on every tested device. But that result comes with an important cost: fitting complexity was driven by the second terrain layer/caves, and the editable mesh retained very large derived CPU and GPU data structures.

**Transferable lesson:** do not select terrain representation from steady-state frame time alone. For each candidate representation measure three independent axes:

1. **steady-state render cost**;
2. **edit/update latency and rebuild radius**;
3. **retained CPU + GPU representation memory**.

This result strengthens rather than weakens the hybrid strategy: a greedy/partitioned mesh can be an excellent presentation for coherent regions, while layered/cave-heavy or highly edited regions may justify a different representation.

Source: https://arxiv.org/abs/2608.17390

## Editing Compact Voxel Representations on the GPU (Pacific Graphics 2024)

This work extends HashDAG-style compact voxel representations with GPU hash tables so large edits such as painting can remain GPU-side at interactive rates.

**Transferable idea:** if we ever add an SVDAG/HashDAG secondary representation, edit propagation does not necessarily need a CPU rebuild of the whole compressed hierarchy. Local GPU-side structural editing is a viable research direction.

Source: https://doi.org/10.2312/pg.20241310

## Encoding Occupancy in Memory Location for Efficient and Compact High-Resolution Voxel Structures (2025/2026 publication cycle)

This work encodes structural occupancy information into memory location/addressing so traversal can infer information without fetching another node. It retains compatibility with editable HashDAG-like structures.

**Transferable idea:** for hierarchical voxel traversal, reducing dependent memory accesses may matter more than maximizing theoretical compression. Any future hierarchy should optimize **bytes accessed and pointer-chasing depth**, not just bytes stored.

Source: https://doi.org/10.1111/cgf.70292

## Dynamic Mesh Processing on the GPU (SIGGRAPH/TOG 2025)

This work partitions a dynamic triangle mesh into small patches, performs topology/attribute updates in GPU shared memory, and uses speculative conflict handling. It demonstrates GPU remeshing and other topology-changing workloads with major speedups over CPU approaches.

**Transferable idea:** a future runtime cluster hierarchy does not necessarily require expensive CPU simplification after each voxel edit. Locally affected surface patches may be rebuilt/simplified entirely on the GPU.

Source: https://doi.org/10.1145/3731162

## End-to-End Compressed Meshlet Rendering (2024) and Real-time Meshlet Decompression (2025)

Recent meshlet-compression work keeps geometry compressed in GPU memory and decompresses only as it is consumed. Research shows random-access meshlet representations, local quantization, crack-aware attribute encoding, and very high connectivity compression.

This is currently demonstrated most naturally with mesh shaders, which are not yet a safe cross-platform Unity foundation for this project.

**Transferable ideas we can use sooner:**

- quantize per small spatial page/cluster instead of globally;
- encode connectivity compactly;
- keep cluster boundaries crack-safe;
- design cluster payloads for random access;
- consider decode-on-consume rather than permanently expanding every representation.

Sources:

- https://doi.org/10.1111/cgf.15002
- https://doi.org/10.1016/j.cag.2025.104292

## Virtualized 3D Gaussians (SIGGRAPH 2025)

Although the primitive is unrelated to our triangles, the architecture independently converges on Nanite-like hierarchical clusters plus online footprint-based selection for very large composed scenes.

**Transferable idea:** cluster hierarchy + projected footprint/error is becoming a general solution across different primitive types, strengthening the case that this is a durable architecture rather than a Nanite-specific trick.

Source: https://arxiv.org/abs/2505.06523

## Sparse Voxels Rasterization (2024)

This work uses sparse voxels, adaptive LOD, and dynamic Morton ordering to improve coherent rasterization/order for sparse voxel representations.

**Transferable idea:** spatial ordering/Morton layout can improve memory behavior and coherent processing even if we retain triangle rasterization. Measure whether mirror directory entries, candidate chunks, and future cluster pages benefit from Morton/spatial ordering.

Source: https://arxiv.org/abs/2412.04459

## NVIDIA RTX Mega Geometry / Cluster Acceleration Structures (2025)

NVIDIA's newer RT stack exposes cluster acceleration structures for dynamic high-density geometry, allowing local clusters to be rebuilt/instanced under a higher-level acceleration structure and reducing rebuild cost versus flat microtriangle geometry.

**Recommendation:** watch this direction. It reinforces cluster-granular dynamic geometry as a hardware trend, but it is too vendor/API-specific to drive the Unity renderer architecture today.

Source: https://developer.nvidia.com/blog/fast-ray-tracing-of-dynamic-scenes-using-nvidia-optix-9-and-nvidia-rtx-mega-geometry/

# Resulting long-term target

A plausible end state, if profiling justifies each stage, is:

```text
AUTHORITATIVE CPU VOXEL WORLD
Storage / gameplay / collision / edits
            |
            | compact change journal
            v
PERSISTENT GPU WORLD DATA
shared voxel mirror
+ optional derived macro/heightfield/hierarchy summaries
            |
            | local change-driven work only
            v
DYNAMIC GPU SURFACE REPRESENTATIONS
heightfield-safe terrain where possible
partitioned variable-resolution terrain where useful
compressed detailed surface clusters where necessary
optional compact voxel hierarchy for selected far/complex regions
            |
            v
LOCAL ERROR/LOD HIERARCHIES
persistent page residency
old generation remains live until replacement ready
            |
            v
PER-CAMERA RENDERGRAPH
screen-space LOD / footprint selection
GPU frustum
cluster cone culling
previous-frame + current-frame Hi-Z
visible cluster compaction
multi-command indirect generation
            |
            v
RASTER / VISIBILITY
compressed vertex decode
possibly VRS
possibly visibility-buffer shading
            |
            v
LIGHTING
clustered lighting first
stochastic/ray-guided lighting only if future scale requires it
```

# Benchmark scenes and required measurements

## Synthetic scenes

### Flat strip
Long single-material flat ground. Validates planar merging and heightfield-safe representation.

### Material-boundary strip
Flat ground with regular material/style/coating boundaries. Ensures merges stop at semantic seams.

### Destruction strip
Flat ground with holes and repeated edits. Measures local invalidation and publication cost.

### Smooth hill field
A surface where planar merging should mostly decline to act.

### Multi-layer/cave strip
At least two vertical solid intervals in some X/Z columns, with tunnels/walls and local destruction. Specifically tests the failure mode highlighted by the 2026 Vangers benchmark.

### Cave/overhang field
Forces general 3D topology and demonstrates where a heightfield path must promote to full representation.

### Occlusion city/canyon
Many resident candidates with most geometry hidden. Measures frustum + Hi-Z funnel.

### Dense-light interior
Measures clustered lighting and future stochastic-lighting need.

### Camera sprint
High-speed traversal to expose residency/page thrash, LOD churn, and temporal-occlusion failure cases.

### Edit storm
Repeated local edits across one partition and then across many neighboring partitions. Measures rebuild radius, dirty amplification, and edit-to-visible latency.

## Metrics

For every experiment record at minimum:

- CPU frame time;
- render-thread/submission time;
- GPU extraction time;
- GPU visibility/culling time;
- GPU raster/shading time;
- dirty mirror bytes/frame;
- resident mirror bytes;
- resident geometry bytes;
- **retained method-specific CPU memory**;
- **retained method-specific GPU memory**;
- vertex stride and index stride;
- pages allocated/used/wasted;
- candidate -> frustum -> occlusion -> drawn counts;
- generated vertices/indices/triangles by LOD/style;
- occupied draw commands and actual submission count;
- **steady-state frame cost separate from edit-maintenance cost**;
- **edit rebuild/refit CPU and GPU time**;
- **number/area of sections dirtied per edit**;
- **dirty amplification: derived bytes/triangles rebuilt per authoritative voxel changed**;
- edit-to-visible latency;
- LOD regeneration frequency;
- peak residency and steady-state residency;
- visual/oracle correctness;
- missing/false-occlusion count (must remain zero).

For RenderGraph work additionally record:

- graph pass count/order;
- unsafe vs compute/raster passes;
- attachment load/store behavior where observable;
- barriers/synchronization where observable;
- async-compute overlap if used;
- Metal/tile-GPU timing before/after restructuring.

# Decision rule

The renderer should move toward **work proportional to visible perceptual complexity and actual edits**, not world size, voxel count, or a fixed number of chunk/bucket submissions.

The strongest pattern across Unity Virtual Mesh, Unreal Nanite and Mesh Terrain, Godot's screen-space LOD/HLOD layering, Aokana, the 2026 Vangers comparison, and recent graphics research is consistent:

> compact spatial representations + bounded partitions + hierarchical screen-space selection + GPU visibility + fine-grained residency + local updates.

Our unique advantage is that voxel structure gives us more options than a generic mesh renderer: we can cheaply identify empty/uniform regions, exploit grid-relative vertex encodings, derive heightfield-safe regions, choose a partitioned mesh where it wins, keep arbitrary 3D topology where required, and rebuild only local surface areas after edits.