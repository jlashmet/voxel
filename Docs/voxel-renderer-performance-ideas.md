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
6. **Specialize representations by topology when measurement proves it.** Flat heightfield-safe terrain should not necessarily pay the same representation cost as caves, destruction, overhangs, and structures.
7. **Partition dynamic derived data so edits have bounded rebuild radius.** A small edit should not force rebuild of a huge terrain section or hierarchy.
8. **Benchmark steady-state rendering separately from edit maintenance and retained representation memory.** The fastest renderer can still be the wrong choice if edits make its derived data prohibitively expensive.
9. **Never trade edit correctness for a static-mesh optimization without a bounded invalidation/rebuild model.**
10. **Treat renderer replacement as an evidence question, not a sunk-cost question.** If a voxel-native renderer beats the current mesh path on representative scenes, do not retain Transvoxel merely because it already exists.

# Priority

This is the current research-informed order. Re-rank after measurements.

| Priority | Idea | Expected value | Risk / scope |
| --- | --- | --- | --- |
| P0 | Instrument the full visibility/geometry/upload/edit-maintenance funnel | Required for every other decision | Low |
| P0 | Verify reversed-Z/depth conventions on every supported backend | Correctness foundation for Hi-Z | Low |
| P1 | **A/B architecture spike: Aokana-style primary solid renderer vs current GPU Transvoxel** | Could remove mesh generation, geometry residency, and LOD regeneration entirely | High |
| P1 | Prototype Unity multi-command indirect instead of fixed indirect submissions | Potentially large CPU/render-thread win | Low-medium |
| P1 | Planar/greedy surface merging | Potentially enormous geometry reduction if mesh path remains | Medium |
| P1 | Audit and compress GPU surface vertex/index representation | Large memory/bandwidth/page-pressure win if mesh path remains | Medium |
| P1 | Move per-camera GPU stages into explicit RenderGraph compute/raster dependencies | Foundation for GPU-driven visibility | Medium |
| P1 | GPU frustum culling | Large and relatively simple | Medium |
| P1 | Two-pass / temporal Hi-Z occlusion + visible compaction | Large in dense/occluded scenes | Medium-high |
| P2 | Hybrid heightfield-safe + general representation | Potentially enormous for open terrain | Medium-high |
| P2 | Partitioned, variable-resolution terrain sections with local rebuilds | Strong fit for destructible/editable terrain | Medium-high |
| P2 | Runtime cluster/meshlet hierarchy with screen-space error LOD | Biggest long-term raster geometry architecture | High |
| P2 | GPU-resident candidate/visibility/indirect pipeline | Removes CPU visible-handle upload and submission pressure | Medium-high |
| P2 | Cluster cone/backface culling | Strong once clusters exist | Medium |
| P2 | Clustered/Forward+ local lighting | Likely practical shading win | Medium |
| P3 | VRS on supported platforms if fragment-bound | Strong but platform-dependent | Medium |
| P3 | RLE/compressed mixed-brick transfer | Useful only if transfer is measured bottleneck | Medium |
| P3 | Page-arena tuning, eviction, fragmentation work | Profile-driven | Medium |
| Research | Editable SVDAG/HashDAG hierarchy and GPU-side local updates | Key enabler if Aokana becomes primary | High |
| Research | Visibility-buffer shading | Attractive if overdraw/material cost dominates | High |
| Watch | End-to-end compressed meshlets / mesh shaders | Very promising, Unity portability/API dependency | High |
| Watch | RTX Mega Geometry / cluster acceleration structures | Cutting edge, hardware/API dependent | High |
| Watch | Stochastic direct lighting similar to Unreal MegaLights | Potentially major with many lights, RT/denoising complexity | Very high |
| Avoid for now | Work Graphs / bespoke native bindless path | Too much engine/platform coupling today | Very high |

# First-class architecture question: can Aokana replace surface meshing?

The renderer should now explicitly test this hypothesis:

> **Can an Aokana-style shallow-SVDAG GPU voxel renderer replace surface meshing as the primary solid voxel renderer, with meshes retained only where they are measurably cheaper or required?**

Do not assume Aokana is only a far-distance representation. The opposite architecture may be better:

```text
AUTHORITATIVE CPU VOXELS
          |
          | changes
          v
EDITABLE / REBUILDABLE GPU VOXEL HIERARCHY
shallow chunked SVDAG / HashDAG-like representation
          |
          +---------------------------+
          |                           |
          v                           v
GENERAL CASE                      SPECIAL CASES
Aokana-style traversal            planar / procedural / mesh path
          |                           |
          +-------------+-------------+
                        v
              visibility / shading
```

## Why Aokana-everywhere is attractive

A voxel-native traversal path could eliminate or substantially reduce an entire class of derived mesh work:

- no Transvoxel triangle explosion for irregular surfaces;
- no persistent vertex/index arena for the default representation;
- no per-LOD mesh regeneration when the camera moves;
- no geometry-page fragmentation for the default path;
- natural hierarchical LOD selection from the voxel hierarchy;
- compact representation of large repeated spatial structure;
- direct compatibility with GPU frustum/Hi-Z/visibility-buffer pipelines;
- potentially much lower memory than retaining detailed voxel data **plus** multiple derived geometry structures.

Aokana reports large gains over the voxel techniques it compares against at high scene resolutions and was implemented in Unity, which makes it much more relevant than a purely theoretical renderer.

## Why not commit to Aokana immediately

Aokana's published implementation is aimed primarily at mostly-static open-world voxels. Runtime modification is not its core solved problem. For our game, destruction/editing is not optional.

The main unresolved risks are:

1. **Edit propagation.** A voxel edit must become visible quickly without rebuilding a huge hierarchy. We need bounded chunk-local rebuild or genuinely editable GPU hierarchy updates.
2. **Material/surface semantics.** Our renderer carries materials, surface semantics, coatings, stylized shading, cutaways, water, and other attributes. The voxel hierarchy must preserve or efficiently reference them.
3. **Traversal cost on simple surfaces.** A huge flat wall or flat terrain patch may be cheaper as two triangles or a procedural grid than as per-pixel hierarchical traversal.
4. **Metal behavior.** The published Aokana evaluation does not prove the same win on Apple tile GPUs. We must benchmark Metal directly.
5. **Collision/navigation do not disappear.** CPU authoritative voxels remain the source for gameplay/collision/navigation even if rendering becomes Aokana-like.
6. **No false-occlusion tolerance.** Temporal Hi-Z and traversal must remain conservative under rapid movement and edits.

## Aokana compatibility / implementation risk matrix

The feature list should be separated into **routine integration work**, **important engineering risks**, and **architecture-decision risks**. Most existing voxel-engine features do not prevent an Aokana-derived renderer; two areas are decisive: fast editable hierarchy maintenance and faithful smooth-density surface reconstruction.

| Capability | Difficulty | Likely approach | Architecture blocker? |
| --- | --- | --- | --- |
| CPU-authoritative Storage | Low | Keep Storage as truth; GPU hierarchy is derived presentation state fed by the existing change journal | No |
| Collision / gameplay / navigation | Low | Continue deriving/querying these from authoritative CPU voxel state or specialized CPU structures | No |
| Cutaways / cross-section clipping | Low | Traversal treats samples inside the active cutaway volume as non-renderable and continues to the next valid surface instead of terminating at the clipped surface | No |
| Dedicated water rendering | Low-medium | Keep liquid voxels on a specialized exposed-water surface/render path unless measurement shows a reason to fold them into traversal | No |
| Standard materials / lighting | Medium | Traversal resolves compact surface identity; a visibility/material resolve pass performs normal shading | No |
| Hybrid Aokana + planar/mesh ownership | Medium | Exactly one active presentation generation owns each bounded region; representation transitions publish atomically | No, but complexity cost must be justified |
| Metal / Apple tile-GPU performance | Medium-high / unknown | Benchmark traversal divergence, random memory access, compute occupancy, and visibility-buffer bandwidth on real Metal hardware early | Potential deployment blocker if performance loses badly |
| Rich material + surface semantic compression | High | Separate occupancy/spatial hierarchy from material/surface/coating payloads so unique attributes do not unnecessarily destroy DAG sharing | Possibly, if attribute bandwidth dominates |
| Smooth density / Transvoxel-quality surfaces | High | Use hierarchy for broad spatial skipping, then perform local density/implicit-surface intersection in the reached brick/leaf or retain a smooth near-mesh path | **Yes: decisive quality/performance risk** |
| Rapid arbitrary destruction / edit propagation | Highest | Bounded shallow-chunk rebuild, GPU-editable HashDAG/SVDAG techniques, generation-safe local publication, and strict dirty-amplification limits | **Yes: primary architecture risk** |

### Cutaways are not a major concern

The current renderer already carries a voxel-space cutaway box (`_CutawayEnabled`, `_CutawayMinVoxel`, `_CutawayMaxVoxel`) for clipping/revealing interior regions. In a triangle shader, clipped fragments can simply be rejected. In a traversal renderer, a hit inside the cutaway cannot terminate the ray; traversal must continue until it reaches the next non-cutaway surface.

Conceptually:

```text
ray
 |
 v
surface A inside cutaway -> ignore / continue
 |
 v
surface B outside cutaway -> visible hit
```

This is traversal semantics work, not a fundamental representation problem.

### Water should remain specialized initially

Do not require the solid Aokana experiment to solve liquid rendering. Water has different topology, exposure rules, transparency/refraction, animation, and shading. The initial architecture should remain:

```text
solid voxels  -> Aokana-style solid traversal candidate
liquid voxels -> dedicated water surface renderer
```

If a later unified representation is measurably better, it can be investigated independently.

### Materials and semantics should not be embedded naively in DAG identity

Occupancy/spatial repetition may compress well even when material/coating data is unique. Avoid requiring every material or semantic difference to create a structurally unique hierarchy node when that would destroy sharing.

Prefer a separation such as:

```text
spatial / occupancy hierarchy
            +
compact surface/material identity
            +
material / coating / semantic payload tables
```

A visibility-buffer-style path is attractive here: traversal determines the winning surface identity first, then the normal voxel material/lighting system shades only the final visible sample.

### Smooth surfaces are a first-class experiment, not an afterthought

Aokana-style occupancy traversal naturally fits discrete/block surfaces. Our source data also contains density/boundary information used to produce smooth Transvoxel-style surfaces. A universal Aokana replacement therefore has to prove that it can recover equivalent smooth geometry without turning every ray into an expensive iterative root-finding problem.

A promising split is hierarchical broad-phase plus local fine intersection:

```text
shallow hierarchy traversal
        |
        v
candidate detailed brick / leaf
        |
        v
local density / boundary intersection
        |
        v
accurate smooth surface hit
```

The benchmark must compare surface position, normals, silhouettes, material boundaries, LOD stability, and GPU traversal cost against the CPU/GPU surface oracle. If this cannot reach acceptable quality/performance, retain rasterized generated geometry for smooth near surfaces while using voxel traversal elsewhere.

### Destruction is the primary architecture gate

The ideal edit path is:

```text
edit N authoritative voxels
        |
        v
identify bounded affected shallow chunks/pages
        |
        v
GPU/local hierarchy update or rebuild
        |
        v
publish new generation atomically
        |
        v
next traversal observes the edit
```

Avoid a design where a small edit causes a rebuild proportional to world size, visible distance, or a deep global DAG. Track:

- hierarchy nodes/pages rebuilt per authoritative voxel changed;
- bytes rewritten per edit;
- CPU and GPU maintenance time;
- edit-to-visible latency;
- maximum rebuild radius;
- old/new generation overlap memory;
- behavior under sustained edit storms.

Recent GPU-editable HashDAG/SVDAG research makes this plausible, but it must be proven in our data rather than assumed from mostly-static Aokana results.

### Renderer-ownership invariant for a hybrid

If some regions remain raster meshes while others use voxel traversal, avoid overlapping ownership ambiguity. A useful invariant is:

> **Each bounded spatial presentation region has exactly one active representation generation for a given rendering domain.**

Representation changes should follow the same no-hole publication model as current GPU geometry:

```text
old representation remains live
        |
new representation builds / becomes ready
        |
atomic ownership publication
        |
old representation retires later
```

This prevents cracks, double rendering, and temporary missing regions when promoting/demoting between Aokana, planar/heightfield, and generated-mesh paths.

### Architecture gates

The Aokana-primary experiment should answer these two questions before broad migration:

1. **Can edits update the hierarchical presentation locally enough that destruction remains immediate and bounded?**
2. **Can the traversal recover our smooth/material-rich surfaces at acceptable quality and cost?**

If both are yes, the other listed capabilities appear tractable and there is no obvious feature-level reason Aokana cannot become the primary solid renderer. If either fails badly, treat Aokana as one representation in a hybrid renderer rather than forcing it universally.

Recent work on GPU-editable compact voxel representations makes the edit problem less discouraging: a future implementation does not necessarily require rebuilding compressed hierarchy state on the CPU after every edit.

## Required A/B/C benchmark

Use the **same authoritative voxel data, camera path, material intent, and visibility range** for all candidates:

```text
A. Current GPU Transvoxel renderer
B. Aokana-style shallow-SVDAG GPU traversal
C. Greedy/planar/procedural mesh path where topology permits
```

Test at minimum:

- flat terrain;
- rolling terrain;
- mountains/cliffs;
- caves and overhangs;
- dense structures/city;
- highly noisy voxel topology;
- repeated destruction;
- large edit storm;
- high-speed camera motion;
- long-distance world view.

Measure:

- CPU frame time;
- render-thread/submission time;
- GPU frame time;
- GPU visibility/traversal time;
- GPU shading time;
- GPU memory;
- method-specific CPU memory;
- edit-to-visible latency;
- hierarchy rebuild/update time;
- bytes/nodes dirtied per voxel edit;
- nodes visited per pixel / traversal steps;
- pixels/rays requiring traversal;
- triangles/vertices generated for mesh candidates;
- LOD transition/regeneration cost;
- streaming/residency churn;
- visual quality and surface-semantic parity;
- oracle correctness;
- behavior on Apple Metal and at least one discrete-GPU Vulkan/DX12 target.

### Decision rule

If the Aokana-style path wins across representative gameplay scenes on frame cost, memory, and edit latency, **make it the primary solid renderer and demote Transvoxel to a specialized/fallback path**.

If meshes clearly win for coherent planar/simple regions, retain a hybrid in which the representation is chosen per bounded region. Do not force one renderer onto every topology for architectural purity.

# Near-term raster-path ideas

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

## 2. Planar / greedy surface merging

If a mesh path remains, a long flat region should not stay tessellated at the extraction grid resolution. Form compatible runs/rectangles and emit a minimal set of planar patches, stopping at material/style/coating boundaries, authored sharp edges, holes, destruction, LOD seams, and topology changes.

## 3. Compress the GPU surface representation

If a mesh path remains important, exploit voxel structure rather than generic float-heavy vertices:

- chunk/cell-local positions;
- edge identifier + interpolation where practical;
- quantized coordinates;
- packed/octahedral normals;
- packed material/surface/coating IDs;
- 16-bit local indices where cluster/page limits permit.

Test 16-byte, 12-byte, and 8-byte encoded vertices only where correctness allows.

## 4. Selective RLE / compressed brick transfer

Do not make the persistent random-access GPU mirror a naive RLE stream. Use RLE/lightweight compression only as a transfer format if dirty mixed-brick upload bandwidth is proven important:

```text
CPU dirty mixed brick
      -> encode
      -> smaller transfer
      -> GPU decode
      -> normal random-access mirror layout
```

# Per-camera RenderGraph architecture

## 5. Use RenderGraph for per-camera GPU visibility and raster work

Keep persistent world/presentation ownership separate from per-camera work:

```text
PERSISTENT WORLD/PRESENTATION
Storage + edits + journal
semantic LOD/readiness
mirror / hierarchy residency
persistent voxel representation
surface extraction only where needed
persistent derived pages

                |
                v

URP RENDERGRAPH PER CAMERA
candidate regions / bounds
        |
        v
GPU frustum
        |
        v
Hi-Z / temporal occlusion
        |
        v
visible region/cluster compaction
        |
        v
indirect command generation or voxel traversal dispatch
        |
        v
raster / visibility-buffer pass
        |
        v
specialized water / shading passes
```

Do not add future camera-dependent GPU stages as ad-hoc standalone `ComputeShader.Dispatch()` calls when they naturally consume/produce same-frame RenderGraph resources.

## 6. Reversed-Z is a required Hi-Z invariant

Modern Unity backends use reversed-Z. Custom depth and Hi-Z code must honor `SystemInfo.usesReversedZBuffer` / `UNITY_REVERSED_Z`. A wrong reduction/comparison can incorrectly remove visible terrain.

## 7. Prefer temporal/two-pass Hi-Z over blindly adding a full voxel depth prepass

A full depth prepass can duplicate substantial work. Start with previous-frame Hi-Z and conservative re-testing of uncertain/newly disoccluded candidates. False-visible work is acceptable; false-occluded geometry is not.

# Representation specialization

## 8. Heightfield-safe terrain path

A derived height/material field plus reusable GPU grid/clipmap may be cheaper than either SVDAG traversal or a general mesh for regions that can prove one relevant surface per X/Z column with no required caves/overhangs/destructive topology.

Treat this as an optimization, not authoritative state. Promote a region back to the general representation when topology changes.

## 9. Partitioned non-uniform mesh terrain

Unreal Engine 5.8's experimental Mesh Terrain is a useful 2026 signal: arbitrary 3D terrain, non-uniform resolution, spatial partitions, and local modifier rebuilds matter more than a traditional global heightfield.

The key lesson is bounded rebuild radius: a small edit should only invalidate nearby sections.

Sources:

- https://dev.epicgames.com/documentation/unreal-engine/mesh-terrain-in-unreal-engine
- https://dev.epicgames.com/documentation/unreal-engine/crafting-mesh-terrain-in-unreal-engine
- https://dev.epicgames.com/documentation/unreal-engine/unreal-engine-5-8-release-notes

## 10. Runtime virtual geometry / cluster hierarchy

If rasterized derived geometry remains important, evolve from one chunk mesh at one `SourceStep` toward local small-cluster hierarchies with screen-space error selection, GPU frustum/cone/Hi-Z culling, compact residency, and local edit rebuilds.

## 11. Cluster cone/backface culling

Once geometry is clustered, store conservative bounds plus a normal cone and reject clusters that cannot face the camera before rasterization.

# Lighting and shading

## 12. Clustered/Forward+ lighting first

Assign local lights to screen/frustum clusters and evaluate only the lights affecting the current pixel. Godot's Forward+ renderer is a production-friendly precedent.

## 13. Stochastic direct lighting is a future option

Unreal MegaLights demonstrates fixed-sample stochastic direct lighting with denoising and ray tracing. Interesting for very large numbers of dynamic shadowed lights, but much more complex and platform-sensitive than clustered lighting.

## 14. Variable Rate Shading

If profiling becomes fragment-bound, use VRS on supported platforms to reduce shading frequency for distant/low-importance regions without reducing depth/geometry coverage.

## 15. Visibility-buffer shading

Aokana and modern GPU-driven renderers reinforce separating visibility determination from expensive shading. Consider this if overdraw and material/light cost remain dominant; carefully measure tile-GPU bandwidth on Metal.

# Research survey

## Unity Virtual Mesh (0.2.0-preview, Dec 2025)

Unity's experimental `com.unity.virtualmesh` package is relevant reference code: GPU-driven LOD/culling, small triangle clusters, hierarchical cluster groups, screen-space error, two-pass occlusion, GPU page requests, async readback/I/O, placeholder geometry, compressed positions, persistent buffers, and RenderGraph integration.

**Recommendation:** use as architecture/reference code, not a drop-in renderer for destructible voxels.

Sources:

- https://github.com/Unity-Technologies/com.unity.virtualmesh
- https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/Documentation~/implementation.md
- https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/CHANGELOG.md

## Unreal Engine 5.8 / Nanite

Transferable lessons include quantized specialized geometry, fine-grained streaming with always-resident roots, screen/perceptual-error LOD, candidate/visible cluster buffers, two-pass occlusion, HLOD above fine virtual geometry, and atomic replacement of stale derived representations.

Nanite Landscapes reinforce a useful correctness model: authoritative/source representation remains valid while derived high-performance data rebuilds; publication happens only when the replacement is ready.

Sources:

- https://dev.epicgames.com/documentation/en-us/unreal-engine/nanite-in-unreal-engine
- https://dev.epicgames.com/documentation/en-us/unreal-engine/nanite-technical-details
- https://dev.epicgames.com/documentation/unreal-engine/using-nanite-with-landscapes-in-unreal-engine
- https://dev.epicgames.com/documentation/en-us/unreal-engine/world-partition---hierarchical-level-of-detail-in-unreal-engine

## Unreal Engine 5.8 Mesh Terrain

Mesh Terrain is a major 2026 direction toward arbitrary 3D terrain with spatial partitions and non-uniform detail. Its section-size/rebuild guidance directly supports bounded local derived-data partitions for destructible worlds.

## Unreal Virtual Shadow Maps and MegaLights

Virtual Shadow Maps apply page virtualization/caching to shadow depth. MegaLights replaces light-count-proportional work with a stochastic fixed sample budget per pixel plus denoising. Keep these as future references after geometry/visibility fundamentals are solved.

Sources:

- https://dev.epicgames.com/documentation/unreal-engine/virtual-shadow-maps-in-unreal-engine
- https://dev.epicgames.com/documentation/en-us/unreal-engine/megalights-in-unreal-engine

## Godot current rendering architecture

Godot reinforces simpler broadly-portable techniques: reverse-Z, clustered Forward+, automatic mesh LOD, screen-space LOD metrics, HLOD/visibility ranges, depth prepass, and instancing. Its CPU-oriented occlusion design is less suitable than GPU Hi-Z for our GPU-resident dynamic voxel path.

Sources:

- https://docs.godotengine.org/en/4.7/engine_details/architecture/internal_rendering_architecture.html
- https://docs.godotengine.org/en/latest/tutorials/3d/occlusion_culling.html
- https://docs.godotengine.org/en/latest/tutorials/3d/mesh_lod.html
- https://docs.godotengine.org/en/latest/tutorials/3d/visibility_ranges.html

# Research papers and transferable ideas

## Aokana: GPU-Driven Voxel Rendering for Open World Games (2025)

This is the most directly relevant renderer paper. It uses multiple shallow SVDAG chunks, separates geometry/color compression, supports LOD and streaming, uses previous-frame Hi-Z, screen-tile/chunk candidate pairs, a compact visibility buffer, and GPU voxel traversal. It was implemented in Unity and designed to coexist with mesh rendering.

The paper's biggest unresolved mismatch with our project is runtime editing: mostly-static voxels are the intended core case. That does **not** rule it out as our primary renderer; it means editable hierarchy maintenance is the decisive experiment.

**New recommendation:** test Aokana-style traversal as the **default** solid rendering architecture, not only as a far/complex secondary path.

Source: https://doi.org/10.1145/3728299

## Six Ways to Draw Vangers with WebGPU (August 2026)

This benchmark compares six rendering approaches over the same editable multi-layer terrain data path. A greedy fitted triangle mesh had the lowest mean frame time on the tested devices, but editability required large retained derived CPU/GPU structures and the second terrain layer/caves drove fitting complexity.

**Lesson:** compare steady-state frame cost, edit/update latency/rebuild radius, and retained CPU/GPU memory independently. This strongly supports benchmark-driven hybrid representation selection.

Source: https://arxiv.org/abs/2608.17390

## Editing Compact Voxel Representations on the GPU (Pacific Graphics 2024)

GPU hash tables make interactive editing of compact HashDAG-like voxel representations viable. This is directly relevant if editable Aokana/SVDAG maintenance becomes the blocker.

Source: https://doi.org/10.2312/pg.20241310

## Encoding Occupancy in Memory Location for Efficient and Compact High-Resolution Voxel Structures (2025/2026)

Encodes occupancy in memory location/addressing to reduce dependent node fetches while retaining compatibility with editable HashDAG-like structures.

**Lesson:** optimize bytes accessed and pointer-chasing depth, not only theoretical compression ratio.

Source: https://doi.org/10.1111/cgf.70292

## Dynamic Mesh Processing on the GPU (SIGGRAPH/TOG 2025)

Partitions dynamic meshes into small patches and performs topology/attribute updates in GPU shared memory.

**Lesson:** if raster cluster hierarchies remain, local simplification/remeshing after voxel edits may stay GPU-side rather than requiring CPU rebuilds.

Source: https://doi.org/10.1145/3731162

## End-to-End Compressed Meshlet Rendering (2024) and Real-time Meshlet Decompression (2025)

Transferable ideas: local quantization, compact connectivity, crack-safe boundaries, random-access cluster payloads, and decode-on-consume rather than permanently expanded representations.

Sources:

- https://doi.org/10.1111/cgf.15002
- https://doi.org/10.1016/j.cag.2025.104292

## Virtualized 3D Gaussians (SIGGRAPH 2025)

Although the primitive differs, hierarchical clusters plus projected-footprint/error selection independently recur, strengthening the case for hierarchy-based perceptual selection as a durable architecture.

Source: https://arxiv.org/abs/2505.06523

## Sparse Voxels Rasterization (2024)

Uses sparse voxels, adaptive LOD, and dynamic Morton ordering for coherent processing.

**Lesson:** test spatial/Morton ordering for mirror entries, voxel hierarchy nodes, candidates, and cluster pages.

Source: https://arxiv.org/abs/2412.04459

## NVIDIA RTX Mega Geometry / Cluster Acceleration Structures (2025)

Hardware trends are moving toward cluster-granular dynamic geometry and local rebuilds. Useful direction to watch, but too vendor/API-specific to drive the Unity architecture today.

Source: https://developer.nvidia.com/blog/fast-ray-tracing-of-dynamic-scenes-using-nvidia-optix-9-and-nvidia-rtx-mega-geometry/

# Resulting long-term target

The long-term target is intentionally representation-agnostic until the Aokana benchmark resolves the primary path:

```text
AUTHORITATIVE CPU VOXEL WORLD
Storage / gameplay / collision / edits
            |
            | compact change journal
            v
PERSISTENT GPU WORLD DATA
shared voxel mirror and/or editable shallow voxel hierarchy
            |
            | local change-driven work only
            v
PRIMARY PRESENTATION (benchmark decides)
Aokana-style voxel traversal
            OR
compressed dynamic surface clusters
            OR
hybrid selected by bounded region
            |
            +--> cheap planar/heightfield/procedural representation where proven useful
            |
            v
PER-CAMERA RENDERGRAPH
screen-space representation/LOD selection
GPU frustum
cone culling where applicable
previous/current-frame Hi-Z
visible region/cluster compaction
multi-command indirect or traversal dispatch
            |
            v
RASTER / VISIBILITY
compressed geometry decode where applicable
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
Long single-material flat ground. Validates planar/heightfield representation and worst-case unnecessary voxel traversal.

### Material-boundary strip
Flat ground with regular material/style/coating boundaries.

### Destruction strip
Flat ground with holes and repeated edits. Measures local invalidation/publication or editable-hierarchy maintenance.

### Smooth hill field
Tests smooth surface quality and traversal/mesh tradeoffs.

### Multi-layer/cave strip
At least two vertical solid intervals in some X/Z columns with tunnels/walls and local destruction.

### Cave/overhang field
Forces general 3D topology.

### Occlusion city/canyon
Many resident candidates with most geometry hidden.

### Dense-light interior
Measures clustered lighting and future stochastic-lighting need.

### Camera sprint
High-speed traversal to expose residency/page thrash, temporal-occlusion errors, and LOD/representation churn.

### Edit storm
Repeated local edits across one partition and then many neighboring partitions.

## Metrics

For every experiment record at minimum:

- CPU frame time;
- render-thread/submission time;
- GPU extraction or hierarchy-maintenance time;
- GPU visibility/culling/traversal time;
- GPU raster/shading time;
- dirty bytes/frame;
- resident authoritative/mirror/hierarchy bytes;
- retained method-specific CPU memory;
- retained method-specific GPU memory;
- vertex/index stride where applicable;
- pages/nodes allocated/used/wasted;
- candidate -> frustum -> occlusion -> drawn/traversed counts;
- nodes visited and dependent memory accesses per visible pixel for voxel traversal;
- generated vertices/indices/triangles for mesh paths;
- occupied draw commands and submission count;
- steady-state frame cost separate from edit-maintenance cost;
- edit rebuild/refit CPU and GPU time;
- sections/nodes dirtied per edit;
- dirty amplification per authoritative voxel changed;
- edit-to-visible latency;
- LOD/representation regeneration frequency;
- peak and steady-state residency;
- visual/oracle correctness;
- missing/false-occlusion count (must remain zero).

For RenderGraph work additionally record pass count/order, unsafe vs compute/raster passes, attachment load/store behavior, barriers/synchronization, async-compute overlap where used, and Metal/tile-GPU timing before/after restructuring.

# Decision rule

The renderer should move toward **work proportional to visible perceptual complexity and actual edits**, not world size, voxel count, or a fixed number of chunk/bucket submissions.

The strongest pattern across Unity Virtual Mesh, Unreal Nanite and Mesh Terrain, Godot's screen-space LOD/HLOD layering, Aokana, the 2026 Vangers comparison, and recent graphics research is consistent:

> compact spatial representations + bounded partitions + hierarchical screen-space selection + GPU visibility + fine-grained residency + local updates.

The newly elevated architecture question is whether **a compact editable voxel hierarchy should itself be the primary presentation representation**, eliminating most surface meshing, rather than merely supplementing the mesh renderer at distance.

Our unique advantage is that the authoritative data is already voxel-native. If Aokana-style traversal proves superior across representative gameplay—including destruction and Metal—we should exploit that directly. If simple coherent regions are still materially cheaper as meshes/heightfields, use a hybrid and specialize only where measurement justifies it.