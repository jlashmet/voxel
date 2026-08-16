# LayerProcGen Integration Proposal

Status: **revised proposal / implementation plan**  
Branch: `agent/layer-procgen-integration`

> This revision supersedes the original proposal on this branch.
>
> The most important correction is architectural: **LayerProcGen is not a new voxel-engine subsystem and Streaming must not depend on it directly.** It is a replaceable implementation of contextual world-generation scheduling behind project-owned WorldGen/runtime boundaries.

---

## 1. Executive decision

We should evaluate Runevision LayerProcGen, but use it for one narrow job:

> **Manage deterministic, contextual, spatial dependencies and lifetime for procedural world-planning work in the XZ plane.**

It should not own:

- semantic world authoring,
- the campaign/world hierarchy,
- terrain formulas,
- voxel data,
- voxel storage,
- physical region residency,
- edits or persistence,
- rendering,
- GPU resources,
- Unity object lifetime,
- networking authority,
- or the definition of our public generation model.

The key architectural gate is:

> **Removing LayerProcGen later must require replacing a scheduler implementation, not redesigning WorldBuilder, WorldGen, Streaming, Storage, Rendering, or gameplay.**

If we cannot maintain that property, the integration is too invasive and should be rejected.

---

## 2. Why this proposal changed

The first draft placed too much conceptual weight on a proposed `VoxelEngine.ProcGen` module and suggested a closer relationship between Streaming and LayerProcGen than the existing architecture warrants.

A deeper review of the repository shows a better separation already exists:

- `Game.WorldBuilder` owns semantic game-world intent.
- WorldGen abstractions represent/generated physical world facts.
- `Game.Composition.WorldBuilderWorldGen` is an intentionally pure bridge between semantic planning and world-generation concepts.
- Streaming owns physical voxel-region demand and residency.
- Storage owns resident voxel representation and physical allocation.
- Rendering consumes resident data; it is not a generation authority.
- Composition/runtime code is the correct place to coordinate independent systems.

Therefore:

1. LayerProcGen should **not** become an architectural peer of Terrain, Streaming, Storage, etc. merely because it is a library.
2. LayerProcGen should **not** leak into the pure `Game.Composition.WorldBuilderWorldGen` assembly.
3. Streaming should **not** call LayerProcGen directly.
4. Our domain-facing generation outputs should be project-owned immutable facts, not LayerProcGen chunks or voxel write commands.
5. A separate runtime scheduler/coordinator should bridge generation demand, LayerProcGen, voxel materialization, and physical residency.

---

## 3. Architectural invariants

These are hard constraints for the integration.

### 3.1 LayerProcGen is an implementation detail

No public game or engine API may expose Runevision types.

Forbidden examples:

```csharp
// Do not expose this from project-owned APIs.
public LayerChunk RequestChunk(...);

// Do not make callers know about TopLayerDependency.
public TopLayerDependency Acquire(...);
```

Project-owned APIs should use project-owned values such as:

```csharp
WorldGenerationDemand
WorldGenerationDemandHandle
GenerationEpoch
WorldGenerationCompletion
ResolvedSitePlacement
ResolvedRoute
ResolvedStructure
```

### 3.2 WorldBuilder remains semantic

WorldBuilder answers questions such as:

- What regions exist?
- Which settlements belong to them?
- What named sites exist?
- Which routes need to connect?
- Which NPCs, quests, secrets, objectives, and cutscenes refer to which sites?
- What semantic constraints must be respected?

WorldBuilder does **not** own:

- voxel coordinates as its primary abstraction,
- LayerProcGen chunks,
- residency,
- mesh generation,
- Storage handles,
- or worker scheduling.

### 3.3 The pure WorldBuilder-to-WorldGen bridge stays pure

`Game.Composition.WorldBuilderWorldGen` is deliberately engine-free. Keep it that way.

Its job is to compile/project semantic intent into immutable world-generation inputs.

It must not reference:

- LayerProcGen,
- UnityEngine runtime objects,
- Storage,
- Streaming runtime,
- Rendering,
- worker queues,
- or GPU state.

### 3.4 Streaming owns residency, not procedural planning

Streaming answers:

> Which physical `int3` voxel regions should be resident, at what mip/detail, and when should they be loaded/evicted?

LayerProcGen answers a different question:

> Which contextual world-generation facts are needed for a spatial XZ area, and which neighboring generation facts must exist to derive them deterministically?

Streaming must not acquire a direct LayerProcGen dependency.

### 3.5 Generated plans are facts, not voxel mutations

LayerProcGen-backed work produces immutable generation facts.

Examples:

```text
ResolvedRoute
ResolvedSettlementPlacement
ResolvedSitePlacement
ResolvedStructure
ResolvedArchitecture
TerrainModificationPlan
VegetationPlan
AmbientSpawnPlan
```

A later materialization stage converts facts into voxel data.

### 3.6 Worker planning cannot mutate authoritative state

LayerProcGen workers must not mutate:

```text
Storage
IRegionResidencyStore
UnityEngine.Object
GameObject
Transform
Mesh
GraphicsBuffer
Renderer
GPU resources
persistent edits
network-authoritative state
```

### 3.7 Procedural generation is a baseline, not persistent authority

After materialization, the authoritative current world is:

```text
procedural baseline
      +
persistent edits
      +
runtime state
      =
current world
```

Destroying a LayerProcGen cache chunk must never destroy player/world state.

### 3.8 Ordering and concurrency must not change the world

Required:

```text
generate A then B == generate B then A
serial generation   == parallel generation
unload/reload       == original baseline
```

---

## 4. Existing repository seams we should preserve

### 4.1 WorldBuilder semantic graph

`BlueprintCompiler` already builds a semantic dependency graph around concepts such as:

```text
regions
  -> routes
  -> settlements
  -> sites
  -> NPCs
  -> objectives
  -> cutscenes
  -> secrets
```

That graph is valuable, but it solves a different problem from LayerProcGen.

The WorldBuilder graph is a **semantic planning DAG**.

LayerProcGen supplies a **spatial contextual generation DAG and lifetime mechanism**.

Do not merge these concepts into one graph.

### 4.2 TerrainQuery

`TerrainQuery` is already a good model for deterministic, stateless world queries.

A call such as:

```csharp
TerrainQuery.HeightAt(x, z, seed)
```

should remain a direct pure query when that is sufficient.

Do not create a LayerProcGen layer simply to wrap every deterministic function.

LayerProcGen becomes useful when generation depends on **contextual neighboring generated facts** with nontrivial lifetime/dependency relationships.

### 4.3 Streaming

The current Streaming API establishes the correct ownership:

```csharp
public interface IRegionStreaming
{
    void QueueLoad(in RegionLoadRequest request);
    int PublishLoaded(float mainThreadBudgetMs);
    bool IsResident(int3 regionCoord);
    bool Evict(int3 regionCoord);
}
```

Streaming owns residency policy and timing.

The current RegionLoader also has a placeholder generation handoff. We should use that as evidence that a generation handoff is needed, **not** as a reason to embed LayerProcGen in Streaming.

The final dependency direction should preserve Streaming's independence from the chosen procedural scheduler.

### 4.4 Storage

Storage owns the resident world and its allocation/representation.

Generation may create data destined for Storage, but procedural planning does not become a Storage concern.

### 4.5 Composition

Runtime Composition is where independent capabilities should be wired together.

That makes it the natural home for a coordinator that can observe/request:

- generation demand,
- voxel materialization,
- Streaming demand/residency,
- bounded Storage publication,
- and render publication readiness,

without forcing those systems to know about each other's implementation details.

---

## 5. Revised system model

```text
                 AUTHORING / PLANNING
                        |
                        v
              Game.WorldBuilder
                        |
                        v
      Game.Composition.WorldBuilderWorldGen
           PURE semantic -> WorldGen plan
                        |
                        v
            immutable generation snapshot
                        |
          +-------------+-------------+
          |                           |
          v                           v
 contextual worldgen            non-voxel consumers
 LayerProcGen scheduler          map / preview / etc.
          |
          | immutable facts
          v
 routes / sites / structures / vegetation plans
          |
          v
 Game.Composition.WorldGeneration.Runtime
          |
     +----+----------------------+
     |                           |
     v                           v
 voxel materialization      Streaming.Api
 jobs / Burst / workers      physical residency demand
     |                           |
     +------------+--------------+
                  v
                Storage
                  |
                  v
               Rendering
```

The exact assembly names are provisional. The dependency shape is not.

---

## 6. Proposed ownership boundaries

### `Game.WorldBuilder`

Owns:

- semantic regions,
- route requirements,
- settlements,
- sites,
- NPC/story associations,
- objectives,
- quest/cutscene constraints,
- hidden-space intent,
- semantic generation policy.

Does not know:

- LayerProcGen,
- region residency,
- voxel chunks,
- Storage,
- rendering.

### `Game.Composition.WorldBuilderWorldGen`

Owns:

- pure projection from semantic WorldBuilder output into immutable generation snapshot/facts/input descriptors.

Must remain:

- engine-free,
- scheduler-free,
- deterministic,
- easy to unit test.

### WorldGen runtime scheduler implementation

Owns:

- translating project-owned generation demand into LayerProcGen top-level dependency lifetime,
- contextual dependency scheduling,
- generated-fact cache lifetime,
- completion reporting,
- cancellation/release bookkeeping,
- generation epochs/request identity.

Does not own:

- semantic game design,
- voxel Storage,
- Streaming residency,
- rendering.

### Runtime composition/coordinator

Owns orchestration between:

- generation demand,
- generation completion,
- voxel materialization,
- physical residency,
- publication budgets.

The coordinator knows capabilities. The capabilities should not form a dependency knot amongst themselves.

### Streaming

Owns:

- `int3` region demand,
- mips/tiering inputs as appropriate,
- residency,
- prefetch,
- eviction,
- publication timing that belongs to Streaming.

Does not know LayerProcGen.

### Materialization

Owns:

- converting immutable generation facts into voxel-region-local output,
- clipping 3D features to physical regions,
- CPU/job/Burst representation of build work,
- deterministic voxelization.

### Storage

Owns:

- physical resident representation,
- world allocation,
- authoritative resident voxel state.

### Rendering

Owns:

- consuming resident world representation,
- render representations,
- GPU publication/resources,
- visual LOD/tiering behavior where appropriate.

---

## 7. Do we need a new `VoxelEngine.ProcGen` module?

**Not as the primary architectural concept.**

The first draft proposed:

```text
Assets/VoxelEngine/ProcGen/Api
Assets/VoxelEngine/ProcGen/Runtime
```

That naming implies procedural world planning is a voxel-engine concern. It also risks teaching unrelated engine systems to depend on a procgen subsystem.

Prefer a project-owned WorldGen scheduling abstraction whose implementation happens to use LayerProcGen.

Possible shape, subject to alignment with existing WorldGen assemblies:

```text
WorldGen/
    Api/
        IWorldGenerationScheduler.cs
        WorldGenerationDemand.cs
        WorldGenerationCompletion.cs
        GenerationEpoch.cs
        generated fact DTOs...

    Runtime/
        LayerProcGen/
            LayerProcGenWorldGenerationScheduler.cs
            Layers/
                ...
```

or an equivalent assembly layout already consistent with repository conventions.

The critical property is dependency isolation, not the exact folder name.

Only the LayerProcGen implementation assembly should reference the Runevision package.

---

## 8. Immutable generation snapshot

WorldBuilder state should not be queried opportunistically from LayerProcGen worker threads.

Instead, compile/project the relevant semantic world into an immutable generation snapshot.

Conceptually:

```csharp
public sealed class WorldGenerationSnapshot
{
    public WorldSeed Seed { get; }
    public WorldBuilderSnapshotHash SourceHash { get; }
    public GeneratorSchemaVersion SchemaVersion { get; }

    public IReadOnlyList<RegionPlan> Regions { get; }
    public IReadOnlyList<RouteRequirement> Routes { get; }
    public IReadOnlyList<SettlementPlan> Settlements { get; }
    public IReadOnlyList<SitePlan> Sites { get; }
}
```

Benefits:

- workers read immutable data,
- no cross-thread access to mutable game objects,
- deterministic generation inputs,
- generation versioning is explicit,
- server/client can identify exactly which baseline they mean,
- tests can construct snapshots without the game runtime.

The snapshot is **input to** spatial generation. It is not resident voxel state.

---

## 9. LayerProcGen's exact role

LayerProcGen is useful for generation where a consumer needs generated neighboring context.

Examples:

- a road segment must connect consistently with nearby route planning,
- settlement placement must consider nearby roads/sites,
- a building placement needs clearance/context around its own chunk,
- vegetation placement must exclude nearby roads/buildings,
- decoration may depend on structure surfaces or local ecological facts,
- multiple player/map requests overlap and should share generated planning work.

LayerProcGen gives us mechanisms for:

- spatial chunking,
- declared inter-layer dependencies,
- dependency padding,
- top-level demand lifetime,
- overlapping-demand reuse,
- chunk generation/destruction lifetime,
- parallel execution of generation work.

It does **not** provide our generation algorithms.

Our algorithms remain project code.

---

## 10. What should *not* become a LayerProcGen layer

Do not wrap functions merely because they participate in generation.

Bad examples:

```text
TerrainHeightLayer -> wraps TerrainQuery.HeightAt
HashLayer -> wraps deterministic hashing
VoxelReadLayer -> wraps Storage reads
RenderLayer -> creates meshes
```

A layer is justified when it represents a meaningful generated fact set with spatial lifetime/context dependencies.

Good candidate layers may include:

```text
RegionPlanLayer
RoutePlanLayer
SettlementPlacementLayer
SitePlacementLayer
ArchitecturePlanLayer
VegetationPlanLayer
AmbientSpawnPlanLayer
```

Exact boundaries should emerge from the first spike rather than be created wholesale.

---

## 11. Demand model

The first draft's illustrative API:

```csharp
RequireRegion(int3 regionCoord, uint seed);
ReleaseRegion(int3 regionCoord);
```

is too weak and couples planning to physical voxel-region semantics.

Use a project-owned demand abstraction instead.

Conceptually:

```csharp
public interface IWorldGenerationScheduler
{
    WorldGenerationDemandHandle Acquire(in WorldGenerationDemand demand);

    void Release(WorldGenerationDemandHandle handle);

    bool TryDequeueCompleted(out WorldGenerationCompletion completion);
}
```

Demand:

```csharp
public readonly struct WorldGenerationDemand
{
    public readonly WorldBoundsXZ Bounds;
    public readonly GenerationDetail Detail;
    public readonly GenerationDemandSource Source;
    public readonly int Priority;
    public readonly GenerationEpoch Epoch;
}
```

Handle:

```csharp
public readonly struct WorldGenerationDemandHandle
{
    public readonly GenerationRequestId RequestId;
}
```

These types are illustrative. Final naming should follow repository conventions.

---

## 12. Why demand is not just a voxel region

Different consumers may need different generation facts.

### Player proximity

May require:

- complete contextual planning,
- actual voxel materialization,
- high-detail physical residency.

### World map

May require:

- regions,
- routes,
- settlements,
- named sites,

without voxelizing terrain/buildings.

### Fast-travel preview

May require:

- route/site facts,
- broad terrain facts,

without keeping the target area's full voxel representation resident.

### Server prewarm

May request:

- low-priority planning/materialization before player arrival.

### Editor/debug visualization

May request:

- planning facts and dependency bounds only.

This is why generation demand and Streaming residency demand should be separate abstractions.

---

## 13. Translating demand to LayerProcGen

The implementation adapter can translate a project-owned demand handle into one or more LayerProcGen top-level dependencies.

Conceptually:

```text
WorldGenerationDemand
        |
        v
LayerProcGenWorldGenerationScheduler
        |
        +--> create/update TopLayerDependency(s)
        |
        +--> track request id / epoch / requested fact level
        |
        v
LayerProcGen layers
```

When multiple demands overlap, LayerProcGen should be allowed to reuse shared provider chunks.

When one demand disappears, shared chunks remain alive while another demand still depends on them.

Callers never manipulate those top dependencies directly.

---

## 14. 2D planning versus 3D voxel residency

This distinction is fundamental.

LayerProcGen's spatial lattice is 2D. For our 3D world, use it as XZ contextual planning space.

Streaming/Storage use 3D `int3` physical regions.

Example:

```text
LayerProcGen XZ planning chunk
        (42, 17)
            |
            | generates once
            v
      ResolvedStructure
      "castle-01"

      bounds:
        X 10752..11080
        Y   190..390
        Z  4352..4660
            |
       +----+----+----+----+
       |         |         |
       v         v         v
    int3       int3       int3 ...
  (42,0,17) (42,1,17) (42,2,17)
```

The castle is **planned once**.

Each intersecting voxel region materializes only its own portion.

### Required consequence

Multiple Y regions at the same XZ location must not independently rerun high-level placement.

### Cave limitation

Do not pretend LayerProcGen solves arbitrary XYZ contextual dependencies.

If future procedural cave/underground systems require a truly 3D spatial dependency scheduler, treat that as a separate architectural problem rather than distorting XZ LayerProcGen usage.

---

## 15. Generated facts

Layer outputs should describe **what exists**, not how to mutate Storage.

Example:

```csharp
public readonly struct ResolvedSitePlacement
{
    public readonly SiteId SiteId;
    public readonly int3 Origin;
    public readonly RotationQuarterTurns Rotation;
    public readonly WorldBounds Bounds;
}
```

Possible fact families:

```text
ResolvedRegion
ResolvedRoute
ResolvedSettlementPlacement
ResolvedSitePlacement
ResolvedStructure
ResolvedArchitecture
TerrainModificationPlan
VegetationPlan
AmbientSpawnPlan
```

Facts should have stable identity where appropriate.

For example:

```csharp
public readonly struct GenerationFeatureId
{
    public readonly ulong Value;
}
```

Stable identities make persistence, network reconciliation, debugging, deterministic ownership, and regeneration much easier.

---

## 16. Fact ownership across padded chunk queries

Dependency padding means a consumer can see neighboring provider facts.

That must not create duplicate world features.

Every generated feature requires a deterministic owner.

Possible rule:

```text
Feature owner = canonical planning chunk containing feature anchor
```

Then:

- neighboring chunks may query/read the feature,
- only the owner creates the fact,
- materializers clip/intersect it into any physical voxel regions it overlaps.

Never use execution order as ownership.

---

## 17. Example contextual layer graph

A mature graph might eventually resemble:

```text
WorldGenerationSnapshot
          |
          v
   RegionPlanLayer
       /       \
      v         v
 RoutePlan   Ecology
    |           |
    v           |
Settlement      |
    |           |
    v           |
 SitePlacement  |
    |           |
    v           |
Architecture    |
    |           |
    +------> VegetationPlan
                    |
                    v
              AmbientSpawnPlan
```

This is illustrative, not an instruction to implement all layers immediately.

A dependency should exist only when the downstream algorithm truly needs spatial context from the upstream fact set.

---

## 18. Example dependency declarations

Illustrative LayerProcGen shape:

```csharp
public sealed class SitePlacementLayer
    : ChunkBasedDataLayer<SitePlacementLayer, SitePlacementChunk>
{
    public override int chunkW => 256;
    public override int chunkH => 256;

    public SitePlacementLayer()
    {
        AddLayerDependency(
            new LayerDependency(
                RoutePlanLayer.instance,
                new Point(512, 512)));
    }
}
```

Vegetation might need much smaller contextual padding:

```csharp
public VegetationPlanLayer()
{
    AddLayerDependency(
        new LayerDependency(
            SitePlacementLayer.instance,
            new Point(32, 32)));

    AddLayerDependency(
        new LayerDependency(
            RoutePlanLayer.instance,
            new Point(16, 16)));
}
```

Padding is part of the algorithm's correctness contract. It should be reasoned about and tested, not selected casually.

---

## 19. Layer chunk code must stay pure

Conceptual pattern:

```csharp
public sealed class SitePlacementChunk
    : LayerChunk<SitePlacementLayer, SitePlacementChunk>
{
    private readonly List<RouteFact> _routes = new();
    public readonly List<ResolvedSitePlacement> Placements = new();

    public override void Create(int level, bool destroy)
    {
        if (destroy)
        {
            _routes.Clear();
            Placements.Clear();
            return;
        }

        _routes.Clear();
        Placements.Clear();

        RoutePlanLayer.instance.GetRoutesInBounds(
            this,
            RequiredContextBounds(),
            _routes);

        foreach (var sourceSite in SnapshotSitesForOwnedArea())
        {
            Placements.Add(
                SitePlacementAlgorithm.Resolve(
                    sourceSite,
                    _routes,
                    GenerationContext));
        }
    }
}
```

The chunk may perform CPU planning.

It must not call Storage or Unity object APIs.

---

## 20. Runtime pipeline

Treat generation as a pipeline with independently bounded stages.

```text
Requested
   |
   v
Planning
   |
   v
Planned
   |
   v
VoxelBuildQueued
   |
   v
VoxelDataReady
   |
   v
StorageCommitQueued
   |
   v
StorageCommitted
   |
   v
Renderable
   |
   v
Refining
   |
   v
Final
```

LayerProcGen participates in:

```text
Planning -> Planned
```

It does not own the rest of the pipeline.

---

## 21. Why off-thread generation alone does not eliminate stutter

The requirement is not merely:

> Run castle generation on another thread.

That can still hitch if a huge completion is published all at once.

A no-stutter design requires backpressure across every expensive stage.

We should have explicit limits for:

```text
max concurrently planning chunks
max concurrently voxelizing regions
max queued completed planning results
max queued completed voxel results
Storage commit work per frame
mesh/SDF extraction work per frame
GPU upload work per frame
render-resource publication per frame
```

The exact mechanism can vary by subsystem, but **unbounded queues are not acceptable**.

---

## 22. Planning versus materialization threading

Recommended division:

### LayerProcGen workers

Good for:

- spatial dependency planning,
- route/site placement,
- deterministic feature selection,
- immutable fact production.

### Job System / Burst / subsystem worker path

Prefer for appropriate data-oriented voxel work such as:

- feature clipping,
- bulk voxelization,
- SDF/voxel field evaluation,
- region-local transformations,
- heavy pure numeric processing.

### Bounded publication path

Owns:

- authoritative Storage mutation,
- Unity-object creation if any,
- render/GPU resource publication,
- work that must occur on a constrained/main thread.

This separation lets us tune each stage independently.

---

## 23. Backpressure contract

The runtime coordinator should never blindly drain an unlimited upstream queue.

Conceptually:

```csharp
while (planning.TryTakeCompleted(out var plan))
{
    if (!_voxelBuildQueue.HasCapacity)
        break;

    _voxelBuildQueue.Enqueue(plan);
}
```

Likewise publication should stop at budget/capacity.

Possible measured budgets:

```text
planning CPU concurrency
voxel-build worker slots
storage commit milliseconds/frame
render extraction milliseconds/frame
GPU upload bytes/frame
```

Do not choose final numbers until profiling.

---

## 24. Priority

Demand should carry priority because generation is not equally urgent.

Example ordering:

```text
1. region immediately needed by player collision/gameplay
2. near-player visual residency
3. near-future movement prefetch
4. second player's overlapping nearby demand
5. map/preview demand
6. background server prewarm
```

LayerProcGen itself does not need to become our global priority scheduler if it cannot express every requirement cleanly. The project-owned coordinator can control which top-level demands are active and when completed work is admitted downstream.

---

## 25. Generation identity

Every baseline must be attributable to explicit generation identity.

Conceptually:

```csharp
public readonly struct GenerationIdentity
{
    public readonly WorldSeed WorldSeed;
    public readonly WorldBuilderSnapshotHash SnapshotHash;
    public readonly GeneratorSchemaVersion SchemaVersion;
    public readonly LayerVersionSet LayerVersions;
}
```

This answers:

> Which exact procedural baseline produced this world data?

Do not rely only on `uint seed` once generation becomes sophisticated.

---

## 26. Generation epochs

Asynchronous work can finish after its request is no longer valid.

Examples:

- a player moves away,
- the region is evicted,
- a new world snapshot replaces the old one,
- generator configuration changes,
- a request is released and later reacquired,
- editor regeneration begins while old work is still running.

Therefore completions require an epoch.

Conceptually:

```csharp
public readonly struct WorldGenerationCompletion
{
    public readonly GenerationEpoch Epoch;
    public readonly GenerationRequestId RequestId;
    public readonly GenerationChunkId ChunkId;
    public readonly GeneratedFactBatch Facts;
}
```

Before accepting completion:

```csharp
if (completion.Epoch != _currentEpoch)
    return CompletionDisposition.Stale;

if (!_activeDemands.Contains(completion.RequestId))
    return CompletionDisposition.NoLongerDemanded;
```

The exact acceptance rules may allow reusable cache results after demand release, but stale data must never accidentally become authoritative.

---

## 27. Why epochs matter for race safety

A worker finishing late must not be able to resurrect invalid work.

Bad sequence:

```text
request region A
   |
worker starts
   |
player leaves
   |
evict A
   |
new snapshot/epoch starts
   |
old worker finishes
   |
old result published into new world   <-- bug
```

Required:

```text
old completion
   |
validate epoch + request identity
   |
   +--> stale -> discard/recycle
```

This is more robust than relying on thread timing or cancellation alone.

---

## 28. Deterministic random seeding

Do not use a shared sequential RNG whose state depends on execution order.

Derive randomness from stable identities.

Conceptually:

```csharp
ulong randomSeed = Hash(
    generationIdentity.WorldSeed,
    generationIdentity.SchemaVersion,
    layerId,
    chunkCoord,
    featureId);
```

For sub-decisions, derive substreams/hashes from stable labels or indices.

This should guarantee that scheduling changes do not change world content.

---

## 29. Deterministic generation rules

Required rules:

1. Same generation identity + same demand facts => same generated facts.
2. Chunk execution order cannot affect outputs.
3. Thread count cannot affect outputs.
4. Duplicate/overlapping requests cannot create duplicate world features.
5. Adjacent chunks agree at seams.
6. Ownership is derived from coordinates/identity, not “who ran first.”
7. Floating-point behavior that can affect topology/ownership must be controlled/tested.
8. Persisted identifiers must not depend on transient object addresses or collection iteration order.

---

## 30. Persistence model

Procedural generation is the reproducible baseline.

Persistence stores what cannot be reconstructed from the baseline alone.

```text
baseline procedural facts / voxelization
                 |
                 +---- persistent edits
                 |
                 +---- persistent gameplay state
                 |
                 v
          authoritative current world
```

Examples of persistent edits/state:

- removed voxel,
- placed voxel,
- cut vine,
- destroyed bridge section,
- opened secret wall,
- harvested persistent resource,
- player-built structure,
- quest-driven permanent geometry change.

---

## 31. Unload/reload semantics

LayerProcGen chunk destruction is cache/lifetime behavior, not world deletion.

On unload:

```text
release generation demand
release disposable planning cache when unreferenced
possibly evict physical voxel residency separately
retain persistent deltas/state
```

On reload:

```text
regenerate baseline for GenerationIdentity
             |
             v
materialize baseline
             |
             v
apply persisted deltas/state
             |
             v
current authoritative world
```

A player-modified castle cannot disappear because a LayerProcGen planning chunk was recycled.

---

## 32. Generator version changes

Changing generation algorithms creates a migration question.

`GeneratorSchemaVersion` / layer versions let us make this explicit.

Possible policies, decided elsewhere:

- old saves pin old generator version,
- old saves migrate baseline IDs/deltas,
- shipped worlds bake some baseline data,
- development worlds invalidate/rebuild.

LayerProcGen should not decide save migration policy.

---

## 33. Networking

Generation identity and stable feature IDs provide a clean network foundation.

Possible model:

```text
server/client share compatible baseline identity
             +
server replicates authoritative deltas/runtime state
```

Do not assume all peers can independently regenerate and therefore need no validation.

Network authority remains outside LayerProcGen.

For multiplayer overlap, generation demand should naturally deduplicate shared XZ planning context while physical Streaming still decides per-client/server residency policy.

---

## 34. Two-player overlap example

Player A requires XZ planning around `(40..45, 15..20)`.

Player B requires `(43..48, 18..23)`.

Desired behavior:

```text
A demand -----------+
                    |
                    v
              shared LayerProcGen provider chunks
                    ^
                    |
B demand -----------+
```

Shared planning work is generated once while depended on.

If A leaves, overlap chunks stay alive if B still needs them.

This is one of the concrete things the spike should prove rather than assume.

---

## 35. Map and fast-travel demand

One advantage of separating generation demand from voxel residency is that a map can request planning facts without loading the entire voxel world.

Example:

```text
Map demand
   |
   +--> RegionPlan
   +--> RoutePlan
   +--> Settlement/Site facts

(no requirement to materialize every voxel region)
```

Likewise a fast-travel preview can ask for enough context to describe a destination before full physical residency is required.

---

## 36. Terrain integration

Keep pure terrain functions direct where possible.

A site-placement algorithm can call:

```csharp
int y = TerrainQuery.HeightAt(x, z, seed);
```

and other deterministic terrain analysis queries without forcing Terrain into LayerProcGen.

If we later discover expensive derived terrain analyses that genuinely benefit from spatial cache/dependency lifetime, introduce a project-owned fact layer for that reason—not merely because LayerProcGen exists.

---

## 37. Structures integration

LayerProcGen should decide contextual placement facts such as:

```text
feature identity
archetype/template identity
origin
orientation
world-space bounds
semantic/site identity
possibly chosen generation parameters
```

Structures/materialization code remains responsible for turning that description into physical voxel/SDF operations.

This keeps structure generation testable independently from world scheduling.

---

## 38. Vegetation integration

Vegetation is a strong later candidate because it depends naturally on contextual exclusions:

```text
terrain/ecology
   + roads
   + structures
   + clearing/exclusion facts
   -> vegetation plan
```

LayerProcGen can ensure the contextual facts exist.

Vegetation algorithms still choose species/density/placement.

Materialization still owns physical voxels/instances/render data.

---

## 39. Ambient life integration

Ambient-life generation should likely consume stable ecological/spawn facts rather than create Unity entities from LayerProcGen workers.

Possible output:

```text
AmbientSpawnPlan
    habitat id
    bounds
    species/group id
    density/budget
    deterministic spawn seed
```

Runtime simulation/entity systems decide what is currently instantiated.

---

## 40. Story and quest integration

Story/quest semantic requirements originate in WorldBuilder.

Spatial generation may resolve them into physical facts.

Example:

```text
WorldBuilder:
    hidden shrine must belong to region R
    reachable from site S
    not visible from primary road

spatial planning:
    choose exact shrine placement
    choose entrance relationship

result:
    ResolvedSitePlacement / ResolvedArchitecture facts
```

LayerProcGen should never become the source of quest truth.

---

## 41. Cutscenes

Cutscenes may consume stable site/anchor facts after resolution.

Do not make cutscene execution or camera objects part of generation layers.

A resolved world fact can expose stable named anchors if needed:

```text
Castle.MainGate
Castle.Courtyard
Castle.ThroneApproach
```

Those anchors remain project-owned facts.

---

## 42. Debugging and observability

We should make the scheduler observable from the beginning.

Useful debug data:

```text
active generation demands
source / priority / bounds
active LayerProcGen chunks by layer
provider dependency counts
chunk generation duration
completion queue depth
voxel-build queue depth
stale completion count
cache hit/reuse count
epoch
Storage publication cost
render publication cost
```

A debug visualization could show:

- layer chunk boundaries,
- padded dependency bounds,
- generated feature anchors/bounds,
- current demand sources,
- ownership chunks,
- stale/rejected completions.

Do not require production systems to reference debug visualization code.

---

## 43. Performance metrics that matter

Track the entire path, not just LayerProcGen worker time.

For a streamed area:

```text
T_demand_to_plan_start
T_planning
T_waiting_for_voxel_slot
T_voxel_build
T_waiting_for_storage_budget
T_storage_commit
T_waiting_for_render_budget
T_render_publish
T_demand_to_renderable
```

Frame metrics:

```text
main-thread generation coordination ms
Storage commit ms
render extraction/publication ms
GPU upload bytes/ms
queue depths
worker saturation
```

A “fast worker generation” result is not success if publication still produces frame spikes.

---

## 44. Failure semantics

Generation failures must be explicit.

Possible completion states:

```text
Success
Cancelled
StaleEpoch
NoLongerDemanded
InvalidInput
AlgorithmFailure
MaterializationFailure
PublicationFailure
```

Not every state belongs to LayerProcGen; the overall pipeline should still expose enough information to debug where work died.

Avoid silently marking a region resident after only partial/failed generation unless a deliberate fallback policy exists.

---

## 45. Cancellation semantics

Cancellation is an optimization; epoch validation is correctness.

If LayerProcGen allows demand to be released while work is running, release it promptly.

However, never assume cancellation prevents completion callbacks/work from racing with newer state.

Every result still passes identity/epoch validation before downstream publication.

---

## 46. Cache semantics

LayerProcGen chunk lifetime is a planning cache lifetime.

Do not let it become a second authoritative world database.

Good cached data:

- derived route facts,
- resolved site placement facts,
- local architecture plan facts,
- vegetation plan facts.

Bad use:

- authoritative destroyed/placed voxels,
- mutable quest state,
- player inventory,
- live NPC entity state,
- Storage allocation handles.

---

## 47. First spike: scope

Do **not** start with the complete Kentridge castle pipeline.

Kentridge already combines too many concerns:

- architecture,
- hidden spaces,
- story,
- cutscenes,
- NPC placement,
- gameplay constraints,
- structure materialization.

That would make it difficult to determine whether LayerProcGen itself is helping.

The first spike should isolate its claimed value.

---

## 48. First spike: proposed vertical slice

Use one contextual site-placement flow:

```text
compiled WorldBuilder hierarchy
          |
          v
immutable generation snapshot
          |
          v
LayerProcGen contextual SitePlacement layer
          |
          v
project-owned ResolvedSitePlacement facts
          |
          v
existing/non-invasive consumer or debug view
```

The spike should prove:

1. semantic input remains scheduler-independent,
2. neighboring context is available deterministically,
3. overlapping demand reuses generated work,
4. acquire/release/reacquire is safe,
5. generation order does not affect output,
6. serial/parallel output is identical,
7. stale completions cannot leak across epochs,
8. XZ planning is shared across multiple Y voxel regions,
9. LayerProcGen types do not escape the implementation assembly.

Only after that should we connect full voxel materialization.

---

## 49. Why the spike should stop before voxelization initially

The first question is:

> Is LayerProcGen a good contextual planning scheduler for our architecture?

If we immediately include:

```text
LayerProcGen
+ Structures
+ voxelization
+ Storage
+ Streaming
+ Rendering
```

then failures become ambiguous.

First prove the scheduler boundary and deterministic facts.

Then add materialization as the next phase and separately prove frame-budgeted publication.

---

## 50. Second spike: materialization

After the planning spike passes, take one resolved placement and materialize it into one or more physical `int3` regions.

Required shape:

```text
ResolvedSitePlacement / ResolvedStructure
              |
              v
    physical region intersection
              |
              v
     off-thread/job voxel build
              |
              v
       bounded commit queue
              |
              v
            Storage
```

Prove that two Y regions use the same planning fact rather than triggering two placements.

---

## 51. Third spike: streaming coordination

Only after planning and materialization are independently understood should runtime composition coordinate them with Streaming.

Conceptually:

```text
player/location policy
      |              \
      v               v
planning demand    region residency demand
      |               |
      v               v
scheduler          Streaming
      |               |
      v               |
planned facts          |
      |               |
      v               |
materializer           |
      +-------+--------+
              v
       bounded Storage commit
```

Streaming remains unaware of LayerProcGen.

---

## 52. Expansion order after spikes

If the spikes pass, expand cautiously:

```text
routes
  |
  v
settlements
  |
  v
sites
  |
  v
architecture
  |
  v
vegetation
  |
  v
ambient plans
```

Each layer should justify:

- why it needs contextual scheduling,
- what immutable facts it owns,
- what padding it requires,
- what stable IDs it produces,
- how determinism is tested,
- how it is profiled.

---

## 53. Proposed package boundary

LayerProcGen package dependency should exist in exactly the runtime implementation assembly/assemblies that need it.

No Runevision reference should appear in:

```text
Game.WorldBuilder
Game.Composition.WorldBuilderWorldGen
Streaming.Api
Streaming.Runtime
Storage.Api
Storage.Runtime
Rendering.Api
Rendering.Runtime
Terrain.Api
Structures.Api
```

unless a later architectural decision explicitly changes this document.

Prefer an asmdef-level test/check that catches accidental leakage.

---

## 54. Package installation

Current documented UPM form for the library is:

```text
https://github.com/runevision/LayerProcGen.git#upm
```

Do not add the package until Phase 0 boundary review is complete.

When added:

- pin a concrete version/commit rather than floating indefinitely,
- record the chosen version,
- keep package changes isolated in the integration branch,
- verify Unity compatibility in CI/editor tests.

---

## 55. Licensing note

LayerProcGen uses MPL-2.0.

The integration plan should avoid modifying the dependency unless necessary. If we modify MPL-covered source, track those modifications and their distribution obligations separately from our proprietary/project-owned code.

This is an engineering note, not legal advice.

---

## 56. First implementation API sketch

A minimal project-owned port might look like:

```csharp
public interface IWorldGenerationScheduler
{
    WorldGenerationDemandHandle Acquire(
        in WorldGenerationDemand demand);

    void Release(
        WorldGenerationDemandHandle handle);

    bool TryDequeueCompleted(
        out WorldGenerationCompletion completion);
}
```

It should be possible to implement a fake/in-memory deterministic scheduler for tests without LayerProcGen.

That is a useful replaceability test in itself.

---

## 57. Completion API sketch

Do not return mutable LayerProcGen chunks.

Conceptually:

```csharp
public readonly struct WorldGenerationCompletion
{
    public readonly GenerationEpoch Epoch;
    public readonly GenerationRequestId RequestId;
    public readonly GenerationChunkId ChunkId;
    public readonly GeneratedFactBatch Facts;
}
```

Ownership/lifetime of `GeneratedFactBatch` must be explicit if pooled memory is used.

Avoid retaining references to recycled LayerProcGen chunk internals downstream.

---

## 58. Memory/lifetime rule

A completion crossing the LayerProcGen adapter boundary must not depend on mutable/recyclable library-owned collections.

Use one of:

- immutable copied arrays,
- project-owned pooled buffers with explicit lease ownership,
- immutable value structures,
- another project-owned lifetime model.

Never enqueue a `List<T>` owned by a LayerProcGen chunk and then recycle that chunk underneath the consumer.

---

## 59. Snapshot switching

When WorldBuilder/world-generation input changes:

```text
old snapshot / epoch N
        |
        +--> stop admitting new demand
        +--> release old top-level dependencies

new snapshot / epoch N+1
        |
        +--> become active generation identity
```

Old in-flight completions are rejected by epoch validation.

Development/editor tooling can be more aggressive about invalidation than shipped-save migration.

---

## 60. Hot reload / editor iteration

For editor workflows, support an explicit “new generation epoch” action rather than relying on accidental cache clearing.

Useful controls:

```text
regenerate current demand
regenerate layer
show active epoch
show snapshot hash
show generator schema version
show feature owner
```

This should help distinguish algorithm changes from cache artifacts.

---

## 61. Seam correctness

A contextual system must be correct at chunk boundaries.

Tests should intentionally place source features:

- exactly on a chunk edge,
- one unit on either side,
- within dependency padding,
- spanning multiple physical voxel regions,
- spanning multiple planning chunks.

Compare complete generated fact sets, not only screenshots.

---

## 62. Chunk sizes

Do not assume one universal chunk size.

Different planning layers may benefit from different scales:

```text
region/biome planning     coarse
route planning            coarse/medium
site placement            medium
architecture              medium/fine
vegetation                fine
```

LayerProcGen supports layers with their own chunk dimensions. Choose sizes based on:

- algorithm context radius,
- density of facts,
- cache churn,
- overlap/padding amplification,
- worker cost,
- memory.

Measure before tuning.

---

## 63. Padding cost

Large dependency padding can multiply upstream chunk demand.

For each layer measure:

```text
consumer chunk count
provider chunk count caused by padding
average provider reuse
peak active provider chunks
memory per active chunk
```

A beautiful dependency graph that causes explosive padded working sets is a failed design.

---

## 64. Floating origin / large coordinates

LayerProcGen's XZ planning coordinates should use stable world-space integer/fixed coordinates independent of Unity scene-transform floating origin.

Floating-origin presentation must not change generation identity or ownership.

If the project shifts Unity transforms, the generation coordinate remains stable.

---

## 65. LOD/tiering relationship

Do not conflate generation detail with render LOD.

Examples:

- a castle's semantic/placement fact should not change because the renderer chooses a coarser visual LOD,
- a map may need low-detail facts without any rendered voxel LOD,
- Streaming may request a mip independently of whether contextual planning is already cached.

If some generation facts themselves have levels/details, define those explicitly rather than reusing render mip terminology blindly.

---

## 66. Structural integrity / simulation

Simulation systems consume the current authoritative world.

They should not query LayerProcGen for whether a wall “really” exists after the player destroys it.

Procedural facts can assist initial construction/metadata, but runtime simulation state lives elsewhere.

---

## 67. Edits

Edits apply to the authoritative materialized world/delta model, not to LayerProcGen cache chunks.

If a generated feature needs stable edit association, use `GenerationFeatureId` or another stable project-owned identity.

Example:

```text
Generated bridge feature #1234
        |
player destroys center span
        |
persistent delta references affected world cells/feature metadata
```

Regeneration reconstructs baseline feature #1234 and reapplies the delta.

---

## 68. Storage publication

The materialization pipeline should produce a project-owned region build result suitable for bounded publication.

Conceptually:

```csharp
public readonly struct VoxelRegionBuildResult
{
    public readonly int3 RegionCoord;
    public readonly GenerationEpoch Epoch;
    public readonly RegionVoxelPayload Payload;
}
```

Before commit:

- validate epoch,
- validate demand/residency policy as appropriate,
- ensure queue/budget rules,
- publish through Storage-owned APIs.

Do not let planning workers acquire Storage internals.

---

## 69. Rendering publication

Rendering should react to committed world data through existing engine boundaries.

Do not let generation workers directly create/update meshes or graphics buffers.

For the no-stutter goal, measure render extraction/upload separately from procedural and Storage costs.

---

## 70. What “success” means

LayerProcGen is worth adopting if the spike demonstrates all of the following:

1. It substantially simplifies contextual dependency/lifetime code we would otherwise write ourselves.
2. Its overlap behavior works for our demand patterns.
3. Its 2D XZ model maps cleanly to our 3D materialization pipeline.
4. Determinism survives parallel scheduling.
5. It stays behind our own API.
6. It does not force Streaming/Storage/Rendering to know about it.
7. It does not introduce unmanageable padding/cache amplification.
8. It does not interfere with frame-budgeted publication.
9. We can unit/integration test the boundary without Unity objects.
10. Replacing it remains feasible.

---

## 71. Kill criteria

Stop or redesign the integration if the spike shows any of these:

- Runevision types need to leak through public project APIs.
- Streaming has to know LayerProcGen chunk/top-dependency semantics.
- generated facts cannot be detached safely from recycled chunks.
- determinism depends on scheduling order.
- contextual padding causes unacceptable working-set explosion.
- cancellation/lifetime cannot be made race-safe with project-owned epochs.
- 2D planning forces hacks for routine required 3D generation.
- LayerProcGen becomes the easiest place to stash mutable authoritative state.
- the library prevents effective priority/backpressure control.
- replacing it would require rewriting WorldBuilder/WorldGen consumers.
- end-to-end frame pacing is worse despite off-thread planning.

A failed spike is still valuable because it identifies exactly what scheduler capability we need to build ourselves.

---

## 72. Acceptance tests before broad adoption

### Determinism

- [ ] A then B produces exactly the same facts as B then A.
- [ ] Serial generation equals parallel generation.
- [ ] Different worker counts produce identical facts.
- [ ] Unload/reload reproduces the same procedural baseline.
- [ ] Stable feature IDs reproduce across runs.

### Boundaries and seams

- [ ] Adjacent planning chunks produce identical seam facts.
- [ ] Features exactly on ownership boundaries have exactly one owner.
- [ ] A feature spanning multiple chunks is not duplicated.
- [ ] Padding gives required context without changing ownership.

### Lifetime and races

- [ ] Overlapping player demands do not duplicate shared generation.
- [ ] Releasing one overlapping demand does not destroy data still required by another.
- [ ] Acquire/release/reacquire is deterministic.
- [ ] A stale worker completion cannot resurrect an evicted/replaced request.
- [ ] Generation epoch change rejects old completions.
- [ ] Cancellation races cannot publish old-world data.

### 2D/3D mapping

- [ ] Multiple Y voxel regions share one XZ planning result.
- [ ] 3D structure bounds clip deterministically into intersecting physical regions.
- [ ] Planning a structure does not depend on which Y region requested first.

### Persistence

- [ ] Persisted edits survive baseline regeneration.
- [ ] LayerProcGen cache eviction cannot erase authoritative edits.
- [ ] Generation identity mismatch is detectable.

### Threading

- [ ] No Storage access occurs from LayerProcGen worker code.
- [ ] No Unity object access occurs from LayerProcGen worker code.
- [ ] No rendering/GPU access occurs from LayerProcGen worker code.
- [ ] Completion data remains valid after source LayerProcGen chunks recycle.

### Backpressure/frame pacing

- [ ] Planning completion cannot create an unbounded queue.
- [ ] Voxel-build completion cannot create an unbounded queue.
- [ ] Storage publication obeys its configured frame budget.
- [ ] render/GPU publication obeys its configured frame budget.
- [ ] large feature generation does not produce one-frame publication spikes.

### Replaceability

- [ ] WorldBuilder has no LayerProcGen reference.
- [ ] pure WorldBuilder->WorldGen composition has no LayerProcGen reference.
- [ ] Streaming has no LayerProcGen reference.
- [ ] Storage has no LayerProcGen reference.
- [ ] Rendering has no LayerProcGen reference.
- [ ] a fake/test scheduler can satisfy the project-owned scheduling interface.

---

## 73. Implementation phases

### Phase 0 — architecture alignment

- [x] Define LayerProcGen's narrow role.
- [x] Separate semantic planning from spatial scheduling.
- [x] Keep `Game.Composition.WorldBuilderWorldGen` pure.
- [x] Decide Streaming must not depend directly on LayerProcGen.
- [x] Formalize XZ planning versus `int3` residency.
- [x] Define baseline + edits + runtime state authority model.
- [x] Define generation epoch requirement.
- [x] Define deterministic identity/seeding requirement.
- [x] Define pipeline-wide backpressure requirement.
- [ ] Audit current WorldGen assemblies/types and choose exact scheduler API home.
- [ ] Audit current Composition runtime assemblies and choose exact coordinator home.
- [ ] Reuse existing IDs/bounds/fact types wherever possible instead of duplicating them.
- [ ] Decide exact snapshot representation.

### Phase 1 — isolated LayerProcGen spike

- [ ] Pin LayerProcGen package version/commit.
- [ ] Create implementation-only assembly reference.
- [ ] Add `IWorldGenerationScheduler` (or aligned existing abstraction).
- [ ] Add demand/handle/epoch/completion project-owned types.
- [ ] Add immutable generation snapshot adapter.
- [ ] Implement one contextual site-placement layer.
- [ ] Implement deterministic ownership.
- [ ] Implement deterministic seeds.
- [ ] Implement completion detachment/lifetime safety.
- [ ] Add overlap/lifetime tests.
- [ ] Add serial-vs-parallel determinism tests.
- [ ] Add seam tests.
- [ ] Add epoch/stale completion tests.
- [ ] Add debug inspection of active demands/chunks.

### Phase 2 — materialization spike

- [ ] Convert one generated fact into physical region-local build work.
- [ ] Ensure one XZ plan serves multiple Y regions.
- [ ] Keep materialization off authoritative Storage until commit.
- [ ] Add bounded build-result queue.
- [ ] Add epoch validation before commit.
- [ ] Add Storage publication budget test.
- [ ] Add unload/reload baseline test.
- [ ] Add persisted-edit reapply test.

### Phase 3 — runtime composition with Streaming

- [ ] Add coordinator between planning demand, materialization, and residency.
- [ ] Keep Streaming interface independent from LayerProcGen.
- [ ] Separate planning demand from physical residency demand.
- [ ] Add prefetch/priority policy.
- [ ] Add two-player overlap integration test.
- [ ] Add movement churn test.
- [ ] Add stale completion under eviction test.
- [ ] Add end-to-end timing metrics.

### Phase 4 — rendering/frame pacing

- [ ] Measure Storage commit separately from render extraction.
- [ ] Measure GPU/resource publication separately.
- [ ] Add bounded render publication if missing.
- [ ] Stress-test large structure arrival while moving.
- [ ] Stress-test rapid travel across generation boundaries.
- [ ] Establish frame-time acceptance thresholds.

### Phase 5 — expand contextual generation

- [ ] Route planning.
- [ ] Settlement placement.
- [ ] Site placement beyond spike fixture.
- [ ] Architecture facts.
- [ ] Vegetation planning/exclusion.
- [ ] Ambient spawn plans where useful.
- [ ] Kentridge integration only after lower-level contracts are proven.

---

## 74. Phase 0 repository questions to answer before code

Before adding the dependency, inspect and document:

1. Which existing WorldGen assembly should own the scheduler port?
2. Which existing ID types can represent stable generated features?
3. Which existing bounds types can represent XZ demand and 3D fact bounds?
4. Whether there is already an immutable campaign/worldgen snapshot concept.
5. Which current Structures API consumes placement/feature descriptions.
6. Which current Composition assembly is allowed to depend on both generation runtime and Streaming.
7. What RegionLoader's placeholder handoff should become after coordination moves outward.
8. Which current job/worker path is best for voxel materialization.
9. Where Storage commit budgeting currently belongs.
10. Where Rendering/GPU publication is currently bounded.

Do not create duplicate abstractions until these are answered.

---

## 75. Example end-to-end flow after integration

Suppose the player approaches a planned fortress area.

```text
1. Player movement policy determines upcoming area.

2. Runtime coordinator acquires:
   - generation demand for required XZ planning facts
   - Streaming demand for physical int3 regions

3. LayerProcGen scheduler resolves contextual facts:
   - route
   - site placement
   - structure/architecture facts

4. Completion crosses adapter boundary as project-owned immutable data.

5. Coordinator validates:
   - generation epoch
   - request identity/current need
   - downstream capacity

6. Materialization jobs clip the structure into needed int3 regions.

7. Completed voxel-region payloads wait in a bounded queue.

8. Storage commit publishes within budget.

9. Rendering observes committed data and builds/publishes render resources within its own budget.

10. Player edit occurs later.

11. Edit is persisted as authoritative delta/state, not written back into LayerProcGen.

12. Area unloads:
    - physical residency may be evicted
    - planning cache may recycle
    - persistent edit remains

13. Area reloads:
    - deterministic baseline regenerates
    - persistent edit reapplies
    - current world is reconstructed
```

At no point does LayerProcGen own the castle after it is generated.

---

## 76. Example: why Streaming should not call the scheduler

Tempting design:

```csharp
public sealed class RegionLoader
{
    private readonly IWorldGenerationScheduler _scheduler;

    public void QueueLoad(RegionLoadRequest request)
    {
        _scheduler.Acquire(...);
    }
}
```

Avoid this as the default architecture.

It makes physical region residency the universal trigger/shape for world planning and makes it harder to support:

- maps,
- previews,
- server prewarm,
- semantic planning at coarser scales,
- generation without voxel residency.

Prefer composition:

```text
WorldRuntimeCoordinator
    -> generation scheduler
    -> Streaming
    -> materialization
```

with each capability independently testable.

---

## 77. Example: world-plan facts versus voxel commands

Avoid:

```csharp
public readonly struct GeneratedCastle
{
    public IReadOnlyList<VoxelWrite> Writes;
}
```

as the planning-layer output.

Prefer:

```csharp
public readonly struct ResolvedStructure
{
    public readonly GenerationFeatureId Id;
    public readonly StructureArchetypeId Archetype;
    public readonly int3 Origin;
    public readonly RotationQuarterTurns Rotation;
    public readonly WorldBounds Bounds;
    public readonly StructureGenerationParameters Parameters;
}
```

Then a structure materializer can produce voxel/SDF output for whichever physical region is requested.

This is the key to sharing one plan across multiple Y regions and non-voxel consumers.

---

## 78. Example: deterministic owner

Suppose a site's anchor lies at world XZ `(10923, 4491)` and planning chunks are 256 x 256.

Canonical owner:

```csharp
int2 ownerChunk = new(
    FloorDiv(anchorX, 256),
    FloorDiv(anchorZ, 256));
```

Only that chunk creates the site's fact.

Neighboring chunks may receive/query it through padded bounds.

The rule is based on stable world coordinates, not whichever chunk happened to execute first.

Use project-standard floor division for negative coordinates.

---

## 79. Example: stale completion

```csharp
private void AdmitCompletion(in WorldGenerationCompletion completion)
{
    if (completion.Epoch != _generationEpoch)
    {
        _metrics.StaleEpochCompletion();
        return;
    }

    if (!_demands.IsRelevant(completion.RequestId, completion.ChunkId))
    {
        _metrics.NoLongerRelevantCompletion();
        return;
    }

    if (!_materializationQueue.TryEnqueue(completion))
    {
        // Do not make this an unbounded fallback queue.
        _backpressure.OnMaterializationCapacityReached(completion);
    }
}
```

Exact policy may differ, but correctness and boundedness should be visible in the API.

---

## 80. Example: stable seed

```csharp
ulong SiteSeed(
    GenerationIdentity identity,
    GenerationLayerId layer,
    int2 ownerChunk,
    GenerationFeatureId feature)
{
    return StableHash.Combine(
        identity.WorldSeed,
        identity.SchemaVersion,
        layer,
        ownerChunk,
        feature);
}
```

Do not derive it from:

- worker number,
- current time,
- request order,
- current frame,
- `GetHashCode()` where runtime/platform stability is not guaranteed,
- mutable list iteration order.

---

## 81. Testing strategy

Use three levels.

### Pure unit tests

No Unity runtime required where possible.

Test:

- deterministic algorithms,
- canonical ownership,
- seed derivation,
- snapshot projection,
- fact equality,
- clipping math,
- epoch validation.

### Scheduler integration tests

Exercise LayerProcGen implementation:

- dependency padding,
- overlapping demand,
- release/reacquire,
- parallel equivalence,
- chunk recycling,
- completion lifetime.

### PlayMode/performance tests

Exercise:

- moving demand,
- materialization,
- Storage publication,
- render publication,
- frame-time behavior,
- multi-player overlap.

Do not rely on visual showcases as correctness tests. Keep showcases as diagnostics/demos alongside machine-checkable tests.

---

## 82. CI guardrails

Potential automated guards:

- asmdef dependency tests preventing LayerProcGen leakage,
- determinism golden/hash tests,
- generated-fact seam tests,
- no-Unity-reference tests for pure assemblies,
- stress test queue upper bounds,
- performance thresholds where stable enough for CI.

Avoid fragile frame-time assertions on noisy CI machines unless the test harness controls variance sufficiently.

---

## 83. Decision log

### Decision: use LayerProcGen only behind a project-owned scheduler boundary

Status: **accepted for spike**.

### Decision: do not create `VoxelEngine.ProcGen` as a broad engine subsystem

Status: **accepted**.

Exact WorldGen assembly placement remains Phase 0 work.

### Decision: preserve pure `Game.Composition.WorldBuilderWorldGen`

Status: **accepted**.

### Decision: Streaming must not directly depend on LayerProcGen

Status: **accepted**.

### Decision: planning outputs are immutable project-owned facts

Status: **accepted**.

### Decision: LayerProcGen planning space is XZ; physical residency remains XYZ/`int3`

Status: **accepted**.

### Decision: persistent world authority is baseline + edits + runtime state

Status: **accepted**.

### Decision: all async completions carry/validate generation epoch

Status: **accepted**.

### Decision: deterministic seeds derive from stable identities

Status: **accepted**.

### Decision: no-stutter work requires pipeline-wide backpressure

Status: **accepted**.

### Decision: first spike is contextual site placement, not full Kentridge

Status: **accepted**.

---

## 84. Open questions

These should be answered from the existing repository before implementation expands the API surface.

1. What existing WorldGen abstraction most closely matches `IWorldGenerationScheduler`?
2. Do current worldgen facts already have stable IDs we can reuse?
3. What exact representation should `WorldGenerationSnapshot` use?
4. Should one demand request a fact level/layer set explicitly or use named generation-detail profiles?
5. How should priority interact with LayerProcGen top-level dependencies?
6. What project-owned memory model should completion batches use?
7. What materialization work is best represented by Jobs/Burst versus existing workers?
8. How are Storage publication budgets currently measured/enforced?
9. How is render/SDF/GPU publication currently budgeted?
10. Which component owns generation-demand prediction versus Streaming prefetch prediction?
11. How should server-authoritative generation identity be negotiated/validated for clients?
12. What save-game migration policy is required when generator schema versions change?
13. Which kinds of underground generation can remain direct deterministic queries and which may eventually need a separate 3D dependency scheduler?

---

## 85. Immediate next action

Before writing integration code:

1. Inspect current WorldGen `Api`/`Runtime` assemblies and types.
2. Inspect current `Game.Composition` asmdef dependency graph.
3. Inspect current Structures inputs/materialization APIs.
4. Inspect current RegionLoader/Streaming composition call sites.
5. Inspect Storage commit and rendering publication paths.
6. Map proposed types in this document to existing types.
7. Update this document with the **exact** selected classes/assemblies.
8. Only then add/pin LayerProcGen and build the isolated site-placement spike.

The goal of Phase 0 is to reuse the architecture we already have, not create a parallel world-generation framework.

---

## 86. Final architectural test

Before merging any LayerProcGen integration, ask:

> If `LayerProcGenWorldGenerationScheduler` were deleted tomorrow and replaced by `CustomWorldGenerationScheduler`, which modules would change?

The desired answer is approximately:

```text
WorldGen runtime implementation/composition
+ implementation-specific tests/debug tooling
```

The undesired answer is:

```text
WorldBuilder
Streaming
Storage
Structures
Terrain
Rendering
gameplay
persistence
networking
```

If the second list begins to become true, stop and restore the boundary.

That replaceability constraint is the strongest protection against turning a useful spatial scheduling library into the architecture of the game.
