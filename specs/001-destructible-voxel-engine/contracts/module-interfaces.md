# Contract — Internal Module Interfaces

**Created**: 2026-08-04

Boundaries between the nine components in [plan.md](../plan.md). Recorded as contracts because these seams are where the project's invariants are enforced — most notably that `Core` never depends on rendering, networking, or `UnityEngine`, which is what makes the cross-device parity harness (SC-003, SC-016) possible at all.

Signatures are indicative C#, not final.

---

## Core.Storage

```csharp
int      AllocateBrick();
void     FreeBrick(int brickIndex);
byte     GetVoxel(int3 worldVoxel);
void     SetVoxel(int3 worldVoxel, byte material);
bool     TryGetRegion(int3 regionCoord, out RegionHandle handle);
void     LoadRegion(int3 regionCoord, in RegionBlob blob);
void     EvictRegion(int3 regionCoord);
```

**Contract**: `SetVoxel` that renders a brick uniform MUST collapse it to a palette pointer and free its pool slot. Violating this is the slow leak this design is most susceptible to.

**Contract**: `AllocateBrick` never fails — pool exhaustion triggers eviction, not an error return.

---

## Core.Edits

```csharp
// Burst job. Integer + seeded PRNG only. No float, no GPU, no managed state.
void ExpandEvent(in AlterationEvent evt, ref BrickWriteBuffer output);
```

**Contract**: bit-identical output for identical input on every platform, PC through mobile. This is the load-bearing assumption of event-sourced replication (R-008) and the thing SC-003 tests.

**Contract**: the caller supplies the seed from the authoritative event, never a local RNG.

---

## Core.Occupancy

```csharp
void  RebuildMips(int3 regionCoord, in DirtyBrickSet dirty);
ulong GetOccupancyMask(int3 brickCoord, int mipLevel);
```

**Contract**: mip rebuild is a bitwise OR up the chain, batched per frame across all edits — never a full recompute.

**Contract**: the top mip level is always resident, on both client and server, for every known region. Far-field visibility and cross-region structural queries must never page anything in.

---

## Core.Structure

```csharp
// Burst jobs over occupancy masks.
void ComputeConnectivity(int3 regionCoord, ref ConnectivityResult result);
void PropagateSupport(int3 regionCoord, ref SupportField field);
```

**Contract**: borders of unloaded regions are treated as **anchored**. Conservative by design — structures fail to collapse rather than collapsing wrongly (SC-008).

**Contract**: both operate on occupancy masks, not individual voxels. Per-voxel union-find is the implementation that does not meet frame budget.

---

## Collision

```csharp
bool Raycast(in Ray ray, float maxDistance, out VoxelHit hit);
bool SweepAABB(in Bounds bounds, float3 displacement, out CollisionResult result);
void ExportLocalHulls(int3 centre, int radius, ref NativeList<ConvexHull> hulls);
```

**Contract**: CPU-authoritative, Burst, deterministic. No collision query may consult GPU state.

**Contract**: `Raycast` shares its DDA implementation with the render raymarch. This is how C-004 is enforced structurally rather than by discipline — one traversal, two callers.

**Contract**: `ExportLocalHulls` is the only bridge to Unity physics, and serves debris and vehicles only. Never characters, never hit registration.

---

## Rendering

```csharp
void SubmitBrickUpdate(int brickIndex);      // partial ComputeBuffer.SetData
void InvalidateIrradiance(Bounds region);
void SetTierBudget(in DeviceTierBudget budget);
```

**Contract**: rendering is a pure consumer of storage. It holds no authoritative state and no simulation may read back from it.

**Contract**: `SubmitBrickUpdate` uploads one brick, never the world. An edit costs one partial buffer write.

**Contract**: `SetTierBudget` may adjust presentation parameters only. It has no access to interest radius, collision, or any `Core` job.

---

## Net.Server

```csharp
ValidationResult Validate(in AlterationRequest req, ushort playerId);
void             AppendEvent(in AlterationEvent evt);
bool             TryGetWorldStateAt(uint tick, int3 regionCoord, out RegionSnapshot snap);
void             CompactLog(int3 regionCoord, uint throughTick);
```

**Contract**: `Validate` runs every predicate in FR-018 through FR-021 before any state mutation. No path exists from a client message to `SetVoxel` that bypasses it.

**Contract**: `TryGetWorldStateAt` exists from the first commit. Reconciliation depends on it, and adding it later means rewriting the log — the retrofit this plan most wants to avoid.

---

## Net.Client

```csharp
void ApplySpeculative(in AlterationRequest req);
void ConfirmSpeculative(uint clientTick);
void RejectSpeculative(uint clientTick, RejectionReason reason);
void Reconcile(uint serverTick, in PlayerState authoritative);
```

**Contract**: speculative voxels live in the overlay, never the real grid, until confirmed. Rejection is a discard, never a diff.

**Contract**: `Reconcile` replays inputs against world state **at each replayed tick** via `TryGetWorldStateAt`, not against present state.

**Contract**: rejection always carries a reason to the player (FR-009). Silent dissolution is a defect.

---

## Streaming

```csharp
void SetResidencyCentre(float3 position, float3 velocity);
void OnRegionReceived(int3 coord, in RegionBlob blob, byte mipLevel);
```

**Contract**: load radius and unload radius differ. Hysteresis is a correctness requirement — without it a player on a boundary thrashes every frame.

**Contract**: prefetch follows `velocity`, never view direction. Players look around far faster than they move; gaze-driven prefetch causes the thrash it is meant to prevent.

**Contract**: region population runs on a worker thread and publishes with a single pointer splice. Nothing is built on the main thread.

**Contract**: client eviction performs no write-back. The client owns no truth.

---

## Tiering

```csharp
DeviceTierBudget ResolveBudget(DeviceClass deviceClass);
```

**Contract**: the returned budget may contain presentation parameters only. `interestRadius`, collision parameters, and any `Core` job parameter are structurally absent from `DeviceTierBudget` — enforced by the type, not by review. This is the C-006 trap, and SC-013 is its test.
