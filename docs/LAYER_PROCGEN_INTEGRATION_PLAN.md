# LayerProcGen Integration Proposal

Status: **proposal / implementation plan**  
Branch: `agent/layer-procgen-integration`

This document defines how the voxel project can use [Runevision LayerProcGen](https://github.com/runevision/LayerProcGen) without making it the owner of terrain, voxel storage, streaming, rendering, or semantic game-world authoring.

The proposed role is deliberately narrow:

> **LayerProcGen is the spatial dependency scheduler and contextual procedural-generation orchestration layer between semantic world intent and our existing generation algorithms.**

It does not replace those algorithms. It decides what generated information must exist before another generation step can run, keeps spatially overlapping requests deduplicated, and gives generators deterministic access to neighboring/contextual data.

---

## 1. Decision summary

Adopt LayerProcGen behind a new `VoxelEngine.ProcGen` module, initially for a single vertical slice from WorldBuilder site intent to generated structure intent.

Do **not** use LayerProcGen as:

- the voxel Storage implementation;
- the `int3` region-residency system;
- the renderer or mesh/SDF renderer;
- the terrain height function itself;
- the WorldBuilder semantic planning graph;
- a replacement for Structures, Vegetation, AmbientLife, or other generation algorithms;
- a place where Unity objects, GPU resources, or Storage mutations occur.

The target architecture is:

```text
Game.WorldBuilder
    semantic intent
    regions / routes / settlements / sites / NPCs / quests / secrets
        |
        v
Game.Composition
    semantic -> spatial generation inputs
        |
        v
VoxelEngine.ProcGen.Api
        |
        v
VoxelEngine.ProcGen.Runtime
    LayerProcGen hidden here
    spatial dependency DAG
    contextual generation
    lifetime / dedupe
        |
        +----> Terrain.Api deterministic queries
        +----> Structures.Api generation capabilities
        +----> Vegetation.Api generation capabilities
        +----> future world-generation algorithms
        |
        v
immutable generation result / intent
        |
        v
VoxelEngine.Streaming
    demand / mip / residency / bounded publication
        |
        v
Structures / Terrain / Vegetation rasterization/application
        |
        v
Storage
        |
        v
Rendering / Simulation
```

The key architectural rule is that **LayerProcGen may decide which generation data is needed, but subsystem APIs still own the actual meaning and application of that data**.

---

## 2. Why this belongs in the current architecture

The repo already separates semantic planning, runtime integration, generation, residency, and physical storage.

`docs/WORLDBUILDER_RUNTIME_INTEGRATION.md` defines WorldBuilder as the owner of semantic authoring/planning and WorldGen as the owner of generated physical facts. It also explicitly keeps WorldBuilder coordinate-free and requires cross-runtime integration to happen through `Game.Composition.*`.

That remains unchanged.

LayerProcGen fills a different gap: once semantic intent has been projected into generation space, generation systems increasingly need answers to questions such as:

- Which neighboring world-plan chunks are required before placing this settlement?
- Which roads cross or approach this site?
- What structures already claim nearby space before vegetation is generated?
- Which coarse region plan applies to this fine terrain/structure chunk?
- If two players request overlapping areas, how do we avoid generating the shared work twice?
- If a map view or fast travel preview requests an area temporarily, how are dependencies acquired and released?
- How does a generator safely ask for context outside its own chunk without creating order-dependent seams?

Those are spatial dependency/lifetime problems, not terrain, structure, or Storage problems.

LayerProcGen is specifically designed around deterministic contextual generation using chunked layers, declared spatial dependencies, dependency padding, and top-level demand.

---

## 3. Existing ownership boundaries that must not change

### 3.1 WorldBuilder

WorldBuilder owns semantic intent:

- region hierarchy;
- routes and route requirements;
- settlements;
- site roles and capabilities;
- NPC identity and semantic placement requirements;
- quests/objectives;
- story/cutscene requirements;
- secret and loot intent.

WorldBuilder should remain coordinate-free wherever the current contract requires it.

WorldBuilder must never reference Runevision/LayerProcGen types.

### 3.2 Game.Composition

`Game.Composition.*` owns the join between semantic game systems and physical generation/runtime systems.

It is the appropriate place to create an immutable generator-facing snapshot from compiled WorldBuilder data.

Example responsibility:

```text
PlanningGraph.HierarchyPlan
WorldSitePlacementPlan
resolved site requirements
        |
        v
WorldGenerationSnapshot
```

The snapshot can contain coarse spatial anchors/bounds selected by the Composition/world-generation backend, while the WorldBuilder authoring remains independent of the spatial scheduler.

### 3.3 Terrain

Terrain continues to own terrain generation/query semantics.

`VoxelEngine.Terrain.Api.TerrainQuery` is already a pure deterministic API whose values depend only on world coordinates and seed. That is ideal input to LayerProcGen-backed generation algorithms.

Do not wrap every `TerrainQuery.HeightAt(...)` call in a LayerProcGen layer. Stateless functional queries do not need chunk lifetime/dependency management.

A future *contextual terrain analysis* layer may make sense if it performs expensive area operations such as watershed analysis, erosion planning, biome transitions, ridge extraction, road-cost fields, etc. That is distinct from the canonical `TerrainQuery` itself.

### 3.4 Streaming

Streaming owns:

- requested `int3` region residency;
- demand and prefetch policy;
- mip/detail target;
- eviction;
- bounded main-thread publication.

`IRegionStreaming` remains the application-facing residency interface.

LayerProcGen must not replace it.

### 3.5 Storage

Storage owns physical voxel/world allocation and residency representation.

LayerProcGen workers must never directly mutate Storage.

Generated results cross a controlled boundary and are applied through existing subsystem/Storage APIs under the publication budget.

### 3.6 Rendering

Rendering consumes resident world state. It must not wait synchronously for LayerProcGen generation.

The generation pipeline must preserve the existing goal that expensive generation and planning happen off the render-critical path and that main-thread application is budgeted.

---

## 4. Critical dimensionality rule: 2D generation planning vs 3D voxel residency

LayerProcGen provides a two-dimensional infinite generation lattice. It can generate data for a 3D world, but the chunk-indexing plane is two-dimensional.

Our Streaming/Storage addressing is three-dimensional (`int3`).

Therefore:

> **LayerProcGen operates in XZ world space; Streaming remains XYZ/int3.**

This is not a temporary compromise. It is the intended architecture.

A single LayerProcGen XZ generation column can produce data spanning many Y voxel regions.

Example:

```text
LayerProcGen XZ chunk (12, 8)

        contains one castle plan
                |
                v
       vertical world extent

        int3 (12,  3, 8)
        int3 (12,  2, 8)
        int3 (12,  1, 8)
        int3 (12,  0, 8)
        int3 (12, -1, 8)
```

The castle must be planned once, not once per Y region.

Generation outputs therefore need world-space vertical bounds so they can be clipped/rasterized into whichever `int3` regions are currently requested.

### Required adapter

The ProcGen API needs a canonical conversion:

```csharp
public readonly struct GenerationArea
{
    public readonly int MinX;
    public readonly int MinZ;
    public readonly int MaxXExclusive;
    public readonly int MaxZExclusive;
}
```

Streaming or a Streaming/ProcGen composition adapter maps a voxel region request to the corresponding XZ generation area.

The exact mapping must derive from the canonical Storage/region dimensions rather than duplicate constants.

---

## 5. Proposed module layout

Add:

```text
Assets/VoxelEngine/ProcGen/
    Api/
        VoxelEngine.ProcGen.Api.asmdef
        IWorldGenerationProvider.cs
        WorldGenerationRequest.cs
        WorldGenerationResult.cs
        GenerationArea.cs
        GenerationDetail.cs
        GenerationDemandHandle.cs
        StructureIntent.cs              (or Structures.Api-owned equivalent)
        ... public immutable contracts only

    Runtime/
        VoxelEngine.ProcGen.Runtime.asmdef
        LayerProcGenWorldGenerationProvider.cs
        LayerProcGenDemandManager.cs
        LayerProcGenResultQueue.cs
        Layers/
            WorldPlanLayer.cs
            WorldPlanChunk.cs
            StructureIntentLayer.cs
            StructureIntentChunk.cs
        Adapters/
            RegionGenerationAreaAdapter.cs
        Diagnostics/
            ProcGenDiagnostics.cs
```

Rules:

1. `VoxelEngine.ProcGen.Api` contains no Runevision references.
2. Only `VoxelEngine.ProcGen.Runtime` references the LayerProcGen package.
3. Other subsystems reference `VoxelEngine.ProcGen.Api`, never `VoxelEngine.ProcGen.Runtime`.
4. `Game.WorldBuilder` does not reference either ProcGen Runtime or LayerProcGen.
5. Cross-runtime wiring remains in Composition/bootstrap code.
6. Public contracts are immutable or read-only and contain no Unity scene objects.

---

## 6. Proposed public API

The first API should be intentionally smaller than LayerProcGen itself.

Possible shape:

```csharp
namespace VoxelEngine.ProcGen.Api
{
    public interface IWorldGenerationProvider
    {
        GenerationDemandHandle Require(in WorldGenerationRequest request);

        void Release(GenerationDemandHandle handle);

        bool TryTakeCompleted(out WorldGenerationResult result);
    }
}
```

Request:

```csharp
public readonly struct WorldGenerationRequest
{
    public readonly GenerationArea Area;
    public readonly uint Seed;
    public readonly GenerationDetail Detail;
    public readonly WorldPlanVersion PlanVersion;

    public WorldGenerationRequest(
        GenerationArea area,
        uint seed,
        GenerationDetail detail,
        WorldPlanVersion planVersion)
    {
        Area = area;
        Seed = seed;
        Detail = detail;
        PlanVersion = planVersion;
    }
}
```

Completion:

```csharp
public sealed class WorldGenerationResult
{
    public GenerationArea Area { get; }
    public uint Seed { get; }
    public GenerationDetail Detail { get; }

    public IReadOnlyList<StructureIntent> Structures { get; }
    public IReadOnlyList<VegetationIntent> Vegetation { get; }

    // Future:
    // Roads
    // terrain modifications
    // ambient-life spawn plans
    // decoration intents
}
```

This is an example contract, not a requirement that all intent types live in ProcGen.Api. Prefer subsystem-owned intent contracts if Structures/Vegetation already expose a suitable API-level representation.

### Why a demand handle

`Require(...)` and `Release(...)` need identity because demand can overlap.

Examples:

```text
player 1 requires area A
player 2 requires area A + B
world map requires B + C
```

LayerProcGen can share overlapping lower-level chunks. Our adapter must preserve that lifetime model instead of collapsing everything into a single global "loaded" flag.

---

## 7. LayerProcGen is an implementation detail

A runtime adapter might conceptually look like:

```csharp
internal sealed class LayerProcGenWorldGenerationProvider
    : IWorldGenerationProvider
{
    private readonly LayerProcGenDemandManager _demand;
    private readonly LayerProcGenResultQueue _results;

    public GenerationDemandHandle Require(
        in WorldGenerationRequest request)
    {
        return _demand.Require(request);
    }

    public void Release(GenerationDemandHandle handle)
    {
        _demand.Release(handle);
    }

    public bool TryTakeCompleted(
        out WorldGenerationResult result)
    {
        return _results.TryDequeue(out result);
    }
}
```

Inside `_demand`, top-level LayerProcGen dependencies are created/moved/removed.

No external code should know about:

- `LayerChunk<>`;
- `ChunkBasedDataLayer<>`;
- `LayerDependency`;
- `TopLayerDependency`;
- `ILC`;
- Runevision `Point`/`GridBounds`.

This gives us the option to replace the scheduling library later without changing game/system APIs.

---

## 8. Initial layer graph

Do not begin by wrapping every generator in a layer.

Start with the smallest graph that proves the architecture:

```text
WorldPlanLayer        coarse semantic/spatial facts
       |
       v
StructureIntentLayer  concrete structure placement
```

Then expand only where contextual dependency management provides real value:

```text
WorldPlanLayer
    |
    +----------------------+
    |                      |
    v                      v
RegionPlanLayer         RouteLayer
    |                      |
    +----------+-----------+
               |
               v
            SiteLayer
               |
       +-------+--------+
       |                |
       v                v
StructureIntentLayer  TerrainContextLayer (only if needed)
       |
       +----------------+
       |
       v
VegetationIntentLayer
       |
       v
Decoration / AmbientLife intent
```

Potential responsibilities:

### WorldPlanLayer

Coarse immutable generator-facing data supplied from Game.Composition:

- region identity/biome intent;
- route intent;
- settlement intent;
- site requirements;
- required/procedural site classification;
- world-plan version/hash.

This layer should not recompute WorldBuilder semantics.

### RegionPlanLayer

Only add if physical coarse-region planning needs expensive/contextual computation beyond the compiled WorldBuilder hierarchy.

Possible output:

- large physical biome zones;
- settlement candidate fields;
- major landmark zones;
- high-level traversal constraints.

### RouteLayer

Potential output:

- deterministic route centerlines;
- road width/type;
- bridges/tunnels/fords intent;
- route intersections;
- approach vectors into settlements/sites.

This is a strong LayerProcGen use case because path geometry commonly requires context outside one small chunk.

### SiteLayer

Potential output:

- exact site anchor;
- site footprint/reservation;
- entrance approach;
- orientation;
- allowed vertical span;
- site capability realization facts.

### StructureIntentLayer

Potential output:

- chosen structure archetype/feature ID;
- exact origin and orientation;
- footprint;
- clearance volume;
- entrance(s);
- voxel/CSG/stamp operations or a reference to deterministic structure generation inputs;
- generated traversal/interior metadata;
- secret/cutscene anchors when required by the site contract.

### VegetationIntentLayer

Potential dependencies:

- terrain/slope/biome;
- roads;
- structure reservations;
- water;
- edits/protected areas where appropriate.

Potential output:

- deterministic vegetation instances or patches;
- exclusion masks/reservations;
- vine/moss/flower placement intent.

---

## 9. First vertical slice: WorldBuilder site -> structure intent -> voxel region

This is the recommended first implementation because it exercises the boundary we actually care about without touching every generator.

### 9.1 Input

WorldBuilder/Composition produces an immutable site snapshot such as:

```csharp
public readonly struct PlannedSite
{
    public readonly SiteRef Site;
    public readonly SiteArchetype Archetype;
    public readonly int ApproximateWorldX;
    public readonly int ApproximateWorldZ;
    public readonly SiteCapabilityRequirementSet Requirements;
}
```

The exact types should reuse existing WorldBuilder API types where those are already suitable.

The important semantic distinction is:

```text
WorldBuilder: "a site with these capabilities must exist"
ProcGen:      "here is the deterministic physical realization/placement"
```

### 9.2 WorldPlanLayer

A coarse LayerProcGen layer exposes the immutable spatial world-plan snapshot to dependent layers.

Conceptual LayerProcGen-backed implementation:

```csharp
internal sealed class WorldPlanChunk
    : LayerChunk<WorldPlanLayer, WorldPlanChunk>
{
    public readonly List<PlannedSite> Sites = new();

    public override void Create(int level, bool destroy)
    {
        if (destroy)
        {
            Sites.Clear();
            return;
        }

        layer.Snapshot.GetSites(bounds, Sites);
    }
}
```

The real adapter should avoid global static state. Snapshot/seed/version state should be owned by the runtime world-generation session.

### 9.3 StructureIntentLayer dependency

The structure layer declares the maximum context it may query from the world-plan layer.

Conceptually:

```csharp
internal sealed class StructureIntentLayer
    : ChunkBasedDataLayer<StructureIntentLayer, StructureIntentChunk>
{
    public override int chunkW => 256;
    public override int chunkH => 256;

    public StructureIntentLayer()
    {
        AddLayerDependency(
            new LayerDependency(
                WorldPlanLayer.instance,
                new Point(512, 512)));
    }
}
```

This means the framework generates all WorldPlan provider chunks needed for the structure chunk plus the declared contextual padding before `StructureIntentChunk.Create(...)` runs.

### 9.4 Structure generation

The structure chunk can then:

1. query nearby planned sites;
2. determine which sites this chunk owns;
3. query `TerrainQuery` for height/slope;
4. inspect dependent route/site/context data when those layers are added;
5. deterministically choose exact placement/orientation;
6. produce immutable `StructureIntent` values.

Conceptual code:

```csharp
private void GenerateSite(in PlannedSite site)
{
    int x = site.ApproximateWorldX;
    int z = site.ApproximateWorldZ;

    int y = TerrainQuery.HeightAt(x, z, layer.Seed);
    int slope = TerrainQuery.SlopeAt(x, z, layer.Seed);

    PlacementResult placement = SitePlacement.FindBestLocation(
        site,
        x,
        z,
        y,
        slope,
        layer.Seed);

    Intents.Add(new StructureIntent(
        site.Site,
        placement.Feature,
        placement.Origin,
        placement.Rotation,
        placement.Bounds));
}
```

No Storage writes occur here.

### 9.5 Ownership rule

Dependency padding means neighboring chunks may both *see* a site. Exactly one chunk must own generation of it.

Ownership must be canonical and deterministic, e.g. by the site anchor coordinate or another explicit owner key.

```csharp
private bool Owns(in PlannedSite site)
{
    return site.ApproximateWorldX >= bounds.min.x &&
           site.ApproximateWorldX <  bounds.max.x &&
           site.ApproximateWorldZ >= bounds.min.y &&
           site.ApproximateWorldZ <  bounds.max.y;
}
```

Never rely on "whichever neighboring chunk generated first."

### 9.6 Result publication

Generated intent enters a thread-safe completion queue owned by the ProcGen runtime.

Streaming/application code drains it under a budget and asks Structures/other owning systems to rasterize/apply the result.

---

## 10. RegionLoader integration

`Assets/VoxelEngine/Streaming/Runtime/RegionLoader.cs` currently states that its worker is a placeholder for the Terrain handoff. That is the seam to replace.

Do not replace Streaming itself.

Current conceptual flow:

```text
QueueLoad(int3 region)
    |
    v
placeholder worker
    |
    v
CompletedRegion
    |
    v
PublishLoaded(...)
    |
    v
Storage.EnsureRegionResident(...)
```

Target flow:

```text
QueueLoad(int3 region)
    |
    +--> retain Streaming demand/mip bookkeeping
    |
    v
map int3 -> XZ GenerationArea
    |
    v
IWorldGenerationProvider.Require(...)
    |
    v
LayerProcGen dependency graph
    |
    v
immutable generation result
    |
    v
completion queue
    |
    v
PublishLoaded(mainThreadBudgetMs)
    |
    +--> apply/rasterize only the part needed by the requested int3 region
    +--> mark Storage region resident
```

The final implementation may move orchestration out of the current static `RegionLoader` into a service if that is required to inject `IWorldGenerationProvider`. Do not introduce global ProcGen singletons just to preserve the current static shape.

### Important separation

A generation result being ready does not automatically mean every Y region touched by that result becomes resident.

Streaming decides which `int3` region is demanded.

Generation provides the deterministic world facts that intersect it.

Storage only allocates/applies the demanded region(s).

---

## 11. Threading contract

LayerProcGen uses managed parallelism. Treat that as a scheduler for CPU generation work, not permission to touch arbitrary Unity/runtime state from worker threads.

The project-level rule is:

> **LayerProcGen workers may compute immutable generation data. They may not mutate Unity scene objects, GPU resources, renderer state, or Storage residency.**

Allowed worker operations include:

- integer/fixed-point math;
- deterministic random/hash operations;
- immutable WorldPlan snapshot reads;
- pure `TerrainQuery` calls;
- CPU-only route/site/structure planning;
- construction of immutable result buffers using thread-safe ownership.

Forbidden worker operations include:

- `GameObject.Instantiate`;
- Unity component creation/destruction;
- renderer uploads;
- direct Storage region/brick mutation unless a Storage API is explicitly designed for safe worker-side staged writes;
- mutable access to gameplay runtimes;
- non-deterministic access to current frame/player state from generation algorithms.

### Main-thread publication budget

Keep the existing bounded publication concept.

If structure rasterization itself is too expensive to fit in one publication slice, reuse/extend the existing incremental structure build model rather than performing an unbounded finalization step.

This is essential for the zero-visible-stutter goal.

---

## 12. Demand and lifetime model

LayerProcGen top-level dependencies should be driven by our demand manager, not created ad hoc by individual generators.

Potential demand sources:

- player streaming radius;
- remote multiplayer player streaming radius;
- prefetch corridor;
- fast-travel destination preview;
- world map;
- editor/debug generation visualization;
- server-side simulation region.

Our adapter should record an independent demand handle per source/request.

Example:

```text
Demand P1 ------\
                 +--> same StructureIntent chunks generated once
Demand P2 ------/

Map demand ----------> additional coarse layers only
```

LayerProcGen's dependency lifetime should be allowed to recycle its own chunk data when no longer required, but this must not implicitly evict Storage. Storage residency is still controlled by Streaming.

Likewise, Storage eviction does not necessarily mean all coarse ProcGen data must disappear if some other demand source still requires it.

---

## 13. Detail / mip policy

Streaming already has requested mip/detail concepts. ProcGen layers should not blindly mirror Storage mip levels.

Different generation layers naturally operate at different abstraction levels:

```text
continent/region plan      very coarse
route graph                coarse
settlement/site plan       medium
structure placement        medium/fine
vegetation                 fine
small decoration           very fine
```

The initial API can expose a small semantic detail enum, e.g.:

```csharp
public enum GenerationDetail : byte
{
    Planning,
    Gameplay,
    Full,
}
```

Then the adapter decides which top-level LayerProcGen layer(s) a demand level requires.

Do not assume `RequestedMipLevel == LayerProcGen internal level`.

They represent different concepts.

---

## 14. Determinism rules

Every generated result must be independent of request order and thread scheduling.

Required rules:

1. Randomness derives from stable inputs: world seed + layer identity + canonical spatial key + stable semantic IDs.
2. Never seed from generation order, list insertion order, frame count, thread ID, object hash code, or current time.
3. Query ordering from neighboring chunks must be normalized before a result depends on it when source ordering is not guaranteed.
4. Each cross-boundary object has a deterministic owner.
5. Results must be reproducible after release/reload.
6. The same world-plan version + seed + request must produce the same physical facts.
7. Changing the world-plan snapshot must change an explicit plan version/hash rather than silently mixing old/new generated chunks.

---

## 15. Seam rules

LayerProcGen solves the *availability of contextual data*; our algorithms still need mathematically deterministic seam behavior.

For every layer that can create cross-boundary data, define:

- maximum query padding;
- canonical object ownership;
- whether geometry may extend outside its owner chunk;
- clipping rules for consumers;
- stable IDs for cross-chunk objects;
- merge/deduplication rules where multiple provider records describe one object.

Examples:

### Road

One route segment may cross several chunks. Route identity and geometry come from a canonical route plan; each chunk does not independently invent its own road endpoints.

### Building

The chunk containing the canonical site anchor owns the building intent even if the building footprint crosses neighboring chunks.

### Vegetation

A plant instance belongs to the chunk containing its canonical root/anchor coordinate. Its visual/physics extent may cross the boundary.

---

## 16. World-plan versioning and invalidation

We need an explicit answer for what happens when authored/generated macro intent changes.

Initial implementation can use a session-level immutable world-plan snapshot:

```csharp
public readonly struct WorldPlanVersion
{
    public readonly ulong Value;
}
```

All LayerProcGen data in a generation session corresponds to exactly one snapshot/version.

For the first implementation, prefer rebuilding/restarting the ProcGen session on a world-plan change rather than attempting fine-grained dependency invalidation prematurely.

Later, if runtime world-plan changes are required, define targeted invalidation semantics intentionally.

Do not confuse **player voxel edits** with **procedural source-plan invalidation**. Player edits are authoritative runtime state layered over/generated from the base world and must not be erased simply because a generation chunk is recycled.

---

## 17. Persistence and player edits

LayerProcGen describes/reconstructs deterministic base-world generation facts.

Persistent modifications remain owned by the appropriate existing systems.

Conceptually:

```text
base deterministic generation
        |
        v
Storage base world
        |
        +--> Edits / destruction / building / simulation state
        |
        v
current authoritative world state
```

Reloading/recreating a LayerProcGen chunk must not reset user edits.

A region load should conceptually become:

1. reproduce deterministic base generation inputs;
2. materialize/rasterize base state as needed;
3. apply persisted authoritative edits/deltas;
4. publish resident state.

The exact Storage/Edits reconciliation remains an owning-subsystem concern, not a LayerProcGen concern.

---

## 18. Multiplayer considerations

The server/authority must use the same stable seed and world-plan version.

Do not network LayerProcGen internal chunk state.

Prefer networking:

- world/session seed;
- plan/version identity where needed;
- authoritative gameplay edits/deltas;
- dynamic simulation/gameplay state.

Base procedural geometry should be reproducible from deterministic inputs unless bandwidth/performance testing demonstrates a reason to transmit generated results.

Overlapping player streaming demands should naturally deduplicate in the ProcGen dependency graph while Streaming continues to make per-region residency choices appropriate for the authority/client.

---

## 19. Rendering and stutter constraints

This integration must improve generation scheduling without moving stalls elsewhere.

Success requires all three stages to be bounded:

```text
A. planning/generation CPU work     -> background/contextual
B. Storage application/rasterizing  -> incremental/budgeted
C. renderer/GPU publication         -> incremental/budgeted
```

LayerProcGen primarily helps stage A.

It does not automatically solve B or C.

Therefore a successful LayerProcGen integration is not complete if the final result is then applied as one huge synchronous castle/terrain mutation on the main thread.

The vertical slice must be profiled end-to-end.

---

## 20. Diagnostics we should expose

At minimum add counters/timings for:

- current top-level demand handles;
- active chunks per ProcGen layer;
- generation queue depth;
- generation completions pending publication;
- generation duration per layer/chunk;
- reused/deduplicated demand count if available;
- publication duration;
- number of structure/vegetation intents applied per frame;
- generation failures;
- missing-dependency/padding errors;
- stale result rejected because seed/plan version changed.

Editor/debug visualization should make it possible to see layer chunk bounds and current demanded areas without coupling production game code to LayerProcGen visualization classes.

---

## 21. Failure behavior

Fail closed when generation cannot satisfy semantic requirements.

Examples:

- a required site cannot find any valid placement;
- generated structure facts cannot satisfy required player-spawn capacity;
- required entrance/traversal constraints are impossible;
- world-plan identity/version does not match the active generation session.

Do not silently drop required semantic constraints to get a chunk generated.

This follows the same rule already established by the Kentridge WorldBuilder integration.

Optional procedural decoration may degrade gracefully, but the distinction between **required semantic realization** and **optional enrichment** must remain explicit.

---

## 22. LayerProcGen package/licensing boundary

Current LayerProcGen documentation identifies the package as LayerProcGen v0.4.0 and documents Unity 2019.4+ support. The project currently uses a much newer Unity version, so the documented minimum version is not a blocker, but package compatibility must still be verified in our project before adoption.

The project is licensed under MPL-2.0.

Practical rule for this repo:

- keep third-party LayerProcGen source/package separate;
- modifications to MPL-covered LayerProcGen files must remain compliant with MPL requirements;
- our own project modules and game code are not thereby required to use the MPL license;
- prefer adapting through `VoxelEngine.ProcGen.Runtime` rather than forking/modifying the dependency unless necessary.

Before merging the package, record the exact package commit/tag so generation behavior cannot change accidentally from an unpinned upstream update.

---

## 23. Package integration approach

Do not add the package until the first adapter contracts are agreed.

When adopted:

1. pin LayerProcGen to an exact reviewed commit/tag in `Packages/manifest.json` rather than an unpinned moving reference;
2. keep Runevision references confined to `VoxelEngine.ProcGen.Runtime.asmdef`;
3. confirm build/platform compatibility;
4. import no sample content into production Assets unless needed for a test fixture;
5. verify package threading behavior under Unity 6/player builds;
6. record license/third-party notice as required by the project's dependency policy.

---

## 24. Proposed implementation phases

### Phase 0 - proposal and contracts

- [x] Create dedicated integration branch.
- [x] Document architectural role and ownership boundaries.
- [x] Document XZ LayerProcGen vs XYZ Streaming mapping.
- [x] Document threading/publication rule.
- [ ] Review proposed API names/types against current subsystem APIs to avoid duplicate intent models.
- [ ] Decide the canonical world-plan snapshot type supplied by `Game.Composition`.
- [ ] Decide where the Streaming -> ProcGen adapter is composed/injected.
- [ ] Decide exact region-to-XZ bounds conversion using canonical region dimensions.

### Phase 1 - API skeleton, no external package dependency

- [ ] Add `VoxelEngine.ProcGen.Api.asmdef`.
- [ ] Add `GenerationArea`.
- [ ] Add `GenerationDemandHandle`.
- [ ] Add `WorldGenerationRequest`.
- [ ] Add `IWorldGenerationProvider`.
- [ ] Add immutable generation result contract.
- [ ] Add architecture tests enforcing Runtime/API dependency boundaries.
- [ ] Keep all new APIs independent of Runevision types.

### Phase 2 - LayerProcGen runtime adapter

- [ ] Pin LayerProcGen package to reviewed version/commit.
- [ ] Add `VoxelEngine.ProcGen.Runtime.asmdef` referencing LayerProcGen.
- [ ] Implement runtime/session ownership of LayerManager/layers without leaking globals.
- [ ] Implement demand handle -> top dependency mapping.
- [ ] Implement result completion queue.
- [ ] Implement clean teardown/restart for world-plan version changes.
- [ ] Add diagnostics for active demands/chunks/results.

### Phase 3 - first WorldPlanLayer

- [ ] Define immutable generator-facing WorldPlan snapshot in the correct API/Composition boundary.
- [ ] Project compiled WorldBuilder hierarchy/site requirements into that snapshot.
- [ ] Implement coarse `WorldPlanLayer`/`WorldPlanChunk`.
- [ ] Implement bounded spatial site query.
- [ ] Test that semantic plan data is not reconstructed from dependency-node strings.
- [ ] Test that WorldBuilder still has no spatial scheduler dependency.

### Phase 4 - StructureIntentLayer vertical slice

- [ ] Define/reuse subsystem-owned `StructureIntent` contract.
- [ ] Implement deterministic site ownership.
- [ ] Declare WorldPlan dependency and maximum padding.
- [ ] Use `TerrainQuery` for exact terrain-relative placement.
- [ ] Generate one known required site through the new path.
- [ ] Reuse existing Structures generation/rasterization APIs instead of writing Storage directly.
- [ ] Preserve generated physical facts required by Kentridge/session binding.

### Phase 5 - Streaming handoff

- [ ] Replace `RegionLoader` placeholder worker behavior with ProcGen demand/request integration.
- [ ] Preserve Streaming ownership of `int3` residency, mip target, prefetch, and eviction.
- [ ] Deduplicate multiple Y-region requests that share one XZ planning column.
- [ ] Clip generation results to only the demanded Storage region during materialization.
- [ ] Release ProcGen demand independently from Storage eviction.
- [ ] Preserve bounded `PublishLoaded(...)` behavior.

### Phase 6 - determinism and seam validation

- [ ] Generate adjacent A then B and compare with B then A.
- [ ] Generate the same area with different worker scheduling and compare results.
- [ ] Release/re-require an area and verify byte-/semantic-equivalent generation.
- [ ] Verify structure crossing a chunk edge appears exactly once.
- [ ] Verify multiple Y regions at one XZ do not duplicate site/structure planning.
- [ ] Verify overlapping two-player demand generates shared ProcGen chunks once.
- [ ] Verify map/debug temporary demand does not prematurely destroy player-required chunks.
- [ ] Verify world-plan version change cannot mix stale/new generation.

### Phase 7 - frame-time validation

- [ ] Profile background generation cost.
- [ ] Profile main-thread result publication cost.
- [ ] Profile structure rasterization cost.
- [ ] Profile renderer/GPU publication after new geometry is applied.
- [ ] Add stress showcase that streams a large structure while moving.
- [ ] Assert/measure that no single finalization step creates an unacceptable frame spike.

### Phase 8 - expand layer graph only after vertical slice succeeds

Candidate order:

- [ ] RouteLayer.
- [ ] Site/settlement physical planning layer if needed separately from WorldPlan.
- [ ] structure reservation/clearance context.
- [ ] VegetationIntentLayer.
- [ ] decoration/ambient-life placement layers.
- [ ] expensive contextual terrain analysis only where pure TerrainQuery is insufficient.

Do not migrate a subsystem merely to make the layer graph look complete.

---

## 25. Required tests in more detail

### Deterministic request-order test

Given the same seed/world plan:

```text
run 1: require A -> complete A -> require B -> complete B
run 2: require B -> complete B -> require A -> complete A
```

The union of generated world facts must be equivalent.

### Neighbor seam test

Place a required site whose footprint crosses a ProcGen chunk boundary. Generate each neighboring chunk independently and together. Assert:

- one canonical structure ID;
- one canonical placement;
- no duplicate voxel application;
- identical boundary geometry.

### XZ/Y deduplication test

Require:

```text
(x=5, y=0, z=8)
(x=5, y=1, z=8)
(x=5, y=2, z=8)
```

Assert coarse world/site/structure planning runs once for the shared XZ area while each demanded `int3` region receives only its intersecting materialization.

### Overlapping multiplayer demand test

Require overlapping areas for two simulated players. Remove player A demand while B remains. Assert shared LayerProcGen chunks remain available until B releases them.

### Main-thread safety test

Instrument/assert that LayerProcGen worker generation never invokes Storage mutation or Unity object creation APIs.

### Publication budget test

Generate a structure large enough that applying everything in one frame would exceed budget. Assert the publication/application system yields and resumes across frames rather than overrunning the configured budget.

---

## 26. Open design questions to resolve before implementation

1. **Where should physical generation intent contracts live?**  
   Prefer `Structures.Api`, `Vegetation.Api`, etc. if those subsystems already have stable reusable types. `ProcGen.Api` should orchestrate rather than become a grab-bag of every subsystem's domain types.

2. **What is the exact generator-facing WorldPlan snapshot?**  
   It should be compiled/typed and immutable, and must not force downstream code to re-parse WorldBuilder authoring structures.

3. **Who owns the ProcGen session lifecycle?**  
   Likely a Composition/bootstrap service associated with a loaded world/session, injected into Streaming integration.

4. **How does a requested Storage mip map to generation detail?**  
   We should map semantically rather than reusing numeric mip values as LayerProcGen levels.

5. **Where is generated base-world materialization cached?**  
   Avoid repeatedly rasterizing the same intent when several Y/neighbor requests overlap.

6. **How are persistent edits layered over regenerated base geometry?**  
   Must be explicitly validated with Edits/Storage before the Streaming cutover.

7. **How much structure rasterization can happen off-main-thread safely with current Storage APIs?**  
   If safe staged writes exist or are added later, use them; otherwise keep worker output immutable and application budgeted.

8. **What is our canonical coordinate unit at the ProcGen boundary?**  
   Prefer existing integer world/voxel units and avoid floating-point planning coordinates.

---

## 27. Rejected alternatives

### Replace Streaming with LayerProcGen

Rejected because Streaming owns 3D voxel residency/mips/eviction while LayerProcGen's infinite chunk lattice is 2D. They solve related but different problems.

### Put LayerProcGen directly inside WorldBuilder

Rejected because WorldBuilder owns semantic planning and is intentionally coordinate-free. It would also leak a third-party spatial scheduler into game-authoring/domain code.

### Let every subsystem create its own top-level LayerProcGen demands

Rejected because lifetime would become impossible to reason about and would duplicate the coordination problem. Top-level demand must be centralized behind our ProcGen provider/demand manager.

### Let LayerProcGen chunks write Storage directly

Rejected because it couples third-party worker scheduling to physical world mutation, undermines thread safety, bypasses Streaming residency ownership, and risks frame-time/race problems.

### Wrap simple TerrainQuery calls in a layer

Rejected because pure O(1)-style deterministic coordinate queries do not benefit from chunk lifetime/dependency machinery. Add layers only for contextual/expensive area generation.

### Expose LayerProcGen types from ProcGen.Api

Rejected because the entire engine would become coupled to the library's abstractions and future replacement would be expensive.

---

## 28. Definition of success for the first integration

The first integration is successful when all of the following are true:

1. A WorldBuilder-required site is represented in an immutable generator-facing world-plan snapshot.
2. Streaming demand for an `int3` region acquires the required XZ ProcGen data without knowing LayerProcGen types.
3. LayerProcGen deterministically generates a structure placement using contextual WorldPlan data and pure Terrain APIs.
4. Structure generation output contains no Unity object/Storage mutation.
5. The result is applied through the owning subsystem under the existing publication budget.
6. Adjacent/request-order tests prove deterministic seam behavior.
7. Multiple Y regions share one XZ planning result.
8. Two overlapping demand sources share generated dependency chunks correctly.
9. Releasing ProcGen data does not erase persistent gameplay edits.
10. Rendering remains asynchronous/budgeted and does not acquire a new visible frame spike.
11. No Runevision type is visible outside `VoxelEngine.ProcGen.Runtime`.
12. Existing WorldBuilder, Streaming, Storage, Terrain, and Structures ownership rules remain intact.

If we cannot meet those conditions with a small vertical slice, we should stop and reassess the dependency rather than spreading it into additional systems.

---

## 29. Current recommendation

Proceed with the proposal, but keep the first implementation intentionally narrow.

The repo has reached the point where independently generated roads, sites, structures, vegetation, terrain context, and streaming demand will otherwise begin inventing their own ad hoc dependency/caching/lifetime mechanisms.

LayerProcGen can provide that missing spatial orchestration layer while our existing architecture continues to own:

- **what the world means** (`Game.WorldBuilder`);
- **how semantic systems connect to physical generation** (`Game.Composition`);
- **the actual generation algorithms** (Terrain/Structures/Vegetation/etc.);
- **what voxel regions are resident** (Streaming);
- **the physical world representation** (Storage);
- **what is drawn** (Rendering).

That is the integration boundary this branch should preserve.