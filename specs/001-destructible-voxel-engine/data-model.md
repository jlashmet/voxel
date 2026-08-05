# Data Model — Destructible & Buildable Multiplayer Voxel World

**Created**: 2026-08-04
**Feeds**: [plan.md](./plan.md) · derived from [spec.md](./spec.md) Key Entities

Sizes are indicative at 10 cm voxels with 8³ bricks and 64³-brick regions; treat as the starting configuration, not a commitment.

---

## Storage tier

### Voxel

| Field | Type | Notes |
|---|---|---|
| material | `byte` | 0 = empty. Material class index into a palette. |

Discrete by constraint C-003 — present or absent, never partial. Not an addressable object; exists only as a byte inside a brick.

**Validation**: material index must exist in the session's material palette.

### Brick

The unit of allocation. 8³ = 512 voxels.

| Field | Type | Size | Notes |
|---|---|---|---|
| voxels | `byte[512]` | 512 B | Only allocated for *mixed* bricks |
| occupancy | `ulong[8]` | 64 B | One bit per voxel; drives raymarch skipping, connectivity, support |

Three states, and the distinction is the whole memory argument:

| State | Representation | Cost |
|---|---|---|
| Empty | Null pointer in the region's brick grid | 0 B |
| Uniform | Pointer to a shared palette brick for that material | 0 B marginal |
| Mixed | Index into the brick pool | 576 B |

**Invariant**: a brick whose voxels become uniform must be collapsed back to a palette pointer and its pool slot freed. Failing to do this is the slow memory leak this design is most susceptible to.

### BrickPool

| Field | Type | Notes |
|---|---|---|
| storage | `NativeArray<byte>` | One large allocation; fixed size from the tiering budget |
| occupancy | `NativeArray<ulong>` | Parallel array |
| freeList | `NativeList<int>` | Indices of unused slots |
| capacity | `int` | Set per device class (C-006 permits this) |

**Invariant**: total resident bricks never exceeds capacity. Exhaustion triggers region eviction, never allocation failure.

### Region

The unit of streaming, persistence, replication scoping, and moderation. 64³ bricks ≈ 51 m cube.

| Field | Type | Size | Notes |
|---|---|---|---|
| coord | `int3` | 12 B | Key in the region table |
| brickPointers | `int[262144]` | ~1 MB | Index into pool, or sentinel for empty/uniform |
| occupancyMips | `ulong[]` | ~100 B–few KB | Levels 0..N; level N is the always-resident far-field/structural summary |
| eventLog | `RegionEventLog` | variable | Server-side; see below |
| residency | `enum` | — | `Cold` / `Warm` / `Hot` (server) · `Resident` / `Evicted` (client) |
| dirty | `bool` | — | Server only; client eviction needs no write-back |
| lastAccessTick | `uint` | — | LRU key |

**State transitions (server)**: `Cold → Warm` on load · `Warm → Hot` when a player enters · `Hot → Warm` when the last player leaves and the rollback window expires · `Warm → Cold` on eviction, write-back if dirty.

**State transitions (client)**: `Evicted → Resident` on entering load radius · `Resident → Evicted` on leaving unload radius. **Load and unload radii must differ** — hysteresis is a correctness requirement, not a tuning preference.

**Invariant**: the top mip level of every region stays resident on the server permanently, so far-field visibility and cross-region structural queries never page anything in.

### RegionTable

| Field | Type | Notes |
|---|---|---|
| regions | `NativeHashMap<int3, RegionHandle>` | Sparse — world extent costs nothing |

A flat top-level grid is not viable: a 10 km world at 0.8 m bricks is ~2×10¹² entries. Sparsity here is the enabling structure, not an optimisation.

---

## Simulation tier

### AlterationEvent

The unit of replication, the moderation record, and the rollback substrate. Transmits the *cause*, not the effect.

| Field | Type | Size | Notes |
|---|---|---|---|
| kind | `byte` | 1 B | Explosion / brush / raw-batch |
| tick | `uint` | 4 B | Server tick; the log's query key |
| origin | `int3` | 12 B | Voxel coordinate |
| shape | union | ~8 B | Radius, or brush extents + rotation, or prefab id |
| material | `byte` | 1 B | Placement only |
| seed | `uint` | 4 B | Deterministic expansion |
| playerId | `ushort` | 2 B | Attribution (FR-020); also the arbitration tie-break |
| sequence | `ushort` | 2 B | Ordinal within the tick, completing the total order |

~32 B expands deterministically to thousands of voxel writes. This is what makes SC-002 achievable and what makes 64 players fit a mobile connection.

**Validation** (server, before acceptance — FR-018 to FR-021, FR-032): within player reach · not inside a protected zone · within the player's rate budget · within the region's density cap · placements attach to existing structure · placements do not intersect an occupied player volume.

**Arbitration** (FR-011, R-010): competing alterations are totally ordered by `(tick, playerId, sequence)`, with material priority breaking ties between same-tick placements into the same voxel. The server assigns the order; clients adopt it without re-deriving. Material priority exists specifically because otherwise the winner depends on hash-map iteration order, which is not stable across runs.

**Invariant**: expansion is integer arithmetic with a seeded PRNG, in Burst, on the CPU. Never GPU, never float (R-008).

### RegionEventLog

| Field | Type | Notes |
|---|---|---|
| events | ring buffer of `AlterationEvent` | Ordered by tick |
| tickIndex | `NativeHashMap<uint, int>` | **Query by tick — required from day one** |
| compactedThrough | `uint` | Events at or below this tick are folded into the snapshot |

Serves four consumers: moderation history (FR-023), lag compensation, reconciliation rollback, and compaction input. The tick index is the retrofit the plan most wants to avoid; build it first.

**Invariant**: the log never grows unbounded. Segments older than the rollback window compact into a baked brick snapshot — log unbounded, snapshot bounded by region volume.

### SpeculativeOverlay

Client-local. Never authoritative (C-005).

| Field | Type | Notes |
|---|---|---|
| pending | `NativeHashMap<int3, PendingBrick>` | Keyed by brick coordinate, parallel to the real grid |
| submittedTick | `uint` | For matching against server response |

**State transitions**: `Pending → Confirmed` (promote into the real grid, discard overlay entry) · `Pending → Rejected` (dissolve with animation, surface reason — FR-009).

**Invariant**: rendered visibly distinct so provisionality is legible. Collision resolves against one side deterministically — never a blend (C-003).

### DebrisBody

| Field | Type | Notes |
|---|---|---|
| brickRef | `int` | Pool index |
| transform | `float3` + `quaternion` | Presentation-side; float is acceptable here |
| velocity | `float3` | — |
| settled | `bool` | On settle, re-bake into the grid |
| visualOnly | `bool` | **Load-bearing flag** |

`visualOnly` debris may be culled per device tier. Debris that settles and rejoins the grid changes world state and **may not** be culled (C-006). Conflating the two is a divergence bug waiting to happen — hence an explicit field rather than a convention.

### SupportField

| Field | Type | Notes |
|---|---|---|
| support | per-brick `byte` | Propagated outward from anchored bricks, decrementing with distance |
| threshold | `byte` | Below this, collapse into debris |

Rides along with the connectivity pass over the same occupancy mips — near-zero marginal cost. Permits cantilevers and arches; forbids floating islands.

**Invariant**: borders of unloaded regions are treated as **anchored**. Conservative by design — structures fail to collapse rather than collapsing wrongly (SC-008).

---

## Presentation and platform tier

### DetailLevel

Derived, not stored — it *is* the occupancy mip hierarchy, consumed at a chosen level.

| Consumer | Level |
|---|---|
| Near field render | 0 (full bricks) |
| Mid field | 2–3 |
| Far field / implicit raymarch | 5+ |
| Far-field replication payload | 5+ |
| Server structural graph | Top level, always resident |

One structure, five consumers. Its layout is effectively a public interface.

### ProtectedZone

| Field | Type | Notes |
|---|---|---|
| regionCoord | `int3` | — |
| mask | `ulong[]` | Coarse per-region bitmask of no-build volume |

Cheap lookup during validation. Covers spawns and objectives (FR-018).

### DeviceTierBudget

| Field | May tier? |
|---|---|
| brickPoolCapacity | Yes |
| fullDetailRadius / mipTransitionDistance | Yes |
| raymarchResolution / maxStepsPerRay | Yes |
| irradianceProbeDensity | Yes |
| maxVisualOnlyDebris | Yes |
| maxViewDistance | Yes |
| **interestRadius** | **No** — the specific C-006 trap |
| **tickRate, reconciliation window** | **No** |
| **collision, hit resolution, world state, integer sim jobs** | **No** |

This table is a test matrix (SC-013), not a convention. Concrete per-tier values live in [device-matrix.md](./device-matrix.md); three tiers only — PC, Console, Mobile-HE.

---

## Cross-cutting invariants

1. **Discreteness** — no partial occupancy anywhere in the model (C-003).
2. **Single source** — visual and collision derive from the same bricks (C-004).
3. **Determinism** — every field participating in cross-client agreement is integer; float fields are presentation-only (R-008).
4. **Surface-proportional allocation** — empty and uniform bricks cost nothing; violating this makes km-scale infeasible.
5. **Tiering boundary** — device class touches presentation fields only (C-006).
6. **Bounded growth** — pool capped by configuration; event logs compacted; player allocation budgeted (FR-022, R-007).
