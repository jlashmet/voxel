# Contract: Module Interfaces

**Feature**: [../spec.md](../spec.md) · **Plan**: [../plan.md](../plan.md)

Engine-facing surfaces. Signatures are indicative; the contracts are the guarantees beneath them.

## PlacementLattice

```csharp
int CandidatesInCell(in FeatureCatalogue catalogue, uint seed, int definitionId,
                     int3 cellCoord, NativeList<Candidate> results);
```

**Guarantees**
- Pure function of its arguments. No residency, no neighbours, no world state.
- Identical results on every platform (Constitution I).
- Bounded: at most `AttemptsPerCell` results.
- Total order: results are comparable by `(Precedence, InstanceId)` across cells, so callers may
  merge results from many cells and sort without ambiguity.

## CandidateScan

```csharp
void ScanRegion(in FeatureCatalogue catalogue, uint seed, int3 regionCoord,
                NativeList<Candidate> ordered);
```

**Guarantees**
- Returns every candidate whose footprint intersects the region, and no others.
- Scans a neighbourhood bounded by each definition's own footprint, not by the largest in the
  catalogue.
- Result is sorted by `(Precedence, InstanceId)`.
- Never exceeds `MaxCandidatesPerRegion`; exceeding it is reported, not truncated (FR-036).

## ShapeProgram

```csharp
int Evaluate(in ShapeProgram program, in ParameterSet parameters, int3 origin, byte orientation,
             in TerrainSampler terrain, NativeList<Primitive> primitives,
             NativeList<ResolvedAnchor> anchors);
```

**Guarantees**
- See [shape-program.md](./shape-program.md). Pure, total, bounded, footprint-respecting.
- `terrain` exposes only `HeightAt(int x, int z)`, a pure function — the evaluator cannot read
  voxels.

## PrimitiveRasteriser

```csharp
void Rasterise(NativeSlice<Primitive> primitives, int3 subVolumeMin, int3 subVolumeMax,
               ref RegionTable table, ref BrickPool pool);
```

**Guarantees**
- **Sub-volume exactness**: rasterising primitives into disjoint sub-volumes that tile a region
  produces exactly the same voxels as rasterising them into the region at once. This is the
  guarantee SC-003 rests on.
- Writes through the existing voxel write path, so brick allocation, uniform collapse, and dirty
  tracking behave exactly as they do for terrain and edits.
- Respects the brick pool budget and reports rather than truncating.

## InstanceIdentity

```csharp
ulong IdOf(int definitionId, int3 cellCoord, int attempt);
bool TryResolveAnchor(in FeatureCatalogue catalogue, uint seed, ulong instanceId,
                      in FixedString32Bytes name, out ResolvedAnchor anchor);
```

**Guarantees**
- Stable across region eviction, regeneration, and session restart within the same seed and
  catalogue (FR-025).
- Identical on every client (SC-011).
- Resolution needs no stored data: the id encodes enough to regenerate the candidate.

## InstanceState (server)

```csharp
bool TryGet(ulong instanceId, out InstanceState state);
void SetOwner(ulong instanceId, PlayerId owner);
void SetProtected(ulong instanceId, bool value);
bool IsAlterationAllowed(ulong instanceId, PlayerId actor, out RejectionReason reason);
```

**Guarantees**
- Server-authoritative (Constitution III). The client copy is replicated and read-only.
- Entries are created on first touch; absence means unowned and unprotected.
- `IsAlterationAllowed` is called on the single existing mutation path, so no edit route bypasses
  protection.
- Rejections always carry a reason (FR-030, Constitution III).

## FarFieldFeatures (presentation only)

```csharp
void AppendCoarsePrimitives(in FeatureCatalogue catalogue, uint seed, float3 viewer,
                            float radiusMetres, NativeList<Primitive> coarse);
```

**Guarantees**
- Presentation only. No collision query and no authoritative state may consult it
  (Constitution II).
- May tier by device class; the candidates it draws from may not (Constitution IV).
