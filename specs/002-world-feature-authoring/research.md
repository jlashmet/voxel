# Phase 0 Research: World Feature Authoring

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)
**Date**: 2026-08-07

Every decision here is forced by one constraint: **a region must generate its slice of the world
knowing only the seed, the catalogue, and its own coordinates** (FR-015, Constraint
"Region-local generation"). Regions stream in any order, are evicted, and regenerate. Anything
that needs a global pass, a neighbour, or an accumulated data structure is not available.

---

## R-001: How a region discovers which features overlap it

**Decision**: A **placement lattice**. The world is divided into placement cells of a fixed edge
length. For each cell and each definition, a seeded integer hash of `(seed, definitionId,
cellCoord)` deterministically yields zero or more *candidates*: a jittered position inside the
cell, an orientation, and the parameter draws. To generate a region, the generator visits every
cell within `ceil(maxFootprint / cellEdge)` cells of the region bounds, regenerates those
candidates from the hash, and keeps the ones whose footprint intersects the region.

**Rationale**: Candidate generation is a pure function of cell coordinate, so any region computes
the same candidates for the same cells without communication. The neighbourhood is bounded
because every definition declares a maximum footprint, so the work per region is bounded and
known ahead of time. This is the only structure found that satisfies FR-014, FR-015 and FR-016
simultaneously.

**Consequences**:
- A definition's declared maximum footprint is load-bearing, not documentation. Exceeding it at
  generation time truncates the feature at a region boundary and produces a seam. Validation
  (FR-009) must enforce it, and generation must clip to it.
- Density is expressed per cell, not per square kilometre. The catalogue converts.
- Very large features (a castle spanning four regions) force a large neighbourhood scan for
  *every* region, including regions containing nothing. Mitigated by scanning per definition
  using that definition's own footprint, so one big castle definition does not tax the scan for
  houses.

**Alternatives considered**:
- *Global placement pass at world load*: violates region-local generation and does not survive
  eviction; also unbounded memory in world size (Principle V).
- *Placement stored server-side and replicated*: makes the world's shape authoritative data
  rather than derived, costs bandwidth proportional to feature count, and breaks
  regenerate-from-seed. Rejected.
- *Placement discovered by expanding search from the region outward*: needs neighbour residency.

---

## R-002: How overlaps resolve identically everywhere

**Decision**: Candidates are totally ordered by `(precedence, instanceId)`, both of which are
derived. A region collects every candidate whose footprint intersects *its own* footprint plus
the maximum footprint margin, sorts them by that order, and applies them in order; later
candidates yield where an earlier candidate's footprint has claimed space.

**Rationale**: The winner of a contest depends only on the contestants' derived properties, never
on which region generated first or which arrived first. Two regions that share a contested
feature compute the same outcome independently (FR-013).

**Consequences**: The contest set must be computable locally, which means conflict resolution can
only consider candidates within the margin. Two features further apart than the margin cannot
contend, which is true by construction because their footprints cannot overlap.

**Alternatives considered**: first-writer-wins (order-dependent, diverges); random tiebreak
(needs a shared random stream); spatial priority map (a global structure).

---

## R-003: How a parametric shape is described and evaluated

**Decision**: A definition compiles to a **shape program** — a flat, immutable array of integer
opcodes — which, given a resolved parameter set, emits a list of **primitives**: axis-aligned
boxes, cylinders, prisms, capsule chains, and ramps, each with a material, a mode (fill or
carve), and a precedence. Generation then clips primitives to the sub-volume being generated and
rasterises them. Programs never touch voxels directly.

**Rationale**: This is what makes FR-008 (evaluable over an arbitrary sub-volume) cheap and
exact. A primitive can be clipped analytically, so generating a region's slice of a castle costs
work proportional to the primitives that overlap that region, not to the castle. It also gives
the far field (FR-034) a resolution-independent description to rasterise coarsely, and gives
validation something to check against the declared footprint without running a full generation.

Integer opcodes keep the evaluator inside Principle I: no floats, Burst-compilable, no codegen,
no managed allocation per instance.

**Consequences**:
- Expressiveness is bounded by the primitive set. Organic shapes and fine ornament are awkward;
  buildings, walls, towers, tunnels and terraces are natural. This is the cost of Q3's answer.
- The opcode set is a public contract; adding an opcode is a versioned change.

**Alternatives considered**:
- *Per-voxel evaluation of a signed-distance program*: uniform and elegant, but costs
  O(sub-volume) per feature regardless of how little of it lands in the region, and rules out
  cheap coarse evaluation for the far field.
- *C# class per feature type*: fails FR-001 (authoring requires an engine change).
- *A scripting language*: determinism across platforms becomes a language-runtime problem, and
  Burst compatibility is lost.

---

## R-004: Cave connectivity without global knowledge

**Decision**: **Portal-anchored tunnel networks.** Cave cells are a coarser lattice than the
structure lattice. For each *face* between two adjacent cave cells, a hash of the two cell
coordinates (ordered canonically, so both sides agree) decides whether a portal exists and where
on the face it sits. Within a cell, tunnels are generated as capsule chains connecting that
cell's portals to each other and to any interior chambers, using only that cell's own seed.

**Rationale**: Both cells sharing a face derive the same portal from the same canonical hash, so
tunnels meet exactly at cell boundaries with no negotiation. Connectivity within a cell is
guaranteed by construction because tunnels are generated as a spanning structure over the cell's
portals and chambers. FR-019 becomes a local property.

**Consequences**:
- Global connectivity (can you walk from cave A to cave Z?) is *not* guaranteed, only local
  connectivity through portals. In practice a portal graph with a reasonable portal probability
  percolates, but the specification's promise is per-opening traversability, which this does
  satisfy.
- Surface openings are portals on the topmost cave cell's upper face, and must be reconciled with
  terrain height, which is itself a pure function of position — so both the cave cell and the
  surface agree.

**Alternatives considered**:
- *Flood-fill validation after generation*: needs the whole cave resident.
- *Tunnels as long random walks from a source*: the walk can leave the cell, and its path depends
  on where it started, so a neighbour cannot reproduce the part that enters it.

---

## R-005: Terrain adaptation that stays region-local

**Decision**: Adaptation is expressed as primitives derived from the terrain height function,
which is already a pure function of world coordinates. A definition declares a *base plane rule*
(for example "lowest ground under the footprint", "mean ground", "fixed altitude"), and the
compiler emits a fill prism from the base plane down to the terrain surface, plus a carve prism
above it, sampled at the footprint's own resolution.

**Rationale**: Because terrain height at any `(x, z)` is derivable anywhere without residency,
adaptation needs no neighbour data. Sampling the footprint's corners and a bounded interior grid
is enough to choose a base plane, and every region that touches the instance derives the same
base plane from the same samples.

**Consequences**: The base plane must be computed from the *whole* footprint even when generating
a sliver of it, so each region redundantly samples the full footprint. Bounded and cheap
(hundreds of height samples), and it is what keeps the seam correct.

**Alternatives considered**: adapting per region slice (produces steps at region boundaries —
fails SC-003).

---

## R-006: Instance identity without storage

**Decision**: `instanceId = hash(definitionId, cellCoord, indexWithinCell)`, 64-bit, computed
during candidate generation. Identity is derived, never stored or transmitted. Mutable state
(ownership, protected status) is a server-side map keyed by `instanceId`, populated only when an
instance is first touched, and replicated to interested clients.

**Rationale**: Satisfies FR-025 (stable, identical everywhere, survives eviction) with zero
storage for the untouched majority, keeping Principle V intact: stored state scales with player
interaction, not with world size.

**Consequences**:
- Changing the catalogue or the seed changes identities. Already accepted in the spec
  (catalogue is part of world identity).
- Hash collisions must be treated as a validation concern: 64 bits over a bounded instance count
  makes collision negligible, but the generator should assert distinctness within a scan
  neighbourhood, which is cheap and catches a broken hash immediately.

**Alternatives considered**: sequential ids assigned at placement (requires a global counter,
order-dependent); position-based ids (unstable under parameter changes, and two features can
share a position).

---

## R-007: Features in the distant view

**Decision**: Features contribute to the region occupancy mip like any other voxel content, and
beyond residency the far field rasterises each candidate's primitives at a coarse step. Because
primitives are analytic, coarse rasterisation is cheap and needs no voxel data.

**Rationale**: This is the direct benefit of R-003. The current far field is procedural terrain
only and shows the unmodified world; adding candidate primitives to it puts castles on distant
ridges (FR-034, SC-006) without materialising them.

**Consequences**: The far field shows features as *generated*, not as *altered* — a distant
castle a player has destroyed still appears intact until replicated mip occupancy exists. This is
the same limitation already recorded for the far field, and is not made worse here.

---

## R-008: Budget numbers

**Decision**: `device-matrix.md` is authoritative for numbers (project constraint), so this
feature adds a section to it rather than inventing numbers locally. Values to be added:

| Parameter | PC | Console | Mobile-HE |
|---|---|---|---|
| Feature generation budget per region | 8 ms | 8 ms | 8 ms |
| Max primitives rasterised per region | 4096 | 4096 | 4096 |
| Max candidates scanned per region | 512 | 512 | 512 |
| Max footprint per definition | 128 m cube | 128 m | 128 m |
| Placement cell edge | 64 m | 64 m | 64 m |
| Stored bytes per touched instance | 64 B | 64 B | 64 B |
| Catalogue size limit | 256 definitions | 256 | 256 |

**Rationale**: Principle VI requires a number before work is scheduled. These are simulation
parameters, not presentation parameters, so under Principle IV they are **identical on every
tier** — a castle must exist in the same place on a phone as on a PC. Only how it is *drawn* may
tier.

**Open**: the generation budget interacts with the existing streaming budget rather than adding
to it. Confirm against measured region generation cost during Milestone 1 rather than assuming.

---

## R-009: Enforcing static water

**Decision**: Water is an ordinary palette material with a distinct appearance and a destruction
class that does not spread. No flow simulation, no settling, no reaction to neighbouring
destruction.

**Rationale**: Directly implements the Q1 answer. The existing palette already carries a
`Spreading` class; water in this feature deliberately does not use it.

**Consequences**: FR-024's honesty requirement — authors must be able to see that removing
support leaves water floating — becomes a preview-tool concern, not an engine one.
