# Data Model: World Feature Authoring

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)
**Date**: 2026-08-07

Everything here is either **immutable and loaded once** (the catalogue), **derived and never
stored** (candidates, instances, primitives), or **stored only on interaction** (instance state).
Nothing scales with world size.

---

## Catalogue — immutable, loaded at world start

### FeatureCatalogue

| Field | Type | Notes |
|---|---|---|
| `Version` | `uint` | Bumped when the opcode set or format changes. Mismatched worlds refuse to load. |
| `CatalogueHash` | `ulong` | Part of world identity with the seed. Two clients must agree or refuse to join. |
| `Definitions` | `FeatureDefinition[]` | ≤ 256 (R-008). Index is the `definitionId`. |
| `PlacementRules` | `PlacementRule[]` | One or more per definition. |

**Invariants**
- Index stability: a definition's index is its identity. Reordering the catalogue changes every
  instance id in the world (accepted — catalogue is world identity).
- `CatalogueHash` covers definitions and rules, so a silent authoring change cannot desync two
  clients that believe they share a world.

### FeatureDefinition

| Field | Type | Notes |
|---|---|---|
| `Kind` | `enum` | `Structure`, `Excavation`, `Landform`, `WaterBody`. Selects generation path. |
| `Parameters` | `ParameterSpec[]` | Name, integer range, default. Draws are integer only. |
| `Footprint` | `int3` | Maximum extent in voxels. ≤ 128 m per axis (R-008). Load-bearing: bounds the scan neighbourhood. |
| `Program` | `ShapeProgram` | Integer opcode array. |
| `Anchors` | `AnchorSpec[]` | Named points resolved per instance (FR-026). |
| `Materials` | `byte[]` | Palette indices used. Validated against the world palette. |
| `BasePlaneRule` | `enum` | `LowestGround`, `MeanGround`, `HighestGround`, `FixedAltitude`. |
| `MaxSlope` | `int` | Steepest ground the definition tolerates, in voxels of rise per 8 voxels of run. |
| `Slots` | `SlotSpec[]` | Composition attachment points (FR-007). |
| `Precedence` | `int` | Higher wins contested space (FR-013). |

**Validation** (FR-009, all before world load)
- Every material index exists in the palette.
- Program output for every corner of the parameter space fits inside `Footprint`.
- No parameter combination produces degenerate geometry (zero-extent primitives, a roof below its
  walls, an opening larger than the face it sits in).
- `Slots` reference definitions that exist and whose footprints fit the slot volume.
- Recursive composition terminates: the slot graph is acyclic.

### ParameterSpec

| Field | Type | Notes |
|---|---|---|
| `Name` | `FixedString32` | Author-facing. |
| `Min`, `Max` | `int` | Inclusive. Draws are `[Min, Max]`. |
| `Quantum` | `int` | Draws snap to multiples, so a wall thickness of 3.5 voxels cannot occur. |

### PlacementRule

| Field | Type | Notes |
|---|---|---|
| `DefinitionId` | `int` | |
| `CellEdge` | `int` | Placement lattice cell, in voxels. Default 64 m (R-008). |
| `AttemptsPerCell` | `int` | Candidates drawn per cell before filtering. |
| `AcceptProbability` | `int` | Out of 65536. Integer, so identical everywhere. |
| `MinAltitude`, `MaxAltitude` | `int` | Voxels. |
| `MaxSlope` | `int` | As above; combined with the definition's own limit. |
| `MinSpacing` | `int` | Voxels between instances of the same definition. |
| `ClusterSize` | `int2` | Min and max instances per cluster. |
| `ExclusionMask` | `int` | Bit per exclusion class the rule respects. |
| `ExplicitPlacements` | `ExplicitPlacement[]` | Authored coordinates that bypass the rule (FR-011). |

**Invariants**
- `MinSpacing` ≤ `CellEdge`, or spacing cannot be enforced from local knowledge alone.
- Every field is integer. No float enters placement (Principle I).

---

## Derived — computed on demand, never stored

### Candidate

Produced by `PlacementLattice` as a pure function of `(seed, definitionId, cellCoord, attempt)`.

| Field | Type | Notes |
|---|---|---|
| `InstanceId` | `ulong` | `hash(definitionId, cellCoord, attempt)` (R-006). |
| `DefinitionId` | `int` | |
| `Origin` | `int3` | World voxel coordinate, jittered inside the cell. |
| `Orientation` | `byte` | One of four cardinal rotations. Integer, so no float transform. |
| `Parameters` | `int[]` | Drawn within each `ParameterSpec` range. |
| `Precedence` | `int` | From the definition; the sort key with `InstanceId`. |

**Invariants**
- Determinism: identical for the same inputs on every platform (SC-001).
- Ordering: `(Precedence, InstanceId)` is a total order, so overlap resolution cannot depend on
  generation order (FR-013).
- Locality: computable without any neighbouring region being resident (FR-015).

### Primitive

Emitted by evaluating a `ShapeProgram` against resolved parameters.

| Field | Type | Notes |
|---|---|---|
| `Shape` | `enum` | `Box`, `Cylinder`, `Prism`, `CapsuleChain`, `Ramp`. |
| `Bounds` | `int3 min, max` | World voxel space. |
| `Material` | `byte` | Palette index. `0` with `Mode = Carve` removes. |
| `Mode` | `enum` | `Fill`, `Carve`, `FillIfEmpty`. |
| `Order` | `int` | Within-instance ordering; later wins. |

**Invariants**
- Every primitive lies inside its instance's footprint, or generation is a defect.
- Clipping to a sub-volume is exact: the union of an instance's primitives clipped to disjoint
  sub-volumes equals the same primitives rasterised whole (Milestone 1 exit criterion).

### ResolvedAnchor

| Field | Type | Notes |
|---|---|---|
| `Name` | `FixedString32` | |
| `Position` | `int3` | World voxel coordinate, after orientation and parameters. |
| `Facing` | `byte` | Cardinal direction. |

Derived, so it resolves identically on every client without replication (SC-011).

---

## Stored — server-authoritative, bounded by interaction

### InstanceState

Allocated on first touch. An untouched instance has no entry and costs nothing.

| Field | Type | Notes |
|---|---|---|
| `InstanceId` | `ulong` | Key. Derived, so it needs no allocation scheme. |
| `Owner` | `PlayerId` | Zero for unowned. |
| `Protected` | `bool` | Rejects alterations inside the footprint (FR-030). |
| `FirstTouchedTick` | `uint` | For eviction ordering if the map ever needs bounding. |

**Invariants**
- Server-authoritative (Principle III). Clients hold a replicated copy and never mutate it
  locally.
- Bounded by the number of instances players have interacted with, not by world size
  (Principle V). Budget: 64 B per touched instance (R-008).
- Survives region eviction: state is keyed by identity, not by residency.
- Session-scoped, consistent with the existing persistence decision.

**State transitions**

```text
(absent) --first touch--> Unowned/Unprotected
Unowned  --claim-->       Owned
Owned    --release-->     Unowned
any      --set protected--> Protected      (server or authored)
Protected --clear-->      previous state
```

Every transition is a server decision and is replicated. A client-side transition is presentation
only until confirmed.

---

## Relationships

```text
FeatureCatalogue 1 ── * FeatureDefinition 1 ── * ParameterSpec
                 1 ── * PlacementRule   * ── 1 FeatureDefinition
FeatureDefinition 1 ── * AnchorSpec
FeatureDefinition 1 ── * SlotSpec  * ── 1 FeatureDefinition   (acyclic)
FeatureDefinition 1 ── 1 ShapeProgram

PlacementRule + seed + cellCoord ──► Candidate *          (derived)
Candidate + ShapeProgram         ──► Primitive *          (derived)
Candidate + AnchorSpec           ──► ResolvedAnchor *     (derived)
Candidate.InstanceId             ──► InstanceState 0..1   (stored, server)
```

## What is deliberately absent

- **No instance registry.** Nothing enumerates the instances in the world. Queries are answered by
  regenerating the candidates for the cells covering the area of interest.
- **No placement cache.** Caching candidates would be an optimisation that introduces a second
  source of truth for where things are, and would need invalidation on eviction.
- **No stored geometry.** A feature's voxels exist only in the brickmap, alongside terrain, and
  are regenerated on demand.
