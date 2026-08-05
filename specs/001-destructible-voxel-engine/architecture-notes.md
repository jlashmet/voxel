# Architecture Notes — Destructible & Buildable Multiplayer Voxel World

**Status**: Pre-planning input. Captures technical direction agreed in discussion, plus the review of the superseded proposal. Formalised by `/speckit-plan`; this document is not itself the plan.

**Created**: 2026-08-04

---

## 1. The framing problem

Destruction couples the rendering data structure to the network data structure. Static voxels can be baked once and shipped; destructible voxels mean the thing rendered is the thing replicated, continuously, for every player. Projects fail here by choosing a rendering structure first and discovering later that it cannot be diffed cheaply.

Building adds a second coupling: it makes world state grow, which collides with the requirement that the world be too large to hold resident.

---

## 2. Storage: three-tier sparse brickmap

The single structure that serves rendering, collision, replication, and streaming.

| Tier | Contents | Purpose |
|------|----------|---------|
| Region table | Hash map, region coordinate → resident region | Paging unit; sparse, so world extent costs nothing |
| Region | Dense grid of brick pointers (~64³ bricks ≈ 51 m cube) | Fixed ~1 MB of pointers; unit of persistence and moderation |
| Brick | 8³ voxels, 1 byte material = 512 B, plus 64-bit occupancy mask | Unit of allocation and of GPU raymarch skipping |

A voxel lookup is two indirections, both cache-friendly.

**Why a flat top level fails**: at 10 cm voxels with 8³ bricks each brick spans 0.8 m; a 10 km world is 12,500 bricks per axis, so a flat pointer grid is ~2×10¹² entries. The sparse region table is not an optimisation, it is the enabling structure.

**Why memory fits**: allocation follows surface area, not volume.

- Entirely-empty brick → null pointer, zero bytes.
- Entirely-uniform brick → pointer into a shared palette brick, zero marginal bytes. Solid rock underground is free at any quantity.
- Only bricks containing a surface get real allocations.

Resident memory therefore scales with the surface area of the working set — roughly the square of view radius, not the cube. A 1 GB brick pool holds ~2 M unique bricks ≈ 1 B voxels of detailed matter.

---

## 3. Rendering: GPU raymarch, no meshing

**Rejected**: greedy-meshed chunks. Every edit invalidates a mesh; re-meshing is CPU work proportional to chunk volume, plus a geometry re-upload. Under full destructibility this consumes the frame budget.

**Adopted**: raymarch the brickmap in a compute shader — DDA through the top grid, then within the brick.

- An edit is a single byte written into a GPU buffer. No rebuild, no geometry upload.
- The occupancy bitmask lets an empty brick be skipped in one instruction.
- Mip the **occupancy**, not colour. Rays that miss terminate in a few steps. Rebuilding a mip after an edit is a bitwise OR up the chain, batched per frame across all edits.
- Beyond a range threshold, fall back to a lightweight implicit raymarch driven by the same mip data. (Retained from the superseded proposal.)

**Lighting is decoupled from geometry.** A world-space irradiance cache — probes on a sparse grid, or surfels — updates across many frames. Blowing out a wall invalidates probes in that region and lets them reconverge over ~10 frames; players read this as light pouring in. This is the single biggest reason destructible-lighting projects stall, and decoupling dissolves it. It is sign-agnostic: building a roof darkens the same probes with no extra system.

---

## 4. Destruction: two-tier representation

1. **The grid** — authoritative, replicated, cheap edits.
2. **Debris** — detached matter becomes a free-floating voxel body with its own transform and rigidbody, rendered by raymarching the same brick pool through a transformed ray. On settling it re-bakes into the grid.

**Connectivity runs as a GPU flood-fill over the occupancy bitmasks**, not CPU union-find over individual voxels. Bitwise propagation over 8³ blocks resolves a building-sized region in a fraction of a millisecond. This is what produces "shoot the supports, the roof falls" rather than floating geometry — the feature players actually notice.

---

## 5. Building: four asymmetries with destruction

Building is not destruction with the sign flipped.

**5.1 — Event compression partly breaks.** Destruction compresses because one cause expands to thousands of voxels. Building is inherently discrete. Fix: make the *tools* generative. Ship brushes ("cube 4×4×2 at P, material M, rotation R"; "extrude face F by 3"; "prefab #17 at P") as the primary verb, each a bounded payload that expands deterministically. Better UX regardless. Keep raw single-voxel placement as a detail escape hatch, and coalesce it — buffer a player's placements over ~100 ms and ship one run-length-encoded batch scoped to a single brick.

**5.2 — Building requires adjudication.** Destruction is self-limiting; building is the vector for every grief and exploit. Server-side predicates from day one:

- **Attachment** — new voxels must touch existing structure (free; reuses the connectivity data).
- **Rate and budget** — per-player voxel budget over a window, per-region density cap.
- **Zone masks** — coarse per-region bitmask marking no-build volumes.
- **Ownership and lifetime** — owner tag per placed voxel, optionally with decay.

That last point is load-bearing: destruction frees bricks, building allocates them, so **a pure-build server has unbounded memory growth**. Decay, budgets, or both must be decided before the allocator is written.

**5.3 — Prediction must be reversible and visible.** A rejected placement is highly visible in a way a mispredicted crater is not. Use a **speculative overlay**: predicted voxels live in a per-client layer keyed by the same brick coordinates, rendered with a subtle tint so they read as pending. Confirmation promotes them into the real grid; rejection dissolves them with a reason. Rollback is a discard, not a diff. This is also the honest UX — players tolerate "pending" far better than "it lied to me".

**5.4 — Structural simulation cuts both ways.** Propagate a **support value** outward from anchored bricks, decrementing with distance; voxels below threshold collapse into debris. A second bitwise propagation over the mips already maintained, so near-zero marginal cost. It permits cantilevers and arches while forbidding floating islands — exactly the desired design space, and it closes the instant-sky-cover exploit.

---

## 6. Networking: replicate the cause, not the effect

**Destruction events, not voxel diffs.** "Explosion, radius 3.5, at P, seed S" is ~16 bytes and expands deterministically to thousands of changes on every client. A grenade carving 4,000 voxels costs what a gunshot costs.

Determinism is achievable **because these are integer grid operations with a seeded PRNG**, bit-exact across platforms in a way float physics is not. This is why the determinism must live on the CPU in integers and never in GPU compute.

The server runs the same expansion and holds truth. Cheap per-region hashes detect drift; the server ships an authoritative brick to repair it. Net model: **event-sourced with state-based repair.**

- **Interest management is spatial and mandatory.** Events are position-tagged, so the filter is trivial — another reason events beat diffs.
- **Keep the per-region event log.** Free rollback, free replay, free "how did this building fall" debugging, and it is how lag compensation for destruction works — rewind a region by replaying its log to a timestamp. It is also the moderation record (FR-023).
- **The collision world is mutable.** Movement reconciliation must replay inputs against world state *at that tick*, so the region log must be queryable by tick. Budget for this early; retrofitting it is miserable.
- **Compact aged log segments to baked brick snapshots.** The log is unbounded; the snapshot is bounded by region volume. This is what keeps a server alive for months rather than days.
- **Separate the bulk streaming channel from live gameplay events.** A player traversing the map must not saturate the channel carrying combat.

---

## 7. Streaming and paging

**What is streamed**: base terrain is procedural from a seed and costs zero bandwidth; only the **edit overlay** crosses the wire. Streaming cost therefore scales with how much of the world has been touched, not how large it is.

**Mips are the replication LOD.** The occupancy hierarchy built for raymarch skipping is also built server-side and is the unit of far-field replication: near field full-resolution bricks, mid field mip 2–3, far field mip 5+ (a handful of bytes per region, feeding the implicit raymarcher). Distant destruction stays visible in the skyline at negligible cost, and approaching a region *refines* mips rather than fetching from scratch — each level is 1/8 the next, so progressive refinement is nearly free.

**Client paging**

- Bricks come from a fixed-size pool free list; exhaustion triggers eviction.
- Evict whole regions, LRU by camera distance — no fragmentation, one bulk free-list splice.
- **Client eviction needs no write-back**, because the client owns no truth. Discard the region; regenerate terrain from seed and re-fetch the overlay on return. Unload is effectively instantaneous, which is what keeps fast traversal smooth.
- **Hysteresis is mandatory** — load and unload radii must differ (e.g. 400 m / 500 m). Without the gap a player on a boundary thrashes every frame. This is the most common way streaming implementations tank.
- **Prefetch along the movement vector, not the view vector.** Players look around far faster than they move; gaze-driven prefetch causes the thrash it is meant to avoid.
- Populate regions on a worker thread, publish with a single pointer splice. Because there is no meshing step the worker only decompresses and memcpys, so region load lands in single-digit milliseconds. Cap regions loaded per frame regardless; a teleporting player gets the always-resident mip approximation, refining over ~a second.

**Server paging**: hot regions (players present) in memory with logs resident; warm regions in memory, compacted, logs flushed; cold regions on disk in a key-value store keyed by region coordinate, region blob as value. Server eviction *does* need write-back — dirty flag per region, write on eviction and periodically. **Coarse mip occupancy for every region stays resident permanently** — a few hundred bytes per region — so the server can answer far-field visibility and cross-region structural queries without paging anything in.

**The cross-region gotcha**: structures span regions, and neighbours may be unloaded. Two mitigations, use both:

1. Treat unloaded region borders as **anchored** — support propagation stops at the boundary and assumes the far side is grounded. Conservative: things fail to collapse rather than collapsing wrongly.
2. The server's always-resident coarse structural graph resolves cross-region collapse correctly and ships the result as an event when those regions load.

---

## 8. Unity implementation direction

Unity-specific consequences of the above. `/speckit-plan` should turn these into a component breakdown.

**Data layer** — Native collections (`NativeArray`, `NativeHashMap`) under Burst-compiled jobs, outside the managed heap. The brick pool is one large `NativeArray<byte>` with a free list; regions are `NativeArray` of indices into it. Deliberately not a `NativeArray` per region — allocation churn during streaming is the thing being avoided. `ComputeBuffer` mirrors, updated with partial `SetData` so an edit uploads one brick, not the world.

**Rendering** — The raymarch is a compute shader dispatched from a `ScriptableRendererFeature`, writing depth and colour that the rest of the pipeline composites against normally, so conventional Unity content (players, vehicles, VFX) coexists. Debris bodies use `Graphics.RenderMeshIndirect` with per-instance transforms; the "mesh" is a proxy cube whose fragment shader raymarches the brick. Persistent buffers, recycled frame to frame, no per-frame allocation.

**HDRP vs URP** is an early decision with large blast radius. URP is the lighter path and the safer default; HDRP brings better volumetrics and a stronger baseline for the irradiance cache but is heavier to customise. Flag for the plan phase.

**Collision** — Unity's built-in colliders cannot represent this world. Expect a custom query layer over the brickmap: raycast is the DDA already written for the raymarch, run on the CPU in a job; character collision is swept-AABB against the occupancy masks. Rigidbodies for debris and vehicles can remain Unity physics, bridged by feeding it generated convex hulls for the local neighbourhood. **This is the largest single piece of Unity work and the most commonly underestimated.** Do not plan around `MeshCollider`.

**Jobs and determinism** — Edit expansion, connectivity, and support propagation run as Burst jobs on integers. Burst is deterministic for integer work across platforms; do not extend that assumption to floats. The authoritative simulation must not depend on any GPU result.

**Networking** — see §8.3. Summary: Unity Transport as the transport, custom replication above it, Netcode for GameObjects rejected.

**Editor tooling** — a brush tool writing region blobs, plus deterministic serialisation, so test worlds can be authored and replayed. Needed earlier than it feels — cross-client parity testing depends on reproducible starting states.

### 8.1 Device tiering (PC + console + mobile crossplay)

Confirmed target, **narrowed 2026-08-04 to high-end mobile only** — mid-tier and low-tier phones are out of scope. The discipline is unchanged: **budgets are set against the lowest supported class and scale up**, never set against PC and trimmed. That floor is now a recent flagship rather than a mass-market device, which raises every budget and largely retires the rendering risk. Concrete per-tier numbers live in [device-matrix.md](./device-matrix.md).

What tiers, and what must not:

| Tunable | Tiers by device? | Notes |
|---|---|---|
| Brick pool size | **Yes** | The pool is already a configured budget — this is the tiering knob the design was built around. Mobile gets a smaller pool, hence a smaller resident working set. |
| Full-detail radius / mip transition distance | **Yes** | Mobile transitions to the implicit raymarch earlier. Falls out of the existing mip-LOD path at no extra architectural cost. |
| Raymarch resolution and step budget | **Yes** | Render at reduced resolution and upscale; cap steps per ray. |
| Irradiance cache probe density and reconvergence rate | **Yes** | Purely presentational. |
| Debris body count | **Yes, with care** | Debris that is *visual* may be culled per tier. Debris that *comes to rest and rejoins the grid* changes world state and must not be. Separate the two categories explicitly or this becomes a divergence bug. |
| World state | **No** | C-006. |
| Collision and hit resolution | **No** | Runs on the server and on the CPU brickmap, independent of what was rendered. |
| Interest-management radius | **No** | Tempting and wrong — tying update range to draw range would disadvantage mobile players competitively. Presentation tiering and simulation tiering stay strictly separate. |
| Edit expansion, connectivity, support propagation | **No** | Integer CPU jobs; identical everywhere. |

**The gate, downgraded**: originally the hardest risk in the project — the compute-shader raymarch is well established on PC and console and was genuinely uncertain on *mid-tier* mobile, with no cheap fallback. Narrowing to flagship hardware largely retires it. Target devices reliably provide Vulkan 1.1+ or Metal 3 with compute throughput closer to console than to the mass-market floor. **Still validate on real hardware before committing the rendering path** (pass condition: ≤ 9 ms at 0.75 render scale), but treat it as expected-pass verification rather than an open question. The more likely failure mode now is sustained thermal throttling over a long session, not raw capability.

Contingency if it fails: render mobile entirely through the implicit/mip raymarch at all distances — cheaper, lower fidelity, but the same data and the same collision. Worth prototyping alongside rather than after. A mesh-based mobile path remains excluded: it would reintroduce exactly the per-edit rebuild cost the architecture exists to avoid, and would require a second collision and LOD pipeline.

**Bandwidth floor**: the event-sourced model (§6) is what makes mobile viable at 64 players. Voxel-diff replication would not fit a cellular connection at this scale; "explosion at P, radius R, seed S" does. Mobile is a reason to hold the line on event replication, not an argument against it — and this holds regardless of how capable the phone is, since the constraint is the network rather than the device.

**Unity specifics**: high-end mobile compute means Vulkan 1.1+ or Metal 3, both reliably present on the target class. Prefer URP given the mobile target (§8 flagged this as an early decision; the mobile requirement effectively settles it). Console certification and mobile store policies constrain networking and update mechanics and should be checked before the transport is chosen.

### 8.2 DOTS scope: Burst/Jobs/Collections yes, Entities no

The superseded proposal specified "DOTS" wholesale. That conflates separable packages, and only some of them fit the data.

**Adopt: `com.unity.burst`, `com.unity.collections`, `com.unity.jobs`.**

The hot paths — edit expansion, connectivity flood-fill, support propagation, mip rebuilds, swept-AABB character collision — are integer and bitwise work over flat native memory, trivially parallel over regions. Burst-compiled jobs over `NativeArray` / `NativeHashMap` are exactly the right tool. `NativeArray` is also the zero-copy path into `ComputeBuffer`. Critically, **this is where the determinism story lives**: Burst is reproducible for integer work across platforms, which is what makes event-sourced replication (§6) safe. That guarantee does not extend to floats, and must never be assumed for GPU results.

**Reject: `com.unity.entities` (the actual ECS).**

ECS earns its place with many heterogeneous entities queried archetypally. The world is the opposite shape: one large `NativeArray<byte>` brick pool plus a `NativeHashMap` region table. Modelling bricks as entities would be strictly worse — paying archetype and chunk-iteration overhead to express a flat array.

The tempting case is **debris bodies**: potentially thousands, homogeneous, carrying transform + velocity + brick reference. But that is a structure-of-arrays across a few `NativeArray`s plus an integration job — on the order of 200 lines, written by hand, fully debuggable. It does not justify the Entities package.

The decisive argument is **risk stacking**. The project already commits to a custom rendering path, a custom collision system, and custom world replication. Entities would make four simultaneous unknowns, plus hybrid-renderer interop, plus materially slower iteration — in exchange for nothing the data model wants.

Revisit only if AI or NPC counts later explode, and then only for that subsystem.

### 8.3 Netcode layering: buy the transport, build the replication

| Layer | Decision | Rationale |
|---|---|---|
| Transport | **Unity Transport (UTP)** — buy | Reliability pipelines, fragmentation, encryption, and working console and mobile backends. Hand-rolled UDP means reimplementing all of it and then failing certification on something unrelated to the game. WebTransport available if browser reach is ever wanted. |
| World state replication | **Custom** — build | Three channels with different characteristics: events (small, reliable, ordered per region), repair (authoritative brick blobs, reliable, rare), bulk streaming (large, lower priority, must never starve combat). No packaged solution provides that separation. |
| Player state + prediction | **Custom on UTP**, pending a buy-vs-build evaluation | See below. |

**Netcode for GameObjects: rejected.** Its object / RPC / `NetworkVariable` model assumes replicated GameObjects — precisely what the brickmap exists to avoid. Netcode for Entities is closer in spirit but its ghost-snapshot model assumes entities too, and §8.2 rules out Entities regardless.

**On player prediction.** The hard part is not replicating 64 transforms; it is **tick-aligned prediction and rollback against a mutable world** (§6). Reconciliation must replay player inputs against world state *at that tick*, so the coupling runs straight through both the player system and the world system. That is the criterion on which any third-party framework should be judged.

Current lean is **custom on UTP**: one tick loop, one authority model, no seam. What that entails is well-trodden rather than research — connection lifecycle, tick loop, input ring buffer, snapshot and delta encoding for player state, interest management (shared with the world system, which needs it anyway), and reconciliation.

**Open evaluation for the plan phase**: Photon Fusion 2 and Netick both provide mature tick-aligned prediction and rollback — genuinely the hardest part, already solved. Buying is defensible and could save months, *if and only if* the framework exposes its tick loop such that the region event log can participate in rollback. A prediction framework that owns its tick loop opaquely becomes the thing the project fights. Evaluate both against that single question, plus licensing and CCU pricing against the 32–64 player target. Decide before writing the reconciliation loop; retrofitting is the expensive path.

**Mobile and console consequence** (per §8.1): event-sourced replication is what makes 64 players fit a constrained mobile connection at all. Voxel-diff replication would not. Mobile is an argument *for* holding the line on event replication, not against it.

---

## 9. Review of the superseded proposal (CTBS + CGVAVS v1.0)

Recorded so the decision is not relitigated.

**Rejected — the confidence map.** The proposal's unifying mechanism is never defined: no units, no source, no meaning for a value of 0.6. Defining it splits it into two quantities that **anticorrelate**: network confidence (a function of ack state and time since correction) and render detail (a function of screen-space projected error). A wall 800 m away untouched for ten minutes has maximum network certainty and deserves minimum detail; a wall being demolished at arm's length is maximally uncertain and needs maximum detail. The claimed cross-layer synergy is exactly where the concept breaks.

**Rejected — the bandwidth model.** `O(surface_complexity × change_rate)` inverts the real cost. Solid volume compresses to almost nothing under RLE; what destruction *does* is manufacture surface area. The claimed scaling metric spikes hardest under the target workload. The proposal half-notices this ("boundary spike exceeds packet limits → switch to delta compression"), meaning its fallback path handles combat and its elegant path handles standing still. The stated "<1 KB/s with 50+ concurrent edits" is not achievable against its own packet struct.

**Rejected — confidence blending.** A voxel is stone or air; there is no intermediate for collision, and collision is what players experience. Blending produces ghost geometry — shooting through visible walls, being stopped by absent ones. Replaced by the speculative overlay (§5.3), which is explicit about provisionality and picks one side deterministically for collision.

**Rejected — GPU compute determinism.** The proposal lists this as a caveat and then depends on it for cross-client parity. Compute results are not bit-identical across vendors, drivers, or occupancy-dependent scheduling; the failure mode is slow silent divergence between clients on different hardware. Determinism lives in integer CPU code (§6).

**Rejected — view-aligned per-frame patch generation.** Geometry that changes as the camera turns produces shimmering silhouettes, unstable shadow maps, and a collision representation disagreeing with the visual one. "Zero popping" is asserted, not argued; view-dependent tessellation is a classic *source* of popping.

**Fatal for this feature — procedural fill seeds as state.** The proposal's state is "confidence grid + boundary field + procedural fill seeds". Seeds compress *generated* terrain beautifully and cannot represent a castle a player built, because arbitrary creativity is by definition not seed-compressible. The moment runtime building enters, real stored voxel data is required and the proposal's memory and bandwidth argument evaporates.

**Provenance signals** (relevant to how much weight the document carries): Appendix C does not compile — `AddRenderPasss` is misspelt, the override is `AddRenderPasses`, and `renderer.Enqueue()` takes a `ScriptableRenderPass`, not a `CommandBuffer`. The header reads "Version 1.0 | March 2024" while claiming novelty. The two system names are backronyms sharing a prefix. The roadmap — custom SRP, custom netcode, DOTS, anti-cheat, editor tooling — is estimated at "3–6 months, single developer"; phase 2 alone is that long. The document appears generated rather than derived from a prototype, and its estimates should not be used for planning.

**Retained from it**: implicit raymarched fallback beyond a range threshold; bulk delta compression for large-volume events (promoted from fallback to primary path); topology tags and deterministic flood-fill on structural flips; server-side anomaly scoring and rejection of topologically illegal changes; buffer recycling, indirect draw, and a zero-GC native data layer; screen-space importance as a detail budget, driven by projected error alone with the confidence term removed.

---

## 10. Highest-risk items for the plan phase

Ordered by likelihood of derailing the project. Revised 2026-08-04 after the mobile narrowing, which moved item 1 down to item 4.

1. **Custom collision and character movement over the brickmap.** No Unity primitive fits. Largest work item, most underestimated. *Now the top risk.*
2. **Reconciliation against a mutable world.** Replaying inputs against historical world state; must be designed in, not retrofitted. The framework question is **resolved** (R-001: build custom on UTP; Netick retained as fallback), so this is now execution risk rather than decision risk.
3. **Streaming and paging as phase-one work.** Km-scale from day one means the region table, mip-LOD replication, hysteresis, off-thread loading, and server paging are all first-playable scope. The largest consequence of the Q1 decision, and unchanged by the mobile narrowing.
4. **Mobile raymarch throughput.** *Downgraded from 1.* Restricting to flagship hardware makes M0 an expected-pass measurement with a defined threshold and a viable contingency (§8.1). Thermal sustain over long sessions is now the likelier failure than raw capability.
5. **Cross-region structural consistency.** Collapse must not depend on which regions happen to be resident.
6. **In-session growth bounding.** Session-scoped persistence removes the cross-session storage problem but *not* this one — a long session still accumulates alterations without bound. Compaction and budgets still needed before the allocator is written.
7. **Presentation tiering leaking into simulation tiering.** C-006 is easy to violate by accident; interest-management radius is the specific trap.
8. **Cross-client parity testing across device classes.** Parity must hold between a PC and a mobile client under cellular packet loss. Requires deterministic seeded worlds and reproducible event logs from very early on.
9. **Scope discipline on voxel scale versus world extent.** The sparse brickmap widens the envelope but does not remove the trade-off. The mobile narrowing widens it back somewhat.
