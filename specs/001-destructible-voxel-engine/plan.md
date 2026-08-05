# Implementation Plan: Destructible & Buildable Multiplayer Voxel World

**Feature Directory**: `001-destructible-voxel-engine`
**Created**: 2026-08-04
**Spec**: [spec.md](./spec.md)
**Research**: [research.md](./research.md)
**Architecture background**: [architecture-notes.md](./architecture-notes.md)
**Status**: Draft — ready for `/speckit-tasks`

## Summary

Build a Unity client and dedicated server for a kilometre-scale voxel world in which all matter is destructible and buildable at runtime, shared by 32–64 concurrent players across PC, console, and mobile.

The approach rests on one structural choice: **a sparse three-tier brickmap serves as storage, render source, collision source, replication unit, and streaming unit simultaneously.** Because an edit is a single byte written into a pooled buffer rather than a mesh rebuild, destruction and construction are cheap on every axis at once. Rendering is a compute-shader raymarch of that structure, so there is no meshing step. Replication is event-sourced — destruction and building transmit their *cause* ("explosion at P, radius R, seed S"), which expands deterministically on every client via integer Burst jobs — with per-region hashing and authoritative brick repair for drift. The occupancy mip hierarchy built for raymarch skipping doubles as the streaming and far-field replication LOD.

Base terrain is procedural from a seed and costs nothing to store or transmit; only the edit overlay crosses the wire or occupies storage.

## Technical Context

| Aspect | Decision |
|---|---|
| Language / Runtime | C# on Unity, Burst-compiled jobs for all simulation hot paths; HLSL compute shaders for rendering |
| Primary Dependencies | `com.unity.burst`, `com.unity.collections`, `com.unity.jobs`, `com.unity.transport`, URP. **Not** `com.unity.entities` (R-002), **not** Netcode for GameObjects (R-001) |
| Storage | Embedded key-value store, region blob keyed by region coordinate; backend selection deferred to M6 (R-006). Session-scoped only |
| Testing | Burst job unit tests; deterministic replay harness over seeded worlds; two-client parity harness spanning device classes; headless server soak tests |
| Target Platform | PC, console, **high-end mobile only** — crossplay in one instance (C-002). Mid/low-tier mobile out of scope. Budgets specified against the lowest supported class and scaled up |
| Performance Goals | 30 Hz tick; 60 fps / 16.6 ms frame budget all tiers; ≤ 150 ms p95 world-update latency; 64 players; 4 km × 4 km × 1 km world. Full budgets in [device-matrix.md](./device-matrix.md) |
| Constraints | Discrete voxel state, no blending (C-003); visual and collision derived from one source (C-004); server authoritative (C-005); device class affects presentation only (C-006) |
| Scale / Scope | 10–20 cm voxels; several km per axis; client memory capped by configured brick pool, not world size |

## Constitution Check

`.specify/memory/constitution.md` exists as of 2026-08-04. Six principles, evaluated below.

| Principle | Status | Evidence |
|---|---|---|
| **I. Determinism is integer and CPU-side** | ✅ Pass | R-008; all `Core` computation is integer Burst jobs; analyzer rule forbids `float` in `Core/`; SC-003 and SC-016 test it |
| **II. One source of truth for geometry** | ✅ Pass | C-004; `Raycast` and the render raymarch share one DDA implementation (`contracts/module-interfaces.md`); SC-012 tests it |
| **III. Server authoritative; prediction is presentation** | ✅ Pass | C-005; speculative overlay is structurally separate from the grid; single validation choke point; SC-011 tests it |
| **IV. Device class affects presentation only** | ✅ Pass | C-006; `DeviceTierBudget` structurally omits simulation parameters; SC-013 tests it |
| **V. Bounded resources by construction** | ✅ Pass | Fixed-capacity brick pool with eviction; log compaction; per-player budgets; SC-005 and SC-010 test it |
| **VI. Quantitative targets before optimisation work** | ✅ Pass *(was failing)* | Now satisfied by [device-matrix.md](./device-matrix.md). **Previously violated**: four success criteria referenced budgets no artifact defined, and M0 had no pass threshold. |

**No violations.** Principle VI was violated at first evaluation and is now resolved; the finding that surfaced it (analysis U1) is closed.

**Post-design re-evaluation**: all six hold after Phase 1 design without exceptions. Principle II is the one most at risk during implementation, since the pressure to add a second geometric representation for collision convenience will be real and will look reasonable each time.

## Project Structure

```text
Assets/
  VoxelEngine/
    Core/                     # No Unity dependencies beyond Collections/Burst
      Storage/                # BrickPool, RegionTable, Region, palette bricks
      Occupancy/              # Mip hierarchy build + query
      Edits/                  # Brush expansion, event application (integer, Burst)
      Structure/              # Connectivity flood-fill, support propagation
      Terrain/                # Seeded procedural generation
    Collision/                # DDA raycast, swept-AABB character queries, hull export
    Rendering/
      Shaders/                # Raymarch, mip build, implicit far-field
      RenderFeature/          # ScriptableRendererFeature, buffer management
      Irradiance/             # Probe cache, invalidation, reconvergence
      Debris/                 # Debris body integration + indirect draw
    Net/
      Transport/              # UTP channel setup: event / repair / bulk
      Protocol/               # Message encode/decode (see contracts/)
      Client/                 # Prediction, speculative overlay, reconciliation
      Server/                 # Authority, validation predicates, region log, compaction
      Interest/               # Spatial filtering, shared by world and player replication
    Streaming/                # Region load/evict, hysteresis, prefetch, worker jobs
    Tiering/                  # Device class detection, budget tables
    Tools/                    # Brush editor, deterministic serialisation, replay
  Tests/
    EditMode/                 # Burst job units, encode/decode round-trips
    PlayMode/                 # Streaming, collision, prediction
    Parity/                   # Cross-client and cross-device determinism harness
Server/                       # Headless build config, region store, soak harness
```

`Core/` deliberately has no dependency on rendering, networking, or `UnityEngine` beyond native collections. It is the deterministic layer, and its isolation is what makes the parity harness possible.

## Architecture

Nine components. The dependency direction is strictly downward — `Core` knows nothing of the layers above it.

| Component | Responsibility | Key constraint |
|---|---|---|
| **Storage** | Brick pool with free list, sparse region table, palette bricks for uniform volumes | Allocation follows surface area, never volume |
| **Occupancy** | Mip hierarchy over occupancy masks; bitwise OR up the chain, batched per frame | Serves raymarch skipping, streaming LOD, and far-field replication from one structure |
| **Edits** | Brush and explosion expansion into voxel writes | Integer + seeded PRNG only; identical on client and server |
| **Structure** | Connectivity flood-fill and support propagation over occupancy masks | Bitwise, region-parallel; unloaded region borders treated as anchored |
| **Collision** | DDA raycast, swept-AABB character queries, convex hull export for Unity physics | CPU-authoritative; shares the DDA with the raymarch (C-004) |
| **Rendering** | Compute raymarch, implicit far-field, irradiance probe cache, debris indirect draw | Reads storage; never authoritative |
| **Net** | Three channels, event sourcing, validation, region log, repair | Server-authoritative; world channel is custom, player channel shares its tick loop |
| **Streaming** | Region residency, hysteresis, off-thread population, prefetch along movement | Client eviction needs no write-back; server eviction does |
| **Tiering** | Device class → budget table | May touch presentation parameters only (C-006) |

Two structures do disproportionate work and deserve naming:

**The occupancy mip hierarchy** is built once and consumed four ways — empty-space skipping in the raymarch, streaming detail level, far-field replication payload, and the substrate for connectivity and support propagation. Changes to it ripple widely; treat its layout as a stable interface early.

**The region event log** is the moderation record (FR-023), the lag-compensation mechanism, the rollback substrate for reconciliation, and the input to compaction. It must be queryable by tick from the start — this is the retrofit the plan most wants to avoid.

## Phases

### Phase 0: Research — complete

[research.md](./research.md). Nine decisions recorded. The §8.3 netcode gate is closed: **build custom on UTP**; Fusion 2 eliminated (non-networked state is not rolled back during resimulation and no restoration hook is documented), Netick retained as a viable fallback. One item is deliberately unresolved — R-004, mobile raymarch throughput — because it is a hardware measurement, not a desk decision.

### Phase 1: Design & Contracts — complete

[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md).

### Phase 2+: Implementation Milestones

Ordered so that each milestone de-risks the next, and so that the two things that cannot be retrofitted — tick-queryable region log, and the mobile rendering answer — land early.

**M0 — Mobile raymarch spike.** A throwaway brickmap raymarch at target resolution on Mobile-HE hardware, alongside the implicit-only contingency path.
*Exit*: **≤ 9 ms voxel rendering at 0.75 render scale on Mobile-HE**, and no thermal throttle over 20 minutes ([device-matrix.md](./device-matrix.md)). Blocks M4.
*Note*: expected-pass since the mobile narrowing to flagship devices. No longer the top project risk, and no longer blocking on an unmade product decision — the device class is now defined tightly enough that model selection does not gate the architecture.

**M1 — Core storage and edits.** Brick pool, free list, palette bricks, sparse region table, seeded terrain generation, brush and explosion expansion, occupancy mip build. All Burst, all integer, no rendering.
*Exit*: 10,000 scripted edits replay to byte-identical state across two processes on different hardware (SC-003 precondition). Growth-bounding policy (R-007) settled before the allocator is finalised.

**M2 — Networking spine.** UTP with three channels; event/repair/bulk protocol per `contracts/wire-protocol.md`; server authority and validation predicates; region event log **queryable by tick**; interest management; player state replication and the shared tick loop.
*Exit*: two clients on different hardware converge on identical world state under injected packet loss (SC-016). Reconciliation replays player inputs against historical world state.
*Decision point*: if the custom tick loop is behind schedule, reconsider Netick (R-001).

**M3 — Collision.** DDA raycast in Burst jobs, swept-AABB character controller, convex hull export bridging to Unity physics for debris and vehicles.
*Exit*: character traversal and hit registration correct against a world mutating underneath, with visual and collision representations demonstrably identical (C-004, SC-012 precondition).
*Note*: largest and most underestimated work item (risk 2). Budget accordingly; do not compress.

**M4 — Rendering.** Compute raymarch with mip skipping, implicit far-field fallback, URP `ScriptableRendererFeature` integration, persistent recycled buffers.
*Exit*: the world renders at target frame rate on PC, and on mobile at whichever fidelity M0 determined.

**M5 — Destruction and construction gameplay.** Connectivity flood-fill, support propagation, debris bodies with settle-and-rebake, generative build brushes, speculative overlay with visible pending state and rejection reasons, concurrent-edit arbitration, player-occupied volume rejection.
*Exit*: acceptance scenarios 1–3 pass. SC-002, SC-007, SC-008, SC-017, SC-018 pass. Covers tasks T077–T110.

**M6 — Streaming and paging.** Client region residency with hysteresis and movement-vector prefetch, off-thread population, mip-LOD far-field replication, bandwidth-driven fidelity degradation, server hot/warm/cold tiers with write-back, always-resident coarse structural graph, region store backend selection.
*Exit*: acceptance scenarios 4–5 pass. SC-004, SC-005, SC-006 pass. Covers tasks T111–T124.

**M7 — Persistence, moderation, tiering, hardening.** Log compaction, late join and reconnect, session lifecycle, protected zones, rate and density limits, attribution, plausibility rejection, device class budget tables and the tiering test matrix.
*Exit*: acceptance scenario 6 passes. SC-009, SC-010, SC-011, SC-013 pass. Covers tasks T125–T142.

**M8 — Scale validation.** Soak at 32–64 players under sustained destruction across the device matrix.
*Exit*: acceptance scenario 7 passes. SC-001, SC-014, SC-015 pass. All success criteria green. Covers tasks T143–T146.

*Milestone exits are stated by task ID as well as by criterion, so that plan and tasks cannot drift apart — the inconsistency that analysis finding I1 caught.*

## Complexity Tracking

Deviations from the simplest thing that could work, each with its justification.

| Deviation | Why not simpler | Simpler option rejected |
|---|---|---|
| Custom rendering path rather than meshes | Per-edit mesh rebuild is the cost the feature cannot absorb | Greedy-meshed chunks |
| Custom collision rather than Unity colliders | No Unity primitive represents a mutable brickmap; `MeshCollider` reintroduces meshing | `MeshCollider` per chunk |
| Custom world replication rather than a netcode framework | No framework rolls back custom world state; frameworks add a second tick loop without removing the hard part (R-001) | NGO, Fusion 2, Netick |
| Custom irradiance cache | URP has no facility for lighting that responds to continuously mutating geometry | Baked or fully dynamic standard lighting |
| Three network channels rather than one | Bulk streaming must not starve combat traffic | Single reliable channel |

Four simultaneous custom subsystems is genuinely a lot, and it is the reason `com.unity.entities` was rejected (R-002) rather than added as a fifth unknown. The mitigation is milestone ordering: M0 through M3 each retire a specific risk before the milestone that depends on it begins.

## Risks

Carried from `architecture-notes.md` §10. Re-ordered 2026-08-04 after the mobile narrowing moved the former top risk down to fourth.

1. **Custom collision and character movement** — largest work item, most commonly underestimated. M3. *Now the top risk.*
2. **Reconciliation against a mutable world** — the region log must be tick-queryable from the start. Execution risk rather than decision risk, since R-001 is resolved. M2.
3. **Streaming as phase-one scope** — the consequence of the km-scale decision; M6 is large and cannot be deferred. Unchanged by the mobile narrowing.
4. **Mobile raymarch throughput** — *downgraded from 1.* Flagship-only scope makes M0 an expected-pass measurement with a defined threshold and a viable contingency. Thermal sustain is now the likelier failure than raw capability.
5. **Cross-region structural consistency** — collapse must not depend on which regions are resident. M5/M6 seam.
6. **In-session growth bounding** — survives session-scoped persistence; settle before the M1 allocator is finalised.
7. **Presentation tiering leaking into simulation** — interest-management radius is the specific trap. M7, enforced as a test matrix.
8. **Cross-device parity under cellular packet loss** — harness must exist from M1/M2, not be added late.
9. **Voxel scale versus world extent discipline** — ongoing; the brickmap widens the envelope and the mobile narrowing widens it further.
