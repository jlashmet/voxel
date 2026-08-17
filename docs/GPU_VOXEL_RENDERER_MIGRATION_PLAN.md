# GPU Voxel Renderer Migration Plan

Status: **ACTIVE WORKING TRACKER**  
Branch: `feature/renderer-lod-hole-regression`  
PR: #87 `Gate renderer holes across voxel LOD transitions`

This file is the source of truth for the renderer migration. Tasks are checked off only when the implementation and its acceptance tests are complete on this branch. If implementation changes the design, update this document in the same commit.

## 1. Goal

Build a destruction-first voxel renderer that keeps authoritative world state on the CPU while moving derived presentation work to the GPU:

- voxel edits remain cheap and deterministic;
- no visible terrain/castle holes are permitted while fine detail is pending;
- rendering work scales with changed/visible bricks rather than total discovered world size;
- CPU-generated vertex/index geometry and large CPU->GPU geometry uploads are eliminated;
- LOD refinement degrades detail, never coverage;
- destruction can touch many bricks without forcing a synchronous remesh wall;
- the renderer remains compatible with headless/server gameplay, persistence, networking, physics, AI queries, and save/load.

The target architecture is **CPU-authoritative voxels + GPU-resident derived rendering**. The GPU is not authoritative world state.

## 2. Non-goals

- Do not move gameplay truth, save data, networking truth, or deterministic voxel edits exclusively to GPU memory.
- Do not require GPU readback for normal rendering or gameplay queries.
- Do not hide voxel renderer gaps with far terrain.
- Do not increase view-distance exclusions or reduce fidelity to make tests pass.
- Do not solve correctness by giving the renderer an effectively unlimited frame budget.
- Do not preserve the current eager `known => dirty => build` policy merely because it already exists.
- Do not require one meshing algorithm for both smooth density surfaces and predominantly planar/cubic structures.

## 3. Architectural invariants

### 3.1 Coverage invariant

For every visible voxel-space region inside the voxel renderer's ownership area, one complete active representation must exist:

1. the desired fine representation, or
2. a complete coarser ancestor/fallback representation.

A missing fine mesh is never permission to draw nothing.

`fine not ready => render parent`, not `fine not ready => hole`.

### 3.2 Atomic refinement invariant

A parent may be replaced by finer children only when the complete child coverage set is ready. For a 2x spatial refinement this means the eight child coordinates are each either:

- GPU-ready with drawable geometry, or
- definitively known empty for the current voxel generation.

Until that condition is true, the parent remains active. When merging outward, children remain active until the parent is ready.

The first implementation should use an atomic switch with no overlap. Cross-fading can be added later if needed for visual quality.

### 3.3 Budget invariant

Reducing build/upload/compute budgets may delay refinement but must not introduce visible holes after an initial coarse-coverage baseline exists.

### 3.4 Authority invariant

CPU owns:

- sparse voxel storage;
- edits/destruction decisions;
- semantic materials and gameplay metadata;
- persistence/save/load;
- multiplayer replication;
- structural/connectivity simulation;
- gameplay/AI queries;
- CPU-side collision truth required by gameplay.

GPU owns derived rendering state:

- streamed voxel brick mirror/cache;
- render mips/summaries;
- surface classification;
- smooth/planar surface extraction;
- transition geometry/data;
- meshlet/page allocation;
- LOD selection/refinement state;
- frustum/occlusion culling;
- indirect draw command generation;
- render-only residency.

### 3.5 No-readback invariant

The production render path must not read generated mesh geometry back to CPU. GPU-generated vertices/indices/meshlets remain GPU-resident through drawing.

## 4. Reference architecture

The migration combines several proven ideas rather than inventing a new rendering model:

- **Transvoxel** for crack-free smooth transitions between differing voxel resolutions: https://transvoxel.org/
- **Godot Voxel** as an inspectable hierarchical smooth-voxel/Transvoxel implementation: https://github.com/Zylann/godot_voxel
- **Voxel Plugin / Unreal** progressive refinement concept: regenerate coarse coverage first and refine asynchronously instead of exposing holes while detailed work is pending: https://docs.voxelplugin.com/
- **Geometry clipmaps / hierarchical LOD** principle: nested coverage with graceful degradation under limited update budget.
- **Teardown** as the destruction-performance north star: rendering architecture should make voxel edits native and localized rather than forcing world-scale synchronous conventional remeshing.

These references validate the broad direction; this repository's implementation remains tailored to Unity, sparse bricks, the existing material system, and the current GPU geometry arena/indirect renderer.

## 5. Current failure mode

PR #87 regression work exposed the current architecture's central correctness problem:

- LOD 1/2/4/8 are treated as mutually exclusive distance shells.
- If the shell-selected representation is not ready, a coarser representation is not allowed to cover it.
- discovery currently tends to make newly known chunks dirty eagerly;
- thousands of chunks can enter the dirty/build pipeline before they are actually required for visible coverage;
- arena pressure can evict an offscreen chunk and immediately mark it dirty again, creating churn;
- `MissingVisibleSolidChunks` only counts already-known coordinates, so an undiscovered visible coordinate can be absent without appearing in that metric;
- far-terrain ownership follows voxel/storage residency rather than drawable voxel-renderer coverage.

The observed result is backlog + allocation pressure becoming literal rectangular sky holes instead of temporary lower detail.

The GPU migration does not remove the need to fix this logical coverage model first. The same parent/fallback invariant must exist regardless of whether the mesher is CPU or GPU.

## 6. Target data flow

```text
CPU authoritative sparse voxel world
        |
        | compact brick deltas + generation/version
        v
GPU voxel brick cache
        |
        +--> mip/occupancy/material summaries
        |
        +--> desired hierarchical LOD selection
        |
        +--> dirty visible meshlet/page worklist
        |
        +--> compute surface extraction
        |       +--> planar/cubic path
        |       +--> smooth Transvoxel path
        |
        +--> GPU page allocator / meshlet metadata
        |
        +--> active parent/child coverage selection
        |
        +--> frustum + optional Hi-Z occlusion culling
        |
        +--> indirect draw argument compaction
        v
DrawProcedural/Indirect rendering
```

Normal frame rendering should require no CPU vertex/index generation and no mesh readback.

## 7. Spatial model

Retain the existing powers-of-two sample steps (`1/2/4/8`) during migration so storage and meshing behavior can be compared against the current renderer.

A chunk at source step `S` spans twice the world size of a chunk at `S/2` in each axis. The eight finer children of coarse coordinate `C` are:

```text
child = 2*C + (x,y,z), x/y/z in {0,1}
```

When deriving parent coordinates from negative child coordinates, use floor division, never C# integer truncation toward zero.

Distance bands become **desired refinement levels**, not exclusive render permission shells.

## 8. GPU representation

### 8.1 Brick mirror

The CPU sends only changed brick payloads plus generation/version metadata. GPU storage should be page/slot based so moving one logical brick does not require repacking the entire world.

Required metadata per resident brick/node should include at minimum:

- logical brick coordinate;
- storage slot/page index;
- content generation/version;
- occupancy/surface summary;
- material summary as needed by meshing;
- dirty flags for dependent mip/render levels.

### 8.2 Render hierarchy

Maintain explicit render nodes for the active LOD hierarchy. Each node tracks:

- coordinate and sample step;
- parent ID;
- child IDs or derivable child coordinates;
- desired-refinement state;
- source voxel generation;
- generated render generation;
- ready/empty/pending state;
- active/inactive state;
- meshlet/page handles;
- transition-neighbor mask or equivalent runtime seam state.

### 8.3 Meshlet/page allocator

Do not require one contiguous allocation per traditional chunk.

Prefer fixed-size GPU pages/meshlet slabs with:

- free-list allocation;
- per-node page chains or ranges;
- deferred free after active representation swap;
- allocator telemetry;
- a reserved headroom policy for refinement staging.

Active coverage pages are not eviction candidates. Inactive/cold pages can be evicted without immediately re-enqueueing them for rebuild.

## 9. Meshing strategy

### 9.1 Smooth terrain

Use a compute implementation of the current smooth surface rules/Transvoxel-compatible topology. During migration the existing CPU mesher is the correctness oracle.

Prefer a bounded-output meshlet/page strategy over giant global append buffers. If exact sizing is required, use a count/prefix/write sequence. If bounded meshlets are sufficient, atomically allocate pages and emit local meshlets.

### 9.2 Planar/cubic structures

Castle/building geometry should not automatically pay the full smooth-density meshing cost where the data is semantically planar/cubic.

Add a specialized path for suitable material/surface modes using greedy face extraction or an equivalent GPU face-merging path. The output still enters the same meshlet/page allocator and active LOD hierarchy.

Do not compromise arbitrary destruction: editing a planar structure must still invalidate only affected bricks/pages and their necessary boundary neighbors.

### 9.3 Boundaries and transitions

Current transition generation is coupled too tightly to fixed camera distance thresholds. The target system derives seam requirements from **actual active neighboring LODs**.

Preferred end state:

- surface generation produces reusable transition-capable data (for example all needed transition faces/secondary positions or equivalent);
- render-node activation supplies the active neighbor mask;
- moving the camera across an LOD boundary does not require a full remesh merely to change which transition side is visible.

## 10. Scheduling and refinement

The renderer must separate these concepts:

- discovered/known voxel space;
- desired render coverage;
- requested build work;
- active drawable coverage.

Discovery must not automatically enqueue every known chunk for expensive rendering work.

Priority order:

1. **P0 missing visible coverage**: establish a coarse drawable ancestor immediately.
2. **P1 coverage preservation**: parent/merge work needed before currently active children may be retired.
3. **P2 visible refinement**: produce desired child detail for visible regions.
4. **P3 lookahead/prefetch**: small camera-motion fringe.
5. **Cold/offscreen discovered data**: no render build unless needed for another explicit reason.

When a massive edit occurs, progressive refinement should look like:

```text
coarse coverage restored/updated
        -> medium visible refinement
        -> fine visible refinement
        -> fringe/offscreen refinement only if useful
```

It must not look like thousands of equal-priority dirty chunks competing before any complete view is established.

## 11. Destruction flow

For an edit/explosion:

1. CPU applies authoritative voxel changes.
2. CPU increments affected brick generations and records a compact dirty-brick set.
3. CPU uploads only changed brick payloads/metadata.
4. GPU updates dependent render summaries/mips.
5. Active old render coverage remains drawable until a replacement generation is ready.
6. GPU prioritizes replacement coverage for visible affected nodes.
7. GPU progressively refines toward desired LOD.
8. Once replacement coverage is complete, activation swaps atomically and old pages become reclaimable.

Render cost should be proportional to the affected and visible surface neighborhood, not to all discovered chunks.

## 12. Collision and simulation

Rendering and collision are intentionally decoupled.

Near gameplay-critical space can continue to maintain CPU collision representations asynchronously. Far regions do not need CPU collision meshes merely because they are renderable.

Future optimization options include:

- voxel-native collision queries against authoritative CPU bricks;
- simplified near-field collider generation;
- promotion of disconnected debris to ordinary rigid bodies only when gameplay-relevant.

GPU rendering must never become a prerequisite for a headless server to answer whether a voxel is solid.

## 13. Far terrain handoff

Far terrain may only cut a hole where voxel rendering has complete drawable coverage. Storage residency alone is insufficient.

The final handoff should use render-coverage state. A conservative scalar radius is acceptable initially if it only advances after all required inner coverage is drawable; a spatial mask/tileset is preferable if a scalar becomes too restrictive.

## 14. Migration strategy

The migration is intentionally incremental so the project stays debuggable.

### Phase A - make coverage logically correct on the current renderer

Add hierarchical parent/fallback activation and demand-driven scheduling while retaining CPU extraction. This isolates correctness from the GPU rewrite and creates an oracle for later stages.

### Phase B - introduce GPU voxel mirror and compute extraction behind a feature gate

Mirror authoritative bricks to GPU and generate geometry in compute while keeping the CPU renderer available for comparison.

### Phase C - GPU allocator and production activation

Keep generated geometry entirely in GPU pages/meshlets and draw indirectly. Eliminate CPU-generated production geometry uploads.

### Phase D - GPU-driven hierarchy/culling

Move desired LOD traversal, visibility compaction, and indirect command generation to GPU where beneficial, keeping only coarse control/telemetry on CPU.

### Phase E - specialized meshing and startup optimization

Add planar/cubic fast path, optional baked coarse startup render data, and further GPU occlusion/streaming optimization.

## 15. Validation strategy

### 15.1 Correctness gates

- Fixed camera coverage at source steps 1/2/4/8.
- Continuous camera traversal across real 96m/192m/288m boundaries with no convergence pause.
- Tiny refinement-budget test: detail may lag, coverage may not disappear.
- Massive destruction test: active old generation remains until complete replacement is ready.
- Arena/page-pressure test: visible parent/fallback coverage survives pressure.
- Negative-coordinate parent/child mapping tests.
- Known-empty child counts as complete coverage for atomic refinement.
- Previously empty child becoming solid invalidates the empty proof correctly.
- Far terrain does not expose sky before voxel drawable coverage exists.

### 15.2 CPU-vs-GPU oracle tests

For representative bricks and transition configurations:

- identical inside/outside classification;
- compatible surface topology/winding;
- material assignment parity;
- transition boundary parity;
- empty/non-empty parity;
- bounded geometric error where exact vertex placement is intentionally changed.

### 15.3 Performance gates

Track at minimum:

- changed voxel bytes uploaded/frame;
- GPU brick-cache residency;
- dirty render-node count by priority;
- active fallback-parent count;
- pending refinement count;
- generated meshlets/pages/frame;
- allocator occupancy and allocation failures;
- geometry bytes generated/frame;
- compute time by pass;
- visible meshlets before/after culling;
- indirect draw count;
- time from edit -> coarse replacement ready;
- time from edit -> desired fine representation ready;
- CPU main-thread renderer cost;
- CPU worker renderer cost.

Success is not simply higher peak throughput. The primary target is predictable bounded frame cost with graceful detail degradation.

## 16. Rollout and rollback

Until GPU parity is established:

- retain the CPU mesher behind a debug/development feature switch;
- support side-by-side deterministic test inputs;
- do not maintain two independent long-term scheduling architectures: hierarchical coverage state should be shared conceptually;
- remove the old production CPU geometry path only after GPU correctness, destruction, LOD traversal, and pressure tests are green.

## 17. Task tracker

The checkboxes below are the execution order. A task is complete only when code, tests, and tracker notes are committed.

### T0 - Establish plan and failing regression baseline

- [x] **T0.1** Add production-render-path LOD coverage regression tests for castle and terrain.
- [x] **T0.2** Put renderer coverage gate before broad PlayMode shards so unrelated runner memory failures cannot hide it.
- [x] **T0.3** Confirm current exclusive-shell renderer fails the new gate and capture dirty/missing/arena diagnostics.
- [x] **T0.4** Research established hierarchical/progressive-refinement voxel architectures and choose CPU-authoritative + GPU-derived rendering direction.
- [x] **T0.5** Check this proposal/tracker into PR #87.
- [ ] **T0.6** Remove the temporary CI-only demand-probe script/workflow injection once source implementation replaces it.

### T1 - Hierarchical coverage contract on current CPU renderer

- [x] **T1.1** Add tested floor-safe parent/child coordinate mapping for steps 1/2/4/8.
- [ ] **T1.2** Represent render-node completion as `Ready`, `KnownEmpty`, or incomplete for a specific source generation.
- [ ] **T1.3** Add explicit parent/fallback active-coverage state above the per-step caches.
- [ ] **T1.4** Change distance bands from exclusive render shells to desired refinement levels.
- [ ] **T1.5** Keep a parent active until all required finer children are complete; atomically switch parent -> children.
- [ ] **T1.6** Keep children active until replacement parent is complete while moving outward; atomically switch children -> parent.
- [ ] **T1.7** Add negative-coordinate, known-empty, inward-transition, and outward-transition unit/PlayMode tests.
- [ ] **T1.8** Make the no-hole traversal gate pass under intentionally constrained CPU build/upload budgets.

### T2 - Demand-driven CPU scheduling and residency cleanup

- [ ] **T2.1** Split `known/discovered` from `requested/dirty` in `CpuTransvoxelChunkCache`.
- [ ] **T2.2** Stop `DiscoverSurfaceBricks` from eagerly invalidating/building every newly known chunk.
- [ ] **T2.3** Add explicit coverage/refinement build requests with P0/P1/P2/P3 priorities.
- [ ] **T2.4** Ensure mutations invalidate generation proofs without flooding cold/offscreen render work.
- [ ] **T2.5** Prevent active coverage geometry from arena-pressure eviction.
- [ ] **T2.6** Evict inactive cold geometry without immediately `MarkDirty`-ing it again.
- [ ] **T2.7** Add renderer telemetry for active coverage, fallback parents, requested work by priority, cold known nodes, staging bytes, and queue latency.
- [ ] **T2.8** Demonstrate startup requested-dirty counts are proportional to visible coverage rather than all discovered surface chunks.

### T3 - Transition and far-terrain correctness

- [ ] **T3.1** Derive transition requirements from actual active neighboring LODs rather than fixed `MinViewDistance` shell assumptions.
- [ ] **T3.2** Define reusable transition-capable mesh data so camera LOD changes do not require unnecessary full remeshes.
- [ ] **T3.3** Keep far terrain visible until voxel render coverage is drawable; stop using storage residency as the sole hole criterion.
- [ ] **T3.4** Add transition-mask and far-terrain handoff regression tests.

### T4 - GPU brick mirror foundation

- [ ] **T4.1** Define compact CPU->GPU brick delta format with coordinate, slot, generation, voxel/material payload metadata.
- [ ] **T4.2** Implement GPU brick slot/page allocator and logical-coordinate lookup.
- [ ] **T4.3** Upload changed bricks only; no full-world reupload on ordinary edits.
- [ ] **T4.4** Build GPU occupancy/surface summaries and mip invalidation/update path.
- [ ] **T4.5** Add debug validation comparing sampled GPU brick content/summaries against CPU authoritative data without putting readback in production frame flow.
- [ ] **T4.6** Add residency/bytes/upload telemetry and stress tests for edit bursts.

### T5 - GPU smooth meshing prototype

- [ ] **T5.1** Port cell classification/density/material sampling needed by smooth meshing to compute.
- [ ] **T5.2** Implement bounded GPU output sizing strategy (meshlet pages or count/prefix/write).
- [ ] **T5.3** Generate smooth base-cell geometry into GPU-resident buffers.
- [ ] **T5.4** Generate Transvoxel-compatible transition geometry/data on GPU.
- [ ] **T5.5** Add CPU-vs-GPU oracle tests for representative terrain, caves, edits, materials, and transition boundaries.
- [ ] **T5.6** Prove production meshing path requires no generated-geometry GPU readback.

### T6 - GPU meshlet/page arena

- [ ] **T6.1** Define fixed-size geometry page/meshlet format and metadata layout.
- [ ] **T6.2** Implement GPU free-list allocation and deferred frees.
- [ ] **T6.3** Allow active parent + staging children to coexist during refinement.
- [ ] **T6.4** Reserve allocator headroom for visible replacement/refinement work.
- [ ] **T6.5** Make active coverage non-evictable and cold inactive pages reclaimable.
- [ ] **T6.6** Add allocator-pressure tests proving no visible holes under constrained capacity.

### T7 - GPU production activation and indirect drawing

- [ ] **T7.1** Bind generated page/meshlet metadata directly to indirect/procedural rendering.
- [ ] **T7.2** Generate/compact draw arguments without CPU geometry enumeration.
- [ ] **T7.3** Integrate hierarchical parent/child active coverage with GPU-generated geometry generations.
- [ ] **T7.4** Atomically activate replacements only after complete coverage is GPU-ready.
- [ ] **T7.5** Keep CPU mesher available only as development oracle/fallback while validation matures.
- [ ] **T7.6** Make the full renderer regression suite pass on the GPU path.

### T8 - GPU-driven LOD and visibility

- [ ] **T8.1** Move desired hierarchical LOD selection to GPU or a GPU-friendly compact traversal where profiling proves it beneficial.
- [ ] **T8.2** Add GPU frustum culling over active meshlets/pages.
- [ ] **T8.3** Add optional Hi-Z occlusion culling after frustum path is stable.
- [ ] **T8.4** Compact visible indirect draw commands entirely on GPU.
- [ ] **T8.5** Measure CPU frame cost and prove renderer scheduling/enumeration no longer scales with total resident render nodes.

### T9 - Planar/cubic fast path

- [ ] **T9.1** Define eligibility rules for planar/cubic surface extraction without changing authoritative voxel semantics.
- [ ] **T9.2** Implement GPU greedy-face or equivalent merged-face extraction.
- [ ] **T9.3** Route suitable castle/building bricks through the cheaper path and smooth terrain through smooth/Transvoxel path.
- [ ] **T9.4** Add destruction-boundary tests where edits cross planar/smooth or material-mode boundaries.
- [ ] **T9.5** Benchmark castle rebuild cost against the smooth-only path.

### T10 - Destruction and gameplay stress validation

- [ ] **T10.1** Add large explosion/edit burst benchmark across many visible bricks.
- [ ] **T10.2** Assert old active generation remains visible until replacement coverage is complete.
- [ ] **T10.3** Measure edit -> coarse replacement latency and edit -> desired refinement latency.
- [ ] **T10.4** Stress repeated destruction while traversing LOD boundaries.
- [ ] **T10.5** Stress allocator pressure while destroying and streaming simultaneously.
- [ ] **T10.6** Verify CPU collision/gameplay queries remain independent of GPU render readiness.

### T11 - Startup and world-streaming optimization

- [ ] **T11.1** Profile startup after GPU path is production-correct before adding new bake data.
- [ ] **T11.2** If still worthwhile, extend baked showcase data with coarse startup render pages/summaries.
- [ ] **T11.3** Ensure runtime edits invalidate baked render data cleanly and re-enter normal GPU generation.
- [ ] **T11.4** Validate streaming in/out does not break parent/fallback coverage.

### T12 - Production cutover and cleanup

- [ ] **T12.1** Make GPU renderer the default production path after parity/performance gates are green.
- [ ] **T12.2** Remove temporary PR diagnostic probe and obsolete workflow patching.
- [ ] **T12.3** Remove obsolete production CPU geometry upload/extraction plumbing that is no longer needed by tests/oracle mode.
- [ ] **T12.4** Retain a minimal deterministic CPU reference mesher only if it provides ongoing test value.
- [ ] **T12.5** Update `docs/ARCHITECTURE.md` and `docs/ASYNC_GEOMETRY_PIPELINE.md` to describe final production architecture.
- [ ] **T12.6** Run all EditMode, PlayMode shards, architecture boundary gates, renderer coverage/destruction stress gates, and performance baselines green.

## 18. Working notes / decisions

### 2026-08-16 - Initial direction

- Keep authoritative sparse voxel state on CPU.
- Move derived render voxel cache, meshing, LOD refinement, culling, allocation, and indirect draw generation progressively to GPU.
- Fix hierarchical coverage semantics before relying on GPU throughput; otherwise a faster renderer can still expose logically invalid holes.
- Preserve the existing CPU smooth mesher as a correctness oracle during compute migration.
- Treat the current `1/2/4/8` steps as desired refinement levels rather than mutually exclusive permission shells.
- Favor localized GPU pages/meshlets so destruction invalidates small regions and does not require contiguous giant chunk reallocations.
- Investigate a planar/cubic fast path for castle/building content after the core GPU smooth path is correct.

## 19. Completion definition

This migration is complete when all of the following are true:

1. Visible voxel-space coverage never depends on fine-detail readiness.
2. LOD traversal and large destruction remain hole-free under constrained refinement budgets.
3. Normal production rendering does not CPU-generate vertex/index geometry.
4. Generated render geometry stays GPU-resident with no production readback.
5. GPU allocator pressure degrades detail/residency, not visible coverage.
6. Destruction updates only affected/render-relevant regions rather than flooding the entire discovered world.
7. Gameplay, persistence, networking, and collision remain valid if GPU rendering is delayed or absent.
8. Full correctness/architecture/PlayMode suites are green.
9. Performance telemetry demonstrates bounded CPU renderer cost and acceptable GPU compute/allocation cost under representative destruction and traversal workloads.
