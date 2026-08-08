# Implementation Plan: World Feature Authoring

**Feature Directory**: `002-world-feature-authoring`
**Created**: 2026-08-07
**Spec**: [spec.md](./spec.md)
**Status**: Draft

## Summary

Feature definitions are authored as data: parameters with declared ranges, compiled to an integer
**shape program** that emits **primitives** — boxes, cylinders, prisms, capsule chains — rather
than voxels. A **placement lattice** decides where instances go: for each cell of a fixed grid, a
seeded hash of `(seed, definitionId, cellCoord)` yields candidates with jittered positions and
parameter draws. A region generating itself scans the bounded neighbourhood of cells that could
reach into it, regenerates those candidates from the hash, clips their primitives to its own
bounds, and rasterises them into the brickmap alongside terrain.

Nothing is stored and nothing is communicated. Two regions generated on different machines in
different orders derive the same instances because both compute the same pure function of cell
coordinate. Identity is derived the same way, so instances are addressable without a registry;
only mutable state — ownership, protected status — is stored, and only for instances a player has
actually touched.

## Technical Context

| Aspect | Decision |
|---|---|
| Language / Runtime | C# on Unity 6000.5.6f1, Burst-compiled jobs, Collections and Mathematics only. No `com.unity.entities`. |
| Primary Dependencies | Existing brickmap storage (`Core/Storage`), terrain height function, region streaming, material palette, protected zones, occupancy mips. |
| Storage | Feature shape: none — derived from seed and catalogue. Mutable instance state: server-side map keyed by derived id, bounded by touched instances. Catalogue: immutable blob loaded at world start. |
| Testing | Parity tests for cross-platform and cross-order determinism; EditMode tests for the shape-program evaluator and catalogue validation; PlayMode tests for streaming budget and seams. All Unity runs via `tools/unity-run.sh`. |
| Target Platform | PC, console, high-end mobile. Feature existence and placement are identical on all three (Principle IV). |
| Performance Goals | Per `device-matrix.md`, extended per research R-008: 8 ms feature generation per region, ≤4096 primitives and ≤512 candidates scanned per region. |
| Constraints | Region-local generation; integer-only in `Core`; server authority for mutable state; bounded memory; one geometric representation. |
| Scale / Scope | 4 km × 4 km × 1 km world, ≤256 definitions per catalogue, max footprint 128 m, placement cell 64 m. |

## Constitution Check

| Principle | Assessment |
|---|---|
| **I — Determinism is integer and CPU-side** | PASS. Candidate hashing, parameter draws, shape-program evaluation, primitive rasterisation, and terrain adaptation are integer arithmetic in Burst jobs. Shape programs carry integer operands only; the evaluator has no `float` path. Far-field coarse rasterisation runs on the GPU but is presentation only and produces no authoritative state. |
| **II — One source of truth for geometry** | PASS. Primitives are an intermediate representation, not a second world: they are rasterised into the same brickmap, and collision and rendering read that brickmap through the existing traversal. Nothing queries primitives at runtime. **Watch**: the far field rasterises primitives directly, making it a second *approximation* of the same source — acceptable under the existing far-field precedent, but it must never be consulted by collision. |
| **III — Server authority; prediction is presentation** | PASS. Feature shape is derived identically by server and client, so it needs no authority. Ownership and protected status are server-owned, replicated, and validated on the single existing mutation path. Protected-instance rejections carry a reason (FR-030). |
| **IV — Device class affects presentation only** | PASS. Every number in R-008 is identical across tiers because all are simulation parameters. Only far-field feature detail may tier. |
| **V — Bounded resources by construction** | PASS. Candidate scan is bounded by max footprint; primitives per region are capped; instance state is allocated on first touch and bounded by interaction, not world size; the catalogue is fixed at world start. **Watch**: the primitive cap must fail loudly (FR-036) rather than truncate silently. |
| **VI — Quantitative targets before optimisation** | PARTIAL. R-008 proposes numbers; they are not yet in `device-matrix.md`, which is the authoritative document. **Gate**: Milestone 0 adds them there before any implementation work is scheduled. |

No unjustified violations. One gate (VI) must close before Milestone 1.

## Project Structure

```text
Assets/VoxelEngine/
  Core/
    Features/
      FeatureCatalogue.cs         # immutable blob: definitions, placement rules, precedence
      FeatureDefinition.cs        # parameters, ranges, footprint, anchors, materials
      ShapeProgram.cs             # integer opcode array + evaluator
      ShapeOps.cs                 # opcode set
      Primitive.cs                # box | cylinder | prism | capsule chain | ramp
      ParameterDraw.cs            # seeded integer draws within declared ranges
      PlacementLattice.cs         # cell -> candidates, pure function of (seed, def, cell)
      CandidateScan.cs            # region -> candidates intersecting it, ordered
      InstanceId.cs               # derived identity + anchor resolution
      TerrainAdaptation.cs        # base plane rule -> fill/carve primitives
      PrimitiveRasteriser.cs      # clip to sub-volume, write bricks
      CatalogueValidation.cs      # FR-009 checks
    Terrain/
      CaveLattice.cs              # portal-anchored tunnel networks
  Net/Server/
    InstanceState.cs              # ownership, protected status, keyed by derived id
    InstanceStateReplication.cs
  Rendering/
    FarFieldFeatures.cs           # coarse primitive rasterisation for the distant view
  Tools/
    FeaturePreview.cs             # isolated preview, parameter sweep (FR-038)
    PlacementInspector.cs         # why was / wasn't this placed (FR-039)

specs/002-world-feature-authoring/
  spec.md  plan.md  research.md  data-model.md  quickstart.md
  contracts/
    catalogue-format.md           # authoring contract: definitions, parameters, rules
    shape-program.md              # opcode set and evaluation semantics
    module-interfaces.md          # engine-facing surfaces
```

## Architecture

```text
Catalogue (immutable, loaded once)
        │
        ▼
PlacementLattice ──hash(seed, defId, cell)──► Candidates {id, pos, orientation, params}
        │                                            │
        │                                            ▼
        │                                    CandidateScan (region ± maxFootprint,
        │                                    ordered by precedence then id)
        ▼                                            │
ShapeProgram.Evaluate(params) ──► Primitives ────────┤
        │                                            │
TerrainAdaptation(height fn, footprint) ─────────────┤
                                                     ▼
                                        PrimitiveRasteriser (clip to region slice)
                                                     │
                                                     ▼
                                          Brickmap  ◄── terrain generation
                                                     │
                          ┌──────────────────────────┼───────────────────────┐
                          ▼                          ▼                       ▼
                    Collision (DDA)          Raymarch renderer        Occupancy mips
                                                                             │
                                                                             ▼
                                                                   Far field (+ coarse
                                                                   primitives, presentation)

InstanceId ──► InstanceState (server) ──► replication ──► protection check on the edit path
```

The important property of this diagram is what is **missing**: no arrow returns from the brickmap
to placement, and no arrow crosses between regions. Generation is a one-way function from
`(seed, catalogue, region coordinate)` to voxels.

## Phases

### Phase 0: Research — complete

[research.md](./research.md) resolves: placement lattice (R-001), deterministic overlap resolution
(R-002), shape programs and primitives (R-003), cave connectivity via portal anchoring (R-004),
region-local terrain adaptation (R-005), derived identity (R-006), far-field representation
(R-007), budgets (R-008), static water (R-009).

### Phase 1: Design & Contracts — complete

[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md).

### Phase 2+: Implementation Milestones

**Milestone 0 — Budgets recorded**
Add the R-008 table to `device-matrix.md` as simulation parameters, identical across tiers.
*Exit*: numbers are in the authoritative document; Constitution gate VI closes.

**Milestone 1 — Shape programs and primitives**
Opcode set, evaluator, primitive types, rasteriser with sub-volume clipping. No placement yet:
stamp one definition at a fixed coordinate.
*Exit*: a definition rasterised as a whole equals the same definition rasterised in eight separate
sub-volumes, voxel for voxel. Evaluator has no float path (analyzer-enforced).

**Milestone 2 — Placement lattice and identity**
Candidate generation, bounded neighbourhood scan, precedence ordering, derived identity.
*Exit*: generating a 3×3 region block in 100 shuffled orders produces byte-identical worlds;
identities are stable across eviction and regeneration.

**Milestone 3 — Terrain adaptation**
Base plane rules, fill and carve prisms from the height function.
*Exit*: structures on slopes up to the declared maximum meet the ground on all sides (SC-007),
with no step at region boundaries (SC-003).

**Milestone 4 — Composition and validation**
Composition slots, catalogue validation, degenerate-parameter detection.
*Exit*: a castle expressed as keep + walls + towers + gatehouse generates correctly across four
regions; every failure mode in FR-009 is reported before world load (SC-010).

**Milestone 5 — Caves and water**
Portal-anchored cave lattice; static water volumes.
*Exit*: every surface opening is traversable from inside (SC-008); water volumes generate, are
destructible, and do not refill (FR-023).

**Milestone 6 — Identity state, protection, replication**
Server-side instance state, ownership, protected status on the edit validation path.
*Exit*: protected instances reject alterations with a reason in 100% of attempts (SC-013); a
joining client receives current state (SC-012).

**Milestone 7 — Far field and distant visibility**
Coarse primitive rasterisation in the far field.
*Exit*: a large structure on a ridge is identifiable at maximum view distance (SC-006).

**Milestone 8 — Authoring tools**
Isolated preview with parameter sweeps; placement inspector.
*Exit*: a designer adds a feature type and sees it in world in under 30 minutes (SC-002); one
definition yields ten distinguishable instances by parameters alone (SC-014).

## Complexity Tracking

| Deviation | Justification |
|---|---|
| An intermediate representation (primitives) between definition and voxels | Sub-volume evaluation (FR-008) is the whole problem. Per-voxel evaluation costs the full sub-volume per feature regardless of overlap, and gives the far field nothing cheap to rasterise. The intermediate form is what makes region-local generation affordable. |
| A bespoke opcode set rather than an existing scripting language | Principle I forbids float and requires Burst-compiled determinism. No off-the-shelf scripting runtime meets both. |
| Redundant full-footprint terrain sampling in every region touching an instance | The alternative — sampling only the local slice — produces different base planes per region and therefore steps at region boundaries, failing SC-003. Redundancy is bounded and cheap. |
| Cave connectivity guaranteed locally, not globally | Global connectivity needs global knowledge. The spec's promise (SC-008: every surface opening reachable from inside) is a local property and is met. |

## Risks

1. **Max footprint becomes a design straitjacket.** Everything bounded — scan cost, memory, seams
   — depends on it. A designer wanting a 500 m castle breaks the scan budget for every region in
   the world. *Mitigation*: validation rejects oversized definitions at authoring time; very large
   structures are expressed as composed neighbours, not one instance.

2. **Parametric-only authoring proves too rigid.** The primitive set will express buildings and
   tunnels well and organic or ornamented forms badly. If designers reject the output, the
   authoring decision needs revisiting, and templates are a different architecture. *Mitigation*:
   bring Milestone 8 preview tooling forward far enough to find this before the rest is built on
   it.

3. **The 8 ms generation budget is guessed.** Measured region generation is already ~45 ms with
   terrain alone, and meshing was removed only because the raymarch replaced it. Features add to a
   budget that is already tight. *Mitigation*: Milestone 1 measures before Milestone 2 builds on
   the assumption; the number moves in `device-matrix.md` if measurement disagrees.

4. **Terrain adaptation quality.** "Meets the ground on all sides" is easy to state and hard to
   make look good on real terrain; the failure is ugly rather than incorrect, so tests pass while
   the world looks wrong. *Mitigation*: SC-007 needs a visual check, not only an automated one.

5. **Cave portals produce gridded cave systems.** Portals anchored to a lattice face may read as
   obviously grid-aligned. *Mitigation*: jitter portal position within the face and vary portal
   probability with depth; accept that this is a look problem to iterate on.

6. **Instance state lifetime.** Ownership is session-scoped by inheritance from the existing
   persistence decision, which may surprise: a player who claims a castle loses it at session end.
   A product question, not a technical one.
