# Voxel Rendering Architecture Migration Plan

**Status:** Proposed  
**Date:** 2026-08-25  
**Scope:** Custom voxel-world rendering ownership, lifecycle, visibility, batching, GPU resource management, and render-pipeline integration.

## 1. Purpose

The voxel project has multiple rendering needs with different execution models: extracted voxel terrain, vegetation/trees, structures and other generated world geometry, water, debug/world overlays, and ordinary Unity-rendered objects such as characters and UI.

This document proposes a migration toward a single **custom voxel-world rendering coordination boundary**, tentatively named `VoxelRenderWorld`, without turning that boundary into a universal wrapper around Unity rendering.

The intended result is not merely cleaner APIs. The architecture should make several performance properties possible:

- unchanged world state does not require repeated render preparation;
- unchanged render data does not require repeated CPU-to-GPU uploads;
- render CPU cost scales primarily with **changes plus the visible set**, not total world size;
- specialized renderers can share visibility, LOD, batching, resource-lifetime, and instrumentation infrastructure;
- GPU-driven techniques can be introduced selectively, with A/B measurements, instead of being architectural requirements.

The first migration steps deliberately preserve current rendering algorithms. Ownership and lifecycle should be cleaned up before changing where work executes.

## 2. Current-state observations and documentation caveat

The repository contains rendering documentation from different generations of the engine. In particular, `architecture-notes.md` describes a raymarch-oriented architecture (`VoxelRenderFeature`, `VoxelRenderPass`, `VoxelRaymarch.shader`, and `VoxelGPUData`), while the newer device-matrix documentation describes extracted geometry constraints such as per-chunk vertex/triangle caps and web-class index limits.

That mismatch is important: this proposal must not encode a historical rendering backend as the architecture.

The target boundary is therefore **backend-agnostic**. A renderable may ultimately be represented by an extracted mesh, persistent `GraphicsBuffer` allocations, instanced geometry, indirect draws, a specialized water pass, or another implementation. The coordination layer owns lifecycle and render submission policy; it does not require one geometry-generation or rendering technique.

Before implementation begins, Phase 0 below must produce a source-level inventory of every current custom draw/submission path and a performance baseline. That inventory becomes the authoritative migration checklist.

## 3. Target architecture

```text
World / Game systems
    |
    | semantic/renderable state and dirty changes
    v
+---------------------------------------------------+
|                 VoxelRenderWorld                  |
|                                                   |
|  persistent registrations                         |
|  resource ownership                               |
|  shared visibility data                           |
|  LOD policy                                       |
|  instrumentation                                  |
|  camera-specific render views                     |
|                                                   |
|   +----------------+  +----------------------+    |
|   | TerrainRenderer|  | VegetationRenderer   |    |
|   +----------------+  +----------------------+    |
|   +----------------+  +----------------------+    |
|   |StructureRenderer| | WaterRenderer        |    |
|   +----------------+  +----------------------+    |
|   +----------------+                               |
|   | DebugRenderer  |                               |
|   +----------------+                               |
+---------------------------+-----------------------+
                            |
                            v
                    URP / render passes
                            |
                            v
                           GPU
```

Unity-native rendering remains outside this ownership boundary:

```text
Characters ------> SkinnedMeshRenderer ---+
Ordinary props --> MeshRenderer -----------+--> Unity / URP --> GPU
Particles -------> ParticleSystem ---------+
UI --------------> Unity UI --------------+

Voxel world -----> VoxelRenderWorld -------+--> Unity / URP --> GPU
```

The boundary is specifically for custom voxel/world rendering. It should cooperate with Unity's render pipeline, not replace Unity rendering globally.

## 4. Ownership model

A core rule of the migration should be:

> Producers own world/semantic data. The rendering layer owns GPU render resources.

For example:

```text
TerrainChunk owns:
- voxel/density data
- generation state
- semantic chunk identity

VoxelRenderWorld / TerrainRenderer owns:
- GPU vertex/index storage
- instance data used only for rendering
- indirect-argument buffers
- render-only material/property state
- render-resource lifetime
```

This prevents ambiguous ownership during streaming and avoids situations where both a producer and renderer retain or dispose the same GPU resource.

Geometry generation is a separate responsibility. The architecture must allow both of these without changing the public ownership model:

```text
CPU extraction ----+
                   +--> persistent render representation --> draw submission
GPU extraction ----+
```

`VoxelRenderWorld` should not absorb Transvoxel/SDF/world-generation algorithms simply because their output is rendered.

## 5. API and lifecycle principles

The API must be lifecycle-oriented, not a per-frame wrapper around `Graphics.*` calls.

Conceptually:

```csharp
RenderHandle Register(RenderDescriptor descriptor);
void Update(RenderHandle handle, RenderUpdate update);
void Unregister(RenderHandle handle);
```

In practice, typed handles/descriptors are preferable where they prevent invalid combinations:

```csharp
TerrainRenderHandle RegisterTerrain(in TerrainRenderDescriptor descriptor);
void UpdateTerrain(TerrainRenderHandle handle, in TerrainRenderUpdate update);
void RemoveTerrain(TerrainRenderHandle handle);

VegetationRenderHandle RegisterVegetation(in VegetationRenderDescriptor descriptor);
void UpdateVegetation(VegetationRenderHandle handle, in VegetationRenderUpdate update);
void RemoveVegetation(VegetationRenderHandle handle);
```

The exact API should follow these constraints:

1. **Persistent registrations.** Static renderables are registered once and remain registered until changed or removed.
2. **Dirty updates.** Updates identify what changed rather than resubmitting the complete object whenever possible.
3. **No avoidable per-frame allocations.** Static-world frames should not allocate descriptors, arrays, matrices, or command objects merely to rediscover the same render state.
4. **Stable identities.** Handles remain valid across frames and are safe to map to pooled/arena GPU allocations.
5. **Explicit teardown.** Streaming/unloading removes registrations deterministically.
6. **Renderer-owned GPU lifetime.** Producers do not manipulate renderer-internal buffers after registration.
7. **Camera-independent world state.** Persistent render state is stored once; visibility/submission is computed per camera.
8. **Backend isolation.** World/game systems do not know whether a backend uses meshes, direct/indirect draws, compute culling, or another technique.

A transient submission API may still be useful for genuinely transient renderables, especially debug visualization, but it should not become the default path for persistent world content.

## 6. Non-goals

This migration should **not**:

- wrap `MeshRenderer`, `SkinnedMeshRenderer`, ParticleSystem, or Unity UI merely for architectural uniformity;
- create one monolithic renderer implementation containing terrain, water, foliage, generation, and simulation logic;
- move Transvoxel or other geometry generation to the GPU as part of the ownership refactor;
- require GPU culling, indirect rendering, or one giant GPU buffer;
- combine all terrain/vegetation/water work into one shader or one render pass;
- change visual output during the initial ownership migration;
- optimize before baseline measurements identify an affected cost.

## 7. Major design gotchas

### 7.1 Centralization can regress performance

Replacing a direct draw call with a wrapper gives essentially no performance benefit. If the wrapper reconstructs render descriptors, copies arrays, or uploads unchanged data every frame, it can be slower than the current code.

The architecture is only a performance enabler if it retains state across frames.

**Required property:** for a static camera and static world, voxel render preparation and CPU-to-GPU uploads should approach zero except for unavoidable frame/camera constants and actual draw/dispatch submission.

### 7.2 Generation and rendering must remain separate

The pipeline has distinct stages:

```text
voxel/world data
    -> surface/instance generation
    -> render representation
    -> visibility/LOD
    -> draw submission
```

The coordination layer should primarily own render representation, visibility/LOD, GPU resources, and submission. It must not become the home for every algorithm that happens to produce geometry.

This is particularly important after prior experiments where moving CPU-heavy Transvoxel work directly to the GPU did not automatically improve frame rate. Uploads, dispatch overhead, barriers, synchronization, and changed execution order can erase theoretical compute gains.

### 7.3 GPU execution is not automatically faster

Any GPU migration must account for:

- CPU-to-GPU upload volume;
- dispatch count and dispatch size;
- producer/consumer barriers;
- buffer transitions;
- synchronization points;
- readbacks;
- resource contention with rendering;
- whether the GPU or CPU was actually the frame bottleneck before the change.

GPU generation/culling should therefore be introduced behind the same architecture as an implementation choice and measured against the existing path.

### 7.4 Resource ownership must be unambiguous

During migration it is easy to create duplicate ownership of `Mesh`, `ComputeBuffer`, `GraphicsBuffer`, render textures, materials, property blocks, and indirect arguments.

Each migrated path needs an explicit ownership table covering:

- creator;
- updater;
- renderer/user;
- disposer;
- behavior on chunk unload;
- behavior on renderer/domain reload;
- behavior on failed or canceled generation.

A migrated system is not complete until resource teardown is tested under streaming, scene reload, and cancellation.

### 7.5 Avoid a giant-buffer requirement

A single giant GPU buffer is tempting but creates difficult streaming behavior if removing or resizing one chunk forces large copies or reallocations.

Initial implementations should favor stable per-resource allocations or pooled/arena allocation with reusable free regions. Compaction should be optional and measured.

Conceptually:

```text
GPU arena
[chunk A][chunk B][free][chunk D][vegetation...]
                    ^
                    reusable allocation
```

### 7.6 Custom rendering inherits responsibilities Unity previously handled

Moving a path away from ordinary Unity renderers may require explicit support for:

- frustum culling;
- shadow caster passes;
- depth/depth-normal passes;
- multiple cameras;
- Scene view/editor cameras;
- LOD selection;
- occlusion behavior;
- transparent sorting;
- shader/material variants;
- editor selection/picking where required;
- motion vectors if required later.

This is a strong reason to keep ordinary characters, props, particles, and UI on Unity-native renderers unless profiling demonstrates a specific need to move them.

### 7.7 Water needs specialized scheduling

Water may live under the common coordinator while retaining a distinct renderer/pass. Its transparency, depth/color dependencies, reflections/refractions, and ordering requirements differ from opaque terrain and structures.

The coordinator should provide shared lifecycle/instrumentation while allowing water to schedule into appropriate render-pipeline stages.

### 7.8 Multiple cameras require per-view visibility

There should not be a global render state that assumes one main camera.

```text
persistent VoxelRenderWorld
        |
        +--> Camera A view --> visibility/LOD --> submissions
        |
        +--> Camera B view --> visibility/LOD --> submissions
```

Persistent geometry/GPU state is shared where valid; visible sets and camera-dependent constants are view-specific.

### 7.9 Centralized LOD can create coupling if policy is too rigid

Terrain, vegetation, structures, and water do not necessarily share the same LOD metric or transition behavior. `VoxelRenderWorld` should own the common view/context and scheduling framework, while specialized renderers remain free to implement domain-appropriate LOD rules.

### 7.10 Debug/editor rendering can accidentally contaminate runtime hot paths

Debug visualization is useful, but debug registrations and editor support must be removable/disabled without adding iteration or branching over large debug collections in player builds.

## 8. Migration plan

### Phase 0 - Inventory and performance baseline

Before architectural changes, create an authoritative inventory of current custom render submission paths. For each path record:

- owning class/system;
- how geometry/instances are produced;
- what Unity/GPU API performs final submission;
- CPU/GPU resource ownership;
- update frequency;
- whether data is rebuilt or uploaded every frame;
- culling behavior;
- LOD behavior;
- shadow/depth/pass participation;
- camera assumptions;
- streaming/unload behavior.

Search specifically for direct custom submission/resource APIs and their wrappers, including whichever of `Graphics.*`, `CommandBuffer`, render-pass APIs, `GraphicsBuffer`, `ComputeBuffer`, `Mesh`, procedural/instanced draws, compute dispatch, and readback/synchronization APIs are actually present in the current source.

Establish reproducible profiler captures for at least:

1. a small/simple scene;
2. Voxel Showcase;
3. dense vegetation;
4. dense Kentridge/buildings;
5. maximum practical view distance;
6. movement/streaming stress.

Record at minimum:

| Metric | Purpose |
|---|---|
| Main-thread frame ms | render preparation competing with gameplay |
| Render-thread ms | submission/state overhead |
| GPU frame ms | determines whether CPU optimizations can improve FPS |
| Draw/dispatch count | batching and scheduling opportunity |
| Batches / SetPass changes | material/state pressure |
| Triangle/vertex count | geometry/LOD pressure |
| Instance count | vegetation/prop scale |
| CPU-to-GPU bytes/frame | redundant-upload opportunity |
| Compute dispatch count/time | GPU compute overhead |
| CPU/GPU synchronization stalls | high-risk hidden cost |
| Visible vs total chunks/cells | culling opportunity |
| Streaming p50/p95/p99 frame time | hitching/resource-lifetime impact |

**Exit gate:** baseline captures and source inventory are checked in or otherwise reproducible. No optimization claims are accepted without identifying which baseline metric should move.

### Phase 1 - Introduce the coordination API with no algorithm change

Add `VoxelRenderWorld` and the minimum typed registration interfaces needed by one existing opaque path.

Initially, the backend should call the same underlying rendering mechanism as the current implementation.

```text
Before:
producer -> current render submission

After:
producer -> VoxelRenderWorld -> adapter/backend -> same render submission
```

Do not add GPU culling, change extraction, merge buffers, or change shaders in this phase.

**Exit gate:**

- visual/replay output matches the old path;
- no meaningful CPU/GPU regression in baseline scenes;
- all resources survive load/unload/reload tests;
- the migrated producer no longer owns renderer-internal GPU lifetime.

A meaningful regression should block further migration until the API cost is understood.

### Phase 2 - Persistent state and dirty updates

Convert the migrated path from repeated frame reconstruction to persistent registrations.

Target behavior:

```text
load/change event -> build/update render representation -> upload dirty data
unchanged frames  -> no rebuild and no bulk re-upload
unload             -> release/recycle render allocation
```

Instrument bytes uploaded and number of registrations/updates/removals per frame.

**Exit gate:** static-world uploads and update work are near zero for migrated data.

### Phase 3 - Shared coarse visibility framework

Introduce common world bounds/spatial keys and camera-view inputs so render backends do not independently rediscover the same coarse visible world set.

Start with simple CPU-side coarse culling. Do not begin with GPU culling.

```text
camera frustum
    -> coarse world cells/pages/chunks
    -> visible candidates
    -> backend-specific culling/LOD
```

Specialized renderers retain fine culling and LOD behavior.

**Exit gate:** visible-set correctness is verified, multiple cameras behave correctly, and CPU cost is lower or neutral.

### Phase 4 - Vegetation batching and submission optimization

Vegetation is the first optimization target after the architecture is stable because high instance counts make CPU submission/culling overhead a likely opportunity.

Group compatible instances by dimensions such as:

- render asset/species;
- material/shader variant;
- LOD;
- shadow behavior;
- spatial cell where useful for culling/streaming.

Evaluate in order:

1. persistent instance data;
2. CPU culling with batched/indirect submission;
3. GPU culling/compaction only if CPU culling/submission remains significant.

Each step is a separate A/B experiment.

**Exit gate:** measured improvement in the dense-vegetation baseline without increasing GPU frame time enough to negate CPU gains.

### Phase 5 - Terrain resource ownership and visibility

Move terrain GPU resource lifetime fully behind the terrain backend while keeping generation/extraction independently replaceable.

Preserve chunk/LOD units where they remain useful for streaming and culling; do not combine all terrain into one allocation/draw merely for draw-count reduction.

Focus first on:

- persistent allocations;
- dirty-region uploads;
- stable unload/reuse;
- coarse visibility;
- LOD decisions using common view context;
- streaming frame-time spikes.

**Exit gate:** equal visuals, stable memory under repeated traversal, and improved or neutral average frame time with no p95/p99 streaming regression.

### Phase 6 - Structures and other persistent voxel/SDF world objects

Migrate structures and long-lived generated props through the same lifecycle model.

Static buildings should become very cheap CPU-side once registered: the world owns their semantic existence while render resources remain persistent until geometry changes or the object unloads/destroys.

Avoid making the renderer responsible for procedural-building generation.

### Phase 7 - Formal URP/render-pass integration

Once resource ownership is stable, make pass scheduling explicit so custom rendering interoperates predictably with Unity-rendered characters and props.

Conceptually:

```text
shadow/depth
  - Unity-native objects
  - applicable voxel-world backends

opaque
  - Unity-native objects
  - terrain/structures/opaque vegetation

transparent/specialized
  - water and applicable voxel effects

post/UI
```

This phase should document ordering/dependency requirements for every backend.

### Phase 8 - Selective GPU-driven experiments

Only after profiling shows a remaining CPU submission/culling bottleneck should the project test:

- GPU frustum/occlusion culling;
- GPU LOD selection;
- instance compaction;
- indirect argument generation;
- other GPU-driven scheduling.

Each experiment must compare against the previous implementation using the same captures and record CPU time, GPU time, upload bandwidth, dispatch overhead, and synchronization.

A GPU path that is architecturally elegant but slower should not be retained as the default.

### Phase 9 - Enforce the ownership boundary

After all intended custom world paths are migrated, make the architectural rule explicit:

> Game/world/generation systems must not perform direct custom render submission or own renderer-internal GPU resources. They publish/register renderable state through the voxel rendering boundary.

Unity-native renderer usage remains allowed by design for objects intentionally owned by Unity rendering.

Consider an automated source/architecture check only after the allowed exceptions are well understood; avoid a brittle rule that bans legitimate Unity renderer use.

## 9. Performance expectations

The following ranges are **engineering hypotheses**, not commitments. They describe the affected cost or plausible whole-frame impact when the corresponding bottleneck is present. They are deliberately non-additive.

| Change | Expected affected-cost change | Plausible whole-frame impact | Main prerequisite |
|---|---:|---:|---|
| API centralization alone | ~0% | ~0% | none; architectural only |
| Persistent registrations | 20-80% less repeated render-prep work where currently rebuilt | 0-15% | repeated CPU prep is measurable |
| Dirty-only GPU updates | 50-95% fewer uploads for mostly static world data | 0-15% | redundant uploads are present |
| Shared/coarse culling | 20-70% fewer submitted candidates in favorable scenes | 2-20% | substantial off-screen population |
| Vegetation batching | 2-5x lower CPU submission cost is plausible | 5-25% | CPU/render thread is instance-submission bound |
| Better terrain resource lifetime | large reduction in allocation/upload spikes is possible | modest average; potentially large p95/p99 improvement | streaming currently reallocates/reuploads significantly |
| Indirect submission | substantial CPU draw-submission reduction | 0-20% | draw submission is CPU bound |
| GPU culling | can regress or improve substantially | negative to +20% | very high instance counts and spare GPU capacity |
| Pass consolidation/scheduling | lower redundant pass/state/bandwidth work | 0-10% | redundant work exists |
| Better centralized LOD inputs | 20-70% less rendered geometry in favorable views | 0-30% | GPU is geometry/vertex bound |

These ranges must not be summed. Multiple changes often attack the same frame-time component, and Amdahl's law limits whole-frame gains.

### Expected priority of gains

Before baseline measurements, the most likely order of opportunity for this game's world scale is:

1. **Vegetation submission and culling** - potentially high instance counts make repeated CPU work expensive.
2. **Eliminating redundant rebuild/upload work** - static procedural world content should not be reconstructed for rendering every frame.
3. **Terrain/chunk visibility and LOD** - especially at long view distances.
4. **Streaming/resource lifetime** - likely more important for p95/p99 hitches than average FPS.
5. **Formal pass integration and redundant-pass elimination.**
6. **API centralization itself** - effectively zero direct performance value.

Baseline data may reorder this list.

## 10. Performance acceptance criteria

The migration should optimize for frame-time components rather than raw FPS alone.

### Static world / static camera

Target properties:

- near-zero world-side registration/update activity;
- near-zero bulk CPU-to-GPU uploads for unchanged renderables;
- no avoidable managed allocations in voxel render preparation;
- stable GPU memory use;
- stable draw/dispatch count.

### Moving camera, unchanged world

Target properties:

- CPU work scales with visibility transitions and the visible candidate set;
- geometry is not regenerated because the camera moved;
- GPU data is not re-uploaded merely because visibility changed unless the backend specifically requires a compacted visible buffer and profiling justifies it.

### Streaming / world edits

Target properties:

- work scales with changed/loaded/unloaded regions;
- allocations are reused where practical;
- no global buffer rebuild for local chunk changes;
- p95/p99 frame time improves or remains neutral;
- cancellation/unload cannot leave stale handles or leaked GPU resources.

## 11. Instrumentation required in the new boundary

`VoxelRenderWorld` should expose profiler markers/counters from the beginning. Useful counters include:

- registered renderables by backend;
- visible renderables by backend/camera;
- registrations, updates, removals per frame;
- dirty bytes uploaded per backend;
- GPU bytes allocated/resident/free in render pools;
- draw and dispatch counts;
- instances submitted/culled;
- LOD population;
- time spent in coarse visibility;
- time spent in each backend's render preparation;
- streaming allocations/reuses/frees.

This instrumentation is part of the architecture, not a later cleanup task. Without it, centralization makes behavior harder rather than easier to diagnose.

## 12. Rollout and rollback strategy

Migrate one backend/path at a time.

For each path:

1. preserve the old implementation or an adapter long enough for A/B comparison;
2. move ownership/lifecycle without changing rendering algorithm;
3. verify visuals/replays;
4. measure baseline scenes;
5. only then introduce optimizations;
6. remove the old path after the new path is both correct and measured.

Do not combine these changes in one experiment:

- ownership/API migration;
- CPU-to-GPU algorithm migration;
- shader rewrite;
- buffer-layout rewrite;
- GPU culling.

Separating them makes regressions attributable and prevents repeating the failure mode where a theoretically faster GPU implementation becomes slower because multiple transfer/synchronization changes happened at once.

## 13. First implementation milestone

The recommended first code milestone is intentionally narrow:

1. complete Phase 0 inventory/profiling;
2. add `VoxelRenderWorld` with persistent typed handles;
3. migrate **one opaque terrain render path** through it;
4. keep the existing geometry-generation and final rendering algorithm unchanged;
5. transfer renderer-internal GPU resource lifetime to the rendering layer;
6. verify identical visual output;
7. verify essentially neutral CPU/GPU performance;
8. measure update/upload activity in static and streaming scenarios.

If this step cannot be made essentially performance-neutral, stop and fix the boundary before migrating additional systems.

The next milestone should be persistent/dirty updates on that path, followed by vegetation, where the first meaningful FPS gain is more likely.

## 14. Open design decisions to resolve during Phase 0/1

The implementation should answer these from current code and profiler evidence rather than assumption:

- What are the complete current custom render-submission paths and owners?
- Which renderables currently rebuild CPU descriptors or matrices every frame?
- Which GPU resources are persistent today, and which are reallocated/reuploaded?
- Which existing systems already perform coarse/fine culling and LOD well enough to retain?
- What is the correct URP hook/pass model for terrain, vegetation, structures, water, and debug rendering?
- Which cameras must be supported in runtime and editor contexts?
- What shadow/depth/depth-normal behavior is required for each backend?
- What render-resource pooling/arena strategy best matches chunk sizes and streaming churn?
- Should spatial visibility consume an existing world-page/spatial structure rather than introduce a renderer-specific spatial index?
- Which data belongs in shared render-world records versus specialized backend records?
- Which debug/editor capabilities rely on Unity renderer components today?
- What thresholds would justify indirect submission or GPU culling?

## 15. Architectural success criteria

The architecture is successful when these statements are true:

1. World/game systems can express that something exists, changed, or disappeared without knowing how Unity/GPU submission is performed.
2. Custom voxel-world GPU resources have a clear renderer-side owner and deterministic lifetime.
3. Static world content does not create repeated CPU preparation or data uploads merely because another frame is rendered.
4. Visibility and camera context are shareable without forcing all backends to use the same fine-culling or LOD algorithm.
5. Terrain, vegetation, structures, water, and debug rendering remain specialized implementations rather than one monolithic renderer.
6. Unity-native characters, ordinary meshes, particles, and UI remain free to use Unity's rendering systems directly.
7. CPU/GPU algorithm choices can be changed behind the boundary and compared with instrumentation.
8. Performance work is driven by measured bottlenecks and reproducible captures, not by the architectural refactor itself.

The central principle is:

> **The world says what exists and what changed. The voxel rendering system owns the persistent render representation and decides how and when it is drawn.**
