# Phase 0 Research — Destructible & Buildable Multiplayer Voxel World

**Created**: 2026-08-04
**Feeds**: [plan.md](./plan.md)
**Background**: [architecture-notes.md](./architecture-notes.md)

Each entry: Decision → Rationale → Alternatives considered.

---

## R-001: Netcode framework — buy vs build for player prediction

**Status**: Open gate from `architecture-notes.md` §8.3, risk item 3. **Now resolved.**

The criterion set in §8.3 was narrow and deliberate: *does the framework expose its tick loop such that the region event log can participate in rollback?* Player reconciliation must replay inputs against **world state at that tick** (§6), and the world is custom state the framework does not own.

### Findings

**Photon Fusion 2 — fails the criterion.**

Fusion's documented behaviour is that networked state is reset to the correct tick before `FixedUpdateNetwork()` during resimulation, but **non-networked state is not**, with an explicit caution about using local state in `FixedUpdateNetwork()`. The voxel world is, by construction, non-networked custom state — it cannot be expressed as Fusion networked properties at this scale. The simulation-loop documentation describes reconciliation as replaying inputs from the server checkpoint forward, and **does not document any hook or callback allowing user code to restore its own state at the start of a resimulation**. Additionally, `SimulationBehaviour` cannot carry networked properties and is not synchronised between peers, so it is not an escape hatch.

Consequence: under Fusion, player inputs would be resimulated against a voxel world that was never rewound — precisely the defect the criterion exists to prevent.

**Netick 2 — satisfies the criterion.**

Netick exposes the required observability on `NetworkSandbox`:

- `IsResimulating` — true while re-simulating a past input/tick; always false on the server, since only clients resimulate.
- `Tick` — during a resimulation on the client, returns the tick currently being re-simulated.
- `AuthoritativeTick` — latest received server tick.
- `PredictedTick` — last predicted tick on the client, regardless of resimulation.

Together these are sufficient: user code can detect that a resimulation is in progress, learn which tick is being replayed, and query the region event log for world state at that tick. Netick also ships a replay system, which is complementary to the region log.

Netick still does **not** roll back custom state automatically. No framework surveyed does.

### Decision

**Build custom on Unity Transport (UTP).** Netick is recorded as a viable fallback; Fusion 2 is eliminated.

### Rationale

The evaluation changed the shape of the question. The hoped-for outcome was that a framework would solve the hard part. It does not: **world rollback is ours to implement under every option**, because no framework owns or rewinds the voxel world. What a framework would buy is the player-side tick loop, input buffer, and snapshot/delta encoding — well-trodden work at 32–64 players, not research.

Against that modest saving, buying costs a second tick loop to reconcile with our own, a licensing and CCU-pricing dependency at the 32–64 player target, and a framework boundary running directly through the most delicate code in the project. One tick loop and one authority model is worth more than the code saved.

Netick specifically remains defensible and should be reconsidered if the custom tick loop slips: it passes the criterion, and its free tier suits the target scale. Revisit at the M2 exit if reconciliation is behind schedule.

### Alternatives considered

| Option | Verdict |
|---|---|
| Photon Fusion 2 + custom world channel | **Eliminated.** Non-networked state not rolled back; no documented restoration hook. |
| Netick 2 + custom world channel | **Viable fallback.** Passes the criterion via `IsResimulating` / `Tick`. Adds a second tick loop and a dependency. |
| Netcode for GameObjects + custom world channel | **Eliminated** (§8.3). GameObject/RPC/`NetworkVariable` model is what the brickmap exists to avoid; weak bandwidth model at 64 players. |
| Netcode for Entities | **Eliminated.** Ghost-snapshot model assumes entities; §8.2 rules out the Entities package regardless. |
| Raw UDP sockets | **Eliminated.** Reimplements reliability pipelines, fragmentation, encryption, and platform backends, then risks console certification on something unrelated to the game. |

---

## R-002: DOTS package scope

**Decision**: Adopt `com.unity.burst`, `com.unity.collections`, `com.unity.jobs`. Reject `com.unity.entities`.

**Rationale**: Hot paths (edit expansion, connectivity flood-fill, support propagation, mip rebuild, swept-AABB collision) are integer/bitwise work over flat native memory, parallel over regions — exactly Burst-job shaped. `NativeArray` is also the zero-copy path into `ComputeBuffer`. Burst's integer reproducibility across platforms is what makes event-sourced replication safe (§6); that guarantee does not extend to floats and must never be assumed for GPU results.

The world is not entity-shaped — a brick pool plus a region hash map. Modelling bricks as entities pays archetype and chunk-iteration overhead to express a flat array. Debris bodies are the only tempting case and are ~200 lines of structure-of-arrays plus an integration job. Decisive factor is risk stacking: custom rendering + custom collision + custom replication is already three unknowns; Entities would make four, plus hybrid-renderer interop and slower iteration.

**Alternatives**: Full DOTS as the superseded proposal specified — rejected as naming technologies without checking data shape. Pure managed C# — rejected; loses Burst determinism and the zero-copy GPU path.

**Revisit if**: AI/NPC counts later explode, and then only for that subsystem.

---

## R-003: Render pipeline — URP vs HDRP

**Decision**: URP.

**Rationale**: C-002 makes mobile a first-class target, and mobile effectively settles this. HDRP's advantages (volumetrics, a stronger baseline for the irradiance cache) do not survive the mobile requirement, and HDRP is heavier to customise for a compute-driven custom path.

**Alternatives**: HDRP — rejected on the mobile floor. Built-in RP — rejected; no `ScriptableRendererFeature` injection point.

**Consequence**: the irradiance cache (§3) is ours to build rather than adapted from HDRP facilities. Budgeted in M4.

---

## R-004: Mobile compute-shader raymarch viability

**Status**: **Substantially de-risked 2026-08-04 by narrowing mobile scope to high-end devices.** Still requires an M0 measurement, but is no longer a project-threatening gate.

**Scope change**: mid-tier and low-tier mobile removed from scope (`spec.md` Out of Scope, C-002). Only recent flagship phones are supported.

**What this changed.** The original risk was that a divergent, memory-bound raymarch has uncertain throughput on *mid-tier* mobile GPUs, where tile-based deferred architectures behave very differently from desktop immediate-mode GPUs, and where there was no cheap fallback if it failed. Restricting to flagship hardware removes most of that uncertainty: the target devices reliably provide Vulkan 1.1+ or Metal 3, with compute throughput far closer to console than to the mass-market floor, and with memory budgets that comfortably hold a 384 MB brick pool.

The measurement is now an **expected-pass verification** rather than an open question. That is a genuine reduction in project risk, not a reframing — the failure mode it removes was the only one in the plan with no acceptable fallback.

**What is still unknown**: measured throughput at the specific render scale and step budget in `device-matrix.md`. Sustained thermal behaviour over a 20-minute session is the more likely failure mode now, rather than raw capability.

**Plan**: M0 spike. Pass condition is **≤ 9 ms for voxel rendering at 0.75 render scale** on the Mobile-HE tier (`device-matrix.md`).

**Contingency if it fails**: render mobile entirely through the implicit/mip raymarch at all distances — lower fidelity, same data, same collision, no second pipeline. Prototype alongside, not after. A mesh-based mobile path is explicitly **not** the contingency: it reintroduces the per-edit rebuild cost the architecture exists to avoid and would require a second collision and LOD pipeline.

**Gate**: M0 must produce a number before M4 begins. Risk ranking drops from 1 to 4.

### R-004/M0: M0 measurement spike results

M0 measurement pending hardware testing. Target: <= 9 ms Mobile-HE at 0.75 scale. No thermal throttle over 20 min.

**Pending measurements**:

| Measurement | Target | Measured |
|---|---|---|
| Full-detail raymarch, Mobile-HE, 0.75 scale | <= 9 ms | _pending_ |
| Implicit-only contingency, Mobile-HE | <= 5 ms | _pending_ |
| Full-detail raymarch, PC, 1.0 scale | <= 6 ms | _pending_ |
| Brick pool 384 MB resident, Mobile-HE | no thermal throttle over 20 min | _pending_ |

**Go/no-go (T011)**: if full-detail exceeds 9 ms on Mobile-HE, ship mobile on the implicit/mip path at all distances — lower fidelity, same data, same collision.

---

## R-010: Concurrent conflicting alteration arbitration

**Status**: Added 2026-08-04, closing analysis finding **G1** (FR-011 had zero task coverage).

**Decision**: total ordering by `(serverTick, playerId, sequenceWithinTick)`, with material priority as the tie-break for same-tick placements into the same voxel. The server assigns the ordering; clients adopt it without re-deriving.

**Rationale**: server authority makes arbitration *possible* but does not make it *deterministic on its own* — clients receiving competing events in different orders must still converge. A total order that every participant can evaluate identically is the requirement. `serverTick` is already carried on every `AlterationEvent`, and `playerId` breaks same-tick ties without extra payload, so this costs nothing on the wire.

Material priority matters for the specific case of two placements into the same empty voxel on the same tick: without it, the winner depends on iteration order over a hash map, which is not stable.

**Alternatives considered**: last-write-wins by arrival — rejected, arrival order differs per client. Timestamp-based — rejected, requires trusting client clocks. Locking a region during edit — rejected, adds latency to the common uncontended case to solve a rare one.

**Verification**: SC-017.

---

## R-011: Player-occupied volume resolution

**Status**: Added 2026-08-04, closing analysis finding **G2** (the "player in destroyed volume" edge case had no coverage).

**Decision**: destruction beneath or around a player is unrestricted — the player simply falls or is exposed. **Building into a player-occupied volume is rejected** by a validation predicate, alongside attachment and protected-zone checks.

**Rationale**: the two directions are asymmetric. Destruction removing support is core gameplay and needs no special handling beyond existing collision. Construction *into* a player would require either ejecting them (a griefing tool: repeatedly displace someone), crushing them (a griefing tool with worse consequences), or leaving them intersecting solid matter (a physics defect and a wall-clip exploit). Rejection is the only option that is neither exploitable nor incorrect.

The predicate is cheap: player positions within the affected region are already known to the server for interest management.

**Alternatives considered**: eject the player upward — rejected as a displacement grief vector. Damage or kill — rejected; makes building a weapon, which the spec does not ask for. Allow intersection and resolve next tick — rejected; produces a wall-clip window.

**Verification**: SC-018.

---

## R-005: Collision representation

**Decision**: Custom query layer over the brickmap. Raycast reuses the DDA written for the raymarch, run on the CPU in a Burst job. Character collision is swept-AABB against occupancy masks. Unity physics is retained only for debris and vehicles, bridged by generating convex hulls for the local neighbourhood.

**Rationale**: no Unity primitive represents a mutable brickmap. `MeshCollider` requires a mesh, which is the artefact the architecture exists to avoid producing; rebuilding one per edit reintroduces the entire cost.

**Alternatives**: `MeshCollider` per chunk — rejected, reintroduces per-edit rebuild. Unity Physics (DOTS) — rejected with the Entities package. GPU collision queries — rejected; C-004 and §6 require collision to be CPU-authoritative and deterministic.

**Note**: this is risk item 2, the largest and most underestimated work item. Budgeted its own milestone (M3).

---

## R-006: Region storage backend

**Decision**: An embedded key-value store keyed by region coordinate, region blob as value. LMDB or RocksDB shaped; final choice deferred to M6, when the access pattern is measured.

**Rationale**: Q2 (session-scoped persistence) removes cross-session durability, backup, and moderation-at-rest from scope, which substantially relaxes this choice. What remains is server-side cold-region paging within a session — write-back on eviction and periodic flush. Sized for one session, not indefinite retention.

**Alternatives**: In-memory only — rejected; the world exceeds server RAM at km-scale. Full RDBMS — rejected; no relational access pattern, blob-per-region is the whole requirement.

**Deferred deliberately**: the decision is cheap to change and is better made against measured eviction rates.

---

## R-007: Bounding in-session world growth

**Decision**: Three mechanisms, all required: per-player voxel budget over a rolling window; per-region density cap; compaction of aged event-log segments into baked brick snapshots.

**Rationale**: Q2 removes cross-session growth but **not** in-session growth — FR-022 survives. Destruction frees bricks; building allocates them, so a build-heavy session grows without bound. Compaction bounds the log (unbounded) against the snapshot (bounded by region volume). Budgets and density caps bound the allocation itself and double as anti-grief controls (FR-019).

**Alternatives**: Decay/expiry of player-placed material — available but **not adopted by default**, because session-scoped persistence already caps the horizon and decay adds a tuning problem plus a gameplay-visible behaviour the spec does not ask for. Retained as a lever if measurement shows budgets are insufficient.

**Must be settled before**: the brick allocator is written (M1).

---

## R-008: Determinism strategy

**Decision**: All cross-client-agreeing computation is integer arithmetic in Burst jobs on the CPU. Seeded PRNG for destruction expansion. No authoritative result may depend on GPU output or on floating-point arithmetic.

**Rationale**: this is the load-bearing assumption of event-sourced replication (§6). Integer grid operations with a seeded PRNG are bit-exact across platforms; float physics is not, and GPU compute results are not bit-identical across vendors, drivers, or occupancy-dependent scheduling. Under C-002 the client population spans PC, console, and mobile GPUs, so the failure mode — slow silent divergence between hardware classes — is not hypothetical.

**Alternatives**: GPU-computed authoritative state — rejected (this is the specific defect identified in the superseded proposal, §9). Float-based simulation with tolerance-based reconciliation — rejected; SC-003 requires byte-identical state.

**Verification**: SC-003 (byte-identical state after 10,000 events) and SC-016 (convergence under mobile packet loss) are the tests. Cross-device parity harness is M2 scope, not a late addition.

---

## R-009: Presentation tiering boundary

**Decision**: Device class may tier brick pool size, full-detail radius / mip transition distance, raymarch resolution and step budget, irradiance probe density, and *visual-only* debris count. It may **not** tier world state, collision, hit resolution, interest-management radius, or any integer simulation job.

**Rationale**: C-006. The specific trap is interest-management radius — tying update range to draw range would quietly disadvantage mobile players competitively, and it is an easy accidental coupling. Debris needs an explicit split: debris that is purely visual may be culled per tier; debris that comes to rest and rejoins the grid changes world state and may not.

**Enforcement**: the tiering table in §8.1 becomes a test matrix, not a convention. SC-013 (identical outcomes across device classes) is the check.

---

## Open items carried into implementation

| Item | Resolved by | Blocks |
|---|---|---|
| Mobile raymarch throughput (R-004) | M0 hardware spike — now expected-pass | M4 |
| ~~Minimum-spec device list~~ | **Resolved**: device classes defined in [device-matrix.md](./device-matrix.md); exact model no longer gates the architecture | — |
| ~~Quantitative performance targets~~ | **Resolved**: [device-matrix.md](./device-matrix.md) | — |
| Region storage backend selection (R-006) | M6, against measured eviction | M6 |
| Console certification and mobile store constraints on transport | Before M2 transport work is finalised | M2 |

---

## Sources

- [Fusion 2 — Network Simulation Loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop)
- [Fusion 2 — Prediction](https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/3-prediction)
- [Fusion 2 — Simulation Behaviour](https://doc.photonengine.com/fusion/current/manual/advanced/simulation-behaviour)
- [Fusion 2 — Networked Properties](https://doc.photonengine.com/fusion/current/manual/data-transfer/networked-properties)
- [Netick — NetworkSandbox API](https://netick.net/docs/2/api/Netick.Unity.NetworkSandbox.html)
- [Netick — Physics Prediction](https://netick.net/docs/2/articles/physics-prediction.html)
- [Netick](https://netick.net/)
